# camnavgen

Generates `tests/Cozmo.Tests/CameraNavGoldenData.g.cs` by running the reference
Python implementation of `cozmo/camera.py` and `cozmo/nav_memory_map.py`.

The camera module imports NumPy and Pillow when it loads, so the generator
interpreter needs both packages. For example, with `uv`:

```sh
uv pip install --python python3 --target .scratch/pylibs numpy pillow
```

Then regenerate from the repository root:

```sh
PYTHONPATH=.scratch/pylibs \
python3 tools/camnavgen/generate_camera_nav_goldens.py \
    --sdk /path/to/ddl-cozmo-python-sdk \
    --output tests/Cozmo.Tests/CameraNavGoldenData.g.cs
```

The script copies the reference modules into a temporary `cozmo` package,
supplies small `event`/`logger` stubs, and imports the real `cozmoclad` classes
from the wheel bundled with the reference SDK. Every byte vector, config
value, quad-tree node, and query result in the generated file comes from
executing Python; nothing is hand-written. The generated file is checked in so
the test suite does not need Python or the reference SDK.

Chunk scenarios run the real `Camera._recv_msg_image_chunk` state machine with
a stand-in for the Pillow decoder. The stand-in records the JPEG bytes that
`_process_completed_image` would have decoded, so the C# tests compare exactly
the bytes produced by the Python chunk handler and mini-JPEG header repair.
