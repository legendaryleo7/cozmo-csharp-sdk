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

namespace Anki.Cozmo.Sdk;

/// <summary>Represents the position of an object in the world, in millimeters.</summary>
/// <remarks>
/// Mirrors <c>Position</c> in <c>cozmo/util.py</c>. Python implements it as an empty subclass
/// of <see cref="Vector3"/>; the C# port keeps that inheritance so a position can be passed
/// wherever a <see cref="Vector3"/> is expected.
/// </remarks>
public sealed class Position : Vector3
{
    /// <summary>Creates a position from its components.</summary>
    /// <param name="x">X position in millimeters.</param>
    /// <param name="y">Y position in millimeters.</param>
    /// <param name="z">Z position in millimeters.</param>
    public Position(double x, double y, double z)
        : base(x, y, z)
    {
    }
}
