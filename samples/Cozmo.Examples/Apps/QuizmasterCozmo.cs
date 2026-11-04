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

// Port of examples/apps/quizmaster_cozmo.py.

using System.Text.Json;
using Anki.Cozmo;
using Anki.Cozmo.Sdk;
using SdkAnimationTrigger = Anki.Cozmo.Sdk.AnimationTrigger;

namespace Cozmo.Samples.Examples.Apps;

/// <summary>Run a three-player quiz using the light cubes as buzzers.</summary>
public static class QuizmasterCozmo
{
    /// <summary>Runs the example.</summary>
    public static Task RunAsync(ExampleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return ExampleRunner.RunAsync(
            async robot =>
            {
                var quizMaster = new CozmoQuizMaster(robot);
                await quizMaster.RunAsync(context.CancellationToken);
            },
            cancellationToken: context.CancellationToken);
    }
}

/// <summary>One multiple-choice question.</summary>
internal sealed class QuizQuestion
{
    private int _answerIndex;

    public QuizQuestion(string question, IReadOnlyList<string> answerOptions)
    {
        if (answerOptions.Count != 4)
        {
            throw new ArgumentException(
                $"Expected 4 answer_options, got {answerOptions.Count}",
                nameof(answerOptions));
        }

        Question = question;
        AnswerOptions = answerOptions.ToList();
        ShuffleAnswerOptions();
    }

    public string Question { get; }

    public List<string> AnswerOptions { get; }

    public int AnswerNumber => _answerIndex + 1;

    public string AnswerString => AnswerOptions[_answerIndex];

    public void ShuffleAnswerOptions()
    {
        string answer = AnswerOptions[_answerIndex];
        AnswerOptions.RemoveAt(_answerIndex);
        for (int index = AnswerOptions.Count - 1; index > 0; index--)
        {
            int swap = Random.Shared.Next(index + 1);
            (AnswerOptions[index], AnswerOptions[swap]) = (AnswerOptions[swap], AnswerOptions[index]);
        }

        _answerIndex = Random.Shared.Next(AnswerOptions.Count + 1);
        AnswerOptions.Insert(_answerIndex, answer);
    }
}

/// <summary>One quiz player and the cube used as their buzzer.</summary>
internal sealed class CozmoQuizPlayer
{
    private int _answerIndex;

    public CozmoQuizPlayer(
        Robot robot,
        LightCube? cube,
        int index,
        Light color,
        string name)
    {
        Robot = robot;
        Cube = cube;
        Index = index;
        Color = color;
        Name = name;
    }

    public Robot Robot { get; }

    public LightCube? Cube { get; }

    public int Index { get; }

    public Light Color { get; }

    public string Name { get; }

    public int Score { get; set; }

    public bool HasBuzzedIn { get; private set; }

    public int AnswerNumber => _answerIndex + 1;

    public int? ObjectId => Cube?.ObjectId;

    public bool VerifySetup()
    {
        if (Cube is not null)
        {
            return true;
        }

        Console.WriteLine(
            $"Cozmo is not connected to a cube {Index + 1} - check the battery.");
        return false;
    }

    public async Task ResetForQuestionAsync()
    {
        await TurnLightOnAsync();
        HasBuzzedIn = false;
    }

    public Task TurnLightOffAsync() => Cube?.SetLightsOffAsync() ?? Task.CompletedTask;

    public Task TurnLightOnAsync() => Cube?.SetLightsAsync(Color) ?? Task.CompletedTask;

    public async Task SetAnswerLightAsync()
    {
        if (Cube is null)
        {
            return;
        }

        Light?[] corners = [Lights.Off, Lights.Off, Lights.Off, Lights.Off];
        for (int index = 0; index < AnswerNumber; index++)
        {
            corners[3 - index] = Color;
        }

        await Cube.SetLightCornersAsync(corners[0], corners[1], corners[2], corners[3]);
    }

    public async Task OnBuzzedInAsync()
    {
        await TurnLightOnAsync();
        HasBuzzedIn = true;
    }

    public async Task StartAnsweringAsync()
    {
        _answerIndex = 0;
        await SetAnswerLightAsync();
    }

    public async Task CycleAnswerAsync()
    {
        _answerIndex++;
        if (_answerIndex > 3)
        {
            _answerIndex = 0;
        }

        await SetAnswerLightAsync();
    }
}

/// <summary>The quiz-master game state.</summary>
internal sealed class CozmoQuizMaster
{
    private readonly Robot _robot;
    private readonly List<CozmoQuizPlayer> _players = [];
    private readonly List<QuizQuestion> _questions = [];
    private CozmoQuizPlayer? _answeringPlayer;
    private bool _buzzingInAccepted;
    private bool _answersAccepted;

    public CozmoQuizMaster(Robot robot)
    {
        _robot = robot;
        Light[] colors = [Lights.Red, Lights.Green, Lights.Blue];
        string[] names = ["Red", "Green", "Blue"];
        IReadOnlyList<LightCube?> cubes = CubeLookup.GetCubes(robot.World);

        for (int index = 0; index < cubes.Count; index++)
        {
            _players.Add(new CozmoQuizPlayer(robot, cubes[index], index, colors[index], names[index]));
        }

        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(ExamplePaths.QuizQuestions));
        foreach (JsonElement element in document.RootElement.EnumerateArray())
        {
            string question = element.GetProperty("question").GetString() ??
                throw new InvalidOperationException("A quiz question has no text.");
            string[] options = element.GetProperty("answer_options")
                .EnumerateArray()
                .Select(option => option.GetString() ?? string.Empty)
                .ToArray();
            _questions.Add(new QuizQuestion(question, options));
        }
    }

    public bool VerifySetup()
    {
        int validPlayers = _players.Count(player => player.VerifySetup());
        return validPlayers > 0;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        if (!VerifySetup())
        {
            return;
        }

        using Handler tapped = _robot.AddAsyncHandler<EvtObjectTapped>(OnCubeTappedAsync);

        while (!cancellationToken.IsCancellationRequested)
        {
            QuizQuestion? question = GetNextQuestion();
            if (question is null)
            {
                break;
            }

            await AskQuestionAsync(question, cancellationToken);
        }

        Console.WriteLine("Quiz is complete!");
        await ReportLeaderAsync(isFinalScore: true);
        SdkAnimationTrigger success = await _robot.PlayAnimationTriggerAsync(Triggers.BuildPyramidSuccess);
        await success.WaitForCompletedAsync();
        SayText goodbye = await _robot.SayTextAsync("Game Over - Bye!");
        await goodbye.WaitForCompletedAsync();
    }

    private async Task OnCubeTappedAsync(EvtObjectTapped evt)
    {
        CozmoQuizPlayer? player = GetPlayerForObjectId(evt.Obj.ObjectId);
        if (_buzzingInAccepted && _answeringPlayer is null)
        {
            if (player is not null && !player.HasBuzzedIn)
            {
                _answeringPlayer = player;
                await player.OnBuzzedInAsync();
            }
        }
        else if (_answersAccepted && player == _answeringPlayer)
        {
            if (player is not null)
            {
                await player.CycleAnswerAsync();
            }
        }
    }

    private CozmoQuizPlayer? GetPlayerForObjectId(int? objectId)
    {
        foreach (CozmoQuizPlayer player in _players)
        {
            if (player.ObjectId == objectId)
            {
                return player;
            }
        }

        Console.WriteLine($"No player for object_id {objectId}");
        return null;
    }

    private async Task TurnPlayerLightsOnAsync()
    {
        foreach (CozmoQuizPlayer player in _players)
        {
            await player.TurnLightOnAsync();
        }
    }

    private async Task TurnPlayerLightsOffAsync()
    {
        foreach (CozmoQuizPlayer player in _players)
        {
            await player.TurnLightOffAsync();
        }
    }

    private QuizQuestion? GetNextQuestion()
    {
        if (_questions.Count == 0)
        {
            return null;
        }

        int index = Random.Shared.Next(_questions.Count);
        QuizQuestion question = _questions[index];
        _questions.RemoveAt(index);
        return question;
    }

    private Task<SayText> SayTextAsync(string text)
    {
        Console.WriteLine(text);
        return _robot.SayTextAsync(text);
    }

    private static string CreateAnswerOptionsString(IReadOnlyList<string> answerOptions)
    {
        string text = "Is it ";
        for (int index = 0; index < answerOptions.Count; index++)
        {
            string conjunction = string.Empty;
            if (index > 0)
            {
                bool isLastOption = index == answerOptions.Count - 1;
                conjunction = isLastOption ? " or " : ", ";
            }

            text += $"{conjunction}{index + 1}: {answerOptions[index]}";
        }

        return text;
    }

    private static async Task<int> WaitForAnswerAsync(
        CozmoQuizPlayer player,
        CancellationToken cancellationToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(2.0), cancellationToken);
        return player.AnswerNumber;
    }

    private List<CozmoQuizPlayer> GetWinningPlayers()
    {
        var winningPlayers = new List<CozmoQuizPlayer>();
        foreach (CozmoQuizPlayer player in _players)
        {
            if (winningPlayers.Count == 0 || player.Score > winningPlayers[0].Score)
            {
                winningPlayers = [player];
            }
            else if (player.Score == winningPlayers[0].Score)
            {
                winningPlayers.Add(player);
            }
        }

        return winningPlayers;
    }

    private async Task ReportLeaderAsync(bool isFinalScore)
    {
        List<CozmoQuizPlayer> winningPlayers = GetWinningPlayers();
        int winningScore = winningPlayers[0].Score;
        string pointsString = winningScore != 1 ? "points" : "point";
        string winningScoreString = $"{winningScore} {pointsString}";

        SayText action;
        if (winningPlayers.Count == _players.Count)
        {
            action = isFinalScore
                ? await SayTextAsync($"It ends as a draw with everyone at {winningScoreString}")
                : await SayTextAsync($"It's all tied at {winningScoreString}");
        }
        else
        {
            string winnerNames = winningPlayers[0].Name;
            for (int index = 1; index < winningPlayers.Count; index++)
            {
                bool isLastPlayer = index == winningPlayers.Count - 1;
                string conjunction = isLastPlayer ? " and " : ", ";
                winnerNames += conjunction + winningPlayers[index].Name;
            }

            if (isFinalScore)
            {
                action = await SayTextAsync($"{winnerNames} won with {winningScoreString}");
            }
            else
            {
                string isOrAre = winningPlayers.Count == 1 ? "is" : "are";
                action = await SayTextAsync(
                    $"{winnerNames} {isOrAre} in the lead with {winningScoreString}");
            }
        }

        await action.WaitForCompletedAsync();
    }

    private async Task<CozmoQuizPlayer?> GetCorrectPlayerAsync(
        QuizQuestion question,
        CancellationToken cancellationToken)
    {
        SayText readOptionsAction = await SayTextAsync(
            CreateAnswerOptionsString(question.AnswerOptions));
        int answerCount = 0;

        for (int attempt = 0; attempt < _players.Count; attempt++)
        {
            if (answerCount > 0)
            {
                readOptionsAction = await SayTextAsync("Anyone else?");
            }

            while (!readOptionsAction.IsCompleted && _answeringPlayer is null)
            {
                await Task.Delay(TimeSpan.FromSeconds(0.1), cancellationToken);
            }

            if (_answeringPlayer is null)
            {
                await Task.Delay(TimeSpan.FromSeconds(1.0), cancellationToken);
            }

            CozmoQuizPlayer? player = _answeringPlayer;
            if (player is null)
            {
                await readOptionsAction.WaitForCompletedAsync();
                return null;
            }

            await Task.Delay(TimeSpan.FromSeconds(0.5), cancellationToken);

            await player.StartAnsweringAsync();
            _answersAccepted = true;

            await readOptionsAction.WaitForCompletedAsync();
            SayText prompt = await SayTextAsync($"{player.Name}?");
            await prompt.WaitForCompletedAsync();

            int playerAnswer = await WaitForAnswerAsync(player, cancellationToken);
            _answersAccepted = false;
            answerCount++;

            if (playerAnswer == question.AnswerNumber)
            {
                player.Score++;
                return player;
            }

            player.Score--;
            SdkAnimationTrigger failure = await _robot.PlayAnimationTriggerAsync(
                Triggers.KnockOverFailure);
            _answeringPlayer = null;
            await player.TurnLightOffAsync();
            await failure.WaitForCompletedAsync();
        }

        return null;
    }

    private async Task AskQuestionAsync(QuizQuestion question, CancellationToken cancellationToken)
    {
        foreach (CozmoQuizPlayer player in _players)
        {
            await player.ResetForQuestionAsync();
        }

        _answeringPlayer = null;
        _buzzingInAccepted = false;
        _answersAccepted = false;

        SayText questionAction = await SayTextAsync(question.Question);
        await questionAction.WaitForCompletedAsync();

        await TurnPlayerLightsOffAsync();
        _buzzingInAccepted = true;

        CozmoQuizPlayer? correctPlayer = await GetCorrectPlayerAsync(
            question,
            cancellationToken);
        if (correctPlayer is null)
        {
            SdkAnimationTrigger fail = await _robot.PlayAnimationTriggerAsync(
                Triggers.FailedToRightFromFace);
            await fail.WaitForCompletedAsync();
            SayText answer = await SayTextAsync(
                $"The answer was {question.AnswerNumber}: {question.AnswerString}");
            await answer.WaitForCompletedAsync();
        }
        else
        {
            SdkAnimationTrigger success = await _robot.PlayAnimationTriggerAsync(
                Triggers.ReactToBlockPickupSuccess);
            await success.WaitForCompletedAsync();
            SayText answer = await SayTextAsync(
                $"Correct it was {question.AnswerNumber}: {question.AnswerString}");
            await answer.WaitForCompletedAsync();
        }
    }
}
