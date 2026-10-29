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

/// <summary>Base class of all Cozmo SDK exceptions.</summary>
public class CozmoSdkException : Exception
{
    /// <summary>Creates an exception.</summary>
    public CozmoSdkException()
    {
    }

    /// <summary>Creates an exception with a message.</summary>
    public CozmoSdkException(string message)
        : base(message)
    {
    }

    /// <summary>Creates an exception with a message and inner exception.</summary>
    public CozmoSdkException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>Raised when the SDK is being shut down.</summary>
public class SdkShutdownException : CozmoSdkException
{
    /// <summary>Creates an exception.</summary>
    public SdkShutdownException()
    {
    }

    /// <summary>Creates an exception with a message.</summary>
    public SdkShutdownException(string message)
        : base(message)
    {
    }

    /// <summary>Creates an exception with a message and inner exception.</summary>
    public SdkShutdownException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>Raised by an event handler to prevent subsequent handlers from being triggered.</summary>
public sealed class StopPropagationException : CozmoSdkException
{
    /// <summary>Creates an exception.</summary>
    public StopPropagationException()
    {
    }

    /// <summary>Creates an exception with a message.</summary>
    public StopPropagationException(string message)
        : base(message)
    {
    }
}

/// <summary>Raised when playing a named animation before the animation list has been received.</summary>
public class AnimationsNotLoadedException : CozmoSdkException
{
    /// <summary>Creates an exception.</summary>
    public AnimationsNotLoadedException()
    {
    }

    /// <summary>Creates an exception with a message.</summary>
    public AnimationsNotLoadedException(string message)
        : base(message)
    {
    }
}

/// <summary>Base class for errors that occur while running robot actions.</summary>
public class ActionException : CozmoSdkException
{
    /// <summary>Creates an exception.</summary>
    public ActionException()
    {
    }

    /// <summary>Creates an exception with a message.</summary>
    public ActionException(string message)
        : base(message)
    {
    }

    /// <summary>Creates an exception with a message and inner exception.</summary>
    public ActionException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>Base class for errors regarding the connection to the device.</summary>
public class CozmoConnectionException : CozmoSdkException
{
    /// <summary>Creates an exception.</summary>
    public CozmoConnectionException()
    {
    }

    /// <summary>Creates an exception with a message.</summary>
    public CozmoConnectionException(string message)
        : base(message)
    {
    }

    /// <summary>Creates an exception with a message and inner exception.</summary>
    public CozmoConnectionException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>Raised when the connection to the device is unexpectedly lost.</summary>
public sealed class ConnectionAbortedException : CozmoConnectionException
{
    /// <summary>Creates an exception.</summary>
    public ConnectionAbortedException()
    {
    }

    /// <summary>Creates an exception with a message.</summary>
    public ConnectionAbortedException(string message)
        : base(message)
    {
    }

    /// <summary>Creates an exception with a message and inner exception.</summary>
    public ConnectionAbortedException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>Raised when the connection check has failed.</summary>
public sealed class ConnectionCheckFailedException : CozmoConnectionException
{
    /// <summary>Creates an exception.</summary>
    public ConnectionCheckFailedException()
    {
    }

    /// <summary>Creates an exception with a message.</summary>
    public ConnectionCheckFailedException(string message)
        : base(message)
    {
    }

    /// <summary>Creates an exception with a message and inner exception.</summary>
    public ConnectionCheckFailedException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>Raised when no device running Cozmo in SDK mode could be found.</summary>
public sealed class NoDevicesFoundException : CozmoConnectionException
{
    /// <summary>Creates an exception.</summary>
    public NoDevicesFoundException()
    {
    }

    /// <summary>Creates an exception with a message.</summary>
    public NoDevicesFoundException(string message)
        : base(message)
    {
    }
}

/// <summary>Raised when the SDK and the app running on the device are not compatible.</summary>
public sealed class SdkVersionMismatchException : CozmoConnectionException
{
    /// <summary>Creates an exception.</summary>
    /// <param name="message">The full diagnostic message shown to the caller.</param>
    /// <param name="sdkVersion">SDK version number in Major.Minor.Patch form.</param>
    /// <param name="sdkAppVersion">App version the SDK is compatible with, in Major.Minor.Patch form.</param>
    /// <param name="appVersion">App version that was detected, in Major.Minor.Patch form.</param>
    public SdkVersionMismatchException(string message, string sdkVersion, string sdkAppVersion, string appVersion)
        : base(message)
    {
        SdkVersion = sdkVersion;
        SdkAppVersion = sdkAppVersion;
        AppVersion = appVersion;
    }

    /// <summary>SDK version number in Major.Minor.Patch form.</summary>
    public string SdkVersion { get; }

    /// <summary>App version the SDK is compatible with, in Major.Minor.Patch form.</summary>
    public string SdkAppVersion { get; }

    /// <summary>App version that was detected, in Major.Minor.Patch form.</summary>
    public string AppVersion { get; }
}

/// <summary>Raised when attempting to pick up or place an object the robot cannot handle.</summary>
public sealed class NotPickupableException : ActionException
{
    /// <summary>Creates an exception.</summary>
    public NotPickupableException()
    {
    }

    /// <summary>Creates an exception with a message.</summary>
    public NotPickupableException(string message)
        : base(message)
    {
    }
}

/// <summary>Raised when attempting to place an object on top of an invalid object.</summary>
public sealed class CannotPlaceObjectsOnThisException : ActionException
{
    /// <summary>Creates an exception.</summary>
    public CannotPlaceObjectsOnThisException()
    {
    }

    /// <summary>Creates an exception with a message.</summary>
    public CannotPlaceObjectsOnThisException(string message)
        : base(message)
    {
    }
}

/// <summary>Raised when attempting an action while another required action is still running.</summary>
public sealed class RobotBusyException : ActionException
{
    /// <summary>Creates an exception.</summary>
    public RobotBusyException()
    {
    }

    /// <summary>Creates an exception with a message.</summary>
    public RobotBusyException(string message)
        : base(message)
    {
    }
}
