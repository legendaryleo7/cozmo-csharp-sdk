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

using System.Text.Json;
using System.Threading.Channels;
using Anki.Cozmo.Sdk;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Cozmo.Samples.Examples.IfThisThenThat;

/// <summary>Hosts one IFTTT webhook and dispatches the request to Cozmo.</summary>
internal static class IfThisThenThatServer
{
    private const string ListenUrl = "http://127.0.0.1:8080";

    public static async Task RunAsync(
        ExampleContext context,
        string route,
        string responseText,
        Func<JsonElement, Robot, Task> handler,
        bool queueRequests = false)
    {
        await ExampleRunner.RunAsync(
            async robot =>
            {
                WebApplicationBuilder builder = WebApplication.CreateBuilder(
                    new WebApplicationOptions { Args = [] });
                builder.Logging.ClearProviders();
                WebApplication app = builder.Build();
                app.Urls.Add(ListenUrl);

                Channel<JsonElement>? queue = null;
                Task? worker = null;
                if (queueRequests)
                {
                    queue = Channel.CreateUnbounded<JsonElement>();
                    ChannelReader<JsonElement> reader = queue.Reader;
                    worker = Task.Run(async () =>
                    {
                        await foreach (JsonElement element in reader.ReadAllAsync())
                        {
                            await InvokeSafelyAsync(handler, element, robot);
                        }
                    });
                }

                app.MapGet("/", () => Results.Text($"Cozmo IFTTT example. POST to {route}."));
                app.MapPost(
                    route,
                    async (HttpRequest request) =>
                    {
                        using JsonDocument document = await JsonDocument.ParseAsync(request.Body);
                        JsonElement element = document.RootElement.Clone();
                        if (queue is not null)
                        {
                            await queue.Writer.WriteAsync(element);
                        }
                        else
                        {
                            _ = Task.Run(() => InvokeSafelyAsync(handler, element, robot));
                        }

                        return Results.Text(responseText);
                    });

                Console.WriteLine($"Listening on {ListenUrl}{route}");
                await app.StartAsync(context.CancellationToken);
                try
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, context.CancellationToken);
                }
                catch (OperationCanceledException)
                {
                    // Ctrl+C stops the server.
                }
                finally
                {
                    if (queue is not null)
                    {
                        queue.Writer.TryComplete();
                    }

                    if (worker is not null)
                    {
                        await worker;
                    }

                    await app.StopAsync();
                    await app.DisposeAsync();
                }
            },
            driveOffChargerOnConnect: false,
            cancellationToken: context.CancellationToken);
    }

    private static async Task InvokeSafelyAsync(
        Func<JsonElement, Robot, Task> handler,
        JsonElement element,
        Robot robot)
    {
        try
        {
            await handler(element, robot);
        }
        catch (OperationCanceledException)
        {
            // Shutdown.
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"IFTTT request failed: {ex}");
        }
    }
}
