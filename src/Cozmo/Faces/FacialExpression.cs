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

/// <summary>The facial expressions Cozmo can distinguish, as reported by the engine.</summary>
/// <remarks>
/// <para>
/// Port of the <c>FACIAL_EXPRESSION_*</c> constants in <c>cozmo/faces.py</c>
/// (faces.py:62-72). Python exposes them as plain strings; this port keeps the same string
/// values in a static class so <see cref="Face.Expression"/> and
/// <see cref="Face.KnownExpression"/> can expose them directly.
/// </para>
/// <para>
/// Expression estimation is disabled by default; call
/// <c>Robot.EnableFacialExpressionEstimationAsync</c> to make the engine report a real
/// expression.
/// </para>
/// </remarks>
public static class FacialExpression
{
    /// <summary>Facial expression not recognized.</summary>
    /// <remarks>Python <c>FACIAL_EXPRESSION_UNKNOWN</c> (faces.py:62).</remarks>
    public const string Unknown = "unknown";

    /// <summary>Facial expression neutral.</summary>
    /// <remarks>Python <c>FACIAL_EXPRESSION_NEUTRAL</c> (faces.py:64).</remarks>
    public const string Neutral = "neutral";

    /// <summary>Facial expression happy.</summary>
    /// <remarks>Python <c>FACIAL_EXPRESSION_HAPPY</c> (faces.py:66).</remarks>
    public const string Happy = "happy";

    /// <summary>Facial expression surprised.</summary>
    /// <remarks>Python <c>FACIAL_EXPRESSION_SURPRISED</c> (faces.py:68).</remarks>
    public const string Surprised = "surprised";

    /// <summary>Facial expression angry.</summary>
    /// <remarks>Python <c>FACIAL_EXPRESSION_ANGRY</c> (faces.py:70).</remarks>
    public const string Angry = "angry";

    /// <summary>Facial expression sad.</summary>
    /// <remarks>Python <c>FACIAL_EXPRESSION_SAD</c> (faces.py:72).</remarks>
    public const string Sad = "sad";

    /// <summary>Converts a CLAD <c>Anki.Vision.FacialExpression</c> value to its string form.</summary>
    /// <param name="cladExpression">The CLAD expression value reported by the engine.</param>
    /// <returns>
    /// One of the string constants on this class, or <see cref="Unknown"/> for the CLAD
    /// <c>Unknown</c> value and for every value this port does not recognize.
    /// </returns>
    /// <remarks>
    /// Python's <c>_clad_facial_expression_to_facial_expression</c> raises
    /// <see cref="ArgumentException"/>-equivalent <c>ValueError</c> for an unexpected value
    /// (faces.py:187-188). The port deliberately returns <see cref="Unknown"/> instead: an
    /// unrecognized value can only come from a malformed or newer engine message, and one bad
    /// observation must not abort the whole dispatch loop. The CLAD <c>Count</c> sentinel is
    /// also treated as unknown.
    /// </remarks>
    public static string FromClad(Anki.Vision.FacialExpression cladExpression) => cladExpression switch
    {
        Anki.Vision.FacialExpression.Unknown => Unknown,
        Anki.Vision.FacialExpression.Neutral => Neutral,
        Anki.Vision.FacialExpression.Happiness => Happy,
        Anki.Vision.FacialExpression.Surprise => Surprised,
        Anki.Vision.FacialExpression.Anger => Angry,
        Anki.Vision.FacialExpression.Sadness => Sad,
        _ => Unknown,
    };
}
