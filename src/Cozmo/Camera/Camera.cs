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

using Anki.Cozmo.ExternalInterface;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Anki.Cozmo.Sdk;

/// <summary>Raised when a new raw image is received from the robot's camera.</summary>
/// <param name="Image">The raw image; its bytes are an unprocessed JPEG frame.</param>
/// <remarks>
/// The Python SDK dispatches a decoded <c>PIL.Image.Image</c> and the world layer wraps it
/// in a <c>CameraImage</c>. This port has no imaging dependency, so the event carries a
/// <see cref="CameraImage"/> whose <see cref="CameraImage.RawImage"/> is the JPEG sequence.
/// See also <c>EvtNewCameraImage</c> in the Python world layer, which additionally provides
/// an annotated image.
/// </remarks>
public sealed record EvtNewRawCameraImage(CameraImage Image) : CozmoEvent;

/// <summary>Raised when the robot observes motion.</summary>
/// <param name="Timestamp">Robot timestamp for when movement was observed.</param>
/// <param name="ImgArea">Area of the supporting region for the point, as a fraction of the image.</param>
/// <param name="ImgPos">Centroid of observed motion, relative to the top-left corner.</param>
/// <param name="GroundArea">Area of the supporting region for the point, as a fraction of the ground region of interest.</param>
/// <param name="GroundPos">Approximate coordinates of observed motion on the ground, relative to the robot, in millimeters.</param>
/// <param name="HasTopMovement">True when movement was detected near the top of the robot's view.</param>
/// <param name="TopImgPos">Centroid of the top movement, relative to the top-left corner.</param>
/// <param name="HasLeftMovement">True when movement was detected near the left edge of the robot's view.</param>
/// <param name="LeftImgPos">Centroid of the left movement, relative to the top-left corner.</param>
/// <param name="HasRightMovement">True when movement was detected near the right edge of the robot's view.</param>
/// <param name="RightImgPos">Centroid of the right movement, relative to the top-left corner.</param>
public sealed record EvtRobotObservedMotion(
    uint Timestamp,
    double ImgArea,
    Vector2 ImgPos,
    double GroundArea,
    Vector2 GroundPos,
    bool HasTopMovement,
    Vector2 TopImgPos,
    bool HasLeftMovement,
    Vector2 LeftImgPos,
    bool HasRightMovement,
    Vector2 RightImgPos) : CozmoEvent;

/// <summary>Represents Cozmo's camera.</summary>
/// <remarks>
/// <para>
/// Port of <c>cozmo/camera.py::Camera</c> and the camera half of
/// <c>cozmo/robot.py</c> (the robot forwards the three camera messages to its camera).
/// The robot normally owns the camera; this port binds it directly to a
/// <see cref="CozmoConnection"/> so it can be used before the robot object model exists.
/// </para>
/// <para>
/// The camera receives images only after <see cref="SetImageStreamEnabledAsync"/> has been
/// called with <see langword="true"/>. On construction the camera sends the same initial
/// <c>ImageRequest</c> and <c>EnableColorImages</c> messages that the Python SDK sends for
/// its initial <c>False</c> property values. Python gates that initialization on NumPy and
/// Pillow being importable; this port has no imaging dependency and always sends it.
/// </para>
/// <para>
/// Image frames are reassembled from <c>ImageChunk</c> messages exactly as in the Python
/// SDK. The resulting JPEG bytes are not decoded: <see cref="CameraImage.RawImage"/> holds
/// the frame, and no imaging library is required.
/// </para>
/// </remarks>
public sealed class Camera : Dispatcher
{
    private const string GrayHeaderHex =
        "ffd8ffe000104a46494600010100000100010000ffdb004300100b0c0e0c0a100e0d0e1211101318281a181616183123" +
        "251d283a333d3c3933383740485c4e404457453738506d51575f626768673e4d71797064785c656763ffc0000b080128" +
        "019001011100ffc400d20000010501010101010100000000000000000102030405060708090a0b100002010303020403" +
        "050504040000017d01020300041105122131410613516107227114328191a1082342b1c11552d1f02433627282090a16" +
        "1718191a25262728292a3435363738393a434445464748494a535455565758595a636465666768696a73747576777879" +
        "7a838485868788898a92939495969798999aa2a3a4a5a6a7a8a9aab2b3b4b5b6b7b8b9bac2c3c4c5c6c7c8c9cad2d3d4" +
        "d5d6d7d8d9dae1e2e3e4e5e6e7e8e9eaf1f2f3f4f5f6f7f8f9faffda0008010100003f00";

    private const string ColorHeaderHex =
        "ffd8ffe000104a46494600010100000100010000ffdb004300100b0c0e0c0a100e0d0e1211101318281a181616183123" +
        "251d283a333d3c3933383740485c4e404457453738506d51575f626768673e4d71797064785c656763ffc000110800f0" +
        "014003012100021100031100ffc400d20000010501010101010100000000000000000102030405060708090a0b100002" +
        "010303020403050504040000017d01020300041105122131410613516107227114328191a1082342b1c11552d1f02433" +
        "627282090a161718191a25262728292a3435363738393a434445464748494a535455565758595a636465666768696a73" +
        "7475767778797a838485868788898a92939495969798999aa2a3a4a5a6a7a8a9aab2b3b4b5b6b7b8b9bac2c3c4c5c6c7" +
        "c8c9cad2d3d4d5d6d7d8d9dae1e2e3e4e5e6e7e8e9eaf1f2f3f4f5f6f7f8f9faffda000c03010002000300003f00";

    private static readonly byte[] GrayHeader = Convert.FromHexString(GrayHeaderHex);
    private static readonly byte[] ColorHeader = Convert.FromHexString(ColorHeaderHex);

    // Resolution table from camera.py:55-69. Images are sent at one of these fixed sizes.
    private static readonly IReadOnlyDictionary<ImageResolution, (int Width, int Height)> Resolutions =
        new Dictionary<ImageResolution, (int Width, int Height)>
        {
            [ImageResolution.VerificationSnapshot] = (16, 16),
            [ImageResolution.QQQQVGA] = (40, 30),
            [ImageResolution.QQQVGA] = (80, 60),
            [ImageResolution.QQVGA] = (160, 120),
            [ImageResolution.QVGA] = (320, 240),
            [ImageResolution.CVGA] = (400, 296),
            [ImageResolution.VGA] = (640, 480),
            [ImageResolution.SVGA] = (800, 600),
            [ImageResolution.XGA] = (1024, 768),
            [ImageResolution.SXGA] = (1280, 960),
            [ImageResolution.UXGA] = (1600, 1200),
            [ImageResolution.QXGA] = (2048, 1536),
            [ImageResolution.QUXGA] = (3200, 2400),
        };

    private readonly CozmoConnection _connection;
    private readonly ILogger<Camera> _logger;
    private readonly Handler _messageHandler;
    private byte[]? _partialData;
    private uint? _partialImageId;
    private bool _partialInvalid;
    private int _partialSize;
    private ImageChunk? _partialMetadata;
    private int _lastChunkId = -1;
    private bool _imageStreamEnabled;
    private bool _colorImageEnabled;
    private CameraConfig? _config;
    private double _gain;
    private int _exposureMs;
    private bool _autoExposureEnabled = true;
    private int _imageNumber;
    private bool _disposed;

    /// <summary>Creates a camera bound to a connection.</summary>
    /// <param name="connection">Connection that carries the camera messages.</param>
    /// <param name="logger">Optional logger; defaults to a no-op logger.</param>
    /// <exception cref="ArgumentNullException"><paramref name="connection"/> is <see langword="null"/>.</exception>
    public Camera(CozmoConnection connection, ILogger<Camera>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(connection);
        _connection = connection;
        _logger = logger ?? NullLogger<Camera>.Instance;
        _messageHandler = connection.AddAsyncHandler<EvtMessageReceived>(OnMessageReceivedAsync);

        // Python sets image_stream_enabled and color_image_enabled to False in its
        // constructor, which sends ImageRequest(Off) and EnableColorImages(False)
        // (camera.py:240-242). The sends cannot be awaited from a constructor.
        _ = SendInitialCameraSettingsAsync();
    }

    /// <summary>True when the robot is streaming camera images.</summary>
    /// <remarks>
    /// This is the read-only half of Python's mutable <c>image_stream_enabled</c> property.
    /// Change it with <see cref="SetImageStreamEnabledAsync"/>.
    /// </remarks>
    public bool ImageStreamEnabled => _imageStreamEnabled;

    /// <summary>True when the robot is sending color camera images.</summary>
    /// <remarks>
    /// This is the read-only half of Python's mutable <c>color_image_enabled</c> property.
    /// Change it with <see cref="SetColorImageEnabledAsync"/>.
    /// </remarks>
    public bool ColorImageEnabled => _colorImageEnabled;

    /// <summary>The read-only calibration for the camera.</summary>
    /// <remarks>
    /// <see langword="null"/> until the robot's <c>PerRobotSettings</c> message has been
    /// received. The Python robot performs the same assignment when it handles that message
    /// (robot.py:1138-1145).
    /// </remarks>
    public CameraConfig? Config => _config;

    /// <summary>True when auto exposure is currently enabled.</summary>
    /// <remarks>When enabled, the robot continually updates <see cref="Gain"/> and
    /// <see cref="ExposureMs"/> based on recent images.</remarks>
    public bool IsAutoExposureEnabled => _autoExposureEnabled;

    /// <summary>The current camera gain setting.</summary>
    public double Gain => _gain;

    /// <summary>The current camera exposure setting in milliseconds.</summary>
    public int ExposureMs => _exposureMs;

    /// <summary>Enables or disables the robot's image stream.</summary>
    /// <param name="enabled">True to request a continuous image stream; false to stop it.</param>
    /// <param name="cancellationToken">Cancellation token for the send.</param>
    /// <returns>A task that completes once the request has been sent.</returns>
    /// <remarks>
    /// Python exposes this as the mutable <c>image_stream_enabled</c> property. Sending an
    /// unchanged value is a no-op, matching the Python setter (camera.py:318-333).
    /// </remarks>
    public async Task SetImageStreamEnabledAsync(bool enabled, CancellationToken cancellationToken = default)
    {
        if (_imageStreamEnabled == enabled)
        {
            return;
        }

        _imageStreamEnabled = enabled;
        ImageSendMode imageSendMode = enabled ? ImageSendMode.Stream : ImageSendMode.Off;
        await _connection.SendAsync(
            new MessageGameToEngine
            {
                ImageRequest = new ImageRequest { mode = (byte)imageSendMode },
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Enables or disables color camera images.</summary>
    /// <param name="enabled">True to request color images; false to request grayscale.</param>
    /// <param name="cancellationToken">Cancellation token for the send.</param>
    /// <returns>A task that completes once the request has been sent.</returns>
    /// <remarks>
    /// Python exposes this as the mutable <c>color_image_enabled</c> property. Sending an
    /// unchanged value is a no-op, matching the Python setter (camera.py:344-353).
    /// </remarks>
    public async Task SetColorImageEnabledAsync(bool enabled, CancellationToken cancellationToken = default)
    {
        if (_colorImageEnabled == enabled)
        {
            return;
        }

        _colorImageEnabled = enabled;
        await _connection.SendAsync(
            new MessageGameToEngine
            {
                EnableColorImages = new EnableColorImages { enable = enabled },
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Enables or disables automatic exposure on the camera.</summary>
    /// <param name="enableAutoExposure">True to let the robot adjust exposure automatically.</param>
    /// <param name="cancellationToken">Cancellation token for the send.</param>
    /// <returns>A task that completes once the request has been sent.</returns>
    /// <remarks>
    /// Mirrors <c>enable_auto_exposure</c> (camera.py:245-256). The local
    /// <see cref="IsAutoExposureEnabled"/> value is updated only when the robot reports it
    /// through <c>CurrentCameraParams</c>.
    /// </remarks>
    public async Task EnableAutoExposureAsync(
        bool enableAutoExposure = true,
        CancellationToken cancellationToken = default)
    {
        await _connection.SendAsync(
            new MessageGameToEngine
            {
                SetCameraSettings = new SetCameraSettings { enableAutoExposure = enableAutoExposure },
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Disables auto exposure and forces the given exposure time and gain.</summary>
    /// <param name="exposureMs">Desired exposure time in milliseconds.</param>
    /// <param name="gain">Desired camera gain.</param>
    /// <param name="cancellationToken">Cancellation token for the send.</param>
    /// <returns>A task that completes once the request has been sent.</returns>
    /// <exception cref="InvalidOperationException">The camera configuration has not been received yet.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="exposureMs"/> or <paramref name="gain"/> is outside the range in
    /// <see cref="Config"/>.
    /// </exception>
    /// <remarks>Mirrors <c>set_manual_exposure</c> (camera.py:258-292).</remarks>
    public async Task SetManualExposureAsync(
        int exposureMs,
        double gain,
        CancellationToken cancellationToken = default)
    {
        CameraConfig? cameraConfig = _config;
        if (cameraConfig is null)
        {
            throw new InvalidOperationException(
                "The camera configuration has not been received from the robot yet.");
        }

        if (exposureMs < cameraConfig.MinExposureTimeMs || exposureMs > cameraConfig.MaxExposureTimeMs)
        {
            throw new ArgumentOutOfRangeException(
                nameof(exposureMs),
                exposureMs,
                FormattableString.Invariant(
                    $"exposure_ms {exposureMs} out of range {cameraConfig.MinExposureTimeMs}..{cameraConfig.MaxExposureTimeMs}"));
        }

        if (gain < cameraConfig.MinGain || gain > cameraConfig.MaxGain)
        {
            throw new ArgumentOutOfRangeException(
                nameof(gain),
                gain,
                FormattableString.Invariant(
                    $"gain {gain} out of range {cameraConfig.MinGain}..{cameraConfig.MaxGain}"));
        }

        await _connection.SendAsync(
            new MessageGameToEngine
            {
                SetCameraSettings = new SetCameraSettings
                {
                    enableAutoExposure = false,
                    exposure_ms = (ushort)exposureMs,
                    gain = (float)gain,
                },
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Repairs a miniGrayToJpeg frame into a standard JPEG byte sequence.</summary>
    /// <param name="miniGray">Mini-gray frame including its leading color flag byte.</param>
    /// <param name="width">Width to write into the JPEG frame header.</param>
    /// <param name="height">Height to write into the JPEG frame header.</param>
    /// <returns>The repaired JPEG bytes, including Python's zero-filled tail padding.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="miniGray"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="miniGray"/> is empty or only padding.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A dimension does not fit in 16 bits.</exception>
    /// <remarks>
    /// Port of <c>_minigray_to_jpeg</c> (camera.py:470-503): the mini format starts with a
    /// color flag byte followed by unstuffed scan data, and <c>_mini_to_jpeg_helper</c>
    /// (camera.py:546-579) prepends the fixed JPEG tables, restores byte stuffing, and
    /// appends the end-of-image marker. The result is not decoded here. Like the Python
    /// implementation, the returned array is sized for the full input (including removed
    /// trailing 0xFF padding) and therefore has zero bytes after the marker.
    /// </remarks>
    public static byte[] MiniGrayToJpeg(byte[] miniGray, int width, int height) =>
        MiniToJpeg(miniGray, width, height, GrayHeader);

    /// <summary>Repairs a miniColorToJpeg frame into a standard JPEG byte sequence.</summary>
    /// <param name="miniColor">Mini-color frame including its leading color flag byte.</param>
    /// <param name="width">Width to write into the JPEG frame header.</param>
    /// <param name="height">Height to write into the JPEG frame header.</param>
    /// <returns>The repaired JPEG bytes, including Python's zero-filled tail padding.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="miniColor"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="miniColor"/> is empty or only padding.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A dimension does not fit in 16 bits.</exception>
    /// <remarks>
    /// Port of <c>_minicolor_to_jpeg</c> (camera.py:505-544). The color JPEG header declares
    /// three components, so callers pass the half-width encoded size for color frames.
    /// </remarks>
    public static byte[] MiniColorToJpeg(byte[] miniColor, int width, int height) =>
        MiniToJpeg(miniColor, width, height, ColorHeader);

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing && !_disposed)
        {
            _disposed = true;
            _messageHandler.Dispose();
            ResetPartialState();
        }

        base.Dispose(disposing);
    }

    private static byte[] MiniToJpeg(byte[] mini, int width, int height, byte[] header)
    {
        ArgumentNullException.ThrowIfNull(mini);
        if (mini.Length == 0)
        {
            throw new ArgumentException("The minimized image is empty.", nameof(mini));
        }

        if (width is < 0 or > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(width));
        }

        if (height is < 0 or > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(height));
        }

        int currentLength = mini.Length;
        while (currentLength > 0 && mini[currentLength - 1] == 0xFF)
        {
            currentLength--;
        }

        if (currentLength == 0)
        {
            // The Python loop would index past the start of the buffer forever here.
            throw new ArgumentException("The minimized image contains only padding bytes.", nameof(mini));
        }

        byte[] output = new byte[checked((mini.Length * 2) + header.Length)];
        header.CopyTo(output, 0);

        // Standard JPEG SOF0 payload: height at 0x5e/0x5f, width at 0x60/0x61
        // (camera.py:558-561).
        output[0x5e] = (byte)(height >> 8);
        output[0x5f] = (byte)height;
        output[0x60] = (byte)(width >> 8);
        output[0x61] = (byte)width;

        int offset = header.Length;
        for (int i = 0; i < currentLength - 1; i++)
        {
            byte value = mini[i + 1];
            output[offset++] = value;
            if (value == 0xFF)
            {
                output[offset++] = 0;
            }
        }

        output[offset++] = 0xFF;
        output[offset] = 0xD9;
        return output;
    }

    private async Task SendInitialCameraSettingsAsync()
    {
        try
        {
            await _connection.SendAsync(
                new MessageGameToEngine
                {
                    ImageRequest = new ImageRequest { mode = (byte)ImageSendMode.Off },
                }).ConfigureAwait(false);
            await _connection.SendAsync(
                new MessageGameToEngine
                {
                    EnableColorImages = new EnableColorImages { enable = false },
                }).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send the initial camera settings.");
        }
    }

    private async Task OnMessageReceivedAsync(EvtMessageReceived evt)
    {
        switch (evt.Message.tag)
        {
            case MessageEngineToGame.Tag.ImageChunk:
                await HandleImageChunkAsync(evt.Message.ImageChunk).ConfigureAwait(false);
                break;

            case MessageEngineToGame.Tag.CurrentCameraParams:
                HandleCurrentCameraParams(evt.Message.CurrentCameraParams);
                break;

            case MessageEngineToGame.Tag.RobotObservedMotion:
                await DispatchEventAsync(CreateObservedMotionEvent(evt.Message.RobotObservedMotion))
                    .ConfigureAwait(false);
                break;

            case MessageEngineToGame.Tag.PerRobotSettings:
                SetConfig(evt.Message.PerRobotSettings.cameraConfig);
                break;
        }
    }

    /// <summary>Stores the camera calibration received from the robot.</summary>
    /// <param name="cladConfig">Camera configuration from a <c>PerRobotSettings</c> message.</param>
    /// <remarks>
    /// Python's robot calls <c>Camera._set_config</c> for the same message
    /// (robot.py:1145); WP-11 may call this directly when it owns the settings message.
    /// </remarks>
    internal void SetConfig(Anki.Cozmo.ExternalInterface.CameraConfig cladConfig) =>
        _config = CameraConfig.FromClad(cladConfig);

    private void HandleCurrentCameraParams(CurrentCameraParams message)
    {
        _gain = message.cameraGain;
        _exposureMs = message.exposure_ms;
        _autoExposureEnabled = message.autoExposureEnabled;
    }

    private static EvtRobotObservedMotion CreateObservedMotionEvent(RobotObservedMotion message) =>
        new(
            message.timestamp,
            message.img_area,
            new Vector2(message.img_x, message.img_y),
            message.ground_area,
            new Vector2(message.ground_x, message.ground_y),
            message.top_img_area > 0,
            new Vector2(message.top_img_x, message.top_img_y),
            message.left_img_area > 0,
            new Vector2(message.left_img_x, message.left_img_y),
            message.right_img_area > 0,
            new Vector2(message.right_img_x, message.right_img_y));

    private async Task HandleImageChunkAsync(ImageChunk message)
    {
        if (_partialImageId is not null && message.chunkId == 0)
        {
            if (!_partialInvalid)
            {
                _logger.LogDebug("Lost final chunk of image; discarding.");
            }

            _partialImageId = null;
        }

        if (_partialImageId is null)
        {
            if (message.chunkId != 0)
            {
                if (!_partialInvalid)
                {
                    _logger.LogDebug("Received chunk of broken image.");
                }

                _partialInvalid = true;
                return;
            }

            // Discard any previous in-progress image.
            ResetPartialState();
            _partialImageId = message.imageId;
            _partialMetadata = message;

            // camera.py:400-403 sizes the buffer for a full RGB image at the reported
            // resolution. The chunk count constant is overwritten by this expression.
            if (!TryGetResolution((ImageResolution)message.resolution, out int width, out int height))
            {
                // Python raises KeyError from RESOLUTIONS[msg.resolution]; drop the image
                // instead of aborting the connection.
                _logger.LogError(
                    "Dropping image with unknown resolution {Resolution}.",
                    message.resolution);
                ResetPartialState();
                _partialInvalid = true;
                return;
            }

            _partialData = new byte[checked(width * height * 3)];
        }

        if (message.chunkId != _lastChunkId + 1 || message.imageId != _partialImageId)
        {
            _logger.LogDebug(
                "Image missing chunks; discarding (last_chunk_id={LastChunkId} partial_image_id={PartialImageId}).",
                _lastChunkId,
                _partialImageId);
            ResetPartialState();
            _partialInvalid = true;
            return;
        }

        byte[] chunkData = message.data ?? Array.Empty<byte>();
        if (_partialData is null || _partialSize + chunkData.Length > _partialData.Length)
        {
            // Python lets NumPy raise a broadcasting ValueError for an oversized chunk.
            // Treat it as a broken image so one malformed message cannot tear down the
            // connection.
            _logger.LogError(
                "Image chunk {ChunkId} of image {ImageId} exceeds the reassembly buffer.",
                message.chunkId,
                message.imageId);
            ResetPartialState();
            _partialInvalid = true;
            return;
        }

        Array.Copy(chunkData, 0, _partialData, _partialSize, chunkData.Length);
        _partialSize += chunkData.Length;
        _lastChunkId = message.chunkId;

        if (message.chunkId == message.imageChunkCount - 1)
        {
            CameraImage? image = ProcessCompletedImage();
            ResetPartialState();
            if (image is not null)
            {
                await DispatchEventAsync(new EvtNewRawCameraImage(image)).ConfigureAwait(false);
            }
        }
    }

    private CameraImage? ProcessCompletedImage()
    {
        if (_partialData is null || _partialMetadata is null || _partialSize == 0)
        {
            _logger.LogError("Completed image has no data or metadata.");
            return null;
        }

        var data = new byte[_partialSize];
        Array.Copy(_partialData, data, _partialSize);

        // The first byte of the image indicates whether it is in color (camera.py:443-444).
        bool isColorImage = data[0] != 0;
        int? width = null;
        int? height = null;
        if (TryGetResolution((ImageResolution)_partialMetadata.resolution, out int resolutionWidth, out int resolutionHeight))
        {
            width = resolutionWidth;
            height = resolutionHeight;
        }

        byte[] jpeg = data;
        if ((ImageEncoding)_partialMetadata.imageEncoding == ImageEncoding.JPEGMinimizedGray)
        {
            // Both gray and color mini images are sent with the JPEGMinimizedGray encoding
            // and distinguished by the leading byte (camera.py:446-455).
            if (width is null || height is null)
            {
                _logger.LogError("Completed image has no resolution metadata.");
                return null;
            }

            jpeg = isColorImage
                ? MiniColorToJpeg(data, width.Value / 2, height.Value)
                : MiniGrayToJpeg(data, width.Value, height.Value);
        }

        var image = new CameraImage(jpeg, _imageNumber, width, height);
        _imageNumber++;
        return image;
    }

    private void ResetPartialState()
    {
        _partialData = null;
        _partialImageId = null;
        _partialInvalid = false;
        _partialSize = 0;
        _partialMetadata = null;
        _lastChunkId = -1;
    }

    private static bool TryGetResolution(ImageResolution resolution, out int width, out int height)
    {
        if (Resolutions.TryGetValue(resolution, out (int Width, int Height) size))
        {
            width = size.Width;
            height = size.Height;
            return true;
        }

        width = 0;
        height = 0;
        return false;
    }
}
