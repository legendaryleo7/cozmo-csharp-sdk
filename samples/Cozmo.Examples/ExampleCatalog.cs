// Copyright (c) 2016-2017 Anki, Inc.
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

using Cozmo.Samples.Examples.Apps;
using Cozmo.Samples.Examples.IfThisThenThat;
using Cozmo.Samples.Examples.MultiRobot;
using Cozmo.Samples.Examples.Tools.Cubes;
using Cozmo.Samples.Examples.Tutorials.Actions;
using Cozmo.Samples.Examples.Tutorials.AsyncPython;
using Cozmo.Samples.Examples.Tutorials.Basics;
using Cozmo.Samples.Examples.Tutorials.CozmoFace;
using Cozmo.Samples.Examples.Tutorials.CubesAndObjects;
using Cozmo.Samples.Examples.Tutorials.Vision;
using AsyncCubeBlinker = Cozmo.Samples.Examples.Tutorials.AsyncPython.CubeBlinker;

namespace Cozmo.Samples.Examples;

/// <summary>The catalogue of C# example ports.</summary>
public static class ExampleCatalog
{
    /// <summary>Every ported example, in Python directory order.</summary>
    public static IReadOnlyList<ExampleDefinition> All { get; } =
    [
        new(
            "tutorials/01_basics/01_hello_world",
            "examples/tutorials/01_basics/01_hello_world.py",
            "Say 'Hello World'.",
            HelloWorld.RunAsync),
        new(
            "tutorials/01_basics/02_drive_and_turn",
            "examples/tutorials/01_basics/02_drive_and_turn.py",
            "Drive forwards and turn 90 degrees.",
            DriveAndTurn.RunAsync),
        new(
            "tutorials/01_basics/03_count",
            "examples/tutorials/01_basics/03_count.py",
            "Count from 1 to 5.",
            Count.RunAsync),
        new(
            "tutorials/01_basics/04_drive_square",
            "examples/tutorials/01_basics/04_drive_square.py",
            "Drive in a square.",
            DriveSquare.RunAsync),
        new(
            "tutorials/01_basics/05_motors",
            "examples/tutorials/01_basics/05_motors.py",
            "Drive the wheel, lift and head motors directly.",
            Motors.RunAsync),
        new(
            "tutorials/01_basics/06_sing_scales",
            "examples/tutorials/01_basics/06_sing_scales.py",
            "Sing Do Re Mi with a rising voice pitch.",
            SingScales.RunAsync),
        new(
            "tutorials/01_basics/07_backpack_lights",
            "examples/tutorials/01_basics/07_backpack_lights.py",
            "Cycle the backpack lights.",
            BackpackLights.RunAsync),
        new(
            "tutorials/01_basics/08_animation",
            "examples/tutorials/01_basics/08_animation.py",
            "Play animations by trigger and by name.",
            Animation.RunAsync),
        new(
            "tutorials/01_basics/09_cube_lights",
            "examples/tutorials/01_basics/09_cube_lights.py",
            "Set the three light cubes to red, green and blue.",
            CubeLights.RunAsync),
        new(
            "tutorials/01_basics/10_play_sound",
            "examples/tutorials/01_basics/10_play_sound.py",
            "Play one-off sounds and the tiny-orchestra loop.",
            PlaySound.RunAsync),
        new(
            "tutorials/01_basics/11_play_song",
            "examples/tutorials/01_basics/11_play_song.py",
            "Play a rising scale and varying note durations.",
            PlaySong.RunAsync),
        new(
            "tutorials/01_basics/12_random_animation",
            "examples/tutorials/01_basics/12_random_animation.py",
            "Play random triggers and triggers containing 'WinGame'.",
            RandomAnimation.RunAsync),
        new(
            "tutorials/02_cozmo_face/01_cozmo_face_image",
            "examples/tutorials/02_cozmo_face/01_cozmo_face_image.py",
            "Display two images on Cozmo's face.",
            CozmoFaceImage.RunAsync),
        new(
            "tutorials/02_cozmo_face/02_cozmo_face_mirror",
            "examples/tutorials/02_cozmo_face/02_cozmo_face_mirror.py",
            "Mirror the camera feed on Cozmo's face.",
            CozmoFaceMirror.RunAsync),
        new(
            "tutorials/02_cozmo_face/03_alarm_clock",
            "examples/tutorials/02_cozmo_face/03_alarm_clock.py",
            "Use Cozmo's face as a clock and alarm.",
            AlarmClock.RunAsync),
        new(
            "tutorials/03_vision/01_light_when_face",
            "examples/tutorials/03_vision/01_light_when_face.py",
            "Light the backpack while a face is visible.",
            LightWhenFace.RunAsync),
        new(
            "tutorials/03_vision/02_face_follower",
            "examples/tutorials/03_vision/02_face_follower.py",
            "Turn towards a face continuously.",
            FaceFollower.RunAsync),
        new(
            "tutorials/03_vision/04_exposure",
            "examples/tutorials/03_vision/04_exposure.py",
            "Demonstrate camera exposure and gain settings.",
            Exposure.RunAsync),
        new(
            "tutorials/04_cubes_and_objects/01_go_to_pose_test",
            "examples/tutorials/04_cubes_and_objects/01_go_to_pose_test.py",
            "Drive to a pose relative to the robot.",
            GoToPoseTest.RunAsync),
        new(
            "tutorials/04_cubes_and_objects/02_create_wall",
            "examples/tutorials/04_cubes_and_objects/02_create_wall.py",
            "Create an invisible wall and drive around it.",
            CreateWall.RunAsync),
        new(
            "tutorials/04_cubes_and_objects/03_go_to_object_test",
            "examples/tutorials/04_cubes_and_objects/03_go_to_object_test.py",
            "Find a cube and drive to within 70 mm.",
            GoToObjectTest.RunAsync),
        new(
            "tutorials/04_cubes_and_objects/04_stack_or_roll",
            "examples/tutorials/04_cubes_and_objects/04_stack_or_roll.py",
            "React to the number of cubes Cozmo sees.",
            StackOrRoll.RunAsync),
        new(
            "tutorials/04_cubes_and_objects/05_cube_stack",
            "examples/tutorials/04_cubes_and_objects/05_cube_stack.py",
            "Pick up one cube and place it on another.",
            CubeStack.RunAsync),
        new(
            "tutorials/04_cubes_and_objects/06_pickup_furthest",
            "examples/tutorials/04_cubes_and_objects/06_pickup_furthest.py",
            "Pick up the furthest cube.",
            PickupFurthest.RunAsync),
        new(
            "tutorials/04_cubes_and_objects/07_lookaround",
            "examples/tutorials/04_cubes_and_objects/07_lookaround.py",
            "Look around, pick up a cube, and put it down.",
            Lookaround.RunAsync),
        new(
            "tutorials/04_cubes_and_objects/08_drive_to_charger_test",
            "examples/tutorials/04_cubes_and_objects/08_drive_to_charger_test.py",
            "Find the charger and drive to it.",
            DriveToChargerTest.RunAsync),
        new(
            "tutorials/04_cubes_and_objects/09_custom_objects",
            "examples/tutorials/04_cubes_and_objects/09_custom_objects.py",
            "Define custom marker objects and report appearances.",
            CustomObjects.RunAsync),
        new(
            "tutorials/04_cubes_and_objects/10_object_moved",
            "examples/tutorials/04_cubes_and_objects/10_object_moved.py",
            "Print cube movement events.",
            ObjectMoved.RunAsync),
        new(
            "tutorials/04_cubes_and_objects/11_dock_with_cube",
            "examples/tutorials/04_cubes_and_objects/11_dock_with_cube.py",
            "Dock with a cube from a 180 degree approach.",
            DockWithCube.RunAsync),
        new(
            "tutorials/04_cubes_and_objects/12_roll_cube",
            "examples/tutorials/04_cubes_and_objects/12_roll_cube.py",
            "Roll a cube.",
            RollCube.RunAsync),
        new(
            "tutorials/04_cubes_and_objects/13_pop_a_wheelie",
            "examples/tutorials/04_cubes_and_objects/13_pop_a_wheelie.py",
            "Pop a wheelie using a cube.",
            PopAWheelie.RunAsync),
        new(
            "tutorials/05_async_python/01_cube_blinker_sync",
            "examples/tutorials/05_async_python/01_cube_blinker_sync.py",
            "Look for a cube, blink it, and wait for a tap.",
            CubeBlinkerSync.RunAsync),
        new(
            "tutorials/05_async_python/02_cube_blinker",
            "examples/tutorials/05_async_python/02_cube_blinker.py",
            "Async version of the cube blinker.",
            AsyncCubeBlinker.RunAsync),
        new(
            "tutorials/06_actions/01_parallel_actions",
            "examples/tutorials/06_actions/01_parallel_actions.py",
            "Run, conflict, and abort parallel actions.",
            ParallelActions.RunAsync),
        new(
            "tools/cubes/auto_cube_disconnect",
            "examples/tools/cubes/auto_cube_disconnect.py",
            "Enable or disable automatic cube disconnection.",
            AutoCubeDisconnect.RunAsync),
        new(
            "tools/cubes/connect_cubes",
            "examples/tools/cubes/connect_cubes.py",
            "Ask Cozmo to connect to its cubes.",
            ConnectCubes.RunAsync),
        new(
            "tools/cubes/disconnect_cubes",
            "examples/tools/cubes/disconnect_cubes.py",
            "Disconnect from the cubes to save battery.",
            DisconnectCubes.RunAsync),
        new(
            "tools/cubes/log_cube_battery_levels",
            "examples/tools/cubes/log_cube_battery_levels.py",
            "Print each connected cube's battery level.",
            LogCubeBatteryLevels.RunAsync),
        new(
            "apps/desk_security_guard",
            "examples/apps/desk_security_guard.py",
            "Patrol a desk and react to the owner or intruders.",
            DeskSecurityGuardProgram.RunAsync),
        new(
            "apps/quizmaster_cozmo",
            "examples/apps/quizmaster_cozmo.py",
            "Run a three-player quiz with cube buzzers.",
            QuizmasterCozmo.RunAsync),
        new(
            "apps/quick_tap",
            "examples/apps/quick_tap.py",
            "Play Quick Tap against Cozmo.",
            QuickTap.RunAsync),
        new(
            "if_this_then_that/ifttt_gmail",
            "examples/if_this_then_that/ifttt_gmail.py",
            "React to a new Gmail message through an IFTTT webhook.",
            IftttGmail.RunAsync),
        new(
            "if_this_then_that/ifttt_sports",
            "examples/if_this_then_that/ifttt_sports.py",
            "React to an in-game sports update through an IFTTT webhook.",
            IftttSports.RunAsync),
        new(
            "if_this_then_that/ifttt_sports_flask",
            "examples/if_this_then_that/ifttt_sports_flask.py",
            "Queue IFTTT sports updates and process them on a worker.",
            IftttSportsFlask.RunAsync),
        new(
            "if_this_then_that/ifttt_stocks",
            "examples/if_this_then_that/ifttt_stocks.py",
            "React to a stock price increase through an IFTTT webhook.",
            IftttStocks.RunAsync),
        new(
            "multi_robot/multi_robot_independent",
            "examples/multi_robot/multi_robot_independent.py",
            "Run independent routines on two robots concurrently.",
            MultiRobotIndependent.RunAsync),
        new(
            "multi_robot/multi_robot_unified",
            "examples/multi_robot/multi_robot_unified.py",
            "Control two robots from one routine.",
            MultiRobotUnified.RunAsync),
    ];

    /// <summary>Finds an example by key, ignoring case.</summary>
    public static ExampleDefinition? Find(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return All.FirstOrDefault(
            example => string.Equals(example.Key, key, StringComparison.OrdinalIgnoreCase));
    }
}
