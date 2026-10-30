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

/// <summary>Base class for errors reported by the usbmux client.</summary>
public class UsbMuxException : CozmoConnectionException
{
    /// <summary>Creates an exception.</summary>
    public UsbMuxException()
    {
    }

    /// <summary>Creates an exception with a message.</summary>
    public UsbMuxException(string message)
        : base(message)
    {
    }

    /// <summary>Creates an exception with a message and inner exception.</summary>
    public UsbMuxException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>Raised when a usbmux message is malformed or uses an unsupported protocol version.</summary>
public sealed class UsbMuxProtocolException : UsbMuxException
{
    /// <summary>Creates an exception with a message.</summary>
    public UsbMuxProtocolException(string message)
        : base(message)
    {
    }

    /// <summary>Creates an exception with a message and inner exception.</summary>
    public UsbMuxProtocolException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>Raised when the requested device is no longer attached.</summary>
public sealed class DeviceNotConnectedException : UsbMuxException
{
    /// <summary>Creates an exception with a message.</summary>
    public DeviceNotConnectedException(string message)
        : base(message)
    {
    }
}

/// <summary>Raised when the device refused the requested port.</summary>
public sealed class UsbMuxConnectionRefusedException : UsbMuxException
{
    /// <summary>Creates an exception with a message.</summary>
    public UsbMuxConnectionRefusedException(string message)
        : base(message)
    {
    }
}

/// <summary>Raised when the usbmux daemon reports an unexpected failure.</summary>
public sealed class UsbMuxConnectionFailedException : UsbMuxException
{
    /// <summary>Creates an exception.</summary>
    public UsbMuxConnectionFailedException()
    {
    }

    /// <summary>Creates an exception with a message.</summary>
    public UsbMuxConnectionFailedException(string message)
        : base(message)
    {
    }
}
