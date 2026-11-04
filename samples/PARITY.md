# Python example parity

The C# examples live in [`Cozmo.Examples`](Cozmo.Examples) and mirror the Python SDK's
`examples/` tree. **47 of the 52 example programs are ported.** The five that are not
ported depend on Python-only GUI, imaging or shell stacks; each is listed with its reason
below.

All ported examples are part of one console project so they build together and share the
small helpers for OLED images, image loading, light chasers and IFTTT hosting. Each example
keeps the Python file's name and directory, and its command-line key is the Python path
without the `.py` extension.

```bash
dotnet run --project samples/Cozmo.Examples -- --list
dotnet run --project samples/Cozmo.Examples -- tutorials/01_basics/01_hello_world
```

Arguments after the key are passed to the example. Multi-robot examples need two device
selectors; IFTTT examples start an HTTP listener on `127.0.0.1:8080`.

## Adaptations shared by all ports

- **Async API.** The C# SDK is async-only; Python's synchronous `wait_for_completed()` calls
  become `await action.WaitForCompletedAsync()`.
- **Drive off charger.** The Python SDK drives off the charger before a program unless
  `drive_off_charger_on_connect` is false. `ExampleRunner` reproduces that default, and the
  examples that set the Python flag false pass `driveOffChargerOnConnect: false`.
- **Viewers and annotations.** The C# SDK has no Tk/OpenGL viewer or image annotator.
  Examples that only used a viewer for debugging run without it; the annotation example is
  not ported.
- **Imaging.** The SDK deliberately has no image stack. The examples use the small,
  public-domain StbImageSharp decoder (package reference only in the example project) for
  the Python examples that used Pillow/numpy. The SDK itself remains image-library-free.
- **Sealed types.** `LightCube` and `Robot` are sealed, so Python subclass-based helpers
  (light chasers, IFTTT robot helpers) are composed in `Helpers/` instead of subclassed.

## Complete matrix

`C# file` is relative to `samples/Cozmo.Examples/`.

### Tutorials

| Python example | C# key | C# file | Status |
|---|---|---|---|
| `tutorials/01_basics/01_hello_world.py` | `tutorials/01_basics/01_hello_world` | `Tutorials/01_Basics/01_HelloWorld.cs` | Ported |
| `tutorials/01_basics/02_drive_and_turn.py` | `tutorials/01_basics/02_drive_and_turn` | `Tutorials/01_Basics/02_DriveAndTurn.cs` | Ported |
| `tutorials/01_basics/03_count.py` | `tutorials/01_basics/03_count` | `Tutorials/01_Basics/03_Count.cs` | Ported |
| `tutorials/01_basics/04_drive_square.py` | `tutorials/01_basics/04_drive_square` | `Tutorials/01_Basics/04_DriveSquare.cs` | Ported |
| `tutorials/01_basics/05_motors.py` | `tutorials/01_basics/05_motors` | `Tutorials/01_Basics/05_Motors.cs` | Ported |
| `tutorials/01_basics/06_sing_scales.py` | `tutorials/01_basics/06_sing_scales` | `Tutorials/01_Basics/06_SingScales.cs` | Ported |
| `tutorials/01_basics/07_backpack_lights.py` | `tutorials/01_basics/07_backpack_lights` | `Tutorials/01_Basics/07_BackpackLights.cs` | Ported |
| `tutorials/01_basics/08_animation.py` | `tutorials/01_basics/08_animation` | `Tutorials/01_Basics/08_Animation.cs` | Ported |
| `tutorials/01_basics/09_cube_lights.py` | `tutorials/01_basics/09_cube_lights` | `Tutorials/01_Basics/09_CubeLights.cs` | Ported |
| `tutorials/01_basics/10_play_sound.py` | `tutorials/01_basics/10_play_sound` | `Tutorials/01_Basics/10_PlaySound.cs` | Ported |
| `tutorials/01_basics/11_play_song.py` | `tutorials/01_basics/11_play_song` | `Tutorials/01_Basics/11_PlaySong.cs` | Ported |
| `tutorials/01_basics/12_random_animation.py` | `tutorials/01_basics/12_random_animation` | `Tutorials/01_Basics/12_RandomAnimation.cs` | Ported |
| `tutorials/02_cozmo_face/01_cozmo_face_image.py` | `tutorials/02_cozmo_face/01_cozmo_face_image` | `Tutorials/02_CozmoFace/01_CozmoFaceImage.cs` | Ported |
| `tutorials/02_cozmo_face/02_cozmo_face_mirror.py` | `tutorials/02_cozmo_face/02_cozmo_face_mirror` | `Tutorials/02_CozmoFace/02_CozmoFaceMirror.cs` | Ported |
| `tutorials/02_cozmo_face/03_alarm_clock.py` | `tutorials/02_cozmo_face/03_alarm_clock` | `Tutorials/02_CozmoFace/03_AlarmClock.cs` | Ported |
| `tutorials/03_vision/01_light_when_face.py` | `tutorials/03_vision/01_light_when_face` | `Tutorials/03_Vision/01_LightWhenFace.cs` | Ported |
| `tutorials/03_vision/02_face_follower.py` | `tutorials/03_vision/02_face_follower` | `Tutorials/03_Vision/02_FaceFollower.cs` | Ported |
| `tutorials/03_vision/03_annotate.py` | — | — | Not ported: requires Pillow/numpy, Tk viewer and the Python image annotator. |
| `tutorials/03_vision/04_exposure.py` | `tutorials/03_vision/04_exposure` | `Tutorials/03_Vision/04_Exposure.cs` | Ported without the viewer annotation overlay. |
| `tutorials/04_cubes_and_objects/01_go_to_pose_test.py` | `tutorials/04_cubes_and_objects/01_go_to_pose_test` | `Tutorials/04_CubesAndObjects/01_GoToPoseTest.cs` | Ported |
| `tutorials/04_cubes_and_objects/02_create_wall.py` | `tutorials/04_cubes_and_objects/02_create_wall` | `Tutorials/04_CubesAndObjects/02_CreateWall.cs` | Ported |
| `tutorials/04_cubes_and_objects/03_go_to_object_test.py` | `tutorials/04_cubes_and_objects/03_go_to_object_test` | `Tutorials/04_CubesAndObjects/03_GoToObjectTest.cs` | Ported |
| `tutorials/04_cubes_and_objects/04_stack_or_roll.py` | `tutorials/04_cubes_and_objects/04_stack_or_roll` | `Tutorials/04_CubesAndObjects/04_StackOrRoll.cs` | Ported |
| `tutorials/04_cubes_and_objects/05_cube_stack.py` | `tutorials/04_cubes_and_objects/05_cube_stack` | `Tutorials/04_CubesAndObjects/05_CubeStack.cs` | Ported |
| `tutorials/04_cubes_and_objects/06_pickup_furthest.py` | `tutorials/04_cubes_and_objects/06_pickup_furthest` | `Tutorials/04_CubesAndObjects/06_PickupFurthest.cs` | Ported |
| `tutorials/04_cubes_and_objects/07_lookaround.py` | `tutorials/04_cubes_and_objects/07_lookaround` | `Tutorials/04_CubesAndObjects/07_Lookaround.cs` | Ported |
| `tutorials/04_cubes_and_objects/08_drive_to_charger_test.py` | `tutorials/04_cubes_and_objects/08_drive_to_charger_test` | `Tutorials/04_CubesAndObjects/08_DriveToChargerTest.cs` | Ported |
| `tutorials/04_cubes_and_objects/09_custom_objects.py` | `tutorials/04_cubes_and_objects/09_custom_objects` | `Tutorials/04_CubesAndObjects/09_CustomObjects.cs` | Ported |
| `tutorials/04_cubes_and_objects/10_object_moved.py` | `tutorials/04_cubes_and_objects/10_object_moved` | `Tutorials/04_CubesAndObjects/10_ObjectMoved.cs` | Ported |
| `tutorials/04_cubes_and_objects/11_dock_with_cube.py` | `tutorials/04_cubes_and_objects/11_dock_with_cube` | `Tutorials/04_CubesAndObjects/11_DockWithCube.cs` | Ported |
| `tutorials/04_cubes_and_objects/12_roll_cube.py` | `tutorials/04_cubes_and_objects/12_roll_cube` | `Tutorials/04_CubesAndObjects/12_RollCube.cs` | Ported |
| `tutorials/04_cubes_and_objects/13_pop_a_wheelie.py` | `tutorials/04_cubes_and_objects/13_pop_a_wheelie` | `Tutorials/04_CubesAndObjects/13_PopAWheelie.cs` | Ported |
| `tutorials/05_async_python/01_cube_blinker_sync.py` | `tutorials/05_async_python/01_cube_blinker_sync` | `Tutorials/05_AsyncPython/01_CubeBlinkerSync.cs` | Ported (async, as the C# SDK has no sync proxy). |
| `tutorials/05_async_python/02_cube_blinker.py` | `tutorials/05_async_python/02_cube_blinker` | `Tutorials/05_AsyncPython/02_CubeBlinker.cs` | Ported |
| `tutorials/06_actions/01_parallel_actions.py` | `tutorials/06_actions/01_parallel_actions` | `Tutorials/06_Actions/01_ParallelActions.cs` | Ported |

### Tools

| Python example | C# key | C# file | Status |
|---|---|---|---|
| `tools/cubes/auto_cube_disconnect.py` | `tools/cubes/auto_cube_disconnect` | `Tools/Cubes/AutoCubeDisconnect.cs` | Ported; takes `-e/--enable` or `-d/--disable`. |
| `tools/cubes/connect_cubes.py` | `tools/cubes/connect_cubes` | `Tools/Cubes/ConnectCubes.cs` | Ported |
| `tools/cubes/disconnect_cubes.py` | `tools/cubes/disconnect_cubes` | `Tools/Cubes/DisconnectCubes.cs` | Ported |
| `tools/cubes/log_cube_battery_levels.py` | `tools/cubes/log_cube_battery_levels` | `Tools/Cubes/LogCubeBatteryLevels.cs` | Ported |

### Apps

| Python example | C# key | C# file | Status |
|---|---|---|---|
| `apps/3d_viewer.py` | — | — | Not ported: requires the Python OpenGL/Tk 3D viewer, which the C# SDK does not provide. |
| `apps/cli.py` | — | — | Not ported: embeds IPython; C# has no equivalent embedded shell. |
| `apps/color_finder.py` | — | — | Not ported: a numpy/Pillow computer-vision application built around the Python annotator; it is not a thin SDK example. |
| `apps/desk_security_guard.py` | `apps/desk_security_guard` | `Apps/DeskSecurityGuard.cs` | Ported without the Tk viewer; set `COZMO_OWNER_FACE` to the enrolled name. |
| `apps/quick_tap.py` | `apps/quick_tap` | `Apps/QuickTap.cs` | Ported; the cube light effects are composed instead of subclassing the sealed cube. |
| `apps/quizmaster_cozmo.py` | `apps/quizmaster_cozmo` | `Apps/QuizmasterCozmo.cs` | Ported |
| `apps/remote_control_cozmo.py` | — | — | Not ported: a Flask/HTML/JavaScript remote-control web UI; porting it is a separate ASP.NET Core application. |

### If This Then That

`common/__init__.py` and `lib/flask_helpers.py` are helper modules, not examples; their
robot helpers are in `Helpers/IfThisThenThatRobot.cs` and the web host is
`IfThisThenThat/IfThisThenThatServer.cs`.

| Python example | C# key | C# file | Status |
|---|---|---|---|
| `if_this_then_that/ifttt_gmail.py` | `if_this_then_that/ifttt_gmail` | `IfThisThenThat/IftttGmail.cs` | Ported; listens on `127.0.0.1:8080/iftttGmail`. |
| `if_this_then_that/ifttt_sports.py` | `if_this_then_that/ifttt_sports` | `IfThisThenThat/IftttSports.cs` | Ported; listens on `/iftttSports`. |
| `if_this_then_that/ifttt_sports_flask.py` | `if_this_then_that/ifttt_sports_flask` | `IfThisThenThat/IftttSportsFlask.cs` | Ported; preserves the queue/worker shape with a channel. |
| `if_this_then_that/ifttt_stocks.py` | `if_this_then_that/ifttt_stocks` | `IfThisThenThat/IftttStocks.cs` | Ported; listens on `/iftttStocks`. |

### Multiple robots

| Python example | C# key | C# file | Status |
|---|---|---|---|
| `multi_robot/multi_robot_independent.py` | `multi_robot/multi_robot_independent` | `MultiRobot/MultiRobotIndependent.cs` | Ported; pass two device selectors (see below). |
| `multi_robot/multi_robot_unified.py` | `multi_robot/multi_robot_unified` | `MultiRobot/MultiRobotUnified.cs` | Ported; pass two device selectors (see below). |

The C# runner needs a distinct connector per robot. The multi-robot examples accept:

```bash
# Android devices
dotnet run --project samples/Cozmo.Examples -- multi_robot/multi_robot_unified \
  --connector android --serial1 <serial1> --serial2 <serial2>

# iOS devices
dotnet run --project samples/Cozmo.Examples -- multi_robot/multi_robot_unified \
  --connector ios --serial1 <udid1> --serial2 <udid2>

# Two engines over TCP
dotnet run --project samples/Cozmo.Examples -- multi_robot/multi_robot_unified \
  --connector tcp --port1 5106 --port2 5107
```

## Verification

The examples build with the rest of the solution:

```bash
dotnet build Cozmo.sln -warnaserror
dotnet test Cozmo.sln
dotnet run --project samples/Cozmo.Examples -- --list
```
