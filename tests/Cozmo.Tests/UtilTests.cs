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

using Anki;
using Anki.Cozmo.Sdk;
using Anki.Cozmo.Sdk.Clad;

namespace Cozmo.Tests;

public sealed class UtilTests
{
    private static void AssertClose(double expected, double actual)
    {
        if (double.IsNaN(expected))
        {
            Assert.True(double.IsNaN(actual), $"expected NaN, got {actual:R}");
            return;
        }

        if (double.IsInfinity(expected))
        {
            Assert.Equal(expected, actual);
            return;
        }

        double tolerance = 1e-12 * Math.Max(1.0, Math.Abs(expected));
        Assert.True(
            Math.Abs(expected - actual) <= tolerance,
            $"expected {expected:R}, got {actual:R}");
    }

    private static void AssertClose(double[] expected, double[] actual)
    {
        Assert.Equal(expected.Length, actual.Length);
        for (int i = 0; i < expected.Length; i++)
        {
            AssertClose(expected[i], actual[i]);
        }
    }

    private static byte[] PackPose(PoseStruct3d pose)
    {
        using var stream = new MemoryStream();
        var writer = new CladBinaryWriter(stream);
        pose.Pack(writer);
        writer.Flush();
        return stream.ToArray();
    }

    [Fact]
    public void AngleFromDegreesMatchesPython()
    {
        foreach (var c in UtilGoldenData.AngleFromDegrees)
        {
            Angle angle = Angle.FromDegrees(c.Degrees);
            AssertClose(c.Radians, angle.Radians);
            AssertClose(c.DegreesOut, angle.Degrees);
            AssertClose(c.AbsRadians, angle.AbsValue.Radians);
            AssertClose(c.Radians, Util.Degrees(c.Degrees).Radians);
        }
    }

    [Fact]
    public void AngleFromRadiansMatchesPython()
    {
        foreach (var c in UtilGoldenData.AngleFromRadians)
        {
            Angle angle = Angle.FromRadians(c.Radians);
            AssertClose(c.Degrees, angle.Degrees);
            AssertClose(c.AbsRadians, angle.AbsValue.Radians);
            AssertClose(c.Degrees, new Angle(c.Radians).Degrees);
            AssertClose(c.Degrees, Util.Radians(c.Radians).Degrees);
        }
    }

    [Fact]
    public void AngleArithmeticMatchesPython()
    {
        foreach (var c in UtilGoldenData.AngleArithmetic)
        {
            Angle a = Angle.FromRadians(c.ARadians);
            Angle b = Angle.FromRadians(c.BRadians);
            AssertClose(c.Sum, (a + b).Radians);
            AssertClose(c.Difference, (a - b).Radians);
            AssertClose(c.Product, (a * c.Scalar).Radians);
            AssertClose(c.Quotient, (a / c.Divisor).Radians);
        }

        Assert.Throws<DivideByZeroException>(() => _ = Angle.FromRadians(1.0) / 0.0);
    }

    [Fact]
    public void AngleComparisonsMatchPython()
    {
        foreach (var c in UtilGoldenData.AngleComparisons)
        {
            Angle a = Angle.FromRadians(c.ARadians);
            Angle b = Angle.FromRadians(c.BRadians);
            Assert.Equal(c.Eq, a == b);
            Assert.Equal(c.Ne, a != b);
            Assert.Equal(c.Gt, a > b);
            Assert.Equal(c.Lt, a < b);
            Assert.Equal(c.Ge, a >= b);
            Assert.Equal(c.Le, a <= b);
            Assert.Equal(c.Eq, a.Equals(b));
        }
    }

    [Fact]
    public void AngleFormattingMatchesPython()
    {
        foreach (var c in UtilGoldenData.AngleFormatting)
        {
            Assert.Equal(c.Text, Angle.FromRadians(c.Radians).ToString());
        }
    }

    [Fact]
    public void DistanceFromMillimetersMatchesPython()
    {
        foreach (var c in UtilGoldenData.DistanceFromMillimeters)
        {
            Distance distance = Distance.FromMillimeters(c.InputMillimeters);
            AssertClose(c.Millimeters, distance.DistanceMillimeters);
            AssertClose(c.Inches, distance.DistanceInches);
            AssertClose(c.Millimeters, Util.DistanceMillimeters(c.InputMillimeters).DistanceMillimeters);
        }
    }

    [Fact]
    public void DistanceFromInchesMatchesPython()
    {
        foreach (var c in UtilGoldenData.DistanceFromInches)
        {
            Distance distance = Distance.FromInches(c.InputInches);
            AssertClose(c.Millimeters, distance.DistanceMillimeters);
            AssertClose(c.Inches, distance.DistanceInches);
            AssertClose(c.Millimeters, Util.DistanceInches(c.InputInches).DistanceMillimeters);
        }
    }

    [Fact]
    public void DistanceArithmeticMatchesPython()
    {
        foreach (var c in UtilGoldenData.DistanceArithmetic)
        {
            Distance a = Distance.FromMillimeters(c.AMillimeters);
            Distance b = Distance.FromMillimeters(c.BMillimeters);
            AssertClose(c.Sum, (a + b).DistanceMillimeters);
            AssertClose(c.Difference, (a - b).DistanceMillimeters);
            AssertClose(c.Product, (a * c.Scalar).DistanceMillimeters);
            AssertClose(c.Quotient, (a / c.Divisor).DistanceMillimeters);
        }

        Assert.Throws<DivideByZeroException>(() => _ = Distance.FromMillimeters(1.0) / 0.0);
    }

    [Fact]
    public void DistanceFormattingMatchesPython()
    {
        foreach (var c in UtilGoldenData.DistanceFormatting)
        {
            Assert.Equal(c.Text, Distance.FromMillimeters(c.Millimeters).ToString());
        }
    }

    [Fact]
    public void SpeedMatchesPython()
    {
        foreach (var c in UtilGoldenData.SpeedCases)
        {
            Speed speed = Speed.FromMillimetersPerSecond(c.InputMmps);
            AssertClose(c.Mmps, speed.SpeedMmps);
            Assert.Equal(c.Text, speed.ToString());
            AssertClose(c.Mmps, Util.SpeedMillimetersPerSecond(c.InputMmps).SpeedMmps);
        }

        foreach (var c in UtilGoldenData.SpeedArithmetic)
        {
            Speed a = Speed.FromMillimetersPerSecond(c.AMmps);
            Speed b = Speed.FromMillimetersPerSecond(c.BMmps);
            AssertClose(c.Sum, (a + b).SpeedMmps);
            AssertClose(c.Difference, (a - b).SpeedMmps);
            AssertClose(c.Product, (a * c.Scalar).SpeedMmps);
            AssertClose(c.Quotient, (a / c.Divisor).SpeedMmps);
        }

        Assert.Throws<DivideByZeroException>(() => _ = Speed.FromMillimetersPerSecond(1.0) / 0.0);
    }

    [Fact]
    public void AngleZToQuaternionMatchesPython()
    {
        foreach (var c in UtilGoldenData.AngleZToQuaternion)
        {
            Angle angle = Angle.FromRadians(c.Radians);
            var (q0, q1, q2, q3) = Util.AngleZToQuaternion(angle);
            AssertClose(c.Q0, q0);
            AssertClose(c.Q1, q1);
            AssertClose(c.Q2, q2);
            AssertClose(c.Q3, q3);

            Quaternion quaternion = Quaternion.FromZAngle(angle);
            AssertClose(c.Q0, quaternion.Q0);
            AssertClose(c.Q1, quaternion.Q1);
            AssertClose(c.Q2, quaternion.Q2);
            AssertClose(c.Q3, quaternion.Q3);
        }
    }

    [Fact]
    public void QuaternionAnglesMatchPython()
    {
        foreach (var c in UtilGoldenData.QuaternionCases)
        {
            Quaternion quaternion = new(c.Q0, c.Q1, c.Q2, c.Q3);
            AssertClose(c.AngleZRadians, quaternion.AngleZ.Radians);
            AssertClose(c.Pitch, quaternion.EulerAngles.Pitch);
            AssertClose(c.Yaw, quaternion.EulerAngles.Yaw);
            AssertClose(c.Roll, quaternion.EulerAngles.Roll);
            Assert.Equal(c.Text, quaternion.ToString());
            Assert.Equal((c.Q0, c.Q1, c.Q2, c.Q3), quaternion.Components);
        }
    }

    [Fact]
    public void QuaternionToMatrixMatchesPython()
    {
        foreach (var c in UtilGoldenData.QuaternionMatrices)
        {
            Matrix44 matrix = new Quaternion(c.Q0, c.Q1, c.Q2, c.Q3).ToMatrix();
            AssertClose(c.Matrix, matrix.InRowOrder);
        }
    }

    [Fact]
    public void QuaternionArithmeticMatchesPython()
    {
        foreach (var c in UtilGoldenData.QuaternionAdd)
        {
            Quaternion a = new(c.AQ0, c.AQ1, c.AQ2, c.AQ3);
            Quaternion b = new(c.BQ0, c.BQ1, c.BQ2, c.BQ3);
            Quaternion result = a + b;
            AssertClose(c.RQ0, result.Q0);
            AssertClose(c.RQ1, result.Q1);
            AssertClose(c.RQ2, result.Q2);
            AssertClose(c.RQ3, result.Q3);
        }

        foreach (var c in UtilGoldenData.QuaternionSubtract)
        {
            Quaternion a = new(c.AQ0, c.AQ1, c.AQ2, c.AQ3);
            Quaternion b = new(c.BQ0, c.BQ1, c.BQ2, c.BQ3);
            Quaternion result = a - b;
            AssertClose(c.RQ0, result.Q0);
            AssertClose(c.RQ1, result.Q1);
            AssertClose(c.RQ2, result.Q2);
            AssertClose(c.RQ3, result.Q3);
        }

        foreach (var c in UtilGoldenData.QuaternionMultiply)
        {
            Quaternion a = new(c.AQ0, c.AQ1, c.AQ2, c.AQ3);
            Quaternion result = a * c.Scalar;
            AssertClose(c.RQ0, result.Q0);
            AssertClose(c.RQ1, result.Q1);
            AssertClose(c.RQ2, result.Q2);
            AssertClose(c.RQ3, result.Q3);
        }

        foreach (var c in UtilGoldenData.QuaternionDivide)
        {
            Quaternion a = new(c.AQ0, c.AQ1, c.AQ2, c.AQ3);
            Quaternion result = a / c.Scalar;
            AssertClose(c.RQ0, result.Q0);
            AssertClose(c.RQ1, result.Q1);
            AssertClose(c.RQ2, result.Q2);
            AssertClose(c.RQ3, result.Q3);
        }

        Assert.Throws<DivideByZeroException>(() => _ = new Quaternion(1.0, 0.0, 0.0, 0.0) / 0.0);
    }

    [Fact]
    public void RotationMatchesPython()
    {
        foreach (var c in UtilGoldenData.RotationCases)
        {
            Rotation rotation = Rotation.FromQuaternion(c.Q0, c.Q1, c.Q2, c.Q3);
            AssertClose(c.AngleZRadians, rotation.AngleZ.Radians);
            AssertClose(c.Pitch, rotation.EulerAngles.Pitch);
            AssertClose(c.Yaw, rotation.EulerAngles.Yaw);
            AssertClose(c.Roll, rotation.EulerAngles.Roll);
            Assert.Equal(c.Text, rotation.ToString());
            Assert.Equal((c.Q0, c.Q1, c.Q2, c.Q3), rotation.Components);

            Quaternion asQuaternion = rotation;
            AssertClose(c.AngleZRadians, asQuaternion.AngleZ.Radians);

            Rotation viaUtil = Util.RotationQuaternion(c.Q0, c.Q1, c.Q2, c.Q3);
            AssertClose(c.AngleZRadians, viaUtil.AngleZ.Radians);
        }

        Rotation zRotation = Util.RotationZAngle(Angle.FromDegrees(45.0));
        AssertClose(Math.PI / 4.0, zRotation.AngleZ.Radians);

        Rotation combined = zRotation + Rotation.FromZAngle(Angle.FromDegrees(45.0));
        AssertClose(Math.PI / 2.0, combined.AngleZ.Radians);
        AssertClose(Math.PI / 4.0 * 2.0, (zRotation * 2.0).AngleZ.Radians);
        AssertClose(Math.PI / 4.0 / 2.0, (zRotation / 2.0).AngleZ.Radians);
    }

    [Fact]
    public void PoseConstructionMatchesPython()
    {
        foreach (var c in UtilGoldenData.PoseConstruction)
        {
            Pose pose;
            if (c.IsQuaternion)
            {
                pose = Pose.FromQuaternion(
                    c.X,
                    c.Y,
                    c.Z,
                    c.Q0,
                    c.Q1,
                    c.Q2,
                    c.Q3,
                    c.OriginId,
                    c.IsAccurateInput);
            }
            else
            {
                pose = Pose.FromZAngle(
                    c.X,
                    c.Y,
                    c.Z,
                    Angle.FromRadians(c.AngleZRadians),
                    c.OriginId,
                    c.IsAccurateInput);
                AssertClose(c.Q0, pose.Rotation.Q0);
                AssertClose(c.Q1, pose.Rotation.Q1);
                AssertClose(c.Q2, pose.Rotation.Q2);
                AssertClose(c.Q3, pose.Rotation.Q3);
            }

            AssertClose(c.PX, pose.Position.X);
            AssertClose(c.PY, pose.Position.Y);
            AssertClose(c.PZ, pose.Position.Z);
            Assert.Equal(c.OriginId, pose.OriginId);
            Assert.Equal(c.IsValid, pose.IsValid);
            Assert.Equal(c.IsAccurate, pose.IsAccurate);
            Assert.Equal(c.Text, pose.ToString());
        }
    }

    [Fact]
    public void PoseAddAndSubtractMatchPython()
    {
        foreach (var c in UtilGoldenData.PoseAdd)
        {
            Pose result = PoseAt(c.AX, c.AY, c.AZ, c.AAngleZRadians, c.AOriginId, c.AIsAccurate) +
                          PoseAt(c.BX, c.BY, c.BZ, c.BAngleZRadians, c.BOriginId, c.BIsAccurate);
            AssertPoseResult(c.RX, c.RY, c.RZ, c.RQ0, c.RQ1, c.RQ2, c.RQ3, c.ROriginId, c.RIsAccurate, result);
        }

        foreach (var c in UtilGoldenData.PoseSubtract)
        {
            Pose result = PoseAt(c.AX, c.AY, c.AZ, c.AAngleZRadians, c.AOriginId, c.AIsAccurate) -
                          PoseAt(c.BX, c.BY, c.BZ, c.BAngleZRadians, c.BOriginId, c.BIsAccurate);
            AssertPoseResult(c.RX, c.RY, c.RZ, c.RQ0, c.RQ1, c.RQ2, c.RQ3, c.ROriginId, c.RIsAccurate, result);
        }
    }

    [Fact]
    public void PoseScaleAndDivideMatchPython()
    {
        foreach (var c in UtilGoldenData.PoseMultiply)
        {
            Pose result = PoseAt(c.AX, c.AY, c.AZ, c.AAngleZRadians, c.AOriginId, c.AIsAccurate) * c.Scalar;
            AssertPoseResult(c.RX, c.RY, c.RZ, c.RQ0, c.RQ1, c.RQ2, c.RQ3, c.ROriginId, c.RIsAccurate, result);
        }

        foreach (var c in UtilGoldenData.PoseDivide)
        {
            Pose result = PoseAt(c.AX, c.AY, c.AZ, c.AAngleZRadians, c.AOriginId, c.AIsAccurate) / c.Scalar;
            AssertPoseResult(c.RX, c.RY, c.RZ, c.RQ0, c.RQ1, c.RQ2, c.RQ3, c.ROriginId, c.RIsAccurate, result);
        }
    }

    [Fact]
    public void PoseDefineRelativeThisMatchesPython()
    {
        foreach (var c in UtilGoldenData.PoseDefineRelative)
        {
            Pose self = PoseAt(c.SelfX, c.SelfY, c.SelfZ, c.SelfAngleZRadians, c.SelfOriginId, c.SelfIsAccurate);
            Pose newPose = PoseAt(c.NewX, c.NewY, c.NewZ, c.NewAngleZRadians, c.NewOriginId, c.NewIsAccurate);
            Pose result = self.DefinePoseRelativeThis(newPose);
            AssertPoseResult(c.RX, c.RY, c.RZ, c.RQ0, c.RQ1, c.RQ2, c.RQ3, c.ROriginId, c.RIsAccurate, result);
        }
    }

    [Fact]
    public void PoseToMatrixMatchesPython()
    {
        foreach (var c in UtilGoldenData.PoseMatrices)
        {
            Pose pose = Pose.FromQuaternion(c.X, c.Y, c.Z, c.Q0, c.Q1, c.Q2, c.Q3);
            AssertClose(c.Matrix, pose.ToMatrix().InRowOrder);
        }
    }

    [Fact]
    public void PoseEncodingMatchesPython()
    {
        foreach (var c in UtilGoldenData.PoseEncodings)
        {
            Pose pose = Pose.FromQuaternion(c.X, c.Y, c.Z, c.Q0, c.Q1, c.Q2, c.Q3, c.OriginId);
            PoseStruct3d encoded = pose.EncodePose();

            AssertClose(c.EX, encoded.x);
            AssertClose(c.EY, encoded.y);
            AssertClose(c.EZ, encoded.z);
            AssertClose(c.EQ0, encoded.q0);
            AssertClose(c.EQ1, encoded.q1);
            AssertClose(c.EQ2, encoded.q2);
            AssertClose(c.EQ3, encoded.q3);
            Assert.Equal(c.EOriginId, encoded.originID);

            byte[] bytes = PackPose(encoded);
            Assert.Equal(32, bytes.Length);
            Assert.Equal(c.Hex, Convert.ToHexString(bytes).ToLowerInvariant());

            using var stream = new MemoryStream(bytes);
            var reader = new CladBinaryReader(stream);
            PoseStruct3d decodedClad = PoseStruct3d.UnpackFrom(reader);
            Pose decoded = Pose.FromClad(decodedClad);
            AssertClose(c.EX, decoded.Position.X);
            AssertClose(c.EY, decoded.Position.Y);
            AssertClose(c.EZ, decoded.Position.Z);
            AssertClose(c.EQ0, decoded.Rotation.Q0);
            AssertClose(c.EQ1, decoded.Rotation.Q1);
            AssertClose(c.EQ2, decoded.Rotation.Q2);
            AssertClose(c.EQ3, decoded.Rotation.Q3);
            Assert.Equal((int)c.EOriginId, decoded.OriginId);
            Assert.Equal(c.Hex, Convert.ToHexString(PackPose(decoded.EncodePose())).ToLowerInvariant());
        }
    }

    [Fact]
    public void PoseEncodeRejectsInvalidOrigin()
    {
        Pose invalid = Pose.CreateDefault();
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => invalid.EncodePose());
        Assert.Contains("originID must be between 0 and 4294967295", error.Message, StringComparison.Ordinal);

        Assert.Throws<ArgumentNullException>(() => Pose.FromClad(null!));
    }

    [Fact]
    public void PoseValidityAndComparabilityMatchPython()
    {
        foreach (var c in UtilGoldenData.PoseComparability)
        {
            Pose a = Pose.FromZAngle(0.0, 0.0, 0.0, Angle.FromDegrees(0.0), c.AOriginId);
            Pose b = Pose.FromZAngle(0.0, 0.0, 0.0, Angle.FromDegrees(0.0), c.BOriginId);
            Assert.Equal(c.IsComparable, a.IsComparable(b));
            Assert.Equal(c.AIsValid, a.IsValid);
            Assert.Equal(c.BIsValid, b.IsValid);
        }

        Pose valid = Pose.FromZAngle(0.0, 0.0, 0.0, Angle.FromDegrees(0.0), originId: 4);
        Assert.True(valid.IsValid);
        valid.OriginId = 7;
        Assert.Equal(7, valid.OriginId);
        valid.Invalidate();
        Assert.Equal(-1, valid.OriginId);
        Assert.False(valid.IsValid);
        Assert.False(valid.IsAccurate);

        Assert.Throws<ArgumentNullException>(() => valid.IsComparable(null!));
        Assert.Throws<ArgumentNullException>(() => valid.DefinePoseRelativeThis(null!));
        Assert.Throws<ArgumentNullException>(() => _ = valid + null!);
        Assert.Throws<DivideByZeroException>(() => _ = valid / 0.0);
    }

    [Fact]
    public void PoseFormattingMatchesPython()
    {
        foreach (var c in UtilGoldenData.PoseFormatting)
        {
            Pose pose = Pose.FromZAngle(
                c.X,
                c.Y,
                c.Z,
                Angle.FromRadians(c.AngleZRadians),
                c.OriginId);
            Assert.Equal(c.Text, pose.ToString());
        }
    }

    [Fact]
    public void Matrix44LayoutMatchesPython()
    {
        foreach (var c in UtilGoldenData.MatrixCases)
        {
            Matrix44 matrix = CreateMatrix(c.Values);
            Assert.Equal(c.RowOrder, matrix.InRowOrder);
            Assert.Equal(c.ColumnOrder, matrix.InColumnOrder);
            AssertClose(c.FX, matrix.ForwardXyz.X);
            AssertClose(c.FY, matrix.ForwardXyz.Y);
            AssertClose(c.FZ, matrix.ForwardXyz.Z);
            AssertClose(c.LX, matrix.LeftXyz.X);
            AssertClose(c.LY, matrix.LeftXyz.Y);
            AssertClose(c.LZ, matrix.LeftXyz.Z);
            AssertClose(c.UX, matrix.UpXyz.X);
            AssertClose(c.UY, matrix.UpXyz.Y);
            AssertClose(c.UZ, matrix.UpXyz.Z);
            AssertClose(c.PX, matrix.PosXyz.X);
            AssertClose(c.PY, matrix.PosXyz.Y);
            AssertClose(c.PZ, matrix.PosXyz.Z);
            Assert.Equal(c.Text, matrix.ToString());
            Assert.Equal(c.Tabulated, matrix.TabulatedString);
        }
    }

    [Fact]
    public void Matrix44SettersMatchPython()
    {
        foreach (var c in UtilGoldenData.MatrixSetters)
        {
            Matrix44 matrix = CreateMatrix(c.Values);
            matrix.SetForward(10.0, 11.0, 12.0);
            matrix.SetLeft(13.0, 14.0, 15.0);
            matrix.SetUp(16.0, 17.0, 18.0);
            matrix.SetPos(19.0, 20.0, 21.0);
            Assert.Equal(c.RowOrder, matrix.InRowOrder);
        }

        Matrix44 mutable = CreateMatrix(UtilGoldenData.MatrixSetters[0].Values);
        mutable.M00 = 42.0;
        mutable.M33 = -42.0;
        Assert.Equal(42.0, mutable.M00);
        Assert.Equal(-42.0, mutable.M33);
    }

    [Fact]
    public void Vector2OperatorsMatchPython()
    {
        foreach (var c in UtilGoldenData.Vector2Cases)
        {
            var a = new Vector2(c.AX, c.AY);
            var b = new Vector2(c.BX, c.BY);

            Vector2 sum = a + b;
            AssertClose(c.SumX, sum.X);
            AssertClose(c.SumY, sum.Y);

            Vector2 difference = a - b;
            AssertClose(c.DifferenceX, difference.X);
            AssertClose(c.DifferenceY, difference.Y);

            Vector2 product = a * c.Scalar;
            AssertClose(c.ProductX, product.X);
            AssertClose(c.ProductY, product.Y);

            Vector2 quotient = a / c.Divisor;
            AssertClose(c.QuotientX, quotient.X);
            AssertClose(c.QuotientY, quotient.Y);

            Assert.Equal(c.Text, a.ToString());
            Assert.Equal((c.AX, c.AY), a.Xy);
        }

        var target = new Vector2(9.0, 9.0);
        target.SetTo(new Vector2(1.5, -2.5));
        Assert.Equal(1.5, target.X);
        Assert.Equal(-2.5, target.Y);

        Assert.Throws<DivideByZeroException>(() => _ = new Vector2(1.0, 2.0) / 0.0);
        Assert.Throws<ArgumentNullException>(() => _ = new Vector2(1.0, 2.0) + null!);
        Assert.Throws<ArgumentNullException>(() => target.SetTo(null!));
    }

    [Fact]
    public void Vector3OperatorsMatchPython()
    {
        foreach (var c in UtilGoldenData.Vector3Cases)
        {
            var a = new Vector3(c.AX, c.AY, c.AZ);
            var b = new Vector3(c.BX, c.BY, c.BZ);

            Vector3 sum = a + b;
            AssertClose(c.SumX, sum.X);
            AssertClose(c.SumY, sum.Y);
            AssertClose(c.SumZ, sum.Z);

            Vector3 difference = a - b;
            AssertClose(c.DifferenceX, difference.X);
            AssertClose(c.DifferenceY, difference.Y);
            AssertClose(c.DifferenceZ, difference.Z);

            Vector3 product = a * c.Scalar;
            AssertClose(c.ProductX, product.X);
            AssertClose(c.ProductY, product.Y);
            AssertClose(c.ProductZ, product.Z);

            Vector3 quotient = a / c.Divisor;
            AssertClose(c.QuotientX, quotient.X);
            AssertClose(c.QuotientY, quotient.Y);
            AssertClose(c.QuotientZ, quotient.Z);

            Assert.Equal(c.Text, a.ToString());
            Assert.Equal((c.AX, c.AY, c.AZ), a.Xyz);
        }

        Assert.Throws<DivideByZeroException>(() => _ = new Vector3(1.0, 2.0, 3.0) / 0.0);
        Assert.Throws<ArgumentNullException>(() => _ = new Vector3(1.0, 2.0, 3.0) - null!);
    }

    [Fact]
    public void PositionOperatorsMatchPython()
    {
        foreach (var c in UtilGoldenData.PositionCases)
        {
            var a = new Position(c.AX, c.AY, c.AZ);
            var b = new Position(c.BX, c.BY, c.BZ);

            Vector3 sum = a + b;
            Assert.IsType<Vector3>(sum); // Python's Vector3.__add__ returns a Vector3, not a Position.
            AssertClose(c.SumX, sum.X);
            AssertClose(c.SumY, sum.Y);
            AssertClose(c.SumZ, sum.Z);

            Vector3 difference = a - b;
            AssertClose(c.DifferenceX, difference.X);
            AssertClose(c.DifferenceY, difference.Y);
            AssertClose(c.DifferenceZ, difference.Z);

            Vector3 product = a * c.Scalar;
            AssertClose(c.ProductX, product.X);
            AssertClose(c.ProductY, product.Y);
            AssertClose(c.ProductZ, product.Z);

            Vector3 quotient = a / c.Divisor;
            AssertClose(c.QuotientX, quotient.X);
            AssertClose(c.QuotientY, quotient.Y);
            AssertClose(c.QuotientZ, quotient.Z);

            Assert.Equal(c.Text, a.ToString());
        }

        var lookAt = new Vector3(100.0, -25.0, 0.0);
        lookAt.SetTo(new Position(1.0, 2.0, 3.0)); // Position is a Vector3, as in Python.
        Assert.Equal((1.0, 2.0, 3.0), lookAt.Xyz);
        Assert.IsAssignableFrom<Vector3>(new Position(0.0, 0.0, 0.0));
    }

    [Fact]
    public void ImageBoxMatchesPython()
    {
        foreach (var c in UtilGoldenData.ImageBoxCases)
        {
            var box = new ImageBox(c.TopLeftX, c.TopLeftY, c.Width, c.Height);
            AssertClose(c.LeftX, box.LeftX);
            AssertClose(c.RightX, box.RightX);
            AssertClose(c.TopY, box.TopY);
            AssertClose(c.BottomY, box.BottomY);
            AssertClose(c.CenterX, box.Center.X);
            AssertClose(c.CenterY, box.Center.Y);

            ImageBox scaled = box * 2.5;
            AssertClose(c.ScaledTopLeftX, scaled.TopLeftX);
            AssertClose(c.ScaledTopLeftY, scaled.TopLeftY);
            AssertClose(c.ScaledWidth, scaled.Width);
            AssertClose(c.ScaledHeight, scaled.Height);
        }

        var first = new ImageBox(1.0, 2.0, 3.0, 4.0);
        var second = new ImageBox(1.0, 2.0, 3.0, 4.0);
        Assert.True(first == second);
        Assert.False(first != second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
        Assert.False(first == (first * 2.0));
        Assert.False(first.Equals((object?)null));
        Assert.False(first.Equals("not a box"));
    }

    [Fact]
    public void ImageBoxFromCladRectMatchesPython()
    {
        foreach (var c in UtilGoldenData.ImageBoxFromClad)
        {
            var rect = new CladRect(
                (float)c.XTopLeft,
                (float)c.YTopLeft,
                (float)c.Width,
                (float)c.Height);
            ImageBox box = ImageBox.FromCladRect(rect);
            AssertClose(c.TopLeftX, box.TopLeftX);
            AssertClose(c.TopLeftY, box.TopLeftY);
            AssertClose(c.BoxWidth, box.Width);
            AssertClose(c.BoxHeight, box.Height);
            AssertClose(c.LeftX, box.LeftX);
            AssertClose(c.RightX, box.RightX);
            AssertClose(c.TopY, box.TopY);
            AssertClose(c.BottomY, box.BottomY);
            AssertClose(c.CenterX, box.Center.X);
            AssertClose(c.CenterY, box.Center.Y);
        }

        Assert.Throws<ArgumentNullException>(() => ImageBox.FromCladRect(null!));
    }

    [Fact]
    public void TimeoutHelperSemanticsMatchPython()
    {
        var noTimeout = new TimeoutHelper();
        Assert.False(noTimeout.IsTimedOut);
        Assert.Null(noTimeout.Remaining);
        Assert.Null(noTimeout.TimeoutSeconds);

        var infinite = new TimeoutHelper(useInf: true);
        Assert.False(infinite.IsTimedOut);
        Assert.Equal(double.PositiveInfinity, infinite.Remaining);

        var expired = new TimeoutHelper(-1.0);
        Assert.True(expired.IsTimedOut);
        Assert.Equal(0.0, expired.Remaining);

        var running = new TimeoutHelper(5.0);
        Assert.False(running.IsTimedOut);
        double? remaining = running.Remaining;
        Assert.NotNull(remaining);
        Assert.InRange(remaining.Value, 0.0, 5.0);
        Assert.Equal(5.0, running.TimeoutSeconds);

        var tiny = new TimeoutHelper(0.005);
        Thread.Sleep(25);
        Assert.True(tiny.IsTimedOut);
        Assert.Equal(0.0, tiny.Remaining);

        var mutated = new TimeoutHelper();
        mutated.TimeoutSeconds = -1.0;
        Assert.True(mutated.IsTimedOut);
        mutated.TimeoutSeconds = null;
        mutated.UseInfinity = true;
        Assert.Equal(double.PositiveInfinity, mutated.Remaining);

        var shifted = new TimeoutHelper(1.0);
        shifted.Start -= 10.0;
        Assert.True(shifted.IsTimedOut);

        // Python's max(0, nan) returns 0 and nan > elapsed is false.
        var nanTimeout = new TimeoutHelper(double.NaN);
        Assert.Equal(0.0, nanTimeout.Remaining);
        Assert.False(nanTimeout.IsTimedOut);
    }

    [Fact]
    public void UtilFactoriesMatchOwningTypes()
    {
        Assert.Equal(
            Angle.FromDegrees(30.0).Radians,
            Util.Degrees(30.0).Radians);
        Assert.Equal(
            Angle.FromRadians(Math.PI).Degrees,
            Util.Radians(Math.PI).Degrees);
        Assert.Equal(
            Distance.FromInches(2.0).DistanceMillimeters,
            Util.DistanceInches(2.0).DistanceMillimeters);
        Assert.Equal(
            Speed.FromMillimetersPerSecond(12.0).SpeedMmps,
            Util.SpeedMillimetersPerSecond(12.0).SpeedMmps);

        Pose quaternionPose = Util.PoseQuaternion(1.0, 2.0, 3.0, 1.0, 0.0, 0.0, 0.0);
        Assert.Equal(0, quaternionPose.OriginId);
        Assert.True(quaternionPose.IsValid);
        Assert.Equal((1.0, 2.0, 3.0), quaternionPose.Position.Xyz);

        Pose zAnglePose = Util.PoseZAngle(1.0, 2.0, 3.0, Angle.FromDegrees(90.0));
        AssertClose(Math.PI / 2.0, zAnglePose.Rotation.AngleZ.Radians);
    }

    private static Pose PoseAt(
        double x,
        double y,
        double z,
        double angleZRadians,
        int originId,
        bool isAccurate) =>
        Pose.FromZAngle(x, y, z, Angle.FromRadians(angleZRadians), originId, isAccurate);

    private static void AssertPoseResult(
        double x,
        double y,
        double z,
        double q0,
        double q1,
        double q2,
        double q3,
        int originId,
        bool isAccurate,
        Pose actual)
    {
        AssertClose(x, actual.Position.X);
        AssertClose(y, actual.Position.Y);
        AssertClose(z, actual.Position.Z);
        AssertClose(q0, actual.Rotation.Q0);
        AssertClose(q1, actual.Rotation.Q1);
        AssertClose(q2, actual.Rotation.Q2);
        AssertClose(q3, actual.Rotation.Q3);
        Assert.Equal(originId, actual.OriginId);
        Assert.Equal(isAccurate, actual.IsAccurate);
    }

    private static Matrix44 CreateMatrix(double[] values)
    {
        Assert.Equal(16, values.Length);
        return new Matrix44(
            values[0],
            values[1],
            values[2],
            values[3],
            values[4],
            values[5],
            values[6],
            values[7],
            values[8],
            values[9],
            values[10],
            values[11],
            values[12],
            values[13],
            values[14],
            values[15]);
    }
}
