// Copyright (c) 2017 Anki, Inc.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

// Port of examples/apps/quick_tap.py.

using Anki.Cozmo.Sdk;

namespace Cozmo.Samples.Examples.Apps;

/// <summary>Play Quick Tap: tap your cube when the colors match, but never on red.</summary>
public static class QuickTap
{
    /// <summary>Runs the example.</summary>
    public static Task RunAsync(ExampleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return ExampleRunner.RunAsync(
            async robot =>
            {
                var game = new QuickTapGame(robot);
                await game.RunAsync(context.CancellationToken);
            },
            cancellationToken: context.CancellationToken);
    }
}

internal static class QuickTapConstants
{
    public const string ChooseCubesState = "choose_cubes";
    public const string GameState = "game";

    public const string MakeBuzzersDifferentColors = "MAKE_BUZZERS_DIFFERENT_COLORS";
    public const string MakeBuzzersRed = "MAKE_BUZZERS_RED";
    public const string MakeBuzzersSameColors = "MAKE_BUZZERS_SAME_COLORS";

    public const double RateMakeBuzzersDifferentColors = 0.17;
    public const double RateMakeBuzzersRed = 0.33;
    public const double RateCozmoAccuracy = 0.9;
    public const int ScoreToWin = 5;
}

/// <summary>A light cube plus the Quick Tap light effects.</summary>
internal sealed class QuickTapCube
{
    private static readonly Light[] RainbowColors = [Lights.Blue, Lights.Red, Lights.Green, Lights.White];
    private readonly List<Light> _rainbow = [.. RainbowColors];
    private LightChaser? _chaser;

    public QuickTapCube(LightCube cube)
    {
        Cube = cube;
    }

    public LightCube Cube { get; }

    public int? ObjectId => Cube.ObjectId;

    public void StartLightChaser(TimeSpan pauseTime)
    {
        if (_chaser is { IsRunning: true })
        {
            throw new InvalidOperationException("Light chaser already running.");
        }

        _chaser = new LightChaser(
            Cube,
            _ =>
            {
                Light[] frame = _rainbow.ToArray();
                Light first = _rainbow[0];
                _rainbow.RemoveAt(0);
                _rainbow.Add(first);
                return frame;
            });
        _chaser.Start(pauseTime);
    }

    public async Task StopLightChaserAsync()
    {
        if (_chaser is not null)
        {
            await _chaser.StopAsync();
            _chaser = null;
        }

        await Cube.SetLightsOffAsync();
    }

    public Task SetLightsAsync(Light light) => Cube.SetLightsAsync(light);

    public Task SetLightsOffAsync() => Cube.SetLightsOffAsync();

    public Task SetLightCornersAsync(Light? light1, Light? light2, Light? light3, Light? light4)
        => Cube.SetLightCornersAsync(light1, light2, light3, light4);

    public async Task CountdownAsync(CancellationToken cancellationToken)
    {
        for (int index = 0; index < 5; index++)
        {
            Light[] corners = new Light[4];
            for (int corner = 0; corner < 4; corner++)
            {
                corners[corner] = corner < 4 - index ? Lights.White : Lights.Off;
            }

            await Cube.SetLightCornersAsync(corners[0], corners[1], corners[2], corners[3]);
            await Task.Delay(TimeSpan.FromSeconds(0.5), cancellationToken);
        }
    }

    public async Task FlairCorrectTapAsync(CancellationToken cancellationToken)
    {
        StartLightChaser(TimeSpan.FromSeconds(0.1));
        await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
        await StopLightChaserAsync();
    }

    public async Task FlairIncorrectTapAsync(CancellationToken cancellationToken)
    {
        for (int index = 0; index < 4; index++)
        {
            await Cube.SetLightsAsync(Lights.Red);
            await Task.Delay(TimeSpan.FromSeconds(0.2), cancellationToken);
            await Cube.SetLightsOffAsync();
            await Task.Delay(TimeSpan.FromSeconds(0.2), cancellationToken);
        }
    }
}

/// <summary>Player state for Quick Tap.</summary>
internal class QuickTapPlayer
{
    public QuickTapCube? Cube { get; set; }

    public int Score { get; set; }

    public bool HasTapped { get; private set; }

    public string Name { get; protected set; } = "Player";

    public double ElapsedTapTime { get; private set; } = double.MaxValue;

    public bool WonRound { get; private set; }

    public void WinsRound()
    {
        Console.WriteLine($"****{Name} wins the round****");
        Score++;
        WonRound = true;
    }

    public void Reset()
    {
        ElapsedTapTime = double.MaxValue;
        HasTapped = false;
        WonRound = false;
    }

    public void RegisterTap(DateTimeOffset roundStartTime)
    {
        ElapsedTapTime = (DateTimeOffset.UtcNow - roundStartTime).TotalSeconds;
        HasTapped = true;
    }
}

/// <summary>Cozmo's side of Quick Tap.</summary>
internal sealed class CozmoQuickTapPlayer : QuickTapPlayer
{
    private readonly Robot _robot;

    public CozmoQuickTapPlayer(Robot robot)
    {
        _robot = robot;
        Name = "Cozmo";
    }

    public async Task SelectCubeAsync()
    {
        LightCube cube = await _robot.World.WaitForObservedLightCubeAsync();
        Cube = new QuickTapCube(cube);
        await Cube.SetLightsAsync(Lights.White);
        await Task.Delay(TimeSpan.FromSeconds(2));
        Cube.StartLightChaser(TimeSpan.FromSeconds(0.5));
        SetLiftHeight lift = await _robot.SetLiftHeightAsync(1.0);
        await lift.WaitForCompletedAsync();
        GoToObject go = await _robot.GoToObjectAsync(cube, Distance.FromMillimeters(40));
        await go.WaitForCompletedAsync();
        AnimationTrigger tap = await _robot.PlayAnimationTriggerAsync(Triggers.OnSpeedtapTap);
        await tap.WaitForCompletedAsync();
        await Cube.StopLightChaserAsync();
        await Cube.SetLightsAsync(Lights.Green);
    }

    public async Task DetermineMoveAsync(string buzzerDisplayType, CancellationToken cancellationToken)
    {
        await HesitateAsync(cancellationToken);
        double probabilityCorrect = Random.Shared.NextDouble();
        if (probabilityCorrect < QuickTapConstants.RateCozmoAccuracy)
        {
            if (buzzerDisplayType == QuickTapConstants.MakeBuzzersSameColors)
            {
                await TapAsync();
            }
            else
            {
                await FailToTapAsync(cancellationToken);
            }
        }
        else if (buzzerDisplayType == QuickTapConstants.MakeBuzzersRed ||
            buzzerDisplayType == QuickTapConstants.MakeBuzzersDifferentColors)
        {
            await TapAsync();
        }
        else
        {
            await FailToTapAsync(cancellationToken);
        }
    }

    private static Task HesitateAsync(CancellationToken cancellationToken)
        => Task.Delay(TimeSpan.FromSeconds(Random.Shared.NextDouble() * 0.5), cancellationToken);

    private async Task TapAsync()
    {
        AnimationTrigger tap = await _robot.PlayAnimationTriggerAsync(Triggers.OnSpeedtapTap);
        await tap.WaitForCompletedAsync();
    }

    private async Task FailToTapAsync(CancellationToken cancellationToken)
    {
        if (Random.Shared.NextDouble() < 0.5)
        {
            AnimationTrigger fakeout = await _robot.PlayAnimationTriggerAsync(
                Triggers.OnSpeedtapFakeout);
            await fakeout.WaitForCompletedAsync();
        }
        else
        {
            AnimationTrigger idle = await _robot.PlayAnimationTriggerAsync(Triggers.OnSpeedtapIdle);
            await idle.WaitForCompletedAsync();
        }

        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();
    }
}

/// <summary>The Quick Tap game state machine.</summary>
internal sealed class QuickTapGame
{
    private readonly Robot _robot;
    private readonly QuickTapPlayer _player = new();
    private readonly CozmoQuickTapPlayer _cozmoPlayer;
    private readonly List<QuickTapCube> _cubes = [];
    private QuickTapCube? _countdownCube;
    private string? _buzzerDisplayType;
    private DateTimeOffset _roundStartTime;
    private QuickTapPlayer? _quickTapPlayer1;
    private QuickTapPlayer? _quickTapPlayer2;
    private bool _roundOver;
    private string _quickTapState = QuickTapConstants.ChooseCubesState;

    public QuickTapGame(Robot robot)
    {
        _robot = robot;
        _cozmoPlayer = new CozmoQuickTapPlayer(robot);
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        using Handler animationHandler = _robot.AddAsyncHandler<EvtAnimationCompleted>(OnAnimationCompletedAsync);
        using Handler tapHandler = _robot.AddAsyncHandler<EvtObjectTapped>(OnCubeTapAsync);

        await MoveCozmoToReadyPoseAsync();
        PrintStartingInstructions();
        if (!CubesConnected())
        {
            Console.WriteLine(
                "Cubes did not connect successfully - check that they are nearby. " +
                "You may need to replace the batteries.");
            return;
        }

        await AssignCubesAsync(cancellationToken);
        _quickTapState = QuickTapConstants.GameState;
        while (Math.Max(_player.Score, _cozmoPlayer.Score) < QuickTapConstants.ScoreToWin)
        {
            await GameRoundAsync(cancellationToken);
        }

        await ReportWinnerAsync(cancellationToken);
    }

    private async Task MoveCozmoToReadyPoseAsync()
    {
        await _robot.SetLiftHeightAsync(0, inParallel: true);
        await _robot.SetHeadAngleAsync(Util.Degrees(0), inParallel: true);
        await _robot.ActionDispatcher.WaitForAllActionsCompletedAsync();
    }

    private async Task GameRoundAsync(CancellationToken cancellationToken)
    {
        _roundOver = false;
        _player.Reset();
        _cozmoPlayer.Reset();
        SetLiftHeight lift = await _robot.SetLiftHeightAsync(1.0);
        await lift.WaitForCompletedAsync();
        await TurnOffBuzzerCubesAsync();

        if (_countdownCube is null)
        {
            throw new InvalidOperationException("No countdown cube was assigned.");
        }

        await _countdownCube.CountdownAsync(cancellationToken);
        await SetRoundLightsAsync(cancellationToken);
        _roundStartTime = DateTimeOffset.UtcNow;
        await _cozmoPlayer.DetermineMoveAsync(
            _buzzerDisplayType ?? QuickTapConstants.MakeBuzzersSameColors,
            cancellationToken);
        while (!_roundOver)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(10), cancellationToken);
        }

        await CozmoAnimReactionAsync();
    }

    private async Task SetRoundLightsAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(
            TimeSpan.FromSeconds(Random.Shared.NextDouble() * 2),
            cancellationToken);
        DetermineBuzzerDisplay();
        await SetBuzzerLightsAsync();
    }

    private void DetermineBuzzerDisplay()
    {
        if (Random.Shared.NextDouble() < QuickTapConstants.RateMakeBuzzersRed)
        {
            _buzzerDisplayType = QuickTapConstants.MakeBuzzersRed;
        }
        else if (Random.Shared.NextDouble() < QuickTapConstants.RateMakeBuzzersDifferentColors)
        {
            _buzzerDisplayType = QuickTapConstants.MakeBuzzersDifferentColors;
        }
        else
        {
            _buzzerDisplayType = QuickTapConstants.MakeBuzzersSameColors;
        }
    }

    private async Task OnCubeTapAsync(EvtObjectTapped evt)
    {
        if (evt.Obj.ObjectId is not { } objectId)
        {
            return;
        }

        if (_quickTapState == QuickTapConstants.ChooseCubesState)
        {
            if (_cozmoPlayer.Cube is null)
            {
                return;
            }

            if (objectId != _cozmoPlayer.Cube.ObjectId)
            {
                _player.Cube = _cubes.First(cube => cube.ObjectId == objectId);
                await _player.Cube.SetLightsOffAsync();
                return;
            }
        }
        else if (_quickTapState == QuickTapConstants.GameState)
        {
            await TurnOffBuzzerCubesAsync();
            if (objectId == _player.Cube?.ObjectId)
            {
                _player.RegisterTap(_roundStartTime);
            }
            else if (objectId == _cozmoPlayer.Cube?.ObjectId)
            {
                _cozmoPlayer.RegisterTap(_roundStartTime);
            }
        }
    }

    private async Task OnAnimationCompletedAsync(EvtAnimationCompleted evt)
    {
        if (_quickTapState == QuickTapConstants.GameState &&
            evt.AnimationName is "OnSpeedtapTap" or "OnSpeedtapFakeout" or "OnSpeedtapIdle")
        {
            await DetermineResultOfRoundAsync();
            _roundOver = true;
        }
    }

    private async Task DetermineResultOfRoundAsync()
    {
        DetermineFirstTapper();
        if (_quickTapPlayer1?.Cube is { } firstCube)
        {
            if (_buzzerDisplayType == QuickTapConstants.MakeBuzzersSameColors)
            {
                _quickTapPlayer1.WinsRound();
                await firstCube.FlairCorrectTapAsync(CancellationToken.None);
            }
            else if (_buzzerDisplayType is QuickTapConstants.MakeBuzzersDifferentColors or
                QuickTapConstants.MakeBuzzersRed)
            {
                _quickTapPlayer2?.WinsRound();
                await firstCube.FlairIncorrectTapAsync(CancellationToken.None);
            }

            ReportScores();
        }
    }

    private void DetermineFirstTapper()
    {
        if (_player.HasTapped || _cozmoPlayer.HasTapped)
        {
            if (_cozmoPlayer.ElapsedTapTime < _player.ElapsedTapTime)
            {
                _quickTapPlayer1 = _cozmoPlayer;
                _quickTapPlayer2 = _player;
            }
            else
            {
                _quickTapPlayer1 = _player;
                _quickTapPlayer2 = _cozmoPlayer;
            }
        }
        else
        {
            _quickTapPlayer1 = null;
        }
    }

    private async Task CozmoAnimReactionAsync()
    {
        AnimationTrigger reaction = await _robot.PlayAnimationTriggerAsync(
            _cozmoPlayer.WonRound
                ? Triggers.OnSpeedtapHandCozmoWin
                : Triggers.OnSpeedtapHandPlayerWin);
        await reaction.WaitForCompletedAsync();
    }

    private async Task AssignCubesAsync(CancellationToken cancellationToken)
    {
        await _cozmoPlayer.SelectCubeAsync();
        BlinkAvailableCubes();
        await _robot.WaitFor<EvtObjectTapped>(timeout: cancellationToken.IsCancellationRequested
            ? TimeSpan.Zero
            : Timeout.InfiniteTimeSpan);
        if (_player.Cube is not null)
        {
            await _player.Cube.StopLightChaserAsync();
        }

        await AssignCountdownCubeAsync();
        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();
    }

    private void BlinkAvailableCubes()
    {
        foreach (QuickTapCube cube in _cubes)
        {
            if (cube.ObjectId != _cozmoPlayer.Cube?.ObjectId)
            {
                cube.StartLightChaser(TimeSpan.FromSeconds(0.5));
            }
        }
    }

    private async Task AssignCountdownCubeAsync()
    {
        foreach (QuickTapCube cube in _cubes)
        {
            if (cube.ObjectId != _cozmoPlayer.Cube?.ObjectId &&
                cube.ObjectId != _player.Cube?.ObjectId)
            {
                _countdownCube = cube;
                await _countdownCube.StopLightChaserAsync();
            }
        }
    }

    private async Task SetBuzzerLightsAsync()
    {
        switch (_buzzerDisplayType)
        {
            case QuickTapConstants.MakeBuzzersRed:
                await TurnOnBuzzerCubesRedAsync();
                break;
            case QuickTapConstants.MakeBuzzersDifferentColors:
                await TurnOnBuzzerCubesDifferentAsync();
                break;
            case QuickTapConstants.MakeBuzzersSameColors:
                await TurnOnBuzzerCubesSameAsync();
                break;
        }
    }

    private async Task TurnOnBuzzerCubesSameAsync()
    {
        Light[] colors = GenerateRandomBuzzerColors();
        await _player.Cube!.SetLightCornersAsync(colors[0], colors[1], colors[2], colors[3]);
        await _cozmoPlayer.Cube!.SetLightCornersAsync(colors[0], colors[1], colors[2], colors[3]);
    }

    private async Task TurnOnBuzzerCubesDifferentAsync()
    {
        Light[] playerColors = GenerateRandomBuzzerColors();
        Light[] cozmoColors = GenerateRandomBuzzerColors();
        while (playerColors.SequenceEqual(cozmoColors))
        {
            cozmoColors = GenerateRandomBuzzerColors();
        }

        await _player.Cube!.SetLightCornersAsync(
            playerColors[0], playerColors[1], playerColors[2], playerColors[3]);
        await _cozmoPlayer.Cube!.SetLightCornersAsync(
            cozmoColors[0], cozmoColors[1], cozmoColors[2], cozmoColors[3]);
    }

    private async Task TurnOnBuzzerCubesRedAsync()
    {
        await _player.Cube!.SetLightsAsync(Lights.Red);
        await _cozmoPlayer.Cube!.SetLightsAsync(Lights.Red);
    }

    private static Light[] GenerateRandomBuzzerColors()
    {
        Light[] palette = [Lights.Blue, Lights.Green, LightFromRgb(255, 0, 255), Lights.Red,
            Lights.White, LightFromRgb(255, 255, 0)];
        int x = Random.Shared.Next(palette.Length);
        int y = Random.Shared.Next(palette.Length);
        while (y == x)
        {
            y = Random.Shared.Next(palette.Length);
        }

        return [palette[x], palette[y], palette[x], palette[y]];
    }

    private static Light LightFromRgb(byte red, byte green, byte blue)
    {
        var color = Color.FromRgb(red, green, blue);
        return new Light(color, color);
    }

    private async Task TurnOffBuzzerCubesAsync()
    {
        if (_player.Cube is not null)
        {
            await _player.Cube.SetLightsOffAsync();
        }

        if (_cozmoPlayer.Cube is not null)
        {
            await _cozmoPlayer.Cube.SetLightsOffAsync();
        }
    }

    private bool CubesConnected()
    {
        IReadOnlyList<LightCube?> cubes = CubeLookup.GetCubes(_robot.World);
        if (cubes.Any(cube => cube is null))
        {
            return false;
        }

        _cubes.Clear();
        foreach (LightCube? cube in cubes)
        {
            if (cube is not null)
            {
                _cubes.Add(new QuickTapCube(cube));
            }
        }

        return true;
    }

    private static void PrintStartingInstructions()
    {
        Console.WriteLine("~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~");
        Console.WriteLine("Welcome to Quick Tap!");
        Console.WriteLine("Put 1 cube in front of Cozmo. It will turn white when he can see it.");
        Console.WriteLine("Cozmo will tap the cube to select it as his buzzer.");
        Console.WriteLine("After Cozmo, tap a cube to select your buzzer.");
        Console.WriteLine("The last cube will display a countdown with its lights start each round.");
        Console.WriteLine("When the buzzers light up, tap if the colors match, but never tap on red!");
        Console.WriteLine("~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~");
    }

    private void ReportScores()
    {
        Console.WriteLine("---------------------------------------------------");
        Console.WriteLine($"Player score: {_player.Score}");
        Console.WriteLine($"Cozmo score: {_cozmoPlayer.Score}");
        Console.WriteLine("---------------------------------------------------");
    }

    private async Task ReportWinnerAsync(CancellationToken cancellationToken)
    {
        Console.WriteLine($"You won {_player.Score} round{(_player.Score == 1 ? string.Empty : "s")}");
        Console.WriteLine(
            $"Cozmo won {_cozmoPlayer.Score} round{(_cozmoPlayer.Score == 1 ? string.Empty : "s")}");
        if (_cozmoPlayer.Score > _player.Score)
        {
            Console.WriteLine("~COZMO WINS QUICK TAP~");
            AnimationTrigger win = await _robot.PlayAnimationTriggerAsync(
                Triggers.OnSpeedtapGameCozmoWinHighIntensity);
            await win.WaitForCompletedAsync();
        }
        else
        {
            Console.WriteLine("~PLAYER WINS QUICK TAP~");
            AnimationTrigger win = await _robot.PlayAnimationTriggerAsync(
                Triggers.OnSpeedtapGamePlayerWinHighIntensity);
            await win.WaitForCompletedAsync();
        }

        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();
    }
}
