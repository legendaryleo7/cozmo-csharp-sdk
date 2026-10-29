#!/usr/bin/env python3
# Copyright (c) 2016-2017 Anki, Inc.
#
# Licensed under the Apache License, Version 2.0 (the "License");
# you may not use this file except in compliance with the License.
# You may obtain a copy of the License in the file LICENSE.txt or at
#
#     http://www.apache.org/licenses/LICENSE-2.0
#
# Unless required by applicable law or agreed to in writing, software
# distributed under the License is distributed on an "AS IS" BASIS,
# WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
# See the License for the specific language governing permissions and
# limitations under the License.

"""Generate golden data for the WP-10 camera and nav memory map port.

The values are produced by importing and running the reference Python
implementation (``cozmo/camera.py`` and ``cozmo/nav_memory_map.py``); nothing
in the generated C# file is hand-written.  A throw-away package is created
containing copies of the reference modules plus small ``event``/``logger``
stubs, and the real ``cozmoclad`` classes are imported from the wheel bundled
with the reference SDK.

The camera module imports NumPy and Pillow at module load time.  Both must be
importable by the interpreter running this script, for example::

    uv pip install --python python3 --target .scratch/pylibs numpy pillow

Run from the repository root::

    PYTHONPATH=.scratch/pylibs \\
    python3 tools/camnavgen/generate_camera_nav_goldens.py \\
        --sdk /path/to/ddl-cozmo-python-sdk \\
        --output tests/Cozmo.Tests/CameraNavGoldenData.g.cs

The generated C# file is consumed by ``tests/Cozmo.Tests/CameraTests.cs`` and
``tests/Cozmo.Tests/NavMemoryMapTests.cs``.
"""

from __future__ import annotations

import argparse
import math
import shutil
import sys
import tempfile
import types
from pathlib import Path

LICENSE_HEADER = """// Copyright (c) 2016-2017 Anki, Inc.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License in the file LICENSE.txt or at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
"""

EVENT_STUB = '''
class Event:
    def __init__(self, **kw):
        for key, value in kw.items():
            setattr(self, key, value)


class Dispatcher:
    def __init__(self, **kw):
        pass

    def dispatch_event(self, event, **kw):
        raise NotImplementedError


def _register_dynamic_event_type(name, attrs):
    return type(name, (Event,), dict(attrs))
'''

LOGGER_STUB = '''
def debug(*args, **kwargs):
    pass


def info(*args, **kwargs):
    pass


def warning(*args, **kwargs):
    pass


def error(*args, **kwargs):
    pass
'''


def load_reference(sdk: Path):
    """Import the reference camera and nav modules in an isolated package."""
    wheel = sdk / "cozmoclad" / "cozmoclad-3.6.6-py3-none-any.whl"
    src = sdk / "src" / "cozmo"
    required = ("camera.py", "nav_memory_map.py", "util.py", "_clad.py")
    missing = [str(src / name) for name in required if not (src / name).is_file()]
    if missing:
        raise SystemExit("missing reference file(s): " + ", ".join(missing))
    if not wheel.is_file():
        raise SystemExit(f"missing bundled cozmoclad wheel: {wheel}")

    temp_dir = tempfile.TemporaryDirectory(prefix="cozmo-camnavgen-")
    package = Path(temp_dir.name) / "cozmo"
    package.mkdir()
    (package / "__init__.py").write_text("", encoding="utf-8")
    for name in required:
        shutil.copyfile(src / name, package / name)
    (package / "event.py").write_text(EVENT_STUB, encoding="utf-8")
    (package / "logger.py").write_text(LOGGER_STUB, encoding="utf-8")

    sys.path.insert(0, str(wheel))
    sys.path.insert(0, temp_dir.name)
    try:
        import cozmo.camera as camera  # noqa: PLC0415 - deliberately late import
        import cozmo.nav_memory_map as nav  # noqa: PLC0415 - deliberately late import
    except ImportError as exc:  # pragma: no cover - depends on host packages
        raise SystemExit(
            "the reference camera module needs NumPy and Pillow; install them and "
            "add the target directory to PYTHONPATH"
        ) from exc

    if camera.np is None:  # pragma: no cover - defensive
        raise SystemExit("NumPy/Pillow were not importable by the generator interpreter")
    return camera, nav, temp_dir


def double_literal(value) -> str:
    value = float(value)
    if math.isnan(value):
        return "double.NaN"
    if math.isinf(value):
        return "double.PositiveInfinity" if value > 0 else "double.NegativeInfinity"
    text = format(value, ".17g")
    return text + "d"


def hex_literal(data: bytes) -> str:
    return 'Convert.FromHexString("' + data.hex() + '")'


def string_or_null(value) -> str:
    if value is None:
        return "null"
    return '"' + value.replace("\\", "\\\\").replace('"', '\\"') + '"'


class FakeImage:
    """Stand-in for a decoded camera image; records the processed JPEG bytes."""

    def __init__(self, data: bytes, size=None):
        self.data = data
        self.size = size

    def convert(self, mode):
        return self

    def resize(self, size):
        self.size = size
        return self


class FakePillow:
    @staticmethod
    def open(fp):
        return FakeImage(fp.read())


def make_camera(camera):
    """Create a Camera without its constructor, so no robot is required."""
    cam = object.__new__(camera.Camera)
    cam.dispatched = []

    def dispatch_event(event, **kw):
        cam.dispatched.append((event.__name__, kw))

    cam.dispatch_event = dispatch_event
    camera.Image = FakePillow
    cam._reset_partial_state()
    return cam


def run_chunk_scenario(camera, cam, steps, encoding, resolution):
    cam._reset_partial_state()
    cam.dispatched.clear()
    for chunk_id, image_id, chunk_count, data in steps:
        msg = types.SimpleNamespace(
            chunkId=chunk_id,
            imageId=image_id,
            imageChunkCount=chunk_count,
            imageEncoding=encoding,
            resolution=resolution,
            data=camera.np.frombuffer(data, dtype=camera.np.uint8),
        )
        cam._recv_msg_image_chunk(None, msg=msg)
    images = [
        kw["image"]
        for name, kw in cam.dispatched
        if name == "EvtNewRawCameraImage"
    ]
    return images


def chunk_specs(steps, encoding, resolution):
    return ",\n            ".join(
        "new ChunkSpec({chunk_id}, {image_id}u, {chunk_count}, {encoding}, {resolution}, {data})".format(
            chunk_id=chunk_id,
            image_id=image_id,
            chunk_count=chunk_count,
            encoding=encoding,
            resolution=resolution,
            data=hex_literal(data),
        )
        for chunk_id, image_id, chunk_count, data in steps
    )


def emit_camera(camera) -> str:
    lines: list[str] = []
    out = lines.append

    clad_iface = camera._clad_to_game_cozmo.ExternalInterface
    clad_cam = camera._clad_to_game_cozmo

    # --- CameraConfig conversion -------------------------------------------------
    cfg = clad_iface.CameraConfig()
    cfg.focalLengthX = 288.87
    cfg.focalLengthY = 288.36
    cfg.centerX = 155.11
    cfg.centerY = 111.40
    cfg.fovX = 62.7
    cfg.fovY = 48.3
    cfg.minCameraExposureTime_ms = 1
    cfg.maxCameraExposureTime_ms = 1000
    cfg.minCameraGain = 0.1
    cfg.maxCameraGain = 8.0
    config = camera.CameraConfig._create_from_clad(cfg)

    out("internal static class CameraGoldenData")
    out("{")
    out("    internal const float CladFocalLengthX = 288.87f;")
    out("    internal const float CladFocalLengthY = 288.36f;")
    out("    internal const float CladCenterX = 155.11f;")
    out("    internal const float CladCenterY = 111.40f;")
    out("    internal const float CladFovXDegrees = 62.7f;")
    out("    internal const float CladFovYDegrees = 48.3f;")
    out("    internal const int CladMinExposureTimeMs = 1;")
    out("    internal const int CladMaxExposureTimeMs = 1000;")
    out("    internal const float CladMinGain = 0.1f;")
    out("    internal const float CladMaxGain = 8.0f;")
    out("")
    out(f"    internal const double FocalLengthX = {double_literal(config.focal_length.x)};")
    out(f"    internal const double FocalLengthY = {double_literal(config.focal_length.y)};")
    out(f"    internal const double CenterX = {double_literal(config.center.x)};")
    out(f"    internal const double CenterY = {double_literal(config.center.y)};")
    out(f"    internal const double FovXRadians = {double_literal(config.fov_x.radians)};")
    out(f"    internal const double FovYRadians = {double_literal(config.fov_y.radians)};")
    out(f"    internal const double FovXDegrees = {double_literal(config.fov_x.degrees)};")
    out(f"    internal const double FovYDegrees = {double_literal(config.fov_y.degrees)};")
    out(f"    internal const int MinExposureTimeMs = {config.min_exposure_time_ms};")
    out(f"    internal const int MaxExposureTimeMs = {config.max_exposure_time_ms};")
    out(f"    internal const double MinGain = {double_literal(config.min_gain)};")
    out(f"    internal const double MaxGain = {double_literal(config.max_gain)};")
    out("")

    # --- Mini JPEG header fix-up vectors ----------------------------------------
    mini_vectors = [
        ("gray-basic", bytes([0x00, 0x12, 0xFF, 0x34, 0x56, 0xFF, 0x78, 0xFF, 0xFF]), 40, 30, False),
        ("gray-single-byte", bytes([0x00]), 1, 1, False),
        ("gray-two-bytes", bytes([0x00, 0x01]), 16, 16, False),
        ("gray-trailing-padding", bytes([0x00, 0xAA, 0xBB, 0xFF, 0xFF, 0xFF]), 320, 240, False),
        ("gray-consecutive-ff", bytes([0x00, 0xFF, 0xFF, 0x01, 0xFF]), 2048, 1536, False),
        ("gray-max-dimensions", bytes([0x00, 0x01, 0x02]), 65535, 65535, False),
        ("color-basic", bytes([0x01, 0x12, 0xFF, 0x00, 0x56, 0x78, 0xFF, 0xFF]), 20, 30, True),
        ("color-single-byte", bytes([0x01]), 2, 2, True),
        ("color-consecutive-ff", bytes([0x01, 0xFF, 0xFF, 0x00, 0xFF]), 1024, 768, True),
    ]
    out("    internal static readonly MiniJpegVector[] MiniJpegVectors =")
    out("    {")
    for name, data, width, height, is_color in mini_vectors:
        helper = camera._minicolor_to_jpeg if is_color else camera._minigray_to_jpeg
        expected = helper(camera.np.frombuffer(data, dtype=camera.np.uint8), width, height).tobytes()
        out(
            "        new MiniJpegVector(\"{name}\", {data}, {width}, {height}, {is_color}, {expected}),".format(
                name=name,
                data=hex_literal(data),
                width=width,
                height=height,
                is_color="true" if is_color else "false",
                expected=hex_literal(expected),
            )
        )
    out("    };")
    out("")

    # --- Chunk reassembly scenarios ---------------------------------------------
    resolution = clad_cam.ImageResolution.QQQQVGA
    width, height = camera.RESOLUTIONS[resolution]
    cam = make_camera(camera)

    valid_gray = (
        bytes([0x00, 0x01, 0x02, 0xFF, 0x03]),
        bytes([0x04, 0x05, 0xFF, 0x06]),
        bytes([0x07, 0x08, 0xFF]),
    )
    valid_color = (
        bytes([0x01, 0x11, 0xFF, 0x22]),
        bytes([0x33, 0x44, 0xFF]),
    )

    scenarios = {
        "InOrderGray": (
            [(0, 7, 3, valid_gray[0]), (1, 7, 3, valid_gray[1]), (2, 7, 3, valid_gray[2])],
            8,
        ),
        "OutOfOrderGray": (
            [
                (1, 7, 3, valid_gray[1]),
                (0, 7, 3, valid_gray[0]),
                (1, 7, 3, valid_gray[1]),
                (2, 7, 3, valid_gray[2]),
            ],
            8,
        ),
        "MissingChunkGray": (
            [
                (0, 7, 3, valid_gray[0]),
                (2, 7, 3, valid_gray[2]),
                (0, 7, 3, valid_gray[0]),
                (1, 7, 3, valid_gray[1]),
                (2, 7, 3, valid_gray[2]),
            ],
            8,
        ),
        "DuplicateChunkGray": (
            [
                (0, 7, 3, valid_gray[0]),
                (1, 7, 3, valid_gray[1]),
                (1, 7, 3, valid_gray[1]),
                (2, 7, 3, valid_gray[2]),
                (0, 7, 3, valid_gray[0]),
                (1, 7, 3, valid_gray[1]),
                (2, 7, 3, valid_gray[2]),
            ],
            8,
        ),
        "ChangedImageId": (
            [
                (0, 7, 3, valid_gray[0]),
                (1, 8, 3, valid_gray[1]),
                (2, 8, 3, valid_gray[2]),
                (0, 8, 3, valid_gray[0]),
                (1, 8, 3, valid_gray[1]),
                (2, 8, 3, valid_gray[2]),
            ],
            8,
        ),
        "InOrderColor": (
            [(0, 9, 2, valid_color[0]), (1, 9, 2, valid_color[1])],
            8,
        ),
        "NewImageRestarts": (
            [
                (0, 7, 2, bytes([0x01, 0x21, 0x22])),
                (0, 8, 2, valid_color[0]),
                (1, 8, 2, valid_color[1]),
            ],
            8,
        ),
        "NonMinimizedPassThrough": (
            [(0, 11, 1, bytes.fromhex("ffd8ffd9"))],
            5,
        ),
    }

    for name, (steps, encoding) in scenarios.items():
        images = run_chunk_scenario(camera, cam, steps, encoding, resolution)
        if not images:
            raise SystemExit(f"scenario {name} produced no image")
        if not images:
            raise SystemExit(f"scenario {name} produced no image")
        image = images[-1]
        out("    internal static readonly ChunkScenario " + name + " = new ChunkScenario(")
        out("        new[]")
        out("        {")
        out("            " + chunk_specs(steps, encoding, resolution))
        out("        },")
        out(f"        {hex_literal(image.data)},")
        out(f"        {len(images)},")
        out(f"        {width},")
        out(f"        {height});")
        out("")

    out("}")
    return "\n".join(lines)


def dump_nodes(node):
    yield node
    if node.children is not None:
        for child in node.children:
            yield from dump_nodes(child)


def emit_nav(nav) -> str:
    lines: list[str] = []
    out = lines.append

    origin_id = 7
    root_depth = 5
    root_size = 100.0
    root_center_x = 10.0
    root_center_y = -20.0

    grid = nav.NavMemoryMapGrid(
        origin_id, root_depth, root_size, root_center_x, root_center_y
    )
    adds = [
        (nav.NodeContentTypes.ClearOfObstacle, 4),
        (nav.NodeContentTypes.ObstacleCube, 4),
        (nav.NodeContentTypes.Cliff, 2),
        (nav.NodeContentTypes.VisionBorder, 0),
        (nav.NodeContentTypes.ClearOfCliff, 1),
        (nav.NodeContentTypes.ObstacleCharger, 0),
    ]
    for content, depth in adds:
        grid._add_quad(content.id, depth)

    nodes = list(dump_nodes(grid.root_node))
    leaves = [node for node in nodes if node.children is None]

    query_points: list[tuple[float, float]] = []
    for node in leaves:
        query_points.append((node.center.x, node.center.y))
    extra_points = [
        (root_center_x, root_center_y),
        (root_center_x + root_size / 2, root_center_y + root_size / 2),
        (root_center_x + root_size / 2 + 0.5, root_center_y),
        (root_center_x - root_size / 2, root_center_y - root_size / 2),
        (root_center_x, root_center_y - root_size / 2 - 0.5),
        (1000.0, 1000.0),
        (-1000.0, -1000.0),
    ]
    for point in extra_points:
        if point not in query_points:
            query_points.append(point)

    contains_cases = [
        (root_center_x, root_center_y),
        (root_center_x + root_size / 2, root_center_y),
        (root_center_x - root_size / 2, root_center_y),
        (root_center_x, root_center_y + root_size / 2),
        (root_center_x, root_center_y - root_size / 2),
        (root_center_x + root_size / 2, root_center_y + root_size / 2),
        (root_center_x - root_size / 2, root_center_y - root_size / 2),
        (root_center_x + root_size / 2 + 0.001, root_center_y),
        (root_center_x, root_center_y + root_size / 2 + 0.001),
        (1000.0, 1000.0),
        (-1000.0, -1000.0),
    ]

    content_types = [
        ("Unknown", nav.NodeContentTypes.Unknown.id),
        ("ClearOfObstacle", nav.NodeContentTypes.ClearOfObstacle.id),
        ("ClearOfCliff", nav.NodeContentTypes.ClearOfCliff.id),
        ("ObstacleCube", nav.NodeContentTypes.ObstacleCube.id),
        ("ObstacleCharger", nav.NodeContentTypes.ObstacleCharger.id),
        ("Cliff", nav.NodeContentTypes.Cliff.id),
        ("VisionBorder", nav.NodeContentTypes.VisionBorder.id),
        ("ObstacleProx", nav.NodeContentTypes._ObstacleProx.id),
    ]

    out("internal static class NavGoldenData")
    out("{")
    out(f"    internal const int OriginId = {origin_id};")
    out(f"    internal const int RootDepth = {root_depth};")
    out(f"    internal const double RootSize = {double_literal(root_size)};")
    out(f"    internal const double RootCenterX = {double_literal(root_center_x)};")
    out(f"    internal const double RootCenterY = {double_literal(root_center_y)};")
    out("")
    out("    internal static readonly (byte ContentId, int Depth)[] Adds =")
    out("    {")
    for content, depth in adds:
        out(f"        ({content.id}, {depth}),")
    out("    };")
    out("")
    out("    internal static readonly NavNodeDump[] Nodes =")
    out("    {")
    for node in nodes:
        content_name = node.content.name if node.content is not None else None
        child_count = len(node.children) if node.children is not None else 0
        out(
            "        new NavNodeDump({depth}, {size}, {x}, {y}, {z}, {children}, {content}),".format(
                depth=node.depth,
                size=double_literal(node.size),
                x=double_literal(node.center.x),
                y=double_literal(node.center.y),
                z=double_literal(node.center.z),
                children=child_count,
                content=string_or_null(content_name),
            )
        )
    out("    };")
    out("")
    out("    internal static readonly NavQuery[] Queries =")
    out("    {")
    for x, y in query_points:
        node = grid.get_node(x, y)
        content = grid.get_content(x, y)
        content_name = content.name if content is not None else None
        if node is None:
            out(
                "        new NavQuery({x}, {y}, false, -1, 0d, 0d, 0d, {content}),".format(
                    x=double_literal(x), y=double_literal(y), content=string_or_null(content_name)
                )
            )
        else:
            out(
                "        new NavQuery({x}, {y}, true, {depth}, {cx}, {cy}, {cz}, {content}),".format(
                    x=double_literal(x),
                    y=double_literal(y),
                    depth=node.depth,
                    cx=double_literal(node.center.x),
                    cy=double_literal(node.center.y),
                    cz=double_literal(node.center.z),
                    content=string_or_null(content_name),
                )
            )
    out("    };")
    out("")
    out("    internal static readonly (double X, double Y, bool Contains)[] ContainsCases =")
    out("    {")
    for x, y in contains_cases:
        out(
            "        ({x}, {y}, {inside}),".format(
                x=double_literal(x),
                y=double_literal(y),
                inside="true" if grid.contains_point(x, y) else "false",
            )
        )
    out("    };")
    out("")
    out("    internal static readonly (string Name, long Id)[] ContentTypes =")
    out("    {")
    for name, content_id in content_types:
        out(f'        ("{name}", {content_id}),')
    out("    };")
    out("}")
    return "\n".join(lines)


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--sdk", required=True, type=Path, help="reference SDK root")
    parser.add_argument("--output", required=True, type=Path, help="generated C# file")
    args = parser.parse_args()

    camera, nav, temp_dir = load_reference(args.sdk.resolve())
    try:
        body = "\n\n".join([emit_camera(camera), emit_nav(nav)])
        source = (
            LICENSE_HEADER
            + "\n// <auto-generated>\n// Generated by tools/camnavgen/generate_camera_nav_goldens.py.\n"
            + "// </auto-generated>\n\nnamespace Cozmo.Tests;\n\n"
            + body
            + "\n"
        )
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(source, encoding="utf-8")
        print(f"wrote {args.output}")
    finally:
        temp_dir.cleanup()


if __name__ == "__main__":
    main()
