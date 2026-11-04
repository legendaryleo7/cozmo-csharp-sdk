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

using Anki.Cozmo;
using Anki.Cozmo.Sdk;

namespace Cozmo.Samples.Examples;

/// <summary>Helpers for looking up the three light cubes the way the Python examples do.</summary>
public static class CubeLookup
{
    /// <summary>The three cube object types, in cube-id order.</summary>
    public static IReadOnlyList<CladEnum<ObjectType>> CubeIds { get; } =
    [
        CustomObjectTypes.LightCube1Id,
        CustomObjectTypes.LightCube2Id,
        CustomObjectTypes.LightCube3Id,
    ];

    /// <summary>Returns a connected cube, or null; unlike <see cref="World.GetLightCube"/> this does not throw.</summary>
    public static LightCube? GetLightCubeOrNull(World world, CladEnum<ObjectType> cubeId)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(cubeId);
        return world.LightCubes.Values.FirstOrDefault(cube => cube.CubeId.Id == cubeId.Id);
    }

    /// <summary>Returns the three cubes; entries are null for cubes that are not connected.</summary>
    public static IReadOnlyList<LightCube?> GetCubes(World world)
    {
        ArgumentNullException.ThrowIfNull(world);
        return CubeIds.Select(id => GetLightCubeOrNull(world, id)).ToArray();
    }
}
