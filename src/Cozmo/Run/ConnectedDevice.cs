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

using System.Collections.ObjectModel;

namespace Anki.Cozmo.Sdk;

/// <summary>An open device transport together with the metadata reported by its connector.</summary>
public sealed record ConnectedDevice
{
    /// <summary>Creates a connected device description.</summary>
    /// <param name="transport">Open CLAD transport to the device.</param>
    /// <param name="deviceInfo">Connector-specific device metadata.</param>
    /// <param name="connectorName">Name of the connector that produced this device.</param>
    /// <exception cref="ArgumentNullException">A required argument is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="connectorName"/> is empty.</exception>
    public ConnectedDevice(
        ICladTransport transport,
        IReadOnlyDictionary<string, string> deviceInfo,
        string connectorName)
    {
        ArgumentNullException.ThrowIfNull(transport);
        ArgumentNullException.ThrowIfNull(deviceInfo);
        ArgumentException.ThrowIfNullOrEmpty(connectorName);

        Transport = transport;
        DeviceInfo = new ReadOnlyDictionary<string, string>(
            new Dictionary<string, string>(deviceInfo, StringComparer.Ordinal));
        ConnectorName = connectorName;
    }

    /// <summary>Open CLAD transport to the device; the caller owns and disposes it.</summary>
    public ICladTransport Transport { get; }

    /// <summary>
    /// Device metadata using the Python SDK keys: <c>device_type</c> plus <c>host</c>,
    /// <c>serial</c> or <c>device_id</c> depending on the connector.
    /// </summary>
    public IReadOnlyDictionary<string, string> DeviceInfo { get; }

    /// <summary>Name of the connector that produced this device.</summary>
    public string ConnectorName { get; }
}
