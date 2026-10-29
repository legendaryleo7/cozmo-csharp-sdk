# utilgen

Generates `tests/Cozmo.Tests/UtilGoldenData.g.cs` by running the reference
Python implementation of `cozmo/util.py`. The generated file is checked in so
the test suite does not need Python or the reference SDK.

Regenerate from the repository root after changing the Python reference or the
test coverage:

```sh
python3 tools/utilgen/generate_util_goldens.py \
    --sdk /path/to/ddl-cozmo-python-sdk \
    --output tests/Cozmo.Tests/UtilGoldenData.g.cs
```

The script copies `src/cozmo/util.py` into a temporary package with an empty
`cozmo/__init__.py` and a minimal `_clad.py` shim, then imports the real
`cozmoclad` classes from the wheel bundled with the reference SDK. Every
expected value and string in the generated file is produced by Python; none is
hand-written. `tests/Cozmo.Tests/UtilTests.cs` consumes the data.
