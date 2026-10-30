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
using PetTypeValues = Anki.Cozmo.Sdk.PetType;

namespace Anki.Cozmo.Sdk;

/// <summary>A single pet that Cozmo has detected.</summary>
/// <remarks>
/// <para>
/// The engine can identify an observed pet as a cat or a dog, but cannot recognize a specific
/// pet. If a pet goes off-screen, it receives a new id and <see cref="Pet"/> instance when it
/// returns.
/// </para>
/// <para>
/// The world routes observed-pet messages to this instance. See <see cref="HandleObservedPet"/>.
/// </para>
/// </remarks>
public sealed class Pet : ObservableElement
{
    /// <summary>
    /// Length of time to go without receiving an observation before the pet is considered
    /// invisible.
    /// </summary>
    public static readonly TimeSpan PetVisibilityTimeout = ObservableElement.ObjectVisibilityTimeout;

    private int? _petId;

    /// <summary>Creates a pet.</summary>
    /// <param name="connection">The connection used to send commands for this pet.</param>
    /// <param name="world">The world that owns this pet; may be <see langword="null"/> in isolation tests.</param>
    /// <param name="petId">The engine pet id, or <see langword="null"/> before allocation.</param>
    /// <param name="timeProvider">Clock and timer source; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="dispatchParent">Optional dispatcher that receives events relayed from this pet.</param>
    /// <param name="logger">Optional logger; defaults to a no-op logger.</param>
    /// <exception cref="ArgumentNullException"><paramref name="connection"/> is <see langword="null"/>.</exception>
    public Pet(
        CozmoConnection connection,
        IWorldContext? world = null,
        int? petId = null,
        TimeProvider? timeProvider = null,
        Dispatcher? dispatchParent = null,
        ILogger? logger = null)
        : base(connection, world, timeProvider, dispatchParent, logger)
    {
        _petId = petId;
    }

    /// <summary>The internal id assigned to the pet.</summary>
    /// <remarks>
    /// <para>The value is <see langword="null"/> until the world allocates the pet.</para>
    /// <para>If the engine changes the id, the new value is applied and a warning is logged.</para>
    /// </remarks>
    public int? PetId
    {
        get => _petId;
        set
        {
            if (_petId is not null)
            {
                Logger.LogWarning(
                    "Changing pet_id for {PetTypeName} from {OldId} to {NewId}.",
                    GetType().Name,
                    _petId.Value,
                    value);
            }
            else
            {
                Logger.LogDebug("Setting pet_id for {PetTypeName} to {NewId}.", GetType().Name, value);
            }

            _petId = value;
        }
    }

    /// <summary>The type of pet Cozmo thinks this is.</summary>
    /// <remarks>
    /// <see langword="null"/> until the pet has been observed, otherwise one of the
    /// <see cref="Anki.Cozmo.Sdk.PetType"/> constants.
    /// </remarks>
    public string? PetType { get; private set; }

    /// <inheritdoc />
    public override TimeSpan VisibilityTimeout => PetVisibilityTimeout;

    /// <summary>Applies a robot-observed-pet message.</summary>
    /// <param name="message">The CLAD observation message routed by the world.</param>
    /// <returns>A task that completes after the observed and, on first sight, appeared events are dispatched.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="message"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// The observed-pet message carries no pose, so <see cref="ObservableElement.Pose"/> stays
    /// <see langword="null"/>.
    /// </remarks>
    public async Task HandleObservedPet(RobotObservedPet message)
    {
        ArgumentNullException.ThrowIfNull(message);

        var changedFields = new HashSet<string>(StringComparer.Ordinal);
        string petType = PetTypeValues.FromClad((Anki.Vision.PetType)message.petType);

        if (!string.Equals(petType, PetType, StringComparison.Ordinal))
        {
            PetType = petType;
            changedFields.Add("pet_type");
        }

        ImageBox imageBox = ImageBox.FromCladRect(message.img_rect);
        await OnObservedAsync(imageBox, message.timestamp, changedFields).ConfigureAwait(false);
    }

    /// <inheritdoc />
    protected override Task DispatchObservedEventAsync(
        IReadOnlySet<string> changedFields,
        ImageBox imageBox) =>
        DispatchEventAsync(new EvtPetObserved(this, changedFields, imageBox));

    /// <inheritdoc />
    protected override Task DispatchAppearedEventAsync(
        IReadOnlySet<string> changedFields,
        ImageBox imageBox) =>
        DispatchEventAsync(new EvtPetAppeared(this, changedFields, imageBox));

    /// <inheritdoc />
    protected override Task DispatchDisappearedEventAsync() =>
        DispatchEventAsync(new EvtPetDisappeared(this));
}

/// <summary>Raised whenever a pet is visually identified by the robot.</summary>
/// <param name="Pet">The pet that was observed.</param>
/// <param name="Updated">
/// Names of the pet fields that were updated. Every observation includes <c>last_observed_time</c>,
/// <c>last_observed_robot_timestamp</c>, <c>last_event_time</c> and
/// <c>last_observed_image_box</c>, plus <c>pet_type</c> when it changed.
/// </param>
/// <param name="ImageBox">Where the pet was last visible within Cozmo's camera view.</param>
/// <remarks>
/// A stream of these events is produced while a pet is visible to the robot. Register for
/// <see cref="EvtPetAppeared"/> instead when only the first event of a visible period matters.
/// </remarks>
public sealed record EvtPetObserved(
    Pet Pet,
    IReadOnlySet<string> Updated,
    ImageBox ImageBox) : CozmoEvent;

/// <summary>Raised when a pet first becomes visible to the robot.</summary>
/// <param name="Pet">The pet that appeared.</param>
/// <param name="Updated">
/// The names of the pet fields that were updated, using the same set supplied to the
/// observation that made the pet visible.
/// </param>
/// <param name="ImageBox">Where the pet was first visible within Cozmo's camera view.</param>
/// <remarks>
/// If the pet disappears for longer than <see cref="Pet.PetVisibilityTimeout"/> and is then
/// observed again, <see cref="EvtPetDisappeared"/> is raised first, followed by another
/// <see cref="EvtPetAppeared"/>.
/// </remarks>
public sealed record EvtPetAppeared(
    Pet Pet,
    IReadOnlySet<string> Updated,
    ImageBox ImageBox) : CozmoEvent;

/// <summary>Raised whenever a pet that was previously being observed is no longer visible.</summary>
/// <param name="Pet">The pet that is no longer being observed.</param>
public sealed record EvtPetDisappeared(Pet Pet) : CozmoEvent;
