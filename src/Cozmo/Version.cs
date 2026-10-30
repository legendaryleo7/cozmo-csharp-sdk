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

/// <summary>Version constants and the runtime CLAD compatibility check.</summary>
/// <remarks>
/// Use the compatibility check to verify that the connected device supports the CLAD version
/// required by this library.
/// </remarks>
public static class SdkVersion
{
    /// <summary>Version of the Cozmo SDK API represented by this library.</summary>
    public const string Version = "1.4.12";

    /// <summary>cozmoclad package version required by this SDK.</summary>
    public const string RequiredCladVersion = "3.6.6";

    /// <summary>Oldest CLAD build version accepted by the SDK.</summary>
    public const string MinCladVersion = "2.0.0";

    /// <summary>True when the installed CLAD build version meets <see cref="MinCladVersion"/>.</summary>
    /// <param name="installedBuildVersion">CLAD build version, for example <c>00003.00006.00000</c>.</param>
    public static bool IsCladVersionSupported(string installedBuildVersion)
    {
        ArgumentNullException.ThrowIfNull(installedBuildVersion);
        return string.CompareOrdinal(Sortable(installedBuildVersion), Sortable(MinCladVersion)) >= 0;
    }

    /// <summary>Throws when the installed CLAD build version is older than <see cref="MinCladVersion"/>.</summary>
    /// <param name="installedBuildVersion">CLAD build version, for example <c>00003.00006.00000</c>.</param>
    /// <exception cref="CozmoSdkException">The installed CLAD version is too old.</exception>
    public static void VerifyMinCladVersion(string installedBuildVersion)
    {
        ArgumentNullException.ThrowIfNull(installedBuildVersion);
        if (IsCladVersionSupported(installedBuildVersion))
        {
            return;
        }

        throw new CozmoSdkException(
            $"Incompatible CLAD version {Trim(installedBuildVersion)} for SDK {Version} - needs at least {MinCladVersion}.\n" +
            "Regenerate the CLAD layer from a newer cozmoclad release.");
    }

    /// <summary>Normalizes a dotted version so that lexical comparison matches numeric order.</summary>
    internal static string Sortable(string version)
    {
        string[] parts = version.Split('.');
        var padded = new string[parts.Length];
        for (int i = 0; i < parts.Length; i++)
        {
            padded[i] = int.TryParse(parts[i], System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int value)
                ? value.ToString("D5", System.Globalization.CultureInfo.InvariantCulture)
                : parts[i].PadLeft(5, '0');
        }

        return string.Join('.', padded);
    }

    private static string Trim(string version)
    {
        string[] parts = version.Split('.');
        for (int i = 0; i < parts.Length; i++)
        {
            if (int.TryParse(parts[i], System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int value))
            {
                parts[i] = value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
        }

        return string.Join('.', parts);
    }
}
