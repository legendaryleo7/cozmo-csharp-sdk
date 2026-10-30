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
using Anki.Vision;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ObjectConnectionStateEvt = Anki.Cozmo.ObjectConnectionState;

namespace Anki.Cozmo.Sdk;

/// <summary>Represents the state of the world, as known to a Cozmo robot.</summary>
/// <remarks>
/// The world owns the object, face and pet registries, routes inbound engine messages to them,
/// assembles navigation memory maps and exposes the latest camera image. The robot adds the
/// world as a child dispatcher, so action and behavior events propagate into
/// <see cref="ActiveAction"/> and <see cref="ActiveBehavior"/>.
/// </remarks>
public sealed class World : Dispatcher, IWorldContext
{
    private readonly CozmoConnection _connection;
    private readonly IWorldRobot _robot;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<World> _logger;
    private readonly Dictionary<int, LightCube> _lightCubes;
    private readonly Dictionary<int, ObservableObject> _objects = new();
    private readonly Dictionary<int, FixedCustomObject> _fixedObjects = new();
    private readonly Dictionary<int, Face> _faces = new();
    private readonly Dictionary<int, Pet> _pets = new();
    private readonly Dictionary<long, CustomObject> _customObjectArchetypes = new();
    private readonly Dictionary<Type, int> _visibleObjectCounts = new();
    private readonly Dictionary<MessageEngineToGame.Tag, Queue<MessageEngineToGame>> _messageBacklog = new();
    private readonly Dictionary<MessageEngineToGame.Tag, List<TaskCompletionSource<MessageEngineToGame>>> _messageWaiters = new();
    private readonly Handler _connectionHandler;
    private readonly Handler _cameraHandler;
    private readonly Handler _objectAppearedHandler;
    private readonly Handler _objectDisappearedHandler;
    private readonly Handler _faceAppearedHandler;
    private readonly Handler _faceDisappearedHandler;
    private readonly Handler _petAppearedHandler;
    private readonly Handler _petDisappearedHandler;
    private readonly Handler _actionStartedHandler;
    private readonly Handler _actionCompletedHandler;
    private readonly Handler _behaviorStartedHandler;
    private readonly Handler _behaviorStoppedHandler;
    private CameraImage? _latestImage;
    private CozmoAction? _activeAction;
    private Behavior? _activeBehavior;
    private NavMemoryMapGrid? _navMemoryMap;
    private NavMemoryMapGrid? _pendingNavMemoryMap;
    private Charger? _charger;
    private int _visibleFaceCount;
    private int _visiblePetCount;
    private int _lastImageNumber = -1;
    private bool _disposed;

    /// <summary>Creates a world bound to a connection and robot.</summary>
    /// <param name="connection">The active connection to the engine.</param>
    /// <param name="robot">The robot this world belongs to.</param>
    /// <param name="timeProvider">Optional clock used for visibility timers and timeouts.</param>
    /// <param name="logger">Optional logger.</param>
    public World(
        CozmoConnection connection,
        IWorldRobot robot,
        TimeProvider? timeProvider = null,
        ILogger<World>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(robot);

        _connection = connection;
        _robot = robot;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _logger = logger ?? NullLogger<World>.Instance;

        _lightCubes = new Dictionary<int, LightCube>
        {
            [(int)CustomObjectTypes.LightCube1Id.Id] = CreateCube(CustomObjectTypes.LightCube1Id),
            [(int)CustomObjectTypes.LightCube2Id.Id] = CreateCube(CustomObjectTypes.LightCube2Id),
            [(int)CustomObjectTypes.LightCube3Id.Id] = CreateCube(CustomObjectTypes.LightCube3Id),
        };

        _connectionHandler = connection.AddAsyncHandler<EvtMessageReceived>(OnMessageReceivedAsync);
        _cameraHandler = robot.Camera.AddHandler<EvtNewRawCameraImage>(OnNewRawCameraImage);
        _objectAppearedHandler = AddHandler<EvtObjectAppeared>(OnObjectAppeared);
        _objectDisappearedHandler = AddHandler<EvtObjectDisappeared>(OnObjectDisappeared);
        _faceAppearedHandler = AddHandler<EvtFaceAppeared>(_ => _visibleFaceCount++);
        _faceDisappearedHandler = AddHandler<EvtFaceDisappeared>(_ => _visibleFaceCount--);
        _petAppearedHandler = AddHandler<EvtPetAppeared>(_ => _visiblePetCount++);
        _petDisappearedHandler = AddHandler<EvtPetDisappeared>(_ => _visiblePetCount--);
        _actionStartedHandler = AddHandler<EvtActionStarted>(evt => _activeAction = evt.Action);
        _actionCompletedHandler = AddHandler<EvtActionCompleted>(_ => _activeAction = null);
        _behaviorStartedHandler = AddHandler<EvtBehaviorStarted>(evt => _activeBehavior = evt.Behavior);
        _behaviorStoppedHandler = AddHandler<EvtBehaviorStopped>(_ => _activeBehavior = null);
    }

    /// <summary>The active connection to the engine.</summary>
    public CozmoConnection Connection => _connection;

    /// <summary>The robot this world belongs to.</summary>
    public IWorldRobot Robot => _robot;

    /// <summary>The robot's three light cubes, keyed by object-type id.</summary>
    public IReadOnlyDictionary<int, LightCube> LightCubes => _lightCubes;

    /// <summary>Cozmo's charger, or null until the engine reports one.</summary>
    public Charger? Charger => _charger;

    /// <summary>Faces currently known to the robot, keyed by face id.</summary>
    public IReadOnlyDictionary<int, Face> Faces => _faces;

    /// <summary>Pets currently known to the robot, keyed by pet id.</summary>
    public IReadOnlyDictionary<int, Pet> Pets => _pets;

    /// <summary>Defined custom-object archetypes, keyed by object-type id.</summary>
    public IReadOnlyDictionary<long, CustomObject> CustomObjects => _customObjectArchetypes;

    /// <summary>All observable objects currently known to the robot.</summary>
    public IReadOnlyCollection<ObservableObject> AllObjects => _objects.Values.ToArray();

    /// <summary>Fixed custom objects created by the SDK, keyed by object id.</summary>
    public IReadOnlyDictionary<int, FixedCustomObject> FixedCustomObjects => _fixedObjects;

    /// <summary>The action currently executing, or null.</summary>
    public CozmoAction? ActiveAction => _activeAction;

    /// <summary>The behavior currently executing, or null.</summary>
    public Behavior? ActiveBehavior => _activeBehavior;

    /// <summary>The latest camera image, or null before the first frame.</summary>
    public CameraImage? LatestImage => _latestImage;

    /// <summary>The latest complete navigation memory map, or null.</summary>
    public NavMemoryMapGrid? NavMemoryMap => _navMemoryMap;

    /// <summary>Each object that Cozmo can currently see.</summary>
    public IEnumerable<ObservableObject> VisibleObjects => _objects.Values.Where(obj => obj.IsVisible);

    /// <summary>Each face that Cozmo can currently see.</summary>
    public IEnumerable<Face> VisibleFaces => _faces.Values.Where(face => face.IsVisible);

    /// <summary>Each pet that Cozmo can currently see.</summary>
    public IEnumerable<Pet> VisiblePets => _pets.Values.Where(pet => pet.IsVisible);

    /// <summary>The number of faces that Cozmo can currently see.</summary>
    public int VisibleFaceCount => _visibleFaceCount;

    /// <summary>The number of pets that Cozmo can currently see.</summary>
    public int VisiblePetCount => _visiblePetCount;

    /// <summary>Each light cube that is currently connected.</summary>
    public IEnumerable<LightCube> ConnectedLightCubes => _lightCubes.Values.Where(cube => cube.IsConnected);

    /// <summary>Returns one of the three light cubes by its object type.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="cubeId"/> is null.</exception>
    /// <exception cref="KeyNotFoundException">The object type is not one of the three cubes.</exception>
    public LightCube GetLightCube(CladEnum<ObjectType> cubeId)
    {
        ArgumentNullException.ThrowIfNull(cubeId);
        return _lightCubes.TryGetValue((int)cubeId.Id, out LightCube? cube)
            ? cube
            : throw new KeyNotFoundException($"Object type {cubeId.Name} is not a light cube.");
    }

    /// <summary>Returns how many objects of a type Cozmo can currently see.</summary>
    /// <param name="objectType">An <see cref="ObservableObject"/> subtype, or null for all objects.</param>
    public int VisibleObjectCount(Type? objectType = null)
    {
        Type key = objectType ?? typeof(ObservableObject);
        if (!typeof(ObservableObject).IsAssignableFrom(key))
        {
            throw new ArgumentException("Expected an ObservableObject subtype.", nameof(objectType));
        }

        return _visibleObjectCounts.GetValueOrDefault(key);
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing && !_disposed)
        {
            _disposed = true;
            _connectionHandler.Dispose();
            _cameraHandler.Dispose();
            _objectAppearedHandler.Dispose();
            _objectDisappearedHandler.Dispose();
            _faceAppearedHandler.Dispose();
            _faceDisappearedHandler.Dispose();
            _petAppearedHandler.Dispose();
            _petDisappearedHandler.Dispose();
            _actionStartedHandler.Dispose();
            _actionCompletedHandler.Dispose();
            _behaviorStartedHandler.Dispose();
            _behaviorStoppedHandler.Dispose();
            FailPendingMessageWaits(new ObjectDisposedException(nameof(World)));
        }

        base.Dispose(disposing);
    }

    private LightCube CreateCube(CladEnum<ObjectType> cubeId) => new(
        cubeId,
        _connection,
        world: this,
        timeProvider: _timeProvider,
        dispatchParent: this,
        logger: _logger);

    private void OnObjectAppeared(EvtObjectAppeared evt) => UpdateVisibleObjectCount(evt.Obj, 1);

    private void OnObjectDisappeared(EvtObjectDisappeared evt) => UpdateVisibleObjectCount(evt.Obj, -1);

    private void UpdateVisibleObjectCount(ObservableObject obj, int delta)
    {
        for (Type? type = obj.GetType(); type is not null && type != typeof(object); type = type.BaseType)
        {
            _visibleObjectCounts[type] = _visibleObjectCounts.GetValueOrDefault(type) + delta;
            if (type == typeof(ObservableObject))
            {
                break;
            }
        }
    }

    private void OnNewRawCameraImage(EvtNewRawCameraImage evt)
    {
        _lastImageNumber++;
        _latestImage = new CameraImage(
            evt.Image.RawImage,
            _lastImageNumber,
            evt.Image.Width,
            evt.Image.Height,
            _timeProvider.GetUtcNow());
        _ = DispatchEventAsync(new EvtNewCameraImage(_latestImage));
    }

    private async Task OnMessageReceivedAsync(EvtMessageReceived evt)
    {
        MessageEngineToGame message = evt.Message;
        switch (message.tag)
        {
            case MessageEngineToGame.Tag.RobotObservedObject:
                await OnRobotObservedObjectAsync(message.RobotObservedObject).ConfigureAwait(false);
                break;
            case MessageEngineToGame.Tag.ConnectedObjectStates:
                await OnConnectedObjectStatesAsync(message.ConnectedObjectStates).ConfigureAwait(false);
                break;
            case MessageEngineToGame.Tag.LocatedObjectStates:
                await OnLocatedObjectStatesAsync(message.LocatedObjectStates).ConfigureAwait(false);
                break;
            case MessageEngineToGame.Tag.ObjectTapped:
                await DispatchObjectEventAsync(
                    (int)message.ObjectTapped.objectID,
                    obj => obj is LightCube cube ? cube.HandleObjectTapped(message.ObjectTapped) : Task.CompletedTask)
                    .ConfigureAwait(false);
                break;
            case MessageEngineToGame.Tag.ObjectMoved:
                await DispatchObjectEventAsync(
                    (int)message.ObjectMoved.objectID,
                    obj => obj is LightCube cube ? cube.HandleObjectMoved(message.ObjectMoved) : Task.CompletedTask)
                    .ConfigureAwait(false);
                break;
            case MessageEngineToGame.Tag.ObjectStoppedMoving:
                await DispatchObjectEventAsync(
                    (int)message.ObjectStoppedMoving.objectID,
                    obj => obj is LightCube cube ? cube.HandleObjectStoppedMoving(message.ObjectStoppedMoving) : Task.CompletedTask)
                    .ConfigureAwait(false);
                break;
            case MessageEngineToGame.Tag.ObjectPowerLevel:
                await DispatchObjectEventAsync(
                    (int)message.ObjectPowerLevel.objectID,
                    obj => obj is LightCube cube ? cube.HandleObjectPowerLevel(message.ObjectPowerLevel) : Task.CompletedTask)
                    .ConfigureAwait(false);
                break;
            case MessageEngineToGame.Tag.ObjectConnectionState:
                await DispatchObjectEventAsync(
                    (int)message.ObjectConnectionState.objectID,
                    obj => obj is LightCube cube ? cube.HandleObjectConnectionState(message.ObjectConnectionState) : Task.CompletedTask)
                    .ConfigureAwait(false);
                break;
            case MessageEngineToGame.Tag.RobotObservedFace:
                await OnRobotObservedFaceAsync(message.RobotObservedFace).ConfigureAwait(false);
                break;
            case MessageEngineToGame.Tag.RobotChangedObservedFaceID:
                if (_faces.TryGetValue(message.RobotChangedObservedFaceID.oldID, out Face? changedFace))
                {
                    await changedFace.HandleChangedObservedFaceId(message.RobotChangedObservedFaceID).ConfigureAwait(false);
                }

                break;
            case MessageEngineToGame.Tag.RobotRenamedEnrolledFace:
                if (_faces.TryGetValue(message.RobotRenamedEnrolledFace.faceID, out Face? renamedFace))
                {
                    await renamedFace.HandleRenamedEnrolledFace(message.RobotRenamedEnrolledFace).ConfigureAwait(false);
                }

                break;
            case MessageEngineToGame.Tag.RobotErasedEnrolledFace:
                if (_faces.TryGetValue(message.RobotErasedEnrolledFace.faceID, out Face? erasedFace))
                {
                    await erasedFace.HandleErasedEnrolledFace(message.RobotErasedEnrolledFace).ConfigureAwait(false);
                }

                break;
            case MessageEngineToGame.Tag.RobotObservedPet:
                await OnRobotObservedPetAsync(message.RobotObservedPet).ConfigureAwait(false);
                break;
            case MessageEngineToGame.Tag.RobotDeletedLocatedObject:
                OnDeletedLocatedObject((int)message.RobotDeletedLocatedObject.objectID);
                break;
            case MessageEngineToGame.Tag.RobotDelocalized:
                OnDelocalized();
                break;
            case MessageEngineToGame.Tag.MemoryMapMessageBegin:
                OnMemoryMapBegin(message.MemoryMapMessageBegin);
                break;
            case MessageEngineToGame.Tag.MemoryMapMessage:
                OnMemoryMapMessage(message.MemoryMapMessage);
                break;
            case MessageEngineToGame.Tag.MemoryMapMessageEnd:
                await OnMemoryMapEndAsync().ConfigureAwait(false);
                break;
        }

        SignalMessage(message.tag, message);
    }

    private async Task OnRobotObservedObjectAsync(RobotObservedObject message)
    {
        // world.py:347-352 - the engine sends observed messages for fixed obstacles too.
        if ((ObjectType)message.objectType == ObjectType.CustomFixedObstacle)
        {
            return;
        }

        ObservableObject? obj = FindOrAllocateObject((int)message.objectID, message.objectFamily, message.objectType);
        if (obj is not null)
        {
            await obj.HandleObserved(message).ConfigureAwait(false);
        }
    }

    private async Task OnConnectedObjectStatesAsync(ConnectedObjectStates message)
    {
        foreach (ConnectedObjectState state in message.objects)
        {
            ObservableObject? obj = FindOrAllocateObject(
                (int)state.objectID, state.objectFamily, state.objectType);
            if (obj is not null)
            {
                await obj.HandleConnectedObjectState(state).ConfigureAwait(false);
            }
        }
    }

    private async Task OnLocatedObjectStatesAsync(LocatedObjectStates message)
    {
        var updated = new HashSet<int>();
        foreach (LocatedObjectState state in message.objects)
        {
            int objectId = (int)state.objectID;
            updated.Add(objectId);
            ObservableObject? obj = FindOrAllocateObject(objectId, state.objectFamily, state.objectType);
            if (obj is not null)
            {
                await obj.HandleLocatedObjectState(state).ConfigureAwait(false);
            }
        }

        // world.py:420-434 - objects missing from the response have invalidated poses.
        foreach (ObservableObject obj in _objects.Values)
        {
            if (obj.ObjectId is int objectId && !updated.Contains(objectId) && obj.Pose is { IsValid: true } pose)
            {
                pose.Invalidate();
            }
        }
    }

    private async Task OnRobotObservedFaceAsync(RobotObservedFace message)
    {
        if (message.faceID < 0)
        {
            // world.py:357-362 - the face is tracked but not yet recognised.
            return;
        }

        if (!_faces.TryGetValue(message.faceID, out Face? face))
        {
            face = new Face(
                _connection,
                world: this,
                faceId: message.faceID,
                timeProvider: _timeProvider,
                dispatchParent: this,
                logger: _logger);
            _faces[message.faceID] = face;
        }

        await face.HandleObservedFace(message).ConfigureAwait(false);
    }

    private async Task OnRobotObservedPetAsync(RobotObservedPet message)
    {
        if (!_pets.TryGetValue(message.petID, out Pet? pet))
        {
            pet = new Pet(
                _connection,
                world: this,
                petId: message.petID,
                timeProvider: _timeProvider,
                dispatchParent: this,
                logger: _logger);
            _pets[message.petID] = pet;
        }

        await pet.HandleObservedPet(message).ConfigureAwait(false);
    }

    private async Task DispatchObjectEventAsync(int objectId, Func<ObservableObject, Task> handler)
    {
        if (!_objects.TryGetValue(objectId, out ObservableObject? obj))
        {
            _logger.LogWarning("Object event received for unknown object id {ObjectId}.", objectId);
            return;
        }

        await handler(obj).ConfigureAwait(false);
    }

    private ObservableObject? FindOrAllocateObject(int objectId, int objectFamily, int objectType)
    {
        if (_objects.TryGetValue(objectId, out ObservableObject? existing))
        {
            return existing;
        }

        ObservableObject? created = (ObjectFamily)objectFamily switch
        {
            ObjectFamily.LightCube => AllocateLightCube(objectId, objectType),
            ObjectFamily.Charger => AllocateCharger(objectId),
            ObjectFamily.CustomObject => AllocateCustomObject(objectId, objectType),
            _ => null,
        };

        if (created is null)
        {
            _logger.LogWarning(
                "Unable to allocate object id {ObjectId} with family {Family} and type {Type}.",
                objectId,
                objectFamily,
                objectType);
            return null;
        }

        _objects[objectId] = created;
        return created;
    }

    private LightCube? AllocateLightCube(int objectId, int objectType)
    {
        if (!_lightCubes.TryGetValue(objectType, out LightCube? cube))
        {
            _logger.LogError("Received invalid cube object type {ObjectType}.", objectType);
            return null;
        }

        cube.ObjectId = objectId;
        return cube;
    }

    private Charger AllocateCharger(int objectId)
    {
        if (_charger is not null)
        {
            _logger.LogError("Allocating a second charger; keeping the newest one.");
        }

        _charger = new Charger(
            _connection,
            world: this,
            objectId: objectId,
            timeProvider: _timeProvider,
            dispatchParent: this,
            logger: _logger);
        return _charger;
    }

    private CustomObject? AllocateCustomObject(int objectId, int objectType)
    {
        if (!_customObjectArchetypes.TryGetValue(objectType, out CustomObject? archetype))
        {
            _logger.LogError(
                "Received custom object type {ObjectType} that has not been defined.",
                objectType);
            return null;
        }

        return new CustomObject(
            _connection,
            this,
            archetype.ObjectType,
            archetype.XSizeMm,
            archetype.YSizeMm,
            archetype.ZSizeMm,
            archetype.MarkerWidthMm,
            archetype.MarkerHeightMm,
            archetype.IsUnique,
            objectId,
            _timeProvider,
            this,
            _logger);
    }

    private void OnDeletedLocatedObject(int objectId)
    {
        if (_objects.TryGetValue(objectId, out ObservableObject? obj))
        {
            if (obj.Pose is { IsValid: true } pose)
            {
                pose.Invalidate();
            }

            return;
        }

        if (_fixedObjects.TryGetValue(objectId, out FixedCustomObject? fixedObject))
        {
            if (fixedObject.Pose.IsValid)
            {
                fixedObject.Pose.Invalidate();
            }

            return;
        }

        _logger.LogWarning("Ignoring deleted located object for unknown object id {ObjectId}.", objectId);
    }

    private void OnDelocalized()
    {
        _logger.LogInformation("Robot delocalized - invalidating poses for all objects.");
        foreach (ObservableObject obj in _objects.Values)
        {
            if (obj.Pose is { IsValid: true } pose)
            {
                pose.Invalidate();
            }
        }

        foreach (FixedCustomObject obj in _fixedObjects.Values)
        {
            if (obj.Pose.IsValid)
            {
                obj.Pose.Invalidate();
            }
        }
    }

    private void OnMemoryMapBegin(MemoryMapMessageBegin message)
    {
        if (_pendingNavMemoryMap is not null)
        {
            _logger.LogError("NavMemoryMap unexpected begin - restarting map.");
        }

        _pendingNavMemoryMap = new NavMemoryMapGrid(
            (int)message.originId,
            message.rootDepth,
            message.rootSize_mm,
            message.rootCenterX,
            message.rootCenterY);
    }

    private void OnMemoryMapMessage(MemoryMapMessage message)
    {
        if (_pendingNavMemoryMap is null)
        {
            _logger.LogError("NavMemoryMap message without begin - ignoring.");
            return;
        }

        foreach (MemoryMapQuadInfo quad in message.quadInfos)
        {
            CladEnum<ENodeContentTypeEnum>? content = NodeContentTypes.FindById(quad.content);
            if (content is null)
            {
                _logger.LogWarning("NavMemoryMap quad has unknown content id {Content}.", quad.content);
                continue;
            }

            _pendingNavMemoryMap.AddQuad(content, quad.depth);
        }
    }

    private async Task OnMemoryMapEndAsync()
    {
        if (_pendingNavMemoryMap is null)
        {
            _logger.LogError("NavMemoryMap end without begin - ignoring.");
            return;
        }

        _navMemoryMap = _pendingNavMemoryMap;
        _pendingNavMemoryMap = null;
        await DispatchEventAsync(new EvtNewNavMemoryMap(_navMemoryMap)).ConfigureAwait(false);
    }

    private void SignalMessage(MessageEngineToGame.Tag tag, MessageEngineToGame message)
    {
        lock (_messageWaiters)
        {
            if (_messageWaiters.TryGetValue(tag, out List<TaskCompletionSource<MessageEngineToGame>>? waiters) &&
                waiters.Count > 0)
            {
                TaskCompletionSource<MessageEngineToGame> waiter = waiters[0];
                waiters.RemoveAt(0);
                waiter.TrySetResult(message);
                return;
            }

            if (!_messageBacklog.TryGetValue(tag, out Queue<MessageEngineToGame>? queue))
            {
                queue = new Queue<MessageEngineToGame>();
                _messageBacklog[tag] = queue;
            }

            queue.Enqueue(message);
        }
    }

    private async Task<MessageEngineToGame> WaitForMessageAsync(MessageEngineToGame.Tag tag, TimeSpan timeout)
    {
        TaskCompletionSource<MessageEngineToGame> tcs;
        lock (_messageWaiters)
        {
            if (_messageBacklog.TryGetValue(tag, out Queue<MessageEngineToGame>? queue) && queue.Count > 0)
            {
                return queue.Dequeue();
            }

            tcs = new TaskCompletionSource<MessageEngineToGame>(TaskCreationOptions.RunContinuationsAsynchronously);
            if (!_messageWaiters.TryGetValue(tag, out List<TaskCompletionSource<MessageEngineToGame>>? waiters))
            {
                waiters = new List<TaskCompletionSource<MessageEngineToGame>>();
                _messageWaiters[tag] = waiters;
            }

            waiters.Add(tcs);
        }

        using var cts = new CancellationTokenSource(timeout, _timeProvider);
        try
        {
            return await tcs.Task.WaitAsync(cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw new TimeoutException($"Timed out waiting for engine message {tag}.");
        }
        finally
        {
            lock (_messageWaiters)
            {
                if (_messageWaiters.TryGetValue(tag, out List<TaskCompletionSource<MessageEngineToGame>>? waiters))
                {
                    waiters.Remove(tcs);
                }
            }
        }
    }

    private void FailPendingMessageWaits(Exception exception)
    {
        lock (_messageWaiters)
        {
            foreach (List<TaskCompletionSource<MessageEngineToGame>> waiters in _messageWaiters.Values)
            {
                foreach (TaskCompletionSource<MessageEngineToGame> waiter in waiters)
                {
                    waiter.TrySetException(exception);
                }
            }

            _messageWaiters.Clear();
        }
    }

    /// <summary>Waits until one of the light cubes is observed.</summary>
    /// <param name="timeout">Maximum wait; null waits indefinitely.</param>
    /// <param name="includeExisting">True to return a cube that is already visible.</param>
    public async Task<LightCube> WaitForObservedLightCubeAsync(
        TimeSpan? timeout = null,
        bool includeExisting = true)
    {
        if (includeExisting)
        {
            LightCube? existing = _lightCubes.Values.FirstOrDefault(cube => cube.IsVisible);
            if (existing is not null)
            {
                return existing;
            }
        }

        EvtObjectObserved evt = await WaitFor<EvtObjectObserved>(
            e => e.Obj is LightCube,
            timeout ?? System.Threading.Timeout.InfiniteTimeSpan).ConfigureAwait(false);
        return (LightCube)evt.Obj;
    }

    /// <summary>Waits until a face is observed.</summary>
    public async Task<Face> WaitForObservedFaceAsync(TimeSpan? timeout = null, bool includeExisting = true)
    {
        if (includeExisting)
        {
            Face? existing = VisibleFaces.FirstOrDefault();
            if (existing is not null)
            {
                return existing;
            }
        }

        EvtFaceObserved evt = await WaitFor<EvtFaceObserved>(
            timeout: timeout ?? System.Threading.Timeout.InfiniteTimeSpan).ConfigureAwait(false);
        return evt.Face;
    }

    /// <summary>Waits until a pet is observed.</summary>
    public async Task<Pet> WaitForObservedPetAsync(TimeSpan? timeout = null, bool includeExisting = true)
    {
        if (includeExisting)
        {
            Pet? existing = VisiblePets.FirstOrDefault();
            if (existing is not null)
            {
                return existing;
            }
        }

        EvtPetObserved evt = await WaitFor<EvtPetObserved>(
            timeout: timeout ?? System.Threading.Timeout.InfiniteTimeSpan).ConfigureAwait(false);
        return evt.Pet;
    }

    /// <summary>Waits until the charger is observed.</summary>
    public async Task<Charger> WaitForObservedChargerAsync(
        TimeSpan? timeout = null,
        bool includeExisting = true)
    {
        if (includeExisting && _charger is { IsVisible: true } existing)
        {
            return existing;
        }

        EvtObjectObserved evt = await WaitFor<EvtObjectObserved>(
            e => e.Obj is Charger,
            timeout ?? System.Threading.Timeout.InfiniteTimeSpan).ConfigureAwait(false);
        return (Charger)evt.Obj;
    }

    /// <summary>Waits until a number of unique objects have been seen at least once.</summary>
    /// <param name="num">Number of unique objects to wait for.</param>
    /// <param name="objectType">Optional object subtype to count.</param>
    /// <param name="timeout">Maximum wait; null waits indefinitely.</param>
    /// <param name="includeExisting">True to count objects that are already visible.</param>
    /// <returns>The objects observed while waiting; may be fewer than requested on timeout.</returns>
    public async Task<IReadOnlyList<ObservableObject>> WaitUntilObserveNumObjectsAsync(
        int num,
        Type? objectType = null,
        TimeSpan? timeout = null,
        bool includeExisting = true)
    {
        if (objectType is not null && !typeof(ObservableObject).IsAssignableFrom(objectType))
        {
            throw new ArgumentException("Expected an ObservableObject subtype.", nameof(objectType));
        }

        var seen = new HashSet<ObservableObject>();
        if (includeExisting)
        {
            foreach (ObservableObject obj in VisibleObjects)
            {
                if (objectType is null || objectType.IsInstanceOfType(obj))
                {
                    seen.Add(obj);
                }
            }
        }

        DateTimeOffset? deadline = timeout is null ? null : _timeProvider.GetUtcNow() + timeout.Value;
        while (seen.Count < num)
        {
            TimeSpan? remaining = deadline is null ? null : deadline.Value - _timeProvider.GetUtcNow();
            if (remaining is { } left && left <= TimeSpan.Zero)
            {
                break;
            }

            try
            {
                EvtObjectAppeared evt = await WaitFor<EvtObjectAppeared>(
                    e => objectType is null || objectType.IsInstanceOfType(e.Obj),
                    remaining ?? System.Threading.Timeout.InfiniteTimeSpan).ConfigureAwait(false);
                seen.Add(evt.Obj);
            }
            catch (TimeoutException)
            {
                break;
            }
        }

        return seen.ToArray();
    }

    /// <summary>Waits until at least a number of objects are visible simultaneously.</summary>
    /// <exception cref="TimeoutException">The required count was not reached in time.</exception>
    public async Task<int> WaitUntilNumObjectsVisibleAsync(
        int num,
        Type? objectType = null,
        TimeSpan? timeout = null)
    {
        int count = VisibleObjectCount(objectType);
        DateTimeOffset? deadline = timeout is null ? null : _timeProvider.GetUtcNow() + timeout.Value;
        while (count < num)
        {
            TimeSpan? remaining = deadline is null ? null : deadline.Value - _timeProvider.GetUtcNow();
            if (remaining is { } left && left <= TimeSpan.Zero)
            {
                break;
            }

            try
            {
                await WaitFor<EvtObjectAppeared>(
                    timeout: remaining ?? System.Threading.Timeout.InfiniteTimeSpan).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                break;
            }

            count = VisibleObjectCount(objectType);
        }

        if (count < num)
        {
            throw new TimeoutException($"Only {count} of {num} objects became visible.");
        }

        return count;
    }

    /// <summary>Asks the engine to send the currently available objects.</summary>
    public Task SendAvailableObjectsAsync() =>
        _connection.SendAsync(new MessageGameToEngine
        {
            SendAvailableObjects = new SendAvailableObjects { enable = true },
        });

    /// <summary>Requests navigation memory map updates at the given frequency.</summary>
    /// <param name="frequencySeconds">Seconds between updates; negative disables updates.</param>
    public Task RequestNavMemoryMapAsync(double frequencySeconds) =>
        _connection.SendAsync(new MessageGameToEngine
        {
            SetMemoryMapBroadcastFrequency_sec = new SetMemoryMapBroadcastFrequency_sec
            {
                frequency = (float)frequencySeconds,
            },
        });

    /// <summary>Enables or disables the engine's light-cube tap filter.</summary>
    public Task EnableBlockTapFilterAsync(bool enable = true) =>
        _connection.SendAsync(new MessageGameToEngine
        {
            EnableBlockTapFilter = new EnableBlockTapFilter { enable = enable },
        });

    /// <summary>Tells the SDK whether to disconnect from cubes when the program ends.</summary>
    public Task AutoDisconnectFromCubesAtEndAsync(bool enable = true) =>
        _connection.SendAsync(new MessageGameToEngine
        {
            SetShouldAutoDisconnectFromCubesAtEnd = new SetShouldAutoDisconnectFromCubesAtEnd
            {
                doAutoDisconnect = enable,
            },
        });

    /// <summary>Disconnects from all light cubes to save their batteries.</summary>
    public Task DisconnectFromCubesAsync() =>
        _connection.SendAsync(new MessageGameToEngine
        {
            BlockPoolResetMessage = new BlockPoolResetMessage
            {
                enable = false,
                maintainPersistentPool = true,
            },
        });

    /// <summary>Asks the engine to connect to all light cubes and waits for the result.</summary>
    /// <returns>True when all three cubes are connected.</returns>
    public async Task<bool> ConnectToCubesAsync()
    {
        int connectedCount = ConnectedLightCubes.Count();
        int missing = 3 - connectedCount;
        if (missing < 1)
        {
            _logger.LogInformation("connect_to_cubes skipped - already connected to {Count} cubes.", connectedCount);
            return true;
        }

        _logger.LogInformation(
            "Connecting to cubes (already connected to {Connected}, waiting for {Missing}).",
            connectedCount,
            missing);

        bool success = true;
        await _connection.SendAsync(new MessageGameToEngine
        {
            BlockPoolResetMessage = new BlockPoolResetMessage
            {
                enable = true,
                maintainPersistentPool = true,
            },
        }).ConfigureAwait(false);

        try
        {
            for (int index = 0; index < missing; index++)
            {
                await WaitForMessageAsync(
                    MessageEngineToGame.Tag.ObjectConnectionState,
                    TimeSpan.FromSeconds(10)).ConfigureAwait(false);
            }
        }
        catch (TimeoutException)
        {
            _logger.LogWarning("Failed to connect to all cubes in time.");
            success = false;
        }

        if (success)
        {
            _logger.LogInformation("Connected to all cubes.");
        }

        await _connection.SendAsync(new MessageGameToEngine
        {
            RequestConnectedObjects = new RequestConnectedObjects(),
        }).ConfigureAwait(false);

        try
        {
            await WaitForMessageAsync(
                MessageEngineToGame.Tag.ConnectedObjectStates,
                TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            _logger.LogWarning("Failed to receive connected cube states.");
            success = false;
        }

        return success;
    }

    /// <summary>Causes the robot to forget all custom marker and fixed custom objects.</summary>
    public async Task DeleteAllCustomObjectsAsync()
    {
        Task<MessageEngineToGame> response = WaitForMessageAsync(
            MessageEngineToGame.Tag.RobotDeletedAllCustomObjects,
            TimeSpan.FromSeconds(5));
        await _connection.SendAsync(new MessageGameToEngine
        {
            DeleteAllCustomObjects = new DeleteAllCustomObjects(),
        }).ConfigureAwait(false);
        await response.ConfigureAwait(false);
        RemoveCustomMarkerObjectInstances();
        _fixedObjects.Clear();
    }

    /// <summary>Causes the robot to forget custom marker object instances.</summary>
    public async Task DeleteCustomMarkerObjectsAsync()
    {
        Task<MessageEngineToGame> response = WaitForMessageAsync(
            MessageEngineToGame.Tag.RobotDeletedCustomMarkerObjects,
            TimeSpan.FromSeconds(5));
        await _connection.SendAsync(new MessageGameToEngine
        {
            DeleteCustomMarkerObjects = new DeleteCustomMarkerObjects(),
        }).ConfigureAwait(false);
        await response.ConfigureAwait(false);
        RemoveCustomMarkerObjectInstances();
    }

    /// <summary>Causes the robot to forget fixed custom objects.</summary>
    public async Task DeleteFixedCustomObjectsAsync()
    {
        Task<MessageEngineToGame> response = WaitForMessageAsync(
            MessageEngineToGame.Tag.RobotDeletedFixedCustomObjects,
            TimeSpan.FromSeconds(5));
        await _connection.SendAsync(new MessageGameToEngine
        {
            DeleteFixedCustomObjects = new DeleteFixedCustomObjects(),
        }).ConfigureAwait(false);
        await response.ConfigureAwait(false);
        _fixedObjects.Clear();
    }

    /// <summary>Removes all custom marker object definitions and instances.</summary>
    public async Task UndefineAllCustomMarkerObjectsAsync()
    {
        Task<MessageEngineToGame> response = WaitForMessageAsync(
            MessageEngineToGame.Tag.RobotDeletedCustomMarkerObjects,
            TimeSpan.FromSeconds(5));
        await _connection.SendAsync(new MessageGameToEngine
        {
            UndefineAllCustomMarkerObjects = new UndefineAllCustomMarkerObjects(),
        }).ConfigureAwait(false);
        await response.ConfigureAwait(false);
        RemoveCustomMarkerObjectInstances();
        _customObjectArchetypes.Clear();
    }

    /// <summary>Defines a cuboid of custom size bound to a custom object type.</summary>
    /// <exception cref="ArgumentException">The six markers are not unique.</exception>
    public async Task<CustomObject?> DefineCustomBoxAsync(
        CladEnum<ObjectType> customObjectType,
        CladEnum<CustomObjectMarker> markerFront,
        CladEnum<CustomObjectMarker> markerBack,
        CladEnum<CustomObjectMarker> markerTop,
        CladEnum<CustomObjectMarker> markerBottom,
        CladEnum<CustomObjectMarker> markerLeft,
        CladEnum<CustomObjectMarker> markerRight,
        double depthMm,
        double widthMm,
        double heightMm,
        double markerWidthMm,
        double markerHeightMm,
        bool isUnique = true)
    {
        ArgumentNullException.ThrowIfNull(customObjectType);
        ArgumentNullException.ThrowIfNull(markerFront);
        ArgumentNullException.ThrowIfNull(markerBack);
        ArgumentNullException.ThrowIfNull(markerTop);
        ArgumentNullException.ThrowIfNull(markerBottom);
        ArgumentNullException.ThrowIfNull(markerLeft);
        ArgumentNullException.ThrowIfNull(markerRight);

        var markers = new HashSet<long>
        {
            markerFront.Id, markerBack.Id, markerTop.Id, markerBottom.Id, markerLeft.Id, markerRight.Id,
        };
        if (markers.Count != 6)
        {
            throw new ArgumentException("All markers must be unique for a custom box.");
        }

        var archetype = new CustomObject(
            _connection,
            this,
            customObjectType,
            depthMm,
            widthMm,
            heightMm,
            markerWidthMm,
            markerHeightMm,
            isUnique,
            dispatchParent: this,
            timeProvider: _timeProvider,
            logger: _logger);

        Task<MessageEngineToGame> response = WaitForMessageAsync(
            MessageEngineToGame.Tag.DefinedCustomObject,
            TimeSpan.FromSeconds(5));
        await _connection.SendAsync(new MessageGameToEngine
        {
            DefineCustomBox = new DefineCustomBox
            {
                customType = (int)customObjectType.Id,
                markerFront = (short)markerFront.Id,
                markerBack = (short)markerBack.Id,
                markerTop = (short)markerTop.Id,
                markerBottom = (short)markerBottom.Id,
                markerLeft = (short)markerLeft.Id,
                markerRight = (short)markerRight.Id,
                xSize_mm = (float)depthMm,
                ySize_mm = (float)widthMm,
                zSize_mm = (float)heightMm,
                markerWidth_mm = (float)markerWidthMm,
                markerHeight_mm = (float)markerHeightMm,
                isUnique = isUnique,
            },
        }).ConfigureAwait(false);

        return await FinishCustomObjectDefinitionAsync(archetype, response).ConfigureAwait(false);
    }

    /// <summary>Defines a cube of custom size bound to a custom object type.</summary>
    public async Task<CustomObject?> DefineCustomCubeAsync(
        CladEnum<ObjectType> customObjectType,
        CladEnum<CustomObjectMarker> marker,
        double sizeMm,
        double markerWidthMm,
        double markerHeightMm,
        bool isUnique = true)
    {
        ArgumentNullException.ThrowIfNull(customObjectType);
        ArgumentNullException.ThrowIfNull(marker);

        var archetype = new CustomObject(
            _connection,
            this,
            customObjectType,
            sizeMm,
            sizeMm,
            sizeMm,
            markerWidthMm,
            markerHeightMm,
            isUnique,
            dispatchParent: this,
            timeProvider: _timeProvider,
            logger: _logger);

        Task<MessageEngineToGame> response = WaitForMessageAsync(
            MessageEngineToGame.Tag.DefinedCustomObject,
            TimeSpan.FromSeconds(5));
        await _connection.SendAsync(new MessageGameToEngine
        {
            DefineCustomCube = new DefineCustomCube
            {
                customType = (int)customObjectType.Id,
                marker = (short)marker.Id,
                size_mm = (float)sizeMm,
                markerWidth_mm = (float)markerWidthMm,
                markerHeight_mm = (float)markerHeightMm,
                isUnique = isUnique,
            },
        }).ConfigureAwait(false);

        return await FinishCustomObjectDefinitionAsync(archetype, response).ConfigureAwait(false);
    }

    /// <summary>Defines a wall of custom width and height bound to a custom object type.</summary>
    public async Task<CustomObject?> DefineCustomWallAsync(
        CladEnum<ObjectType> customObjectType,
        CladEnum<CustomObjectMarker> marker,
        double widthMm,
        double heightMm,
        double markerWidthMm,
        double markerHeightMm,
        bool isUnique = true)
    {
        ArgumentNullException.ThrowIfNull(customObjectType);
        ArgumentNullException.ThrowIfNull(marker);

        const double wallThicknessMm = 10.0; // world.py:918 - hardcoded engine constant.

        var archetype = new CustomObject(
            _connection,
            this,
            customObjectType,
            wallThicknessMm,
            widthMm,
            heightMm,
            markerWidthMm,
            markerHeightMm,
            isUnique,
            dispatchParent: this,
            timeProvider: _timeProvider,
            logger: _logger);

        Task<MessageEngineToGame> response = WaitForMessageAsync(
            MessageEngineToGame.Tag.DefinedCustomObject,
            TimeSpan.FromSeconds(5));
        await _connection.SendAsync(new MessageGameToEngine
        {
            DefineCustomWall = new DefineCustomWall
            {
                customType = (int)customObjectType.Id,
                marker = (short)marker.Id,
                width_mm = (float)widthMm,
                height_mm = (float)heightMm,
                markerWidth_mm = (float)markerWidthMm,
                markerHeight_mm = (float)markerHeightMm,
                isUnique = isUnique,
            },
        }).ConfigureAwait(false);

        return await FinishCustomObjectDefinitionAsync(archetype, response).ConfigureAwait(false);
    }

    /// <summary>Creates a fixed custom object at a pose in the world.</summary>
    /// <param name="pose">The object's pose.</param>
    /// <param name="xSizeMm">Length along the X axis, in millimetres.</param>
    /// <param name="ySizeMm">Length along the Y axis, in millimetres.</param>
    /// <param name="zSizeMm">Length along the Z axis, in millimetres.</param>
    /// <param name="relativeToRobot">True when the pose is relative to the robot's pose.</param>
    /// <param name="useRobotOrigin">True to use the robot's origin id for the pose.</param>
    public async Task<FixedCustomObject> CreateCustomFixedObjectAsync(
        Pose pose,
        double xSizeMm,
        double ySizeMm,
        double zSizeMm,
        bool relativeToRobot = false,
        bool useRobotOrigin = true)
    {
        ArgumentNullException.ThrowIfNull(pose);

        Pose target = pose;
        if (useRobotOrigin)
        {
            target.OriginId = _robot.Pose?.OriginId ??
                throw new InvalidOperationException("The robot has no pose yet.");
        }

        if (relativeToRobot)
        {
            Pose robotPose = _robot.Pose ??
                throw new InvalidOperationException("The robot has no pose yet.");
            target = robotPose.DefinePoseRelativeThis(target);
        }

        Task<MessageEngineToGame> response = WaitForMessageAsync(
            MessageEngineToGame.Tag.CreatedFixedCustomObject,
            TimeSpan.FromSeconds(5));
        await _connection.SendAsync(new MessageGameToEngine
        {
            CreateFixedCustomObject = new CreateFixedCustomObject
            {
                pose = target.EncodePose(),
                xSize_mm = (float)xSizeMm,
                ySize_mm = (float)ySizeMm,
                zSize_mm = (float)zSizeMm,
            },
        }).ConfigureAwait(false);

        CreatedFixedCustomObject created = (await response.ConfigureAwait(false)).CreatedFixedCustomObject;
        var fixedObject = new FixedCustomObject(target, xSizeMm, ySizeMm, zSizeMm, (int)created.objectID);
        _fixedObjects[(int)created.objectID] = fixedObject;
        return fixedObject;
    }

    private async Task<CustomObject?> FinishCustomObjectDefinitionAsync(
        CustomObject archetype,
        Task<MessageEngineToGame> response)
    {
        DefinedCustomObject defined = (await response.ConfigureAwait(false)).DefinedCustomObject;
        if (!defined.success)
        {
            _logger.LogError("Failed to define custom object {ObjectType}.", archetype.ObjectType.Name);
            return null;
        }

        _customObjectArchetypes[archetype.ObjectType.Id] = archetype;
        _logger.LogInformation("Defined custom object {ObjectType}.", archetype.ObjectType.Name);
        return archetype;
    }

    private void RemoveCustomMarkerObjectInstances()
    {
        foreach (int objectId in _objects
                     .Where(pair => pair.Value is CustomObject)
                     .Select(pair => pair.Key)
                     .ToArray())
        {
            _objects.Remove(objectId);
        }
    }
}
