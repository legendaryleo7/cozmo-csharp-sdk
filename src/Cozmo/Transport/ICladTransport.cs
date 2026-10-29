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

namespace Anki.Cozmo.Sdk;

/// <summary>Byte transport that moves length-prefixed CLAD frames to and from a device.</summary>
/// <remarks>
/// A frame is the payload between the two-byte little-endian length prefix and the end of
/// the message, that is, the CLAD union tag plus body. Implementations own framing and
/// expose complete frames only.
/// </remarks>
public interface ICladTransport : IAsyncDisposable
{
    /// <summary>Opens the underlying connection.</summary>
    Task ConnectAsync(CancellationToken cancellationToken = default);

    /// <summary>Queues one complete frame for delivery.</summary>
    Task SendAsync(ReadOnlyMemory<byte> frame, CancellationToken cancellationToken = default);

    /// <summary>Yields complete frames as they arrive; ends when the peer closes.</summary>
    IAsyncEnumerable<byte[]> ReceiveFramesAsync(CancellationToken cancellationToken = default);

    /// <summary>Closes the connection and releases resources. Safe to call twice.</summary>
    Task CloseAsync();
}
