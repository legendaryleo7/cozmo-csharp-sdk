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

/// <summary>Finds a device running the Cozmo app in SDK mode and opens a CLAD transport to it.</summary>
/// <remarks>
/// The connector only establishes the raw transport. The SDK handshake and robot setup are
/// performed by the higher-level runner once a device has been returned.
/// </remarks>
public interface IDeviceConnector : IAsyncDisposable
{
    /// <summary>Stable connector name such as <c>tcp</c>, <c>android</c> or <c>ios</c>.</summary>
    string Name { get; }

    /// <summary>True when the connector has enough configuration to attempt a connection.</summary>
    bool Enabled { get; }

    /// <summary>Opens a transport to the first matching device.</summary>
    /// <param name="cancellationToken">Cancels device discovery and the connection attempt.</param>
    /// <returns>The connected transport and the device metadata reported by the connector.</returns>
    /// <exception cref="CozmoConnectionException">A device could not be reached.</exception>
    Task<ConnectedDevice> ConnectAsync(CancellationToken cancellationToken = default);

    /// <summary>Releases resources owned by the connector; transports already returned are unaffected.</summary>
    new ValueTask DisposeAsync();
}
