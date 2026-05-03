// SPDX-License-Identifier: GPL-3.0-only

using System.Runtime.InteropServices;
using JoystickGremlin.Core.ForceFeedback;
using Microsoft.Extensions.Logging;

namespace JoystickGremlin.Interop.VJoy;

/// <summary>
/// Listens for force feedback commands from the vJoy driver via the native FFB callback
/// and raises them as <see cref="IForceFeedbackSource.CommandReceived"/> events.
/// </summary>
/// <remarks>
/// The vJoy FFB callback fires on a <b>native thread</b>. The <see cref="CommandReceived"/>
/// event is therefore raised on a native thread. Consumers must be thread-safe.
/// </remarks>
public sealed class VJoyFfbSource : IForceFeedbackSource
{
    private readonly uint _vjoyDeviceId;
    private readonly ILogger<VJoyFfbSource> _logger;

    // Keep the delegate alive for the lifetime of registration.
    // If the delegate is GC'd while the native driver holds the raw function pointer,
    // the next callback invocation will crash with an access violation.
    private readonly FfbGenCB _callbackDelegate;

    // GCHandle pinning the delegate to prevent GC movement.
    private GCHandle _delegateHandle;

    // GCHandle pinning 'this' so the context pointer passed to FfbRegisterGenCB
    // can be converted back to the object reference in the static callback.
    private GCHandle _selfHandle;

    private volatile bool _running;
    private bool _disposed;

    /// <summary>
    /// Initialises a new <see cref="VJoyFfbSource"/> for the specified vJoy device.
    /// </summary>
    /// <param name="vjoyDeviceId">The vJoy device ID (1-based) to monitor for FFB commands.</param>
    /// <param name="logger">Logger instance.</param>
    public VJoyFfbSource(uint vjoyDeviceId, ILogger<VJoyFfbSource> logger)
    {
        _vjoyDeviceId = vjoyDeviceId;
        _logger = logger;
        _callbackDelegate = NativeCallback;
        _delegateHandle = GCHandle.Alloc(_callbackDelegate, GCHandleType.Normal);
    }

    /// <inheritdoc />
    public uint VJoyDeviceId => _vjoyDeviceId;

    /// <inheritdoc />
    public bool IsRunning => _running;

    /// <inheritdoc />
    public bool IsFfbCapable => VJoyFfbNative.IsDeviceFfb(_vjoyDeviceId);

    /// <inheritdoc />
    public event EventHandler<FfbCommand>? CommandReceived;

    /// <inheritdoc />
    public void Start()
    {
        if (_disposed || _running)
        {
            return;
        }

        _selfHandle = GCHandle.Alloc(this, GCHandleType.Normal);
        VJoyFfbNative.FfbRegisterGenCB(_callbackDelegate, GCHandle.ToIntPtr(_selfHandle));
        _running = true;
        _logger.LogInformation("VJoyFfbSource started for device {DeviceId}.", _vjoyDeviceId);
    }

    /// <inheritdoc />
    public void Stop()
    {
        if (!_running)
        {
            return;
        }

        _running = false;

        if (_selfHandle.IsAllocated)
        {
            _selfHandle.Free();
        }

        _logger.LogInformation("VJoyFfbSource stopped for device {DeviceId}.", _vjoyDeviceId);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Stop();

        if (_delegateHandle.IsAllocated)
        {
            _delegateHandle.Free();
        }
    }

    // ── Static native callback ────────────────────────────────────────────────

    /// <summary>
    /// Static callback invoked on a native thread by the vJoy driver whenever
    /// force feedback data is available. The <paramref name="context"/> parameter
    /// contains the GCHandle of the <see cref="VJoyFfbSource"/> instance.
    /// </summary>
    private static void NativeCallback(IntPtr ffbData, IntPtr context)
    {
        if (context == IntPtr.Zero)
        {
            return;
        }

        var handle = GCHandle.FromIntPtr(context);
        if (!handle.IsAllocated)
        {
            return;
        }

        if (handle.Target is not VJoyFfbSource source)
        {
            return;
        }

        if (!source._running)
        {
            return;
        }

        source.ProcessPacket(ffbData);
    }

    // ── Packet processing ─────────────────────────────────────────────────────

    private void ProcessPacket(IntPtr packet)
    {
        // Verify this packet belongs to our device.
        if (VJoyFfbNative.Ffb_h_DeviceID(packet, out uint deviceId) != 0)
        {
            return;
        }

        if (deviceId != _vjoyDeviceId)
        {
            return;
        }

        if (VJoyFfbNative.Ffb_h_Type(packet, out FfbPacketType packetType) != 0)
        {
            return;
        }

        FfbCommand? command = null;

        switch (packetType)
        {
            case FfbPacketType.PT_EFFREP:
                command = DecodeEffectReport(packet);
                break;

            case FfbPacketType.PT_CONSTREP:
                command = DecodeConstantForce(packet);
                break;

            case FfbPacketType.PT_RAMPREP:
                command = DecodeRampForce(packet);
                break;

            case FfbPacketType.PT_PRIDREP:
                command = DecodePeriodic(packet);
                break;

            case FfbPacketType.PT_CONDREP:
                command = DecodeCondition(packet);
                break;

            case FfbPacketType.PT_ENVREP:
                command = DecodeEnvelope(packet);
                break;

            case FfbPacketType.PT_EFOPREP:
                command = DecodeEffectOperation(packet);
                break;

            case FfbPacketType.PT_CTRLREP:
                command = DecodeDeviceControl(packet);
                break;

            case FfbPacketType.PT_GAINREP:
                command = DecodeDeviceGain(packet);
                break;

            default:
                // Unsupported/unknown packet — log at trace level to avoid spam in hot path.
                _logger.LogTrace("VJoyFfbSource: unsupported packet type 0x{Type:X} — skipping.", (uint)packetType);
                break;
        }

        if (command is null)
        {
            return;
        }

        CommandReceived?.Invoke(this, command);
    }

    private FfbCommand? DecodeEffectReport(IntPtr packet)
    {
        IntPtr buf = Marshal.AllocHGlobal(Marshal.SizeOf<VJoyFfbEffReport>());
        try
        {
            if (VJoyFfbNative.Ffb_h_Eff_Report(packet, buf) != 0)
            {
                return null;
            }

            var native = Marshal.PtrToStructure<VJoyFfbEffReport>(buf);
            return new SetEffectReportCommand(
                native.EBI,
                (FfbEffectType)native.EffectType,
                native.Duration,
                native.TriggerRpt,
                native.SamplePrd,
                native.StartDelay,
                native.Gain,
                native.TriggerBtn,
                (native.AxesEnabledDir & 0x01) != 0,
                native.Polar != 0,
                native.DirectionOrX,
                native.DirY);
        }
        finally
        {
            Marshal.FreeHGlobal(buf);
        }
    }

    private FfbCommand? DecodeConstantForce(IntPtr packet)
    {
        IntPtr buf = Marshal.AllocHGlobal(Marshal.SizeOf<VJoyFfbConstant>());
        try
        {
            if (VJoyFfbNative.Ffb_h_Eff_Const(packet, buf) != 0)
            {
                return null;
            }

            var native = Marshal.PtrToStructure<VJoyFfbConstant>(buf);
            return new SetConstantForceCommand(native.EBI, native.Magnitude);
        }
        finally
        {
            Marshal.FreeHGlobal(buf);
        }
    }

    private FfbCommand? DecodeRampForce(IntPtr packet)
    {
        IntPtr buf = Marshal.AllocHGlobal(Marshal.SizeOf<VJoyFfbRamp>());
        try
        {
            if (VJoyFfbNative.Ffb_h_Eff_Ramp(packet, buf) != 0)
            {
                return null;
            }

            var native = Marshal.PtrToStructure<VJoyFfbRamp>(buf);
            return new SetRampForceCommand(native.EBI, native.Start, native.End);
        }
        finally
        {
            Marshal.FreeHGlobal(buf);
        }
    }

    private FfbCommand? DecodePeriodic(IntPtr packet)
    {
        IntPtr buf = Marshal.AllocHGlobal(Marshal.SizeOf<VJoyFfbPeriod>());
        try
        {
            if (VJoyFfbNative.Ffb_h_Eff_Period(packet, buf) != 0)
            {
                return null;
            }

            var native = Marshal.PtrToStructure<VJoyFfbPeriod>(buf);
            return new SetPeriodicCommand(native.EBI, native.Magnitude, native.Offset, native.Phase, native.Period);
        }
        finally
        {
            Marshal.FreeHGlobal(buf);
        }
    }

    private FfbCommand? DecodeCondition(IntPtr packet)
    {
        IntPtr buf = Marshal.AllocHGlobal(Marshal.SizeOf<VJoyFfbCondition>());
        try
        {
            if (VJoyFfbNative.Ffb_h_Eff_Cond(packet, buf) != 0)
            {
                return null;
            }

            var native = Marshal.PtrToStructure<VJoyFfbCondition>(buf);
            return new SetConditionCommand(
                native.EBI,
                native.IsY != 0,
                native.CenterPointOffset,
                native.PosCoeff,
                native.NegCoeff,
                native.PosSatur,
                native.NegSatur,
                native.DeadBand);
        }
        finally
        {
            Marshal.FreeHGlobal(buf);
        }
    }

    private FfbCommand? DecodeEnvelope(IntPtr packet)
    {
        IntPtr buf = Marshal.AllocHGlobal(Marshal.SizeOf<VJoyFfbEnvelope>());
        try
        {
            if (VJoyFfbNative.Ffb_h_Eff_Envlp(packet, buf) != 0)
            {
                return null;
            }

            var native = Marshal.PtrToStructure<VJoyFfbEnvelope>(buf);
            return new SetEnvelopeCommand(native.EBI, native.AttackLevel, native.FadeLevel, native.AttackTime, native.FadeTime);
        }
        finally
        {
            Marshal.FreeHGlobal(buf);
        }
    }

    private FfbCommand? DecodeEffectOperation(IntPtr packet)
    {
        IntPtr buf = Marshal.AllocHGlobal(Marshal.SizeOf<VJoyFfbEffOp>());
        try
        {
            if (VJoyFfbNative.Ffb_h_EffOp(packet, buf) != 0)
            {
                return null;
            }

            var native = Marshal.PtrToStructure<VJoyFfbEffOp>(buf);
            return new EffectOperationCommand(native.EBI, (FfbOperation)native.EffectOp, native.LoopCount);
        }
        finally
        {
            Marshal.FreeHGlobal(buf);
        }
    }

    private FfbCommand? DecodeDeviceControl(IntPtr packet)
    {
        if (VJoyFfbNative.Ffb_h_DevCtrl(packet, out uint control) != 0)
        {
            return null;
        }

        return new DeviceControlCommand((FfbDeviceCommand)control);
    }

    private FfbCommand? DecodeDeviceGain(IntPtr packet)
    {
        if (VJoyFfbNative.Ffb_h_Eff_Gain(packet, out byte gain) != 0)
        {
            return null;
        }

        return new DeviceGainCommand(gain);
    }
}
