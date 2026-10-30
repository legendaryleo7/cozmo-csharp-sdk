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
/// These string constants are used by <see cref="Face.Expression"/> and
/// <see cref="Face.KnownExpression"/>.
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
    public const string Unknown = "unknown";

    /// <summary>Facial expression neutral.</summary>
    public const string Neutral = "neutral";

    /// <summary>Facial expression happy.</summary>
    public const string Happy = "happy";

    /// <summary>Facial expression surprised.</summary>
    public const string Surprised = "surprised";

    /// <summary>Facial expression angry.</summary>
    public const string Angry = "angry";

    /// <summary>Facial expression sad.</summary>
    public const string Sad = "sad";

    /// <summary>Converts a CLAD <c>Anki.Vision.FacialExpression</c> value to its string form.</summary>
    /// <param name="cladExpression">The CLAD expression value reported by the engine.</param>
    /// <returns>One of the string constants on this class, or <see cref="Unknown"/> if the CLAD value is unknown or unrecognized.</returns>
    /// <remarks>
    /// Unrecognized values and the CLAD <c>Count</c> sentinel are treated as unknown.
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
