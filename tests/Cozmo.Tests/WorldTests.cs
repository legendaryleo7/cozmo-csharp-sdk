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
using Anki.Cozmo;
using Anki.Cozmo.ExternalInterface;
using Anki.Cozmo.Sdk;
using Anki.Cozmo.Sdk.Clad;
using Xunit;

namespace Cozmo.Tests;

public sealed class WorldTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(2);

    [Fact]
    public async Task ObservedLightCubeIsAllocatedAndBecomesVisible()
    {
        await using Harness harness = await Harness.CreateAsync();
        Task<EvtObjectAppeared> appeared = harness.World.WaitFor<EvtObjectAppeared>(
            evt => evt.Obj is LightCube,
            TestTimeout);

        harness.Enqueue(new MessageEngineToGame { RobotObservedObject = Observed(101, ObjectFamily.LightCube, (int)ObjectType.Block_LIGHTCUBE1) });

        EvtObjectAppeared evt = await appeared;
        var cube = Assert.IsType<LightCube>(evt.Obj);
        Assert.Equal(101, cube.ObjectId);
        Assert.True(cube.IsVisible);
        Assert.Equal(1, harness.World.VisibleObjectCount(typeof(LightCube)));
        Assert.Equal(1, harness.World.VisibleObjectCount());
        Assert.Same(cube, harness.World.LightCubes[(int)ObjectType.Block_LIGHTCUBE1]);
    }

    [Fact]
    public async Task CubeDisappearsAfterVisibilityTimeout()
    {
        await using Harness harness = await Harness.CreateAsync();
        Task<EvtObjectAppeared> appeared = harness.World.WaitFor<EvtObjectAppeared>(timeout: TestTimeout);
        harness.Enqueue(new MessageEngineToGame { RobotObservedObject = Observed(102, ObjectFamily.LightCube, (int)ObjectType.Block_LIGHTCUBE2) });
        await appeared;

        Task<EvtObjectDisappeared> disappeared = harness.World.WaitFor<EvtObjectDisappeared>(timeout: TestTimeout);
        harness.Time.Advance(TimeSpan.FromMilliseconds(500));

        await disappeared;
        Assert.Equal(0, harness.World.VisibleObjectCount());
    }

    [Fact]
    public async Task ObjectTapIsRoutedToTheCube()
    {
        await using Harness harness = await Harness.CreateAsync();
        Task<EvtObjectAppeared> appeared = harness.World.WaitFor<EvtObjectAppeared>(timeout: TestTimeout);
        harness.Enqueue(new MessageEngineToGame { RobotObservedObject = Observed(103, ObjectFamily.LightCube, (int)ObjectType.Block_LIGHTCUBE3) });
        var cube = Assert.IsType<LightCube>((await appeared).Obj);

        Task<EvtObjectTapped> tapped = cube.WaitFor<EvtObjectTapped>(timeout: TestTimeout);
        harness.Enqueue(new MessageEngineToGame
        {
            ObjectTapped = new ObjectTapped { objectID = 103, timestamp = 5, numTaps = 2 },
        });

        EvtObjectTapped evt = await tapped;
        Assert.Equal(2, evt.TapCount);
    }

    [Fact]
    public async Task ObservedFaceIsAllocated()
    {
        await using Harness harness = await Harness.CreateAsync();
        Task<EvtFaceAppeared> appeared = harness.World.WaitFor<EvtFaceAppeared>(timeout: TestTimeout);

        harness.Enqueue(new MessageEngineToGame
        {
            RobotObservedFace = new RobotObservedFace
            {
                faceID = 7,
                timestamp = 1,
                pose = Pose.FromZAngle(0, 0, 0, Util.Degrees(0)).EncodePose(),
                name = "Bob",
            },
        });

        EvtFaceAppeared evt = await appeared;
        Assert.Equal(7, evt.Face.FaceId);
        Assert.Equal("Bob", evt.Face.Name);
        Assert.Equal(1, harness.World.VisibleFaceCount);
        Assert.Same(evt.Face, harness.World.Faces[7]);
    }

    [Fact]
    public async Task ObservedPetIsAllocated()
    {
        await using Harness harness = await Harness.CreateAsync();
        Task<EvtPetAppeared> appeared = harness.World.WaitFor<EvtPetAppeared>(timeout: TestTimeout);

        harness.Enqueue(new MessageEngineToGame
        {
            RobotObservedPet = new RobotObservedPet
            {
                petID = 9,
                timestamp = 1,
                petType = (byte)Anki.Vision.PetType.Cat,
            },
        });

        EvtPetAppeared evt = await appeared;
        Assert.Equal(9, evt.Pet.PetId);
        Assert.Equal(PetType.Cat, evt.Pet.PetType);
        Assert.Equal(1, harness.World.VisiblePetCount);
    }

    [Fact]
    public async Task RawCameraImageIsWrappedAndPublished()
    {
        await using Harness harness = await Harness.CreateAsync();
        Task<EvtNewCameraImage> received = harness.World.WaitFor<EvtNewCameraImage>(timeout: TestTimeout);
        byte[] raw = [1, 2, 3, 4];

        await harness.Robot.Camera.DispatchEventAsync(
            new EvtNewRawCameraImage(new CameraImage(raw)));

        EvtNewCameraImage evt = await received;
        Assert.Equal(0, evt.Image.ImageNumber);
        Assert.Equal(raw, evt.Image.RawImage);
        Assert.Same(evt.Image, harness.World.LatestImage);
    }

    [Fact]
    public async Task NavMemoryMapIsAssembled()
    {
        await using Harness harness = await Harness.CreateAsync();
        Task<EvtNewNavMemoryMap> received = harness.World.WaitFor<EvtNewNavMemoryMap>(timeout: TestTimeout);

        harness.Enqueue(new MessageEngineToGame
        {
            MemoryMapMessageBegin = new MemoryMapMessageBegin
            {
                originId = 1,
                rootDepth = 2,
                rootSize_mm = 100,
                rootCenterX = 0,
                rootCenterY = 0,
            },
        });
        harness.Enqueue(new MessageEngineToGame
        {
            MemoryMapMessage = new MemoryMapMessage
            {
                quadInfos = [new MemoryMapQuadInfo { content = (byte)ENodeContentTypeEnum.ClearOfObstacle, depth = 2 }],
            },
        });
        harness.Enqueue(new MessageEngineToGame { MemoryMapMessageEnd = new MemoryMapMessageEnd() });

        EvtNewNavMemoryMap evt = await received;
        Assert.Equal(1, evt.NavMemoryMap.OriginId);
        Assert.Equal(100, evt.NavMemoryMap.Size);
        Assert.Same(evt.NavMemoryMap, harness.World.NavMemoryMap);
    }

    [Fact]
    public async Task DefineCustomBoxSendsMessageAndStoresArchetype()
    {
        await using Harness harness = await Harness.CreateAsync();

        Task<CustomObject?> defining = harness.World.DefineCustomBoxAsync(
            CustomObjectTypes.CustomType00,
            CustomObjectMarkers.Circles2,
            CustomObjectMarkers.Circles3,
            CustomObjectMarkers.Circles4,
            CustomObjectMarkers.Circles5,
            CustomObjectMarkers.Diamonds2,
            CustomObjectMarkers.Diamonds3,
            depthMm: 30,
            widthMm: 40,
            heightMm: 50,
            markerWidthMm: 20,
            markerHeightMm: 20);

        await WaitUntilAsync(() => harness.Transport.SentFrames.Count > 0);
        MessageGameToEngine sent = DecodeGameToEngine(harness.Transport.SentFrames[^1]);
        Assert.Equal(MessageGameToEngine.Tag.DefineCustomBox, sent.tag);
        Assert.Equal((int)CustomObjectTypes.CustomType00.Id, sent.DefineCustomBox.customType);
        Assert.Equal(30f, sent.DefineCustomBox.xSize_mm);

        harness.Enqueue(new MessageEngineToGame
        {
            DefinedCustomObject = new DefinedCustomObject { success = true },
        });

        CustomObject? archetype = await defining;
        Assert.NotNull(archetype);
        Assert.Equal(30, archetype!.XSizeMm);
        Assert.True(harness.World.CustomObjects.ContainsKey(CustomObjectTypes.CustomType00.Id));
    }

    [Fact]
    public async Task SendAvailableObjectsSendsEnableMessage()
    {
        await using Harness harness = await Harness.CreateAsync();

        await harness.World.SendAvailableObjectsAsync();

        await WaitUntilAsync(() => harness.Transport.SentFrames.Count > 0);
        MessageGameToEngine sent = DecodeGameToEngine(harness.Transport.SentFrames[^1]);
        Assert.Equal(MessageGameToEngine.Tag.SendAvailableObjects, sent.tag);
        Assert.True(sent.SendAvailableObjects.enable);
    }

    [Fact]
    public async Task DeleteAllCustomObjectsWaitsForResponse()
    {
        await using Harness harness = await Harness.CreateAsync();

        Task deleting = harness.World.DeleteAllCustomObjectsAsync();
        await WaitUntilAsync(() => harness.Transport.SentFrames.Count > 0);
        MessageGameToEngine sent = DecodeGameToEngine(harness.Transport.SentFrames[^1]);
        Assert.Equal(MessageGameToEngine.Tag.DeleteAllCustomObjects, sent.tag);

        harness.Enqueue(new MessageEngineToGame
        {
            RobotDeletedAllCustomObjects = new RobotDeletedAllCustomObjects(),
        });

        await deleting.WaitAsync(TestTimeout);
    }

    private static RobotObservedObject Observed(int objectId, ObjectFamily family, int objectType) => new()
    {
        objectID = objectId,
        objectFamily = (int)family,
        objectType = objectType,
        timestamp = 1,
        pose = Pose.FromZAngle(10, 20, 0, Util.Degrees(0)).EncodePose(),
        img_rect = new CladRect(1, 2, 3, 4),
    };

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
                throw new TimeoutException("Condition was not met in time.");
            }

            await Task.Delay(5);
        }
    }

    private sealed class FakeRobot : IWorldRobot
    {
        public FakeRobot(CozmoConnection connection)
        {
            Connection = connection;
            Camera = new Camera(connection);
        }

        public CozmoConnection Connection { get; }

        public int RobotId => 1;

        public Pose? Pose { get; set; } = Util.PoseZAngle(0, 0, 0, Util.Degrees(0));

        public Camera Camera { get; }
    }

    private sealed class Harness : IAsyncDisposable
    {
        private Harness()
        {
            Transport = new FakeTransport();
            Connection = new CozmoConnection(Transport);
            Robot = new FakeRobot(Connection);
            World = new World(Connection, Robot, Time);
        }

        public FakeTransport Transport { get; }

        public ManualTimeProvider Time { get; } = new();

        public CozmoConnection Connection { get; }

        public FakeRobot Robot { get; }

        public World World { get; }

        public static async Task<Harness> CreateAsync()
        {
            var harness = new Harness();
            await harness.Connection.ConnectAsync();
            return harness;
        }

        public void Enqueue(MessageEngineToGame message)
        {
            using var stream = new MemoryStream();
            message.Pack(new CladBinaryWriter(stream));
            Transport.EnqueueFrame(stream.ToArray());
        }

        public async ValueTask DisposeAsync()
        {
            World.Dispose();
            Robot.Camera.Dispose();
            await Connection.DisposeAsync();
            await Transport.DisposeAsync();
        }
    }
}
