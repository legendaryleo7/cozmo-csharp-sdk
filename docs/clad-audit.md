# CLAD audit: shipped `CladCSharp.dll` vs cozmoclad 3.6.6

Status: **complete**. This is the WP-00 deliverable for the Python 1.4.12 → C# port.

## Decision

**Do not use `clad_library/CladCSharp.dll` as the CLAD layer. Regenerate the CLAD C#
layer from cozmoclad 3.6.6 and verify it byte-for-byte against the Python runtime.**

Three independent blockers make the shipped DLL unusable, in increasing order of severity:

1. It cannot be loaded by any modern .NET runtime (32-bit-only, .NET Framework era).
2. Its wire format differs from 3.6.6 (one-byte union tags vs two-byte tags).
3. Its schema is a 2018-era CLAD whose messages disagree with 3.6.6, including the
   single most important inbound message (`RobotState`).

The regenerated layer is implemented in WP-CLAD and already validated: 739 CLAD types,
410 byte-exact oracle cases, all passing.

## Artifacts examined

| Artifact | Identity |
|---|---|
| `clad_library/CladCSharp.dll` | 1,355,264 bytes, SHA-256 `423553930307d1f63c72884aa509d99c0bd341aef536265c4517438fc74ec09b` |
| cozmoclad 3.6.6 | `ddl-cozmo-python-sdk/cozmoclad/cozmoclad-3.6.6-py3-none-any.whl` |
| Python SDK source | `ddl-cozmo-python-sdk` at `Release 1.4.12` (`__cozmoclad_version__ = "3.6.6"`) |

The `cozmo-python-sdk` checkout in this workspace is `1.4.11.dev0` with
`__cozmoclad_version__ = None` and differs from 1.4.12 in `robot.py`, `conn.py`,
`event.py` and `action.py`. It is not the reference; `ddl-cozmo-python-sdk` is.

## Finding 1: the DLL cannot be loaded

| Property | Value |
|---|---|
| PE type | PE32 (I386), 3 sections |
| CLR header | runtime 2.5 (a .NET 4.x-era assembly) |
| `COMIMAGE_FLAGS` | `0x00000003` = `ILONLY` + **`32BITREQUIRED`** |
| COFF characteristics | includes `IMAGE_FILE_32BIT_MACHINE` |
| Assembly name/version | `CladCSharp, Version=0.0.0.0` |
| Referenced assemblies | `mscorlib` only |

Loading it from a `net10.0` x64 process gives
`FileLoadException: The assembly architecture is not compatible with the current process
architecture`. Clearing `32BITREQUIRED` and `IMAGE_FILE_32BIT_MACHINE` on a copy (a valid
operation for a pure-IL assembly) still gives `BadImageFormatException`.

The README states the reason plainly: users **"must remain on version 3.0 of the Cozmo
mobile app"**. The DLL targets app 3.0; the port targets app 3.6 (cozmoclad 3.6.6).

Metadata is nevertheless readable without loading, and ILSpy 11.2 decompiles it to
2.27 MB of C# that **compiles cleanly on net10.0**. It is a useful style reference and
proves the CLAD emitter's output shape is valid modern C#, but it is not the base.

## Finding 2: the schema and wire format are not 3.6.6

### 2a. Declared CLAD identity

| Value | Shipped DLL | cozmoclad 3.6.6 |
|---|---|---|
| `CladVersion._Data` / `__version__` | `"3.0.1"` | `"3.6.0"` |
| App build version | n/a | `"00003.00006.00000"` |
| Engine→Game hash | `84c15bde513f3659046b97de66547b69` | `9600964e76f57c5e4bdf4a72a6903eda` |
| Game→Engine hash | `2b5f5a957ca9df61ddc68d2654f9b319` | `4ebbcab05f9a2c3957b2d1ab20c4303a` |

The handshake compares both hashes (`conn.py:329`), so the DLL could never complete a
handshake with a 3.6.6 app.

### 2b. Union tag width

The DLL's union `Pack` writes the type tag as **one byte**:

```csharp
writer.Write((byte)GetTag());
...
Tag tag = (Tag)reader.ReadByte();
```

cozmoclad 3.6.6 writes a **`uint16`** for both top-level unions
(`MessageGameToEngine.py`: `writer.write(self._tag, 'H')`,
`tag = reader.read('H')`). Nested unions use `uint_8` when they have few alternatives.
Every frame would be misaligned by one byte.

### 2c. Missing messages and tag renumbering

`MessageGameToEngine` (255 Python tags, 254 DLL tags):

| Difference | Detail |
|---|---|
| Missing in DLL | `ConnectToSpecificCube`, `DisconnectFromSpecificCube` |
| DLL-only | `INVALID` |
| Renumbered | 19 tags: `SaveCalibrationImage` 236→234 through `RequestRobotSettings` 254→252 |

`MessageEngineToGame` (155 tags each):

| Difference | Detail |
|---|---|
| Missing in DLL | `CSharpTrickStarted` |
| DLL-only | `INVALID` |
| Renumbered | 11 tags: `OnboardingState` 144→143 through `EndOfMessage` 154→153 |

### 2d. Message field drift

Most message field lists are unchanged (176/182 and 119/123 share the same field names),
but the differences land on messages the SDK depends on:

| Message | 3.6.6 | DLL |
|---|---|---|
| `RobotState` | 17 fields: `pose`, `poseAngle_rad`, `posePitch_rad`, `leftWheelSpeed_mmps`, `rightWheelSpeed_mmps`, `headAngle_rad`, `liftHeight_mm`, `batteryVoltage`, `accel`, `gyro`, 4 object ids, `lastImageTimeStamp`, `status`, `gameStatus` | 15 fields: `timestamp`, `pose_frame_id`, `pose_origin_id`, `pose`, `lwheel_speed_mmps`, `rwheel_speed_mmps`, `headAngle`, `liftAngle`, `accel`, `gyro`, `batteryVoltage`, `status`, `cliffDataRaw`, `backpackTouchSensorRaw`, `currPathSegment` |
| `UiDeviceConnected` | 6 fields | 7 fields (extra `reserved` first) |
| `UiDeviceConnectionWrongVersion` | 3 fields | 4 fields (extra `reserved` first) |
| `NotificationTextKeys` | `textKeys`, `titleKeys` | `textKeys` only |
| `SetDeterminantData` | 3 fields | `installDateTimestamp` only |
| `ExitSdkMode` | 2 fields | `isExternedSdkMode` only |
| `EraseQuad` | `quadID` | `quadType`, `quadID` |

`RobotState` alone rules the DLL out: its layout, field set and even its `pose` struct
type (`RobotPose` vs `PoseStruct3d`) are different, so robot state could not be decoded.

## Finding 3: the Python side is a complete, executable specification

The generated cozmoclad modules encode the wire format explicitly rather than
declaratively:

- each class has a kind docstring and `__slots__` with CLAD type comments;
- each field is read with an explicit operation: `read('f')`, `read_string('B')`,
  `read_farray('B', 16)`, `read_varray('B', 'H')`, `read_object(T.unpack_from)`,
  `read_object_farray(T, n)`, `read_object_varray(T, 'H')`, `read_string_varray(...)`;
- unions list alternatives with tag values and `_tag_unpack_methods` lambdas, so even
  primitive-only unions are fully specified;
- the wheel vendors `msgbuffers`, so the Python layer runs locally as an oracle.

There are 12 distinct wire operations in total, and 739 CLAD types (185 enums, 93
structs, 12 unions, 449 messages). The two SDK unions carry 255 + 155 tags.

## What we built instead (WP-CLAD)

| Step | Tool | Result |
|---|---|---|
| Schema extraction | `tools/cladgen/extract_schema.py` | `tools/cladgen/clad_schema.json`, 739 types |
| C# emission | `tools/cladgen/emit_csharp.py` | `src/Cozmo/Clad/Generated/*.g.cs`, 18 namespaces |
| Runtime | `src/Cozmo/Clad/CladBinary{Reader,Writer}.cs` | primitives, strings, fixed/variable arrays |
| Oracle tests | `tools/cladgen/emit_oracle_tests.py` | 410 cases, byte-compared with cozmoclad |

Validation result: **410/410 oracle cases pass** — every message in both unions is packed
identically by C# and Python (including tags, nested structs and unions, strings, fixed
arrays, variable arrays, object arrays), and every C# decode re-encodes to the same bytes.
`CladIdentity` carries the 3.6.6 hashes in wire order for the handshake.

## Consequences for later work packages

- **No later WP needs hand-written messages.** The generated layer is complete, so
  WP-05 (anim/behavior/audio), WP-08 (objects), WP-09 (faces/pets), WP-10 (camera/nav)
  and WP-11 (robot/world) consume generated types as-is.
- WP-03 sends `CladIdentity.BuildVersion` and compares `CladIdentity.EngineToGameHash` /
  `GameToEngineHash`; the old `CladVersion._Data` and `SDKVersion._Data` constants are
  replaced.
- WP-13 removes `clad_library/CladCSharp.dll` from the build. The binary can be kept in
  `legacy/` for historical reference only; nothing may reference it.
- The old sample (`csharp_interface/`) is not compiled after WP-01; WP-13 ports it to the
  new API.

## Reproduction

```bash
# Extract the schema and emit the CLAD layer (needs the cozmoclad wheel).
python3 tools/cladgen/extract_schema.py \
  --cozmoclad ../ddl-cozmo-python-sdk/cozmoclad/cozmoclad-3.6.6-py3-none-any.whl \
  --workdir /tmp/cladwork --out tools/cladgen/clad_schema.json
python3 tools/cladgen/extract_schema.py --help

# Emit C# then regenerate the oracle tests from the Python runtime.
python3 tools/cladgen/emit_csharp.py --schema tools/cladgen/clad_schema.json --out-dir src/Cozmo/Clad/Generated
python3 tools/cladgen/emit_oracle_tests.py \
  --schema tools/cladgen/clad_schema.json \
  --cozmoclad ../ddl-cozmo-python-sdk/cozmoclad/cozmoclad-3.6.6-py3-none-any.whl \
  --out tests/Cozmo.Tests/CladOracleTests.g.cs

# Verify.
dotnet test tests/Cozmo.Tests
```
