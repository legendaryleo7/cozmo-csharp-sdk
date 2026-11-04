// Copyright (c) 2016 Anki, Inc.
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

// Port of examples/tutorials/02_cozmo_face/03_alarm_clock.py.

using System.Globalization;
using Anki.Cozmo.Sdk;

namespace Cozmo.Samples.Examples.Tutorials.CozmoFace;

/// <summary>Use Cozmo's face as a clock and optionally speak an alarm.</summary>
public static class AlarmClock
{
    private static readonly bool ShowAnalogClock = false;

    /// <summary>Runs the example until cancelled.</summary>
    public static Task RunAsync(ExampleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return ExampleRunner.RunAsync(
            async robot =>
            {
                TimeOnly? alarmTime = ExtractTimeFromArgs(context.Arguments);
                Console.WriteLine(alarmTime is { } alarm
                    ? $"Alarm set for {alarm:HH:mm:ss}"
                    : "No Alarm time provided. Usage example: 'alarm_clock 17:23' to set alarm for 5:23 PM. (Input uses the 24-hour clock.)");
                Console.WriteLine("Press CTRL-C to quit");

                await FacePositioning.GetInPositionAsync(robot);

                bool wasBeforeAlarmTime = false;
                int lastDisplayedSecond = -1;
                while (!context.CancellationToken.IsCancellationRequested)
                {
                    DateTime currentTime = DateTime.Now;
                    bool doAlarm = false;
                    if (alarmTime is { } alarmAt)
                    {
                        bool isBeforeAlarmTime = currentTime.TimeOfDay < alarmAt.ToTimeSpan();
                        doAlarm = wasBeforeAlarmTime && !isBeforeAlarmTime;
                        wasBeforeAlarmTime = isBeforeAlarmTime;
                    }

                    if (doAlarm)
                    {
                        await robot.AbortAllActionsAsync();
                        await using PerformOffChargerContext charger =
                            await robot.PerformOffChargerAsync();
                        string shortTime = $"{currentTime.Hour}:{currentTime.Minute}";
                        SayText speech = await robot.SayTextAsync(
                            $"Wake up lazy human! it's {shortTime}");
                        await speech.WaitForCompletedAsync();
                    }
                    else if (lastDisplayedSecond != currentTime.Second)
                    {
                        byte[] screenData = MakeClockImage(currentTime);
                        await robot.DisplayOledFaceImageAsync(screenData, 1000);
                        lastDisplayedSecond = currentTime.Second;
                    }

                    await Task.Delay(TimeSpan.FromSeconds(0.1), context.CancellationToken);
                }
            },
            driveOffChargerOnConnect: false,
            cancellationToken: context.CancellationToken);
    }

    private static byte[] MakeClockImage(DateTime currentTime)
    {
        string timeText = currentTime.ToString("hh:mm:ss tt", CultureInfo.InvariantCulture);
        if (!ShowAnalogClock)
        {
            return MakeTextImage(timeText, 8, 6);
        }

        var canvas = new PixelCanvas(OledFace.ScreenWidth, OledFace.ScreenHalfHeight);
        const int textHeight = 9;
        double screenWidth = OledFace.ScreenWidth;
        double analogHeight = OledFace.ScreenHalfHeight - textHeight;
        double centerX = screenWidth * 0.5;
        double centerY = analogHeight * 0.5;

        double secondHandLength = Math.Min(screenWidth, analogHeight) * 0.5;
        double minuteHandLength = 0.85 * secondHandLength;
        double hourHandLength = 0.7 * secondHandLength;

        double secondRatio = currentTime.Second / 60.0;
        double minuteRatio = (currentTime.Minute + secondRatio) / 60.0;
        double hourRatio = (currentTime.Hour + minuteRatio) / 12.0;

        DrawClockHand(canvas, centerX, centerY, hourRatio, hourHandLength);
        DrawClockHand(canvas, centerX, centerY, minuteRatio, minuteHandLength);
        DrawClockHand(canvas, centerX, centerY, secondRatio, secondHandLength);
        BitmapFont.DrawText(canvas, timeText, 32, OledFace.ScreenHalfHeight - textHeight);
        return canvas.ToScreenData();
    }

    private static byte[] MakeTextImage(string text, int x, int y)
    {
        var canvas = new PixelCanvas(OledFace.ScreenWidth, OledFace.ScreenHalfHeight);
        BitmapFont.DrawText(canvas, text, x, y);
        return canvas.ToScreenData();
    }

    private static void DrawClockHand(
        PixelCanvas canvas,
        double centerX,
        double centerY,
        double circleRatio,
        double handLength)
    {
        double handAngle = circleRatio * Math.PI * 2.0;
        double vectorX = handLength * Math.Sin(handAngle);
        double vectorY = -handLength * Math.Cos(handAngle);

        // The OLED interlacing halves the vertical resolution, so double x.
        const double xScalar = 2.0;
        double endX = centerX + (xScalar * vectorX);
        double endY = centerY + vectorY;

        double widthRatio = 0.1;
        double endX2 = centerX - ((xScalar * vectorY) * widthRatio);
        double endY2 = centerY + (vectorX * widthRatio);
        double endX3 = centerX + ((xScalar * vectorY) * widthRatio);
        double endY3 = centerY - (vectorX * widthRatio);

        canvas.DrawLine((int)endX, (int)endY, (int)endX2, (int)endY2);
        canvas.DrawLine((int)endX, (int)endY, (int)endX3, (int)endY3);
        canvas.DrawLine((int)endX2, (int)endY2, (int)endX3, (int)endY3);
    }

    private static TimeOnly? ExtractTimeFromArgs(string[] args)
    {
        var splitTimeArgs = new List<string>();
        foreach (string argument in args)
        {
            splitTimeArgs.AddRange(argument.Split(':'));
        }

        if (splitTimeArgs.Count < 2)
        {
            return null;
        }

        try
        {
            int hours = ConvertToTimeInt(splitTimeArgs[0], "hours", 23);
            int minutes = ConvertToTimeInt(splitTimeArgs[1], "minutes", 59);
            int seconds = splitTimeArgs.Count >= 3
                ? ConvertToTimeInt(splitTimeArgs[2], "seconds", 59)
                : 0;
            return new TimeOnly(hours, minutes, seconds);
        }
        catch (ArgumentException ex)
        {
            Console.WriteLine($"ValueError {ex.Message}");
            return null;
        }
    }

    private static int ConvertToTimeInt(string value, string timeUnit, int maximum)
    {
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int result))
        {
            throw new ArgumentException($"{timeUnit} value '{value}' is not an int");
        }

        if (result < 0)
        {
            throw new ArgumentException($"{timeUnit} value {result} is negative");
        }

        if (result > maximum)
        {
            throw new ArgumentException($"{timeUnit} value {result} exceeded {maximum}");
        }

        return result;
    }
}
