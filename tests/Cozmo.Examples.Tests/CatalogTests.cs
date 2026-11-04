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

using Cozmo.Samples.Examples;
using Xunit;

namespace Cozmo.Examples.Tests;

public sealed class CatalogTests
{
    private static readonly string[] ExpectedKeys =
    [
        "tutorials/01_basics/01_hello_world",
        "tutorials/01_basics/02_drive_and_turn",
        "tutorials/01_basics/03_count",
        "tutorials/01_basics/04_drive_square",
        "tutorials/01_basics/05_motors",
        "tutorials/01_basics/06_sing_scales",
        "tutorials/01_basics/07_backpack_lights",
        "tutorials/01_basics/08_animation",
        "tutorials/01_basics/09_cube_lights",
        "tutorials/01_basics/10_play_sound",
        "tutorials/01_basics/11_play_song",
        "tutorials/01_basics/12_random_animation",
        "tutorials/02_cozmo_face/01_cozmo_face_image",
        "tutorials/02_cozmo_face/02_cozmo_face_mirror",
        "tutorials/02_cozmo_face/03_alarm_clock",
        "tutorials/03_vision/01_light_when_face",
        "tutorials/03_vision/02_face_follower",
        "tutorials/03_vision/04_exposure",
        "tutorials/04_cubes_and_objects/01_go_to_pose_test",
        "tutorials/04_cubes_and_objects/02_create_wall",
        "tutorials/04_cubes_and_objects/03_go_to_object_test",
        "tutorials/04_cubes_and_objects/04_stack_or_roll",
        "tutorials/04_cubes_and_objects/05_cube_stack",
        "tutorials/04_cubes_and_objects/06_pickup_furthest",
        "tutorials/04_cubes_and_objects/07_lookaround",
        "tutorials/04_cubes_and_objects/08_drive_to_charger_test",
        "tutorials/04_cubes_and_objects/09_custom_objects",
        "tutorials/04_cubes_and_objects/10_object_moved",
        "tutorials/04_cubes_and_objects/11_dock_with_cube",
        "tutorials/04_cubes_and_objects/12_roll_cube",
        "tutorials/04_cubes_and_objects/13_pop_a_wheelie",
        "tutorials/05_async_python/01_cube_blinker_sync",
        "tutorials/05_async_python/02_cube_blinker",
        "tutorials/06_actions/01_parallel_actions",
        "tools/cubes/auto_cube_disconnect",
        "tools/cubes/connect_cubes",
        "tools/cubes/disconnect_cubes",
        "tools/cubes/log_cube_battery_levels",
        "apps/desk_security_guard",
        "apps/quizmaster_cozmo",
        "apps/quick_tap",
        "if_this_then_that/ifttt_gmail",
        "if_this_then_that/ifttt_sports",
        "if_this_then_that/ifttt_sports_flask",
        "if_this_then_that/ifttt_stocks",
        "multi_robot/multi_robot_independent",
        "multi_robot/multi_robot_unified",
    ];

    private static readonly string[] PythonOnlyKeys =
    [
        "apps/3d_viewer",
        "apps/cli",
        "apps/color_finder",
        "apps/remote_control_cozmo",
        "tutorials/03_vision/03_annotate",
    ];

    [Fact]
    public void Catalog_has_one_entry_per_ported_python_example()
    {
        Assert.Equal(47, ExampleCatalog.All.Count);
        Assert.Equal(ExpectedKeys.OrderBy(key => key), ExampleCatalog.All.Select(x => x.Key).OrderBy(key => key));
        Assert.Equal(47, ExampleCatalog.All.Select(x => x.Key).Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void Catalog_excludes_python_only_examples()
    {
        foreach (string key in PythonOnlyKeys)
        {
            Assert.Null(ExampleCatalog.Find(key));
        }
    }

    [Fact]
    public void Catalog_keys_and_python_paths_are_consistent()
    {
        foreach (ExampleDefinition example in ExampleCatalog.All)
        {
            Assert.Equal($"examples/{example.Key}.py", example.PythonPath);
            Assert.False(string.IsNullOrWhiteSpace(example.Description));
            Assert.NotNull(example.RunAsync);
        }
    }

    [Fact]
    public void Find_is_case_insensitive()
    {
        Assert.NotNull(ExampleCatalog.Find("TUTORIALS/01_BASICS/01_HELLO_WORLD"));
        Assert.Null(ExampleCatalog.Find("tutorials/01_basics/does_not_exist"));
    }
}
