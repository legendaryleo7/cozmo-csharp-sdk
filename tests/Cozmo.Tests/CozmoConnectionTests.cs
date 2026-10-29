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

namespace Cozmo.Tests;

public sealed class CozmoConnectionTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(2);

    [Fact]
    public async Task MatchingHandshakeSendsSuccessAndRequestsObjects()
    {
        await using var transport = new FakeTransport();
        await using var connection = new CozmoConnection(transport);
        await connection.ConnectAsync();

        Task<EvtConnected> connected = connection.WaitFor<EvtConnected>(timeout: TestTimeout);
        transport.EnqueueFrame(PackEngineToGame(new MessageEngineToGame
        {
            UiDeviceConnected = Handshake(CladIdentity.EngineToGameHash, CladIdentity.GameToEngineHash, "00003.00006.00000"),
        }));

        EvtConnected evt = await connected;
        Assert.Same(connection, evt.Conn);
        Assert.True(connection.IsUiConnected);

        await WaitUntilAsync(() => transport.SentFrames.Count >= 3);
        List<MessageGameToEngine> sent = transport.SentFrames.Select(DecodeGameToEngine).ToList();
        Assert.Equal(MessageGameToEngine.Tag.UiDeviceConnectionSuccess, sent[0].tag);
        Assert.Equal(SdkVersion.Version, sent[0].UiDeviceConnectionSuccess.sdkModuleVersion);
        Assert.Equal(CladIdentity.PackageVersion, sent[0].UiDeviceConnectionSuccess.buildVersion);
        Assert.Equal("C#", sent[0].UiDeviceConnectionSuccess.pythonImplementation);
        Assert.Equal(MessageGameToEngine.Tag.RequestConnectedObjects, sent[1].tag);
        Assert.Equal(MessageGameToEngine.Tag.RequestLocatedObjectStates, sent[2].tag);
    }

    [Fact]
    public async Task HashMismatchSendsWrongVersionAndAborts()
    {
        await using var transport = new FakeTransport();
        await using var connection = new CozmoConnection(transport);
        await connection.ConnectAsync();

        Task<EvtConnectionClosed> closed = connection.WaitFor<EvtConnectionClosed>(timeout: TestTimeout);
        byte[] badHash = (byte[])CladIdentity.EngineToGameHash.Clone();
        badHash[0] ^= 0xFF;
        transport.EnqueueFrame(PackEngineToGame(new MessageEngineToGame
        {
            UiDeviceConnected = Handshake(badHash, CladIdentity.GameToEngineHash, "00003.00006.00000"),
        }));

        EvtConnectionClosed evt = await closed;
        var mismatch = Assert.IsType<SdkVersionMismatchException>(evt.Reason);
        Assert.Contains("CLAD Hashes do not match", mismatch.Message, StringComparison.Ordinal);
        Assert.False(connection.IsConnected);

        List<MessageGameToEngine> sent = transport.SentFrames.Select(DecodeGameToEngine).ToList();
        Assert.Equal(MessageGameToEngine.Tag.UiDeviceConnectionWrongVersion, sent[0].tag);
        Assert.Equal(CladIdentity.PackageVersion, sent[0].UiDeviceConnectionWrongVersion.buildVersion);
    }

    [Fact]
    public async Task MinorBuildVersionDifferenceIsAccepted()
    {
        await using var transport = new FakeTransport();
        await using var connection = new CozmoConnection(transport);
        await connection.ConnectAsync();

        Task<EvtConnected> connected = connection.WaitFor<EvtConnected>(timeout: TestTimeout);
        transport.EnqueueFrame(PackEngineToGame(new MessageEngineToGame
        {
            UiDeviceConnected = Handshake(CladIdentity.EngineToGameHash, CladIdentity.GameToEngineHash, "00003.00006.00001"),
        }));

        await connected;
        Assert.True(connection.IsUiConnected);
        Assert.Equal(MessageGameToEngine.Tag.UiDeviceConnectionSuccess, DecodeGameToEngine(transport.SentFrames[0]).tag);
    }

    [Fact]
    public async Task MajorBuildVersionDifferenceIsRejected()
    {
        await using var transport = new FakeTransport();
        await using var connection = new CozmoConnection(transport);
        await connection.ConnectAsync();

        Task<EvtConnectionClosed> closed = connection.WaitFor<EvtConnectionClosed>(timeout: TestTimeout);
        transport.EnqueueFrame(PackEngineToGame(new MessageEngineToGame
        {
            UiDeviceConnected = Handshake(CladIdentity.EngineToGameHash, CladIdentity.GameToEngineHash, "00004.00000.00000"),
        }));

        EvtConnectionClosed evt = await closed;
        var mismatch = Assert.IsType<SdkVersionMismatchException>(evt.Reason);
        Assert.Contains("App and SDK versions do not match", mismatch.Message, StringComparison.Ordinal);
        Assert.Contains("http://cozmosdk.anki.bot/docs/downloads.html", mismatch.Message, StringComparison.Ordinal);
        Assert.Equal("4.0.0", mismatch.AppVersion);
        Assert.Equal("1.4.12", mismatch.SdkVersion);
    }

    [Fact]
    public async Task HandshakeForAnotherConnectionTypeIsIgnored()
    {
        await using var transport = new FakeTransport();
        await using var connection = new CozmoConnection(transport);
        await connection.ConnectAsync();

        transport.EnqueueFrame(PackEngineToGame(new MessageEngineToGame
        {
            UiDeviceConnected = Handshake(
                CladIdentity.EngineToGameHash,
                CladIdentity.GameToEngineHash,
                "00003.00006.00000",
                (byte)UiConnectionType.UI),
        }));

        await Task.Delay(50);
        Assert.Empty(transport.SentFrames);
        Assert.False(connection.IsUiConnected);
        Assert.True(connection.IsConnected);
    }

    [Fact]
    public async Task PingIsEchoedWithSameCounter()
    {
        await using var transport = new FakeTransport();
        await using var connection = new CozmoConnection(transport);
        await connection.ConnectAsync();

        transport.EnqueueFrame(PackEngineToGame(new MessageEngineToGame
        {
            Ping = new Ping { counter = 42, timeSent_ms = 1234.5, isResponse = false },
        }));

        await WaitUntilAsync(() => transport.SentFrames.Count >= 1);
        MessageGameToEngine sent = DecodeGameToEngine(transport.SentFrames[0]);
        Assert.Equal(MessageGameToEngine.Tag.Ping, sent.tag);
        Assert.Equal(42u, sent.Ping.counter);
        Assert.Equal(1234.5, sent.Ping.timeSent_ms);
        Assert.True(sent.Ping.isResponse);
    }

    [Fact]
    public async Task AbortFailsPendingWaitsAndClosesTransport()
    {
        await using var transport = new FakeTransport();
        await using var connection = new CozmoConnection(transport);
        await connection.ConnectAsync();

        Task<EvtConnected> waiting = connection.WaitFor<EvtConnected>(timeout: TimeSpan.FromSeconds(5));
        await connection.AbortAsync(new ConnectionAbortedException("gone"));

        await Assert.ThrowsAsync<ConnectionAbortedException>(() => waiting);
        Assert.False(connection.IsConnected);
        Assert.True(transport.CloseCalled);
    }

    [Fact]
    public async Task ShutdownFailsPendingWaitsWithSdkShutdown()
    {
        await using var transport = new FakeTransport();
        await using var connection = new CozmoConnection(transport);
        await connection.ConnectAsync();

        Task<EvtConnected> waiting = connection.WaitFor<EvtConnected>(timeout: TimeSpan.FromSeconds(5));
        await connection.ShutdownAsync();

        await Assert.ThrowsAsync<SdkShutdownException>(() => waiting);
        Assert.True(transport.CloseCalled);
    }

    [Fact]
    public async Task LostConnectionAbortsConnection()
    {
        await using var transport = new FakeTransport();
        await using var connection = new CozmoConnection(transport);
        await connection.ConnectAsync();

        Task<EvtConnectionClosed> closed = connection.WaitFor<EvtConnectionClosed>(timeout: TestTimeout);
        transport.CompleteInbound();

        EvtConnectionClosed evt = await closed;
        Assert.IsType<ConnectionAbortedException>(evt.Reason);
        Assert.False(connection.IsConnected);
    }

    [Fact]
    public async Task SendAsyncPacksTheUnion()
    {
        await using var transport = new FakeTransport();
        await using var connection = new CozmoConnection(transport);
        await connection.ConnectAsync();

        await connection.SendAsync(new MessageGameToEngine
        {
            StopAllAudioEvents = new Anki.AudioEngine.Multiplexer.StopAllAudioEvents(),
        });

        Assert.Single(transport.SentFrames);
        Assert.Equal(MessageGameToEngine.Tag.StopAllAudioEvents, DecodeGameToEngine(transport.SentFrames[0]).tag);
    }

    private static UiDeviceConnected Handshake(
        byte[] toGameHash,
        byte[] toEngineHash,
        string buildVersion,
        byte connectionType = (byte)UiConnectionType.SdkOverTcp) => new()
        {
            connectionType = connectionType,
            deviceID = CozmoConnection.SdkDeviceId,
            successful = 1,
            toGameCLADHash = toGameHash,
            toEngineCLADHash = toEngineHash,
            buildVersion = buildVersion,
        };

    private static byte[] PackEngineToGame(MessageEngineToGame message)
    {
        using var stream = new MemoryStream();
        message.Pack(new CladBinaryWriter(stream));
        return stream.ToArray();
    }

    private static MessageGameToEngine DecodeGameToEngine(byte[] payload)
    {
        using var stream = new MemoryStream(payload, writable: false);
        return MessageGameToEngine.UnpackFrom(new CladBinaryReader(stream));
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        DateTime deadline = DateTime.UtcNow + TestTimeout;
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException("Condition was not met before the deadline.");
            }

            await Task.Delay(5);
        }
    }
}
