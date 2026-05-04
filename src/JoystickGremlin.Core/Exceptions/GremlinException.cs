// SPDX-License-Identifier: GPL-3.0-only

namespace JoystickGremlin.Core.Exceptions;

/// <summary>
/// Base exception for all Joystick Gremlin domain errors.
/// </summary>
public class GremlinException : Exception
{
    /// <inheritdoc />
    public GremlinException(string message) : base(message) { }

    /// <inheritdoc />
    public GremlinException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Raised when a profile cannot be loaded or saved.
/// </summary>
public sealed class ProfileException : GremlinException
{
    /// <inheritdoc />
    public ProfileException(string message) : base(message) { }

    /// <inheritdoc />
    public ProfileException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Raised when an error occurs communicating with the vJoy virtual joystick driver.
/// </summary>
public sealed class VJoyException : GremlinException
{
    /// <inheritdoc />
    public VJoyException(string message) : base(message) { }

    /// <inheritdoc />
    public VJoyException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Raised when an error occurs reading from a physical device via DILL.
/// </summary>
public sealed class DeviceException : GremlinException
{
    /// <inheritdoc />
    public DeviceException(string message) : base(message) { }

    /// <inheritdoc />
    public DeviceException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Raised when an operation references an unknown or invalid mode name.
/// </summary>
public sealed class ModeException : GremlinException
{
    /// <inheritdoc />
    public ModeException(string message) : base(message) { }

    /// <inheritdoc />
    public ModeException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Raised when an error occurs with the EmuWheel virtual wheel device or its identity spoof.
/// </summary>
public sealed class EmuWheelException : GremlinException
{
    /// <inheritdoc />
    public EmuWheelException(string message) : base(message) { }

    /// <inheritdoc />
    public EmuWheelException(string message, Exception innerException) : base(message, innerException) { }
}
