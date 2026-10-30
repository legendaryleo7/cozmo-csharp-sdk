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

/// <summary>A 4x4 matrix representing the rotation and/or position of an object in the world.</summary>
/// <remarks>
/// The matrix is mutable. Its constructor takes components in column order
/// (<c>m00, m10, m20, m30, m01, ...</c>); <see cref="InRowOrder"/> exposes them in row order.
/// </remarks>
public sealed class Matrix44
{
    /// <summary>Creates a matrix from sixteen components in column order.</summary>
    /// <param name="m00">Row 0, column 0.</param>
    /// <param name="m10">Row 1, column 0.</param>
    /// <param name="m20">Row 2, column 0.</param>
    /// <param name="m30">Row 3, column 0.</param>
    /// <param name="m01">Row 0, column 1.</param>
    /// <param name="m11">Row 1, column 1.</param>
    /// <param name="m21">Row 2, column 1.</param>
    /// <param name="m31">Row 3, column 1.</param>
    /// <param name="m02">Row 0, column 2.</param>
    /// <param name="m12">Row 1, column 2.</param>
    /// <param name="m22">Row 2, column 2.</param>
    /// <param name="m32">Row 3, column 2.</param>
    /// <param name="m03">Row 0, column 3.</param>
    /// <param name="m13">Row 1, column 3.</param>
    /// <param name="m23">Row 2, column 3.</param>
    /// <param name="m33">Row 3, column 3.</param>
    public Matrix44(
        double m00,
        double m10,
        double m20,
        double m30,
        double m01,
        double m11,
        double m21,
        double m31,
        double m02,
        double m12,
        double m22,
        double m32,
        double m03,
        double m13,
        double m23,
        double m33)
    {
        M00 = m00;
        M10 = m10;
        M20 = m20;
        M30 = m30;
        M01 = m01;
        M11 = m11;
        M21 = m21;
        M31 = m31;
        M02 = m02;
        M12 = m12;
        M22 = m22;
        M32 = m32;
        M03 = m03;
        M13 = m13;
        M23 = m23;
        M33 = m33;
    }

    /// <summary>Row 0, column 0.</summary>
    public double M00 { get; set; }

    /// <summary>Row 1, column 0.</summary>
    public double M10 { get; set; }

    /// <summary>Row 2, column 0.</summary>
    public double M20 { get; set; }

    /// <summary>Row 3, column 0.</summary>
    public double M30 { get; set; }

    /// <summary>Row 0, column 1.</summary>
    public double M01 { get; set; }

    /// <summary>Row 1, column 1.</summary>
    public double M11 { get; set; }

    /// <summary>Row 2, column 1.</summary>
    public double M21 { get; set; }

    /// <summary>Row 3, column 1.</summary>
    public double M31 { get; set; }

    /// <summary>Row 0, column 2.</summary>
    public double M02 { get; set; }

    /// <summary>Row 1, column 2.</summary>
    public double M12 { get; set; }

    /// <summary>Row 2, column 2.</summary>
    public double M22 { get; set; }

    /// <summary>Row 3, column 2.</summary>
    public double M32 { get; set; }

    /// <summary>Row 0, column 3.</summary>
    public double M03 { get; set; }

    /// <summary>Row 1, column 3.</summary>
    public double M13 { get; set; }

    /// <summary>Row 2, column 3.</summary>
    public double M23 { get; set; }

    /// <summary>Row 3, column 3.</summary>
    public double M33 { get; set; }

    /// <summary>The contents of the matrix in row order.</summary>
    /// <remarks>A new array is allocated on every access.</remarks>
    public double[] InRowOrder =>
    [
        M00, M01, M02, M03,
        M10, M11, M12, M13,
        M20, M21, M22, M23,
        M30, M31, M32, M33,
    ];

    /// <summary>The contents of the matrix in column order.</summary>
    /// <remarks>A new array is allocated on every access.</remarks>
    public double[] InColumnOrder =>
    [
        M00, M10, M20, M30,
        M01, M11, M21, M31,
        M02, M12, M22, M32,
        M03, M13, M23, M33,
    ];

    /// <summary>The x, y and z components of the matrix's forward vector.</summary>
    public (double X, double Y, double Z) ForwardXyz => (M00, M01, M02);

    /// <summary>The x, y and z components of the matrix's left vector.</summary>
    public (double X, double Y, double Z) LeftXyz => (M10, M11, M12);

    /// <summary>The x, y and z components of the matrix's up vector.</summary>
    public (double X, double Y, double Z) UpXyz => (M20, M21, M22);

    /// <summary>The x, y and z components of the matrix's position vector.</summary>
    public (double X, double Y, double Z) PosXyz => (M30, M31, M32);

    /// <summary>A multi-line string formatted with tabs that shows the matrix contents.</summary>
    public string TabulatedString
    {
        get
        {
            double[] values = InRowOrder;
            return string.Join(
                "\n",
                Row(values, 0),
                Row(values, 4),
                Row(values, 8),
                Row(values, 12));
        }
    }

    /// <summary>Sets the x, y and z components of the matrix's forward vector.</summary>
    /// <param name="x">The X component.</param>
    /// <param name="y">The Y component.</param>
    /// <param name="z">The Z component.</param>
    public void SetForward(double x, double y, double z)
    {
        M00 = x;
        M01 = y;
        M02 = z;
    }

    /// <summary>Sets the x, y and z components of the matrix's left vector.</summary>
    /// <param name="x">The X component.</param>
    /// <param name="y">The Y component.</param>
    /// <param name="z">The Z component.</param>
    public void SetLeft(double x, double y, double z)
    {
        M10 = x;
        M11 = y;
        M12 = z;
    }

    /// <summary>Sets the x, y and z components of the matrix's up vector.</summary>
    /// <param name="x">The X component.</param>
    /// <param name="y">The Y component.</param>
    /// <param name="z">The Z component.</param>
    public void SetUp(double x, double y, double z)
    {
        M20 = x;
        M21 = y;
        M22 = z;
    }

    /// <summary>Sets the x, y and z components of the matrix's position vector.</summary>
    /// <param name="x">The X component.</param>
    /// <param name="y">The Y component.</param>
    /// <param name="z">The Z component.</param>
    public void SetPos(double x, double y, double z)
    {
        M30 = x;
        M31 = y;
        M32 = z;
    }

    /// <summary>Returns a diagnostic string representation of this matrix.</summary>
    /// <returns>A single-line string containing all sixteen components in row order.</returns>
    public override string ToString() => // util.py:534-538
        FormattableString.Invariant(
            $"<{nameof(Matrix44)}: {string.Join(" ", InRowOrder.Select(FormatComponent))}>");

    private static string Row(double[] values, int offset) =>
        string.Join(
            "\t",
            FormatComponent(values[offset]),
            FormatComponent(values[offset + 1]),
            FormatComponent(values[offset + 2]),
            FormatComponent(values[offset + 3]));

    private static string FormatComponent(double value) =>
        value.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
}
