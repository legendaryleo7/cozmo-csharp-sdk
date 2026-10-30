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

/// <summary>The pet types Cozmo can distinguish, as reported by the engine.</summary>
/// <remarks>
/// <para>
/// These string constants are returned by <see cref="Pet.PetType"/>.
/// </para>
/// <para>
/// The engine can only tell whether something looks like a cat or a dog; it cannot identify a
/// specific pet.
/// </para>
/// </remarks>
public static class PetType
{
    /// <summary>Pet type reported by Cozmo when unsure of the type of pet.</summary>
    public const string Unknown = "unknown";

    /// <summary>Pet type reported by Cozmo when he thinks it is a cat.</summary>
    public const string Cat = "cat";

    /// <summary>Pet type reported by Cozmo when he thinks it is a dog.</summary>
    public const string Dog = "dog";

    /// <summary>Converts a CLAD <c>Anki.Vision.PetType</c> value to its string form.</summary>
    /// <param name="cladPetType">The CLAD pet type reported by the engine.</param>
    /// <returns>One of the string constants on this class, or <see cref="Unknown"/> if the CLAD value is unknown or unrecognized.</returns>
    /// <remarks>
    /// An unrecognized value is treated as unknown so a newer or malformed engine message
    /// does not interrupt event dispatch.
    /// </remarks>
    public static string FromClad(Anki.Vision.PetType cladPetType) => cladPetType switch
    {
        Anki.Vision.PetType.Unknown => Unknown,
        Anki.Vision.PetType.Cat => Cat,
        Anki.Vision.PetType.Dog => Dog,
        _ => Unknown,
    };
}
