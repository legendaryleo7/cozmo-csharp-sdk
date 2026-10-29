# CLAD code generator

Regenerates the C# CLAD layer in `src/Cozmo/Clad/Generated` and the byte-exact oracle
tests in `tests/Cozmo.Tests/CladOracleTests.g.cs` from a cozmoclad release.

The Python source is the specification: cozmoclad's generated modules encode every wire
operation explicitly, and the wheel vendors `msgbuffers`, which lets the generator run the
real Python implementation as an oracle.

## Usage

```bash
# 1. Extract a schema from the cozmoclad wheel (or an extracted package directory).
python3 tools/cladgen/extract_schema.py \
  --cozmoclad ../ddl-cozmo-python-sdk/cozmoclad/cozmoclad-3.6.6-py3-none-any.whl \
  --out tools/cladgen/clad_schema.json

# 2. Emit the C# layer.
python3 tools/cladgen/emit_csharp.py \
  --schema tools/cladgen/clad_schema.json \
  --out-dir src/Cozmo/Clad/Generated

# 3. Emit the oracle tests (runs the Python CLAD layer, never the C# one).
python3 tools/cladgen/emit_oracle_tests.py \
  --schema tools/cladgen/clad_schema.json \
  --cozmoclad ../ddl-cozmo-python-sdk/cozmoclad/cozmoclad-3.6.6-py3-none-any.whl \
  --out tests/Cozmo.Tests/CladOracleTests.g.cs

# 4. Verify.
dotnet test tests/Cozmo.Tests
```

## What is generated

| Output | Contents |
|---|---|
| `src/Cozmo/Clad/Generated/*.g.cs` | 739 CLAD types in 18 namespaces: enums, structs, messages and unions with `Pack`/`Unpack` |
| `src/Cozmo/Clad/Generated/CladIdentity.g.cs` | cozmoclad version, app build version and both 16-byte hashes in wire order |
| `tests/Cozmo.Tests/CladOracleTests.g.cs` | one test per message in both top-level unions |

The oracle test builds the same value in Python, packs it there, and asserts that the C#
layer produces identical bytes and round-trips them. Never edit generated files by hand.

## Scope and assumptions

- Only `clad` and `util/ankiLab` modules are read.
- Union tag width comes from the generated Python: top-level unions use `uint_16`,
  smaller nested unions use `uint_8`.
- Array sizes written as enum constants (for example
  `Anki::Vision::FacialExpression::Count`) are resolved through the extracted enums.
