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
using Anki.Cozmo.ExternalInterface;
using Anki.Cozmo.Sdk;
using Anki.Cozmo.Sdk.Clad;
using CladCameraConfig = Anki.Cozmo.ExternalInterface.CameraConfig;
using SdkCameraConfig = Anki.Cozmo.Sdk.CameraConfig;

namespace Cozmo.Tests;

/// <summary>A mini-JPEG header repair input and its expected output.</summary>
internal sealed record MiniJpegVector(
    string Name,
    byte[] Input,
    int Width,
    int Height,
    bool Color,
    byte[] Expected);

/// <summary>Camera chunk metadata and data used by a test scenario.</summary>
internal sealed record ChunkSpec(
    byte ChunkId,
    uint ImageId,
    byte ChunkCount,
    byte Encoding,
    sbyte Resolution,
    byte[] Data);

/// <summary>A camera chunk sequence and its expected image output.</summary>
internal sealed record ChunkScenario(
    ChunkSpec[] Chunks,
    byte[] ExpectedJpeg,
    int ExpectedImages,
    int ExpectedWidth,
    int ExpectedHeight);

/// <summary>Tests camera settings, frame reconstruction and image events.</summary>
public sealed class CameraTests
{
    [Fact]
    public void ConstructorSendsInitialImageSettings()
    {
        using var fixture = new CameraFixture();

        List<MessageGameToEngine> sent = fixture.SentMessages();
        Assert.Equal(2, sent.Count);
        Assert.Equal(MessageGameToEngine.Tag.ImageRequest, sent[0].tag);
        Assert.Equal((byte)ImageSendMode.Off, sent[0].ImageRequest.mode);
        Assert.Equal(MessageGameToEngine.Tag.EnableColorImages, sent[1].tag);
        Assert.False(sent[1].EnableColorImages.enable);
        Assert.False(fixture.Camera.ImageStreamEnabled);
        Assert.False(fixture.Camera.ColorImageEnabled);
    }

    [Fact]
    public async Task SetImageStreamEnabledSendsOnlyWhenTheValueChanges()
    {
        using var fixture = new CameraFixture();

        await fixture.Camera.SetImageStreamEnabledAsync(true);
        Assert.True(fixture.Camera.ImageStreamEnabled);
        List<MessageGameToEngine> sent = fixture.SentMessages();
        Assert.Equal(3, sent.Count);
        Assert.Equal(MessageGameToEngine.Tag.ImageRequest, sent[2].tag);
        Assert.Equal((byte)ImageSendMode.Stream, sent[2].ImageRequest.mode);

        await fixture.Camera.SetImageStreamEnabledAsync(true);
        Assert.Equal(3, fixture.SentMessages().Count);

        await fixture.Camera.SetImageStreamEnabledAsync(false);
        sent = fixture.SentMessages();
        Assert.Equal(4, sent.Count);
        Assert.Equal((byte)ImageSendMode.Off, sent[3].ImageRequest.mode);
        Assert.False(fixture.Camera.ImageStreamEnabled);
    }

    [Fact]
    public async Task SetColorImageEnabledSendsOnlyWhenTheValueChanges()
    {
        using var fixture = new CameraFixture();

        await fixture.Camera.SetColorImageEnabledAsync(true);
        Assert.True(fixture.Camera.ColorImageEnabled);
        List<MessageGameToEngine> sent = fixture.SentMessages();
        Assert.Equal(3, sent.Count);
        Assert.Equal(MessageGameToEngine.Tag.EnableColorImages, sent[2].tag);
        Assert.True(sent[2].EnableColorImages.enable);

        await fixture.Camera.SetColorImageEnabledAsync(true);
        Assert.Equal(3, fixture.SentMessages().Count);

        await fixture.Camera.SetColorImageEnabledAsync(false);
        sent = fixture.SentMessages();
        Assert.Equal(4, sent.Count);
        Assert.False(sent[3].EnableColorImages.enable);
    }

    [Fact]
    public async Task EnableAutoExposureSendsCameraSettings()
    {
        using var fixture = new CameraFixture();

        await fixture.Camera.EnableAutoExposureAsync();
        List<MessageGameToEngine> sent = fixture.SentMessages();
        Assert.Equal(MessageGameToEngine.Tag.SetCameraSettings, sent[2].tag);
        Assert.True(sent[2].SetCameraSettings.enableAutoExposure);
        Assert.Equal((ushort)0, sent[2].SetCameraSettings.exposure_ms);
        Assert.Equal(0.0f, sent[2].SetCameraSettings.gain);

        await fixture.Camera.EnableAutoExposureAsync(false);
        sent = fixture.SentMessages();
        Assert.False(sent[3].SetCameraSettings.enableAutoExposure);
    }

    [Fact]
    public async Task SetManualExposureRequiresTheCameraConfig()
    {
        using var fixture = new CameraFixture();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => fixture.Camera.SetManualExposureAsync(10, 1.0));
        Assert.Equal(2, fixture.SentMessages().Count);
    }

    [Fact]
    public async Task SetManualExposureRejectsAnOutOfRangeExposure()
    {
        using var fixture = new CameraFixture();
        await fixture.ReceiveConfigAsync();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => fixture.Camera.SetManualExposureAsync(0, 1.0));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => fixture.Camera.SetManualExposureAsync(1001, 1.0));
        Assert.Equal(2, fixture.SentMessages().Count);
    }

    [Fact]
    public async Task SetManualExposureRejectsAnOutOfRangeGain()
    {
        using var fixture = new CameraFixture();
        await fixture.ReceiveConfigAsync();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => fixture.Camera.SetManualExposureAsync(10, 0.09));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => fixture.Camera.SetManualExposureAsync(10, 8.01));
        Assert.Equal(2, fixture.SentMessages().Count);
    }

    [Fact]
    public async Task SetManualExposureSendsTheRequestedValues()
    {
        using var fixture = new CameraFixture();
        await fixture.ReceiveConfigAsync();

        await fixture.Camera.SetManualExposureAsync(100, 2.5);

        List<MessageGameToEngine> sent = fixture.SentMessages();
        Assert.Equal(3, sent.Count);
        Assert.Equal(MessageGameToEngine.Tag.SetCameraSettings, sent[2].tag);
        Assert.False(sent[2].SetCameraSettings.enableAutoExposure);
        Assert.Equal((ushort)100, sent[2].SetCameraSettings.exposure_ms);
        Assert.Equal(2.5f, sent[2].SetCameraSettings.gain);

        // Python updates gain/exposure/auto-exposure only from CurrentCameraParams.
        Assert.Equal(0.0, fixture.Camera.Gain);
        Assert.Equal(0, fixture.Camera.ExposureMs);
        Assert.True(fixture.Camera.IsAutoExposureEnabled);
    }

    [Fact]
    public async Task CurrentCameraParamsUpdatesTheReportedState()
    {
        using var fixture = new CameraFixture();

        await fixture.DeliverAsync(new MessageEngineToGame
        {
            CurrentCameraParams = new CurrentCameraParams
            {
                cameraGain = 2.5f,
                exposure_ms = 17,
                autoExposureEnabled = false,
            },
        });

        Assert.Equal(2.5, fixture.Camera.Gain);
        Assert.Equal(17, fixture.Camera.ExposureMs);
        Assert.False(fixture.Camera.IsAutoExposureEnabled);
    }

    [Fact]
    public async Task PerRobotSettingsPopulatesTheCameraConfig()
    {
        using var fixture = new CameraFixture();
        Assert.Null(fixture.Camera.Config);

        await fixture.ReceiveConfigAsync();

        SdkCameraConfig config = Assert.IsType<SdkCameraConfig>(fixture.Camera.Config);
        Assert.Equal(CameraGoldenData.FocalLengthX, config.FocalLength.X);
        Assert.Equal(CameraGoldenData.FocalLengthY, config.FocalLength.Y);
        Assert.Equal(CameraGoldenData.CenterX, config.Center.X);
        Assert.Equal(CameraGoldenData.CenterY, config.Center.Y);
        Assert.Equal(CameraGoldenData.FovXRadians, config.FovX.Radians);
        Assert.Equal(CameraGoldenData.FovYRadians, config.FovY.Radians);
        Assert.Equal(CameraGoldenData.FovXDegrees, config.FovX.Degrees);
        Assert.Equal(CameraGoldenData.FovYDegrees, config.FovY.Degrees);
        Assert.Equal(CameraGoldenData.MinExposureTimeMs, config.MinExposureTimeMs);
        Assert.Equal(CameraGoldenData.MaxExposureTimeMs, config.MaxExposureTimeMs);
        Assert.Equal(CameraGoldenData.MinGain, config.MinGain);
        Assert.Equal(CameraGoldenData.MaxGain, config.MaxGain);
    }

    [Fact]
    public async Task RobotObservedMotionDispatchesTheCameraEvent()
    {
        using var fixture = new CameraFixture();
        EvtRobotObservedMotion? observed = null;
        using Handler handler = fixture.Camera.AddHandler<EvtRobotObservedMotion>(evt => observed = evt);

        await fixture.DeliverAsync(new MessageEngineToGame
        {
            RobotObservedMotion = new RobotObservedMotion
            {
                timestamp = 1234,
                img_area = 0.25f,
                img_x = 4,
                img_y = -5,
                ground_area = 0.5f,
                ground_x = 6,
                ground_y = -7,
                top_img_area = 0.75f,
                top_img_x = 8,
                top_img_y = -9,
                left_img_area = 0.0f,
                left_img_x = 10,
                left_img_y = -11,
                right_img_area = 0.125f,
                right_img_x = 12,
                right_img_y = -13,
            },
        });

        EvtRobotObservedMotion evt = Assert.IsType<EvtRobotObservedMotion>(observed);
        Assert.Equal(1234u, evt.Timestamp);
        Assert.Equal(0.25, evt.ImgArea);
        Assert.Equal(4.0, evt.ImgPos.X);
        Assert.Equal(-5.0, evt.ImgPos.Y);
        Assert.Equal(0.5, evt.GroundArea);
        Assert.Equal(6.0, evt.GroundPos.X);
        Assert.Equal(-7.0, evt.GroundPos.Y);
        Assert.True(evt.HasTopMovement);
        Assert.Equal(8.0, evt.TopImgPos.X);
        Assert.Equal(-9.0, evt.TopImgPos.Y);
        Assert.False(evt.HasLeftMovement);
        Assert.Equal(10.0, evt.LeftImgPos.X);
        Assert.Equal(-11.0, evt.LeftImgPos.Y);
        Assert.True(evt.HasRightMovement);
        Assert.Equal(12.0, evt.RightImgPos.X);
        Assert.Equal(-13.0, evt.RightImgPos.Y);
    }

    [Fact]
    public void CameraConfigFromCladConvertsEveryValue()
    {
        var cladConfig = new CladCameraConfig
        {
            focalLengthX = CameraGoldenData.CladFocalLengthX,
            focalLengthY = CameraGoldenData.CladFocalLengthY,
            centerX = CameraGoldenData.CladCenterX,
            centerY = CameraGoldenData.CladCenterY,
            fovX = CameraGoldenData.CladFovXDegrees,
            fovY = CameraGoldenData.CladFovYDegrees,
            minCameraExposureTime_ms = CameraGoldenData.CladMinExposureTimeMs,
            maxCameraExposureTime_ms = CameraGoldenData.CladMaxExposureTimeMs,
            minCameraGain = CameraGoldenData.CladMinGain,
            maxCameraGain = CameraGoldenData.CladMaxGain,
        };

        SdkCameraConfig config = SdkCameraConfig.FromClad(cladConfig);

        Assert.Equal(CameraGoldenData.FocalLengthX, config.FocalLength.X);
        Assert.Equal(CameraGoldenData.FocalLengthY, config.FocalLength.Y);
        Assert.Equal(CameraGoldenData.CenterX, config.Center.X);
        Assert.Equal(CameraGoldenData.CenterY, config.Center.Y);
        Assert.Equal(CameraGoldenData.FovXRadians, config.FovX.Radians);
        Assert.Equal(CameraGoldenData.FovYRadians, config.FovY.Radians);
        Assert.Equal(CameraGoldenData.FovXDegrees, config.FovX.Degrees);
        Assert.Equal(CameraGoldenData.FovYDegrees, config.FovY.Degrees);
        Assert.Equal(CameraGoldenData.MinExposureTimeMs, config.MinExposureTimeMs);
        Assert.Equal(CameraGoldenData.MaxExposureTimeMs, config.MaxExposureTimeMs);
        Assert.Equal(CameraGoldenData.MinGain, config.MinGain);
        Assert.Equal(CameraGoldenData.MaxGain, config.MaxGain);
        Assert.Throws<ArgumentNullException>(() => SdkCameraConfig.FromClad(null!));
    }

    [Fact]
    public void MiniJpegRepairMatchesPython()
    {
        foreach (MiniJpegVector vector in CameraGoldenData.MiniJpegVectors)
        {
            byte[] repaired = vector.Color
                ? Camera.MiniColorToJpeg(vector.Input, vector.Width, vector.Height)
                : Camera.MiniGrayToJpeg(vector.Input, vector.Width, vector.Height);

            Assert.True(
                vector.Expected.AsSpan().SequenceEqual(repaired),
                $"mini-JPEG vector '{vector.Name}' does not match the Python implementation");
        }
    }

    [Fact]
    public void MiniJpegRepairRejectsInvalidInput()
    {
        Assert.Throws<ArgumentException>(() => Camera.MiniGrayToJpeg(Array.Empty<byte>(), 40, 30));
        Assert.Throws<ArgumentException>(() => Camera.MiniGrayToJpeg(new byte[] { 0xFF, 0xFF }, 40, 30));
        Assert.Throws<ArgumentOutOfRangeException>(() => Camera.MiniGrayToJpeg(new byte[] { 0 }, 65536, 30));
        Assert.Throws<ArgumentOutOfRangeException>(() => Camera.MiniGrayToJpeg(new byte[] { 0 }, 40, -1));
    }

    [Fact]
    public void CameraImageCarriesTheRawJpegAndOptionalDimensions()
    {
        var bytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xD9 };
        var image = new CameraImage(bytes);

        Assert.Same(bytes, image.RawImage);
        Assert.Equal(0, image.ImageNumber);
        Assert.Null(image.Width);
        Assert.Null(image.Height);
        Assert.True(DateTimeOffset.UtcNow - image.ImageRecvTime < TimeSpan.FromMinutes(1));

        var sized = new CameraImage(bytes, 4, 40, 30);
        Assert.Equal(4, sized.ImageNumber);
        Assert.Equal(40, sized.Width);
        Assert.Equal(30, sized.Height);
    }

    [Fact]
    public async Task InOrderChunksReassembleIntoThePythonJpeg()
    {
        using var fixture = new CameraFixture();
        await AssertScenarioAsync(fixture, CameraGoldenData.InOrderGray);
    }

    [Fact]
    public async Task AnOutOfOrderFirstChunkIsDiscardedUntilTheImageRestarts()
    {
        using var fixture = new CameraFixture();
        await AssertScenarioAsync(fixture, CameraGoldenData.OutOfOrderGray);
    }

    [Fact]
    public async Task AMissingChunkResetsThePartialImage()
    {
        using var fixture = new CameraFixture();
        await AssertScenarioAsync(fixture, CameraGoldenData.MissingChunkGray);
    }

    [Fact]
    public async Task ADuplicateChunkResetsThePartialImage()
    {
        using var fixture = new CameraFixture();
        await AssertScenarioAsync(fixture, CameraGoldenData.DuplicateChunkGray);
    }

    [Fact]
    public async Task AChangedImageIdResetsThePartialImage()
    {
        using var fixture = new CameraFixture();
        await AssertScenarioAsync(fixture, CameraGoldenData.ChangedImageId);
    }

    [Fact]
    public async Task ColorChunksUseTheColorHeaderRepair()
    {
        using var fixture = new CameraFixture();
        await AssertScenarioAsync(fixture, CameraGoldenData.InOrderColor);
    }

    [Fact]
    public async Task ANewImageIdRestartsThePartialImage()
    {
        using var fixture = new CameraFixture();
        await AssertScenarioAsync(fixture, CameraGoldenData.NewImageRestarts);
    }

    [Fact]
    public async Task NonMinimizedEncodingsArePassedThroughUnchanged()
    {
        using var fixture = new CameraFixture();
        await AssertScenarioAsync(fixture, CameraGoldenData.NonMinimizedPassThrough);
    }

    [Fact]
    public async Task DisposeUnsubscribesFromTheConnection()
    {
        using var fixture = new CameraFixture();

        fixture.Camera.Dispose();
        await fixture.DeliverAsync(new MessageEngineToGame
        {
            CurrentCameraParams = new CurrentCameraParams
            {
                cameraGain = 9.0f,
                exposure_ms = 99,
                autoExposureEnabled = false,
            },
        });

        Assert.Equal(0.0, fixture.Camera.Gain);
        Assert.Equal(0, fixture.Camera.ExposureMs);
        Assert.True(fixture.Camera.IsAutoExposureEnabled);
    }

    private static async Task AssertScenarioAsync(CameraFixture fixture, ChunkScenario scenario)
    {
        var images = new List<CameraImage>();
        using Handler handler = fixture.Camera.AddHandler<EvtNewRawCameraImage>(evt => images.Add(evt.Image));

        foreach (ChunkSpec chunk in scenario.Chunks)
        {
            await fixture.DeliverAsync(new MessageEngineToGame
            {
                ImageChunk = new ImageChunk(
                    frameTimeStamp: 0,
                    imageId: chunk.ImageId,
                    chunkDebug: 0,
                    imageEncoding: chunk.Encoding,
                    resolution: chunk.Resolution,
                    imageChunkCount: chunk.ChunkCount,
                    chunkId: chunk.ChunkId,
                    status: 0,
                    data: chunk.Data),
            });
        }

        Assert.Equal(scenario.ExpectedImages, images.Count);
        CameraImage image = Assert.Single(images);
        Assert.Equal(scenario.ExpectedJpeg, image.RawImage);
        Assert.Equal(scenario.ExpectedWidth, image.Width);
        Assert.Equal(scenario.ExpectedHeight, image.Height);
        Assert.Equal(0, image.ImageNumber);
    }

    private sealed class CameraFixture : IDisposable
    {
        public CameraFixture()
        {
            Connection = new CozmoConnection(Transport);
            Connection.ConnectAsync().GetAwaiter().GetResult();
            Camera = new Camera(Connection);
        }

        public FakeTransport Transport { get; } = new();

        public CozmoConnection Connection { get; }

        public Camera Camera { get; }

        public List<MessageGameToEngine> SentMessages() =>
            Transport.SentFrames.Select(Decode).ToList();

        public Task DeliverAsync(MessageEngineToGame message) =>
            Connection.DispatchEventAsync(new EvtMessageReceived(message));

        public Task ReceiveConfigAsync() => DeliverAsync(new MessageEngineToGame
        {
            PerRobotSettings = new PerRobotSettings
            {
                cameraConfig = new CladCameraConfig
                {
                    focalLengthX = CameraGoldenData.CladFocalLengthX,
                    focalLengthY = CameraGoldenData.CladFocalLengthY,
                    centerX = CameraGoldenData.CladCenterX,
                    centerY = CameraGoldenData.CladCenterY,
                    fovX = CameraGoldenData.CladFovXDegrees,
                    fovY = CameraGoldenData.CladFovYDegrees,
                    minCameraExposureTime_ms = CameraGoldenData.CladMinExposureTimeMs,
                    maxCameraExposureTime_ms = CameraGoldenData.CladMaxExposureTimeMs,
                    minCameraGain = CameraGoldenData.CladMinGain,
                    maxCameraGain = CameraGoldenData.CladMaxGain,
                },
            },
        });

        public void Dispose()
        {
            Camera.Dispose();
            Connection.DisposeAsync().AsTask().GetAwaiter().GetResult();
            Transport.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }

        private static MessageGameToEngine Decode(byte[] payload)
        {
            using var stream = new MemoryStream(payload, writable: false);
            return MessageGameToEngine.UnpackFrom(new CladBinaryReader(stream));
        }
    }
}
