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

using System.Buffers.Binary;
using System.Text;
using Anki.Cozmo.Sdk;

namespace Cozmo.Tests;

[Collection(ConnectorCollection.Name)]
public sealed class UsbMuxTests
{
    // Bytes below are the exact payloads produced by Python 3 plistlib.dumps with its default
    // XML format and sorted keys; they pin the wire format the usbmux daemon expects.
    private static readonly string CapturedResultPlist = string.Join("\n", new[]
    {
        "<?xml version=\"1.0\" encoding=\"UTF-8\"?>",
        "<!DOCTYPE plist PUBLIC \"-//Apple//DTD PLIST 1.0//EN\" \"http://www.apple.com/DTDs/PropertyList-1.0.dtd\">",
        "<plist version=\"1.0\">",
        "<dict>",
        "\t<key>MessageType</key>",
        "\t<string>Result</string>",
        "\t<key>Number</key>",
        "\t<integer>0</integer>",
        "</dict>",
        "</plist>",
        string.Empty,
    });

    private static readonly string CapturedListenPlist = string.Join("\n", new[]
    {
        "<?xml version=\"1.0\" encoding=\"UTF-8\"?>",
        "<!DOCTYPE plist PUBLIC \"-//Apple//DTD PLIST 1.0//EN\" \"http://www.apple.com/DTDs/PropertyList-1.0.dtd\">",
        "<plist version=\"1.0\">",
        "<dict>",
        "\t<key>ClientVersionString</key>",
        "\t<string>pyusbmux</string>",
        "\t<key>MessageType</key>",
        "\t<string>Listen</string>",
        "\t<key>ProgName</key>",
        "\t<string>pyusbmux</string>",
        "</dict>",
        "</plist>",
        string.Empty,
    });

    private static readonly string CapturedConnectPlist = string.Join("\n", new[]
    {
        "<?xml version=\"1.0\" encoding=\"UTF-8\"?>",
        "<!DOCTYPE plist PUBLIC \"-//Apple//DTD PLIST 1.0//EN\" \"http://www.apple.com/DTDs/PropertyList-1.0.dtd\">",
        "<plist version=\"1.0\">",
        "<dict>",
        "\t<key>ClientVersionString</key>",
        "\t<string>pyusbmux</string>",
        "\t<key>DeviceID</key>",
        "\t<integer>7</integer>",
        "\t<key>MessageType</key>",
        "\t<string>Connect</string>",
        "\t<key>PortNumber</key>",
        "\t<integer>61971</integer>",
        "\t<key>ProgName</key>",
        "\t<string>pyusbmux</string>",
        "</dict>",
        "</plist>",
        string.Empty,
    });

    private static readonly string CapturedAttachedPlist = string.Join("\n", new[]
    {
        "<?xml version=\"1.0\" encoding=\"UTF-8\"?>",
        "<!DOCTYPE plist PUBLIC \"-//Apple//DTD PLIST 1.0//EN\" \"http://www.apple.com/DTDs/PropertyList-1.0.dtd\">",
        "<plist version=\"1.0\">",
        "<dict>",
        "\t<key>MessageType</key>",
        "\t<string>Attached</string>",
        "\t<key>Properties</key>",
        "\t<dict>",
        "\t\t<key>ConnectionType</key>",
        "\t\t<string>USB</string>",
        "\t\t<key>DeviceID</key>",
        "\t\t<integer>7</integer>",
        "\t\t<key>ProductID</key>",
        "\t\t<integer>4776</integer>",
        "\t\t<key>SerialNumber</key>",
        "\t\t<string>CPW123</string>",
        "\t</dict>",
        "</dict>",
        "</plist>",
        string.Empty,
    });

    [Fact]
    public async Task ListenRequestAndCapturedResultUseThePlistWireFormat()
    {
        using var cancellation = new CancellationTokenSource(ConnectorTestDefaults.Timeout);
        var factory = new DuplexServerFactory();
        await using var client = new UsbMuxClient(factory.CreateStream, TimeSpan.FromSeconds(2));

        Task connectTask = client.ConnectAsync(cancellation.Token);
        Stream serverStream = await factory.AcceptAsync(cancellation.Token);
        (byte[] header, byte[] payload) = await ReadRawFrameAsync(serverStream, cancellation.Token);

        byte[] expectedPayload = Encoding.UTF8.GetBytes(CapturedListenPlist);
        Assert.Equal(16 + expectedPayload.Length, (int)BinaryPrimitives.ReadUInt32LittleEndian(header));
        Assert.Equal(UsbMuxProtocol.ProtocolVersion, BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(4)));
        Assert.Equal(UsbMuxProtocol.PlistRequestType, BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(8)));
        Assert.Equal(UsbMuxProtocol.DefaultTag, BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(12)));
        Assert.Equal(expectedPayload, payload);

        await serverStream.WriteAsync(FramePayload(CapturedResultPlist), cancellation.Token);
        await connectTask;
        Assert.Empty(client.Attached);
    }

    [Fact]
    public async Task WaitForSerialDecodesCapturedAttachedFrameCaseInsensitively()
    {
        using var cancellation = new CancellationTokenSource(ConnectorTestDefaults.Timeout);
        var factory = new DuplexServerFactory();
        (UsbMuxClient client, TestUsbMuxPeer control) = await ConnectClientAsync(factory, cancellation.Token);
        await using (client)
        {
            Task<long> wait = client.WaitForSerialAsync("cpw123", TimeSpan.FromSeconds(2), cancellation.Token);
            await control.WriteRawAsync(FramePayload(CapturedAttachedPlist), cancellation.Token);

            long deviceId = await wait;

            Assert.Equal(7, deviceId);
            Assert.Equal("CPW123", client.Attached[7]["SerialNumber"]);
        }
    }

    [Fact]
    public async Task ConnectToFirstDeviceUsesCapturedConnectFrameAndTransitionsToClad()
    {
        using var cancellation = new CancellationTokenSource(ConnectorTestDefaults.Timeout);
        var factory = new DuplexServerFactory();
        (UsbMuxClient client, TestUsbMuxPeer control) = await ConnectClientAsync(factory, cancellation.Token);
        await using (client)
        {
            await control.WriteAsync(AttachedMessage(7, "CPW123"), cancellation.Token);

            Task<(long DeviceId, IReadOnlyDictionary<string, object?> DeviceInfo, StreamTransport Transport)> connect =
                client.ConnectToFirstDeviceAsync(5106, null, TimeSpan.FromSeconds(2), cancellation.Token);
            Stream deviceStream = await factory.AcceptAsync(cancellation.Token);
            (_, byte[] connectPayload) = await ReadRawFrameAsync(deviceStream, cancellation.Token);

            Assert.Equal(Encoding.UTF8.GetBytes(CapturedConnectPlist), connectPayload);
            Dictionary<string, object?> decoded = UsbMuxPlist.Deserialize(connectPayload);
            Assert.Equal("Connect", decoded["MessageType"]);
            Assert.Equal(7L, decoded["DeviceID"]);
            Assert.Equal(61971L, decoded["PortNumber"]);

            // Include a CLAD frame after the result in the same write: the client must keep the
            // unread bytes and hand them to the StreamTransport.
            byte[] response = FramePayload(CapturedResultPlist)
                .Concat(CladFrame(1, 2, 3))
                .ToArray();
            await deviceStream.WriteAsync(response, cancellation.Token);

            (long deviceId, IReadOnlyDictionary<string, object?> deviceInfo, StreamTransport transport) =
                await connect;
            Assert.Equal(7, deviceId);
            Assert.Equal("CPW123", deviceInfo["SerialNumber"]);

            await transport.ConnectAsync(cancellation.Token);
            try
            {
                await using (IAsyncEnumerator<byte[]> frames =
                    transport.ReceiveFramesAsync(cancellation.Token).GetAsyncEnumerator(cancellation.Token))
                {
                    Assert.True(await frames.MoveNextAsync());
                    Assert.Equal(new byte[] { 1, 2, 3 }, frames.Current);
                }

                await transport.SendAsync(new byte[] { 9, 8 }, cancellation.Token);
                var sent = new byte[4];
                await deviceStream.ReadExactlyAsync(sent, cancellation.Token);
                Assert.Equal(new byte[] { 2, 0, 9, 8 }, sent);
            }
            finally
            {
                await transport.DisposeAsync();
            }
        }
    }

    [Fact]
    public async Task ConnectToFirstDeviceExcludesAlreadyConnectedIds()
    {
        using var cancellation = new CancellationTokenSource(ConnectorTestDefaults.Timeout);
        var factory = new DuplexServerFactory();
        (UsbMuxClient client, TestUsbMuxPeer control) = await ConnectClientAsync(factory, cancellation.Token);
        await using (client)
        {
            await control.WriteAsync(AttachedMessage(1, "AAA"), cancellation.Token);
            await control.WriteAsync(AttachedMessage(2, "BBB"), cancellation.Token);

            Task<(long DeviceId, IReadOnlyDictionary<string, object?> DeviceInfo, StreamTransport Transport)> connect =
                client.ConnectToFirstDeviceAsync(
                    5106,
                    new HashSet<long> { 1 },
                    TimeSpan.FromSeconds(2),
                    cancellation.Token);

            Stream deviceStream = await factory.AcceptAsync(cancellation.Token);
            var device = new TestUsbMuxPeer(deviceStream);
            Dictionary<string, object?> request = await device.ReadAsync(cancellation.Token);
            Assert.Equal("Connect", request["MessageType"]);
            Assert.Equal(2L, request["DeviceID"]);

            await device.WriteAsync(ResultMessage(), cancellation.Token);
            (long deviceId, IReadOnlyDictionary<string, object?> info, StreamTransport transport) = await connect;

            Assert.Equal(2, deviceId);
            Assert.Equal("BBB", info["SerialNumber"]);
            await transport.DisposeAsync();
        }
    }

    [Fact]
    public async Task ConnectToFirstDeviceTimesOutWhenNothingIsAttached()
    {
        using var cancellation = new CancellationTokenSource(ConnectorTestDefaults.Timeout);
        var factory = new DuplexServerFactory();
        (UsbMuxClient client, _) = await ConnectClientAsync(factory, cancellation.Token);
        await using (client)
        {
            await Assert.ThrowsAsync<TimeoutException>(() =>
                client.ConnectToFirstDeviceAsync(
                    5106,
                    null,
                    TimeSpan.FromMilliseconds(100),
                    cancellation.Token));
        }
    }

    [Fact]
    public async Task ConnectToDeviceMapsRefusedStatus()
    {
        using var cancellation = new CancellationTokenSource(ConnectorTestDefaults.Timeout);
        var factory = new DuplexServerFactory();
        await using var client = new UsbMuxClient(factory.CreateStream, TimeSpan.FromSeconds(2));

        Task<(long DeviceId, IReadOnlyDictionary<string, object?> DeviceInfo, StreamTransport Transport)> connect =
            client.ConnectToDeviceAsync(9, 5106, cancellation.Token);
        Stream deviceStream = await factory.AcceptAsync(cancellation.Token);
        var device = new TestUsbMuxPeer(deviceStream);
        await device.ReadAsync(cancellation.Token);
        await device.WriteAsync(
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["MessageType"] = "Result",
                ["Number"] = 3L,
            },
            cancellation.Token);

        var exception = await Assert.ThrowsAsync<UsbMuxConnectionRefusedException>(() => connect);

        Assert.Equal("Connection refused to device_id=9 port=5106", exception.Message);
    }

    [Fact]
    public async Task ListDevicesParsesTheDaemonDeviceList()
    {
        using var cancellation = new CancellationTokenSource(ConnectorTestDefaults.Timeout);
        var factory = new DuplexServerFactory();
        await using var client = new UsbMuxClient(factory.CreateStream, TimeSpan.FromSeconds(2));

        Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> listTask = client.ListDevicesAsync(cancellation.Token);
        Stream stream = await factory.AcceptAsync(cancellation.Token);
        var peer = new TestUsbMuxPeer(stream);
        Dictionary<string, object?> request = await peer.ReadAsync(cancellation.Token);
        Assert.Equal("ListDevices", request["MessageType"]);

        await peer.WriteAsync(ResultMessage(), cancellation.Token);
        await peer.WriteAsync(
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["MessageType"] = "DeviceList",
                ["DeviceList"] = new object?[]
                {
                    new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["MessageType"] = "Attached",
                        ["DeviceID"] = 7L,
                        ["Properties"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                        {
                            ["DeviceID"] = 7L,
                            ["SerialNumber"] = "CPW123",
                        },
                    },
                },
            },
            cancellation.Token);

        IReadOnlyList<IReadOnlyDictionary<string, object?>> devices = await listTask;

        Assert.Single(devices);
        Assert.Equal("CPW123", devices[0]["SerialNumber"]);
    }

    [Fact]
    public async Task IOSConnectorReturnsSerialDeviceInfoAndStreamTransport()
    {
        using var cancellation = new CancellationTokenSource(ConnectorTestDefaults.Timeout);
        var factory = new DuplexServerFactory();
        await using var connector = new IOSConnector(
            serial: "CPW123",
            cozmoPort: 5106,
            streamFactory: factory.CreateStream,
            waitTimeout: TimeSpan.FromSeconds(2));

        Task<ConnectedDevice> connectTask = connector.ConnectAsync(cancellation.Token);
        Stream controlStream = await factory.AcceptAsync(cancellation.Token);
        var control = new TestUsbMuxPeer(controlStream);
        Dictionary<string, object?> listen = await control.ReadAsync(cancellation.Token);
        Assert.Equal("Listen", listen["MessageType"]);
        await control.WriteAsync(ResultMessage(), cancellation.Token);
        await control.WriteAsync(AttachedMessage(7, "CPW123"), cancellation.Token);

        Stream deviceStream = await factory.AcceptAsync(cancellation.Token);
        var device = new TestUsbMuxPeer(deviceStream);
        Dictionary<string, object?> request = await device.ReadAsync(cancellation.Token);
        Assert.Equal("Connect", request["MessageType"]);
        await device.WriteAsync(ResultMessage(), cancellation.Token);

        ConnectedDevice connected = await connectTask;
        try
        {
            Assert.Equal(IOSConnector.ConnectorName, connected.ConnectorName);
            Assert.Equal("ios", connected.DeviceInfo["device_type"]);
            Assert.Equal("7", connected.DeviceInfo["device_id"]);
            Assert.Equal("CPW123", connected.DeviceInfo["serial"]);
            Assert.IsType<StreamTransport>(connected.Transport);
        }
        finally
        {
            await connected.Transport.DisposeAsync();
        }
    }

    [Fact]
    public async Task IOSConnectorWrapsWaitTimeoutWithPythonMessage()
    {
        using var cancellation = new CancellationTokenSource(ConnectorTestDefaults.Timeout);
        var factory = new DuplexServerFactory();
        await using var connector = new IOSConnector(
            streamFactory: factory.CreateStream,
            waitTimeout: TimeSpan.FromMilliseconds(100));

        Task<ConnectedDevice> connectTask = connector.ConnectAsync(cancellation.Token);
        Stream controlStream = await factory.AcceptAsync(cancellation.Token);
        var control = new TestUsbMuxPeer(controlStream);
        await control.ReadAsync(cancellation.Token);
        await control.WriteAsync(ResultMessage(), cancellation.Token);

        var exception = await Assert.ThrowsAsync<CozmoConnectionException>(() => connectTask);

        Assert.Equal("No connected iOS devices running Cozmo in SDK mode", exception.Message);
        Assert.IsType<TimeoutException>(exception.InnerException);
    }

    private static async Task<(UsbMuxClient Client, TestUsbMuxPeer Control)> ConnectClientAsync(
        DuplexServerFactory factory,
        CancellationToken cancellationToken)
    {
        var client = new UsbMuxClient(factory.CreateStream, TimeSpan.FromSeconds(2));
        try
        {
            Task connectTask = client.ConnectAsync(cancellationToken);
            Stream controlStream = await factory.AcceptAsync(cancellationToken);
            var control = new TestUsbMuxPeer(controlStream);
            Dictionary<string, object?> listen = await control.ReadAsync(cancellationToken);
            Assert.Equal("Listen", listen["MessageType"]);
            await control.WriteRawAsync(FramePayload(CapturedResultPlist), cancellationToken);
            await connectTask;
            return (client, control);
        }
        catch (Exception)
        {
            await client.DisposeAsync();
            throw;
        }
    }

    private static Dictionary<string, object?> AttachedMessage(long deviceId, string serialNumber)
    {
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["MessageType"] = "Attached",
            ["Properties"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["ConnectionType"] = "USB",
                ["DeviceID"] = deviceId,
                ["ProductID"] = 4776L,
                ["SerialNumber"] = serialNumber,
            },
        };
    }

    private static Dictionary<string, object?> ResultMessage()
    {
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["MessageType"] = "Result",
            ["Number"] = 0L,
        };
    }

    private static byte[] FramePayload(string payload)
    {
        byte[] plist = Encoding.UTF8.GetBytes(payload);
        var frame = new byte[UsbMuxProtocol.HeaderSize + plist.Length];
        BinaryPrimitives.WriteUInt32LittleEndian(frame.AsSpan(0, 4), (uint)frame.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(frame.AsSpan(4, 4), UsbMuxProtocol.ProtocolVersion);
        BinaryPrimitives.WriteUInt32LittleEndian(frame.AsSpan(8, 4), UsbMuxProtocol.PlistRequestType);
        BinaryPrimitives.WriteUInt32LittleEndian(frame.AsSpan(12, 4), UsbMuxProtocol.DefaultTag);
        plist.CopyTo(frame.AsSpan(UsbMuxProtocol.HeaderSize));
        return frame;
    }

    private static byte[] CladFrame(params byte[] payload)
    {
        var frame = new byte[payload.Length + 2];
        BinaryPrimitives.WriteUInt16LittleEndian(frame, (ushort)payload.Length);
        payload.CopyTo(frame.AsSpan(2));
        return frame;
    }

    private static async Task<(byte[] Header, byte[] Payload)> ReadRawFrameAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        var header = new byte[UsbMuxProtocol.HeaderSize];
        await stream.ReadExactlyAsync(header, cancellationToken);
        int length = (int)BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(0, 4));
        var payload = new byte[length - UsbMuxProtocol.HeaderSize];
        if (payload.Length > 0)
        {
            await stream.ReadExactlyAsync(payload, cancellationToken);
        }

        return (header, payload);
    }
}
