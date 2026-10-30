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
using Xunit;

namespace Cozmo.Tests;

public sealed class RunnerTests
{
    [Fact]
    public async Task ConnectInitializesRobotAndDisposes()
    {
        var connector = new FakeConnector();
        connector.Enqueue(Handshake());

        CozmoRunner runner = await CozmoRunner.ConnectAsync(
            connector,
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(2));

        Assert.True(runner.Robot.IsReady);
        Assert.True(runner.Connection.IsUiConnected);
        Assert.Equal("fake", runner.Device.ConnectorName);
        Assert.Equal(1, runner.Robot.RobotId);

        await runner.DisposeAsync();
    }

    [Fact]
    public async Task RunProgramReceivesReadyRobot()
    {
        var connector = new FakeConnector();
        connector.Enqueue(Handshake());

        bool ran = false;
        await CozmoRunner.RunProgramAsync(
            robot =>
            {
                ran = true;
                Assert.True(robot.IsReady);
                return Task.CompletedTask;
            },
            connector);

        Assert.True(ran);
    }

    [Fact]
    public async Task ConnectTimesOutWithoutHandshake()
    {
        var connector = new FakeConnector();

        await Assert.ThrowsAsync<TimeoutException>(
            () => CozmoRunner.ConnectAsync(connector, TimeSpan.FromMilliseconds(200)));
    }

    private static MessageEngineToGame Handshake() => new()
    {
        UiDeviceConnected = new UiDeviceConnected
        {
            connectionType = (byte)UiConnectionType.SdkOverTcp,
            deviceID = CozmoConnection.SdkDeviceId,
            successful = 1,
            toGameCLADHash = CladIdentity.EngineToGameHash,
            toEngineCLADHash = CladIdentity.GameToEngineHash,
            buildVersion = "00003.00006.00000",
        },
    };

    private sealed class FakeConnector : IDeviceConnector
    {
        private readonly FakeTransport _transport = new();

        public FakeConnector()
        {
            _transport.FrameSent += OnFrameSent;
        }

        public string Name => "fake";

        public bool Enabled => true;

        public bool Disposed { get; private set; }

        public void Enqueue(MessageEngineToGame message)
        {
            using var stream = new MemoryStream();
            message.Pack(new CladBinaryWriter(stream));
            _transport.EnqueueFrame(stream.ToArray());
        }

        public Task<ConnectedDevice> ConnectAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new ConnectedDevice(
                _transport,
                new Dictionary<string, string> { ["device_type"] = "fake" },
                "fake"));

        public async ValueTask DisposeAsync()
        {
            Disposed = true;
            _transport.FrameSent -= OnFrameSent;
            await _transport.DisposeAsync();
        }

        private void OnFrameSent(byte[] payload)
        {
            MessageGameToEngine message;
            using (var stream = new MemoryStream(payload, writable: false))
            {
                message = MessageGameToEngine.UnpackFrom(new CladBinaryReader(stream));
            }

            switch (message.tag)
            {
                case MessageGameToEngine.Tag.DeleteAllCustomObjects:
                    Enqueue(new MessageEngineToGame
                    {
                        RobotDeletedAllCustomObjects = new RobotDeletedAllCustomObjects(),
                    });
                    break;
                case MessageGameToEngine.Tag.RequestAvailableAnimations:
                    Enqueue(new MessageEngineToGame
                    {
                        AnimationAvailable = new AnimationAvailable { animName = "anim_a" },
                    });
                    Enqueue(new MessageEngineToGame { EndOfMessage = new EndOfMessage() });
                    break;
            }
        }
    }
}
