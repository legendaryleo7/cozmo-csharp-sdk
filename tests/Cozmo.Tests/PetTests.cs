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
using Anki.Cozmo.ExternalInterface;
using Anki.Cozmo.Sdk;

namespace Cozmo.Tests;

/// <summary>Tests pet type conversion, observations and events.</summary>
public sealed class PetTests
{
    [Theory]
    [InlineData(0, PetType.Unknown)]
    [InlineData(1, PetType.Cat)]
    [InlineData(2, PetType.Dog)]
    [InlineData(3, PetType.Unknown)]
    [InlineData(200, PetType.Unknown)]
    public void PetTypeValuesConvertToPythonStrings(int cladValue, string expected)
    {
        Assert.Equal(expected, PetType.FromClad((Anki.Vision.PetType)cladValue));
    }

    [Fact]
    public void PetTypeConstantsMatchThePythonStrings()
    {
        Assert.Equal("unknown", PetType.Unknown);
        Assert.Equal("cat", PetType.Cat);
        Assert.Equal("dog", PetType.Dog);
    }

    [Fact]
    public async Task ObservedPetRaisesObservedThenAppearedAndUpdatesFields()
    {
        await using var transport = new FakeTransport();
        await using var connection = new CozmoConnection(transport);
        var clock = new ManualTimeProvider();
        var pet = new Pet(connection, petId: 5, timeProvider: clock);

        var observed = new List<EvtPetObserved>();
        var appeared = new List<EvtPetAppeared>();
        pet.AddHandler<EvtPetObserved>(observed.Add);
        pet.AddHandler<EvtPetAppeared>(appeared.Add);

        Assert.Equal(TimeSpan.FromSeconds(0.4), pet.VisibilityTimeout);
        Assert.Equal(TimeSpan.FromSeconds(0.4), Pet.PetVisibilityTimeout);
        Assert.False(pet.IsVisible);
        Assert.Null(pet.PetType);
        Assert.Null(pet.Pose);
        Assert.Equal(5, pet.PetId);

        await pet.HandleObservedPet(ObservedMessage(
            petId: 5,
            timestamp: 100,
            petType: (int)Anki.Vision.PetType.Cat));

        Assert.True(pet.IsVisible);
        Assert.Equal(PetType.Cat, pet.PetType);
        Assert.Single(observed);
        Assert.Single(appeared);
        Assert.Same(pet, observed[0].Pet);
        Assert.Same(pet, appeared[0].Pet);
        Assert.Equal(new ImageBox(1.0, 2.0, 3.0, 4.0), observed[0].ImageBox);
        Assert.Equal(new ImageBox(1.0, 2.0, 3.0, 4.0), appeared[0].ImageBox);
        Assert.Equal(100u, pet.LastObservedRobotTimestamp);
        Assert.Equal(new ImageBox(1.0, 2.0, 3.0, 4.0), pet.LastObservedImageBox);
        Assert.Equal(clock.GetUtcNow(), pet.LastObservedTime);
        Assert.Equal(clock.GetUtcNow(), pet.LastEventTime);
        Assert.Null(pet.Pose);

        var firstUpdated = new HashSet<string>(StringComparer.Ordinal)
        {
            "pet_type",
            "last_observed_time",
            "last_observed_robot_timestamp",
            "last_event_time",
            "last_observed_image_box",
        };
        Assert.True(observed[0].Updated.SetEquals(firstUpdated));
        Assert.True(appeared[0].Updated.SetEquals(firstUpdated));

        await pet.HandleObservedPet(ObservedMessage(
            petId: 5,
            timestamp: 200,
            petType: (int)Anki.Vision.PetType.Cat));

        Assert.Equal(2, observed.Count);
        Assert.Single(appeared);
        Assert.Equal(200u, pet.LastObservedRobotTimestamp);
        Assert.DoesNotContain("pet_type", observed[1].Updated);
        Assert.True(observed[1].Updated.SetEquals(new[]
        {
            "last_observed_time",
            "last_observed_robot_timestamp",
            "last_event_time",
            "last_observed_image_box",
        }));

        await pet.HandleObservedPet(ObservedMessage(
            petId: 5,
            timestamp: 300,
            petType: (int)Anki.Vision.PetType.Dog));

        Assert.Equal(PetType.Dog, pet.PetType);
        Assert.Equal(3, observed.Count);
        Assert.Single(appeared);
        Assert.Contains("pet_type", observed[2].Updated);
    }

    [Fact]
    public async Task PetVisibilityExpiresAfterTimeout()
    {
        await using var transport = new FakeTransport();
        await using var connection = new CozmoConnection(transport);
        var clock = new ManualTimeProvider();
        var pet = new Pet(connection, petId: 5, timeProvider: clock);

        var appeared = new List<EvtPetAppeared>();
        var disappeared = new List<EvtPetDisappeared>();
        pet.AddHandler<EvtPetAppeared>(appeared.Add);
        pet.AddHandler<EvtPetDisappeared>(disappeared.Add);

        await pet.HandleObservedPet(ObservedMessage(timestamp: 10));

        clock.Advance(TimeSpan.FromMilliseconds(399));
        Assert.True(pet.IsVisible);
        Assert.Empty(disappeared);

        clock.Advance(TimeSpan.FromMilliseconds(2));
        Assert.False(pet.IsVisible);
        Assert.Single(disappeared);
        Assert.Same(pet, disappeared[0].Pet);

        clock.Advance(TimeSpan.FromSeconds(5));
        Assert.Single(disappeared);

        await pet.HandleObservedPet(ObservedMessage(timestamp: 20));
        Assert.True(pet.IsVisible);
        Assert.Equal(2, appeared.Count);
    }

    [Fact]
    public async Task PetIdCanBeReassignedWithoutThrowing()
    {
        await using var transport = new FakeTransport();
        await using var connection = new CozmoConnection(transport);
        var pet = new Pet(connection);

        Assert.Null(pet.PetId);

        pet.PetId = 1;
        Assert.Equal(1, pet.PetId);

        // Python raises ValueError on reassignment (pets.py:157-162); the port logs a
        // warning instead so the world can repair its lookup table.
        pet.PetId = 2;
        Assert.Equal(2, pet.PetId);
    }

    private static RobotObservedPet ObservedMessage(
        int petId = 5,
        uint timestamp = 100,
        int petType = 1) => new()
        {
            petID = petId,
            timestamp = timestamp,
            numTimesObserved = 3,
            score = 0.75f,
            petType = (byte)petType,
            img_rect = new CladRect(1.0f, 2.0f, 3.0f, 4.0f),
        };
}
