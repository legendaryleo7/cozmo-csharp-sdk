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
/// Port of the <c>PET_TYPE_*</c> constants in <c>cozmo/pets.py</c> (pets.py:58-64). Python
/// exposes them as plain strings; this port keeps the same string values in a static class so
/// <see cref="Pet.PetType"/> can expose them directly.
/// </para>
/// <para>
/// The engine can only tell whether something looks like a cat or a dog; it cannot identify a
/// specific pet.
/// </para>
/// </remarks>
public static class PetType
{
    /// <summary>Pet type reported by Cozmo when unsure of the type of pet.</summary>
    /// <remarks>Python <c>PET_TYPE_UNKNOWN</c> (pets.py:60).</remarks>
    public const string Unknown = "unknown";

    /// <summary>Pet type reported by Cozmo when he thinks it is a cat.</summary>
    /// <remarks>Python <c>PET_TYPE_CAT</c> (pets.py:62).</remarks>
    public const string Cat = "cat";

    /// <summary>Pet type reported by Cozmo when he thinks it is a dog.</summary>
    /// <remarks>Python <c>PET_TYPE_DOG</c> (pets.py:64).</remarks>
    public const string Dog = "dog";

    /// <summary>Converts a CLAD <c>Anki.Vision.PetType</c> value to its string form.</summary>
    /// <param name="cladPetType">The CLAD pet type reported by the engine.</param>
    /// <returns>
    /// One of the string constants on this class, or <see cref="Unknown"/> for the CLAD
    /// <c>Unknown</c> value and for every value this port does not recognize.
    /// </returns>
    /// <remarks>
    /// Python's <c>_clad_pet_type_to_pet_type</c> raises a <c>ValueError</c> for an unexpected
    /// value (pets.py:110-111). The port deliberately returns <see cref="Unknown"/> instead: an
    /// unrecognized value can only come from a malformed or newer engine message, and one bad
    /// observation must not abort the whole dispatch loop.
    /// </remarks>
    public static string FromClad(Anki.Vision.PetType cladPetType) => cladPetType switch
    {
        Anki.Vision.PetType.Unknown => Unknown,
        Anki.Vision.PetType.Cat => Cat,
        Anki.Vision.PetType.Dog => Dog,
        _ => Unknown,
    };
}
