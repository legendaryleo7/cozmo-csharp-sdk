# Cozmo C# examples

This project contains C# ports of the programs in the Python SDK's `examples/` tree.
Each example keeps the Python file's directory and name; the command-line key is the Python
path without the `.py` extension.

```bash
# From the repository root
dotnet run --project samples/Cozmo.Examples -- --list

# Run one example
dotnet run --project samples/Cozmo.Examples -- tutorials/01_basics/01_hello_world

# Pass arguments to the example
dotnet run --project samples/Cozmo.Examples -- tools/cubes/auto_cube_disconnect --enable
dotnet run --project samples/Cozmo.Examples -- multi_robot/multi_robot_unified \
  --connector android --serial1 <serial1> --serial2 <serial2>
```

The complete Python-to-C# matrix, including the five Python-only examples that are not
ported, is in [`../PARITY.md`](../PARITY.md).

## Layout

| Path | Contents |
|---|---|
| `Program.cs` | command-line dispatcher and `--list` |
| `ExampleCatalog.cs` | every ported example, keyed by its Python path |
| `Tutorials/` | ports of `examples/tutorials/` |
| `Tools/Cubes/` | ports of `examples/tools/cubes/` |
| `Apps/` | ports of the portable `examples/apps/` programs |
| `IfThisThenThat/` | ports of `examples/if_this_then_that/` (HTTP listener on port 8080) |
| `MultiRobot/` | ports of `examples/multi_robot/` (two device selectors) |
| `Helpers/` | shared runner, image, OLED, light-chaser and IFTTT helpers |
| `Assets/` | face images and quiz data copied from the Python examples |

The examples project references StbImageSharp to decode the PNG/JPEG assets used by the
imaging ports. The `Cozmo` library itself remains free of image-library dependencies.

## Notes

- The C# SDK is async-only, so the ports use `await` throughout.
- `ExampleRunner` reproduces the Python default of driving off the charger before the
  program starts. Examples that disable that in Python pass
  `driveOffChargerOnConnect: false`.
- Python examples that subclass `Robot` or `LightCube` are composed with helpers because
  the C# types are sealed.
- The C# SDK has no Tk/OpenGL viewer or image annotator; examples that only used them for
  display run without a viewer, and the annotation example is documented as not ported.
