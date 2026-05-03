// SPDX-License-Identifier: GPL-3.0-only

using System.Runtime.InteropServices;
using JoystickGremlin.Core.ForceFeedback;
using JoystickGremlin.Interop.DirectInput;
using Microsoft.Extensions.Logging;

namespace JoystickGremlin.Interop.Moza;

/// <summary>
/// Force feedback sink that targets a MOZA steering wheel via DirectInput 8.
/// Connects to the first MOZA device found (VID 0x346e) unless a specific instance GUID is configured.
/// </summary>
/// <remarks>
/// <b>Safety:</b> <see cref="Disconnect"/> sends a StopAll command before releasing the device
/// to ensure the wheel is not left applying torque when the application exits.
/// </remarks>
public sealed class MozaFfbSink : IForceFeedbackSink
{
    private const ushort MozaVendorId = 0x346e;

    private readonly ILogger<MozaFfbSink> _logger;

    private IDirectInput8W? _directInput;
    private IDirectInputDevice8W? _device;
    private IntPtr _hwnd;
    private IntPtr _dataFormatNative;
    private IntPtr _dataFormatObjsNative;
    private IntPtr _axisObjectNative;

    private readonly Dictionary<byte, EffectSlot> _effectSlots = [];
    private readonly object _lock = new();

    private string _deviceId = string.Empty;
    private string _displayName = string.Empty;
    private bool _isConnected;
    private bool _disposed;

    /// <summary>
    /// Initialises a new <see cref="MozaFfbSink"/>.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    public MozaFfbSink(ILogger<MozaFfbSink> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public string DeviceId => _deviceId;

    /// <inheritdoc />
    public string DisplayName => _displayName;

    /// <inheritdoc />
    public bool IsConnected => _isConnected;

    /// <inheritdoc />
    public IReadOnlyList<FfbEffectType> SupportedEffects { get; } = new List<FfbEffectType>
    {
        FfbEffectType.ConstantForce,
        FfbEffectType.Spring,
        FfbEffectType.Damper,
        FfbEffectType.Sine,
        FfbEffectType.Square,
        FfbEffectType.Triangle,
        FfbEffectType.SawtoothUp,
        FfbEffectType.SawtoothDown,
    };

    /// <inheritdoc />
    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
        {
            return;
        }

        // DirectInput enumeration is synchronous Win32; run on a thread-pool thread
        // to avoid blocking the calling async context.
        await Task.Run(() => ConnectCore(), cancellationToken).ConfigureAwait(false);
    }

    private void ConnectCore()
    {
        _logger.LogInformation("MozaFfbSink: scanning for MOZA wheel device (VID 0x{VID:X4}).", MozaVendorId);

        IntPtr hInstance = DirectInputNative.GetModuleHandle(null);

        int hr = DirectInputNative.DirectInput8Create(
            hInstance,
            DirectInputNative.DIRECTINPUT_VERSION,
            DirectInputNative.IID_IDirectInput8W,
            out IntPtr di8Ptr,
            IntPtr.Zero);

        if (hr < 0 || di8Ptr == IntPtr.Zero)
        {
            throw new InvalidOperationException($"DirectInput8Create failed with HRESULT 0x{hr:X8}.");
        }

        _directInput = (IDirectInput8W)Marshal.GetObjectForIUnknown(di8Ptr);
        Marshal.Release(di8Ptr);

        // Enumerate FF joystick devices, looking for a MOZA device.
        DiDeviceInstance? mozaInstance = null;

        EnumDevicesCallback callback = (ref DiDeviceInstance instance, IntPtr _) =>
        {
            byte[] guidBytes = instance.guidProduct.ToByteArray();
            ushort vid = (ushort)(guidBytes[0] | (guidBytes[1] << 8));
            if (vid == MozaVendorId)
            {
                mozaInstance = instance;
                return 0; // DIENUM_STOP — stop after first match
            }
            return 1; // DIENUM_CONTINUE
        };

        // Pin the callback delegate and enumerate.
        GCHandle callbackHandle = GCHandle.Alloc(callback);
        IntPtr callbackPtr = Marshal.GetFunctionPointerForDelegate(callback);
        try
        {
            _directInput.EnumDevices(
                DirectInputNative.DIDEVTYPE_JOYSTICK,
                callbackPtr,
                IntPtr.Zero,
                DirectInputNative.DIEDFL_FORCEFEEDBACK);
        }
        finally
        {
            callbackHandle.Free();
        }

        if (mozaInstance is null)
        {
            throw new InvalidOperationException("No MOZA force feedback wheel found. Ensure the MOZA driver is installed and the wheel is connected.");
        }

        var found = mozaInstance.Value;
        _deviceId = found.guidInstance.ToString();
        _displayName = found.tszInstanceName;

        _logger.LogInformation("MozaFfbSink: found device '{Name}' ({Guid}).", _displayName, _deviceId);

        // Create the device.
        hr = _directInput.CreateDevice(found.guidInstance, out IntPtr devicePtr, IntPtr.Zero);
        if (hr < 0 || devicePtr == IntPtr.Zero)
        {
            throw new InvalidOperationException($"IDirectInput8W::CreateDevice failed with HRESULT 0x{hr:X8}.");
        }

        _device = (IDirectInputDevice8W)Marshal.GetObjectForIUnknown(devicePtr);
        Marshal.Release(devicePtr);

        // Set up a minimal 1-axis data format so we can acquire the device.
        SetupDataFormat();

        // Create a message-only window for SetCooperativeLevel.
        _hwnd = DirectInputNative.CreateWindowEx(
            0, "STATIC", "JGFFBBridge", 0, 0, 0, 0, 0,
            new IntPtr(-3),   // HWND_MESSAGE
            IntPtr.Zero,
            DirectInputNative.GetModuleHandle(null),
            IntPtr.Zero);

        hr = _device.SetCooperativeLevel(_hwnd, DirectInputNative.DISCL_EXCLUSIVE | DirectInputNative.DISCL_BACKGROUND);
        if (hr < 0)
        {
            _logger.LogWarning("SetCooperativeLevel returned 0x{HR:X8} — trying non-exclusive.", hr);
            hr = _device.SetCooperativeLevel(_hwnd, DirectInputNative.DISCL_NONEXCLUSIVE | DirectInputNative.DISCL_BACKGROUND);
        }

        hr = _device.Acquire();
        if (hr < 0)
        {
            throw new InvalidOperationException($"IDirectInputDevice8W::Acquire failed with HRESULT 0x{hr:X8}.");
        }

        _isConnected = true;
        _logger.LogInformation("MozaFfbSink: connected and acquired '{Name}'.", _displayName);
    }

    private unsafe void SetupDataFormat()
    {
        if (_device is null)
        {
            return;
        }

        // Allocate native memory for one DiObjectDataFormat describing the X axis.
        int objSize = Marshal.SizeOf<DiObjectDataFormat>();
        _dataFormatObjsNative = Marshal.AllocHGlobal(objSize);

        // Allocate space for the GUID_XAxis pointer.
        _axisObjectNative = Marshal.AllocHGlobal(Marshal.SizeOf<Guid>());
        Marshal.StructureToPtr(DirectInputNative.GUID_XAxis, _axisObjectNative, false);

        var objFmt = new DiObjectDataFormat
        {
            pguid = _axisObjectNative,
            dwOfs = 0,
            dwType = DirectInputNative.DIDFT_AXIS | DirectInputNative.DIDFT_ANYINSTANCE,
            dwFlags = 0,
        };
        Marshal.StructureToPtr(objFmt, _dataFormatObjsNative, false);

        // Allocate and fill the DiDataFormat.
        int fmtSize = Marshal.SizeOf<DiDataFormat>();
        _dataFormatNative = Marshal.AllocHGlobal(fmtSize);
        var fmt = new DiDataFormat
        {
            dwSize    = (uint)fmtSize,
            dwObjSize = (uint)objSize,
            dwFlags   = DirectInputNative.DIDF_ABSAXIS,
            dwDataSize = 4,          // one DWORD
            dwNumObjs  = 1,
            rgodf      = _dataFormatObjsNative,
        };
        Marshal.StructureToPtr(fmt, _dataFormatNative, false);

        int hr = _device.SetDataFormat(_dataFormatNative);
        if (hr < 0)
        {
            _logger.LogWarning("SetDataFormat returned 0x{HR:X8}.", hr);
        }
    }

    /// <inheritdoc />
    public void Disconnect()
    {
        if (!_isConnected)
        {
            return;
        }

        _isConnected = false;

        lock (_lock)
        {
            // Stop all effects on the device.
            if (_device is not null)
            {
                try
                {
                    _device.SendForceFeedbackCommand(DirectInputNative.DISFFC_STOPALL);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "MozaFfbSink: failed to send STOPALL during disconnect.");
                }
            }

            // Unload all cached effects.
            foreach (var slot in _effectSlots.Values)
            {
                slot.Dispose();
            }
            _effectSlots.Clear();

            // Unacquire and release the device.
            if (_device is not null)
            {
                try
                {
                    _device.Unacquire();
                    Marshal.ReleaseComObject(_device);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "MozaFfbSink: error releasing device COM object.");
                }
                _device = null;
            }

            if (_directInput is not null)
            {
                try
                {
                    Marshal.ReleaseComObject(_directInput);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "MozaFfbSink: error releasing DirectInput COM object.");
                }
                _directInput = null;
            }

            // Destroy the message-only window.
            if (_hwnd != IntPtr.Zero)
            {
                DirectInputNative.DestroyWindow(_hwnd);
                _hwnd = IntPtr.Zero;
            }

            FreeNativeMemory();
        }

        _logger.LogInformation("MozaFfbSink: disconnected from '{Name}'.", _displayName);
    }

    private void FreeNativeMemory()
    {
        if (_dataFormatNative != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(_dataFormatNative);
            _dataFormatNative = IntPtr.Zero;
        }

        if (_dataFormatObjsNative != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(_dataFormatObjsNative);
            _dataFormatObjsNative = IntPtr.Zero;
        }

        if (_axisObjectNative != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(_axisObjectNative);
            _axisObjectNative = IntPtr.Zero;
        }
    }

    /// <inheritdoc />
    public void SendCommand(FfbCommand command)
    {
        if (!_isConnected || _device is null)
        {
            return;
        }

        lock (_lock)
        {
            try
            {
                switch (command)
                {
                    case SetEffectReportCommand report:
                        HandleSetEffectReport(report);
                        break;

                    case SetConstantForceCommand cf:
                        HandleSetConstantForce(cf);
                        break;

                    case SetPeriodicCommand periodic:
                        HandleSetPeriodic(periodic);
                        break;

                    case SetConditionCommand condition:
                        HandleSetCondition(condition);
                        break;

                    case SetEnvelopeCommand envelope:
                        HandleSetEnvelope(envelope);
                        break;

                    case SetRampForceCommand ramp:
                        _logger.LogTrace("MozaFfbSink: ramp force received for EBI {EBI} — not directly supported by DirectInput; skipping.", ramp.EffectBlockIndex);
                        break;

                    case EffectOperationCommand op:
                        HandleEffectOperation(op);
                        break;

                    case DeviceControlCommand ctrl:
                        HandleDeviceControl(ctrl);
                        break;

                    case DeviceGainCommand gain:
                        _logger.LogWarning("MozaFfbSink: DeviceGainCommand received (gain={Gain}) — global gain is set per-effect; ignoring device-level gain.", gain.Gain);
                        break;

                    default:
                        _logger.LogWarning("MozaFfbSink: unrecognised command type '{Type}' — skipping.", command.GetType().Name);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "MozaFfbSink: error processing command '{Type}'.", command.GetType().Name);
            }
        }
    }

    private void HandleSetEffectReport(SetEffectReportCommand report)
    {
        if (!_effectSlots.TryGetValue(report.EffectBlockIndex, out var slot))
        {
            slot = new EffectSlot(_logger);
            _effectSlots[report.EffectBlockIndex] = slot;
        }

        slot.EffectType = report.EffectType;
        slot.DurationUs = report.DurationMs == 0xFFFF ? DirectInputNative.INFINITE_DURATION : (uint)(report.DurationMs * 1000u);
        slot.Gain = report.Gain;
        slot.TriggerRepeatUs = (uint)(report.TriggerRepeatMs * 1000u);
    }

    private void HandleSetConstantForce(SetConstantForceCommand cf)
    {
        if (!_effectSlots.TryGetValue(cf.EffectBlockIndex, out var slot))
        {
            slot = new EffectSlot(_logger) { EffectType = FfbEffectType.ConstantForce };
            _effectSlots[cf.EffectBlockIndex] = slot;
        }

        if (_device is null)
        {
            return;
        }

        Guid effectGuid = DirectInputNative.GetEffectGuid(FfbEffectType.ConstantForce);

        slot.EnsureEffect(_device, effectGuid, FfbEffectType.ConstantForce, slot.DurationUs, slot.Gain);

        if (slot.Effect is null)
        {
            return;
        }

        var cfParams = new DiConstantForce { lMagnitude = cf.Magnitude };
        slot.UpdateTypeSpecificParams(cfParams, slot.Effect);
    }

    private void HandleSetPeriodic(SetPeriodicCommand periodic)
    {
        if (!_effectSlots.TryGetValue(periodic.EffectBlockIndex, out var slot))
        {
            slot = new EffectSlot(_logger) { EffectType = FfbEffectType.Sine };
            _effectSlots[periodic.EffectBlockIndex] = slot;
        }

        if (_device is null)
        {
            return;
        }

        FfbEffectType effectType = slot.EffectType == FfbEffectType.None ? FfbEffectType.Sine : slot.EffectType;
        Guid effectGuid = DirectInputNative.GetEffectGuid(effectType);

        if (effectGuid == Guid.Empty)
        {
            _logger.LogWarning("MozaFfbSink: no DirectInput GUID for effect type {Type}.", effectType);
            return;
        }

        slot.EnsureEffect(_device, effectGuid, effectType, slot.DurationUs, slot.Gain);

        if (slot.Effect is null)
        {
            return;
        }

        var perParams = new DiPeriodic
        {
            dwMagnitude = periodic.Magnitude,
            lOffset     = periodic.Offset,
            dwPhase     = periodic.Phase,
            dwPeriod    = periodic.Period,
        };
        slot.UpdateTypeSpecificParams(perParams, slot.Effect);
    }

    private void HandleSetCondition(SetConditionCommand condition)
    {
        if (!_effectSlots.TryGetValue(condition.EffectBlockIndex, out var slot))
        {
            slot = new EffectSlot(_logger) { EffectType = FfbEffectType.Spring };
            _effectSlots[condition.EffectBlockIndex] = slot;
        }

        if (_device is null)
        {
            return;
        }

        FfbEffectType effectType = slot.EffectType == FfbEffectType.None ? FfbEffectType.Spring : slot.EffectType;
        Guid effectGuid = DirectInputNative.GetEffectGuid(effectType);

        if (effectGuid == Guid.Empty)
        {
            return;
        }

        slot.EnsureEffect(_device, effectGuid, effectType, slot.DurationUs, slot.Gain);

        if (slot.Effect is null)
        {
            return;
        }

        var condParams = new DiCondition
        {
            lOffset                 = condition.CenterOffset,
            lPositiveCoefficient    = condition.PosCoeff,
            lNegativeCoefficient    = condition.NegCoeff,
            dwPositiveSaturation    = condition.PosSatur,
            dwNegativeSaturation    = condition.NegSatur,
            lDeadBand               = condition.DeadBand,
        };
        slot.UpdateTypeSpecificParams(condParams, slot.Effect);
    }

    private void HandleSetEnvelope(SetEnvelopeCommand envelope)
    {
        if (!_effectSlots.TryGetValue(envelope.EffectBlockIndex, out var slot))
        {
            _logger.LogTrace("MozaFfbSink: envelope for unknown EBI {EBI} — skipping.", envelope.EffectBlockIndex);
            return;
        }

        if (slot.Effect is null)
        {
            return;
        }

        slot.UpdateEnvelope(envelope, slot.Effect);
    }

    private void HandleEffectOperation(EffectOperationCommand op)
    {
        if (!_effectSlots.TryGetValue(op.EffectBlockIndex, out var slot))
        {
            _logger.LogTrace("MozaFfbSink: operation {Op} on unknown EBI {EBI} — skipping.", op.Operation, op.EffectBlockIndex);
            return;
        }

        if (slot.Effect is null)
        {
            return;
        }

        switch (op.Operation)
        {
            case FfbOperation.Start:
            case FfbOperation.StartSolo:
                int iterations = op.LoopCount == 255 ? unchecked((int)0xFFFFFFFF) : op.LoopCount;
                slot.Effect.Start((uint)iterations, 0);
                break;

            case FfbOperation.Stop:
                slot.Effect.Stop();
                break;
        }
    }

    private void HandleDeviceControl(DeviceControlCommand ctrl)
    {
        if (_device is null)
        {
            return;
        }

        uint flag = ctrl.Control switch
        {
            FfbDeviceCommand.StopAll         => DirectInputNative.DISFFC_STOPALL,
            FfbDeviceCommand.Reset           => DirectInputNative.DISFFC_RESET,
            FfbDeviceCommand.EnableActuators => DirectInputNative.DISFFC_ACTUATORSE,
            FfbDeviceCommand.DisableActuators => DirectInputNative.DISFFC_ACTUATORSD,
            FfbDeviceCommand.Pause           => DirectInputNative.DISFFC_PAUSE,
            FfbDeviceCommand.Continue        => DirectInputNative.DISFFC_CONTINUE,
            _ => 0,
        };

        if (flag == 0)
        {
            return;
        }

        _device.SendForceFeedbackCommand(flag);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Disconnect();
    }

    // ── EnumDevices callback delegate ─────────────────────────────────────────

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int EnumDevicesCallback(ref DiDeviceInstance lpddi, IntPtr pvRef);

    // ── Effect slot helper ────────────────────────────────────────────────────

    /// <summary>
    /// Holds state for one DirectInput effect slot (one effect block index).
    /// </summary>
    private sealed class EffectSlot : IDisposable
    {
        private readonly ILogger _logger;

        internal IDirectInputEffect? Effect { get; private set; }
        internal FfbEffectType EffectType { get; set; }
        internal uint DurationUs { get; set; } = DirectInputNative.INFINITE_DURATION;
        internal uint Gain { get; set; } = DirectInputNative.DI_FFNOMINALMAX;
        internal uint TriggerRepeatUs { get; set; } = 0;

        private IntPtr _typeParamsNative;
        private IntPtr _axisNative;
        private IntPtr _directionNative;
        private IntPtr _envelopeNative;

        internal EffectSlot(ILogger logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Ensures an effect of the given type exists; creates it if it doesn't.
        /// If the type differs from the existing effect, the old effect is unloaded and a new one created.
        /// </summary>
        internal void EnsureEffect(
            IDirectInputDevice8W device,
            Guid effectGuid,
            FfbEffectType effectType,
            uint durationUs,
            uint gain)
        {
            if (Effect is not null && EffectType == effectType)
            {
                return;
            }

            if (Effect is not null)
            {
                try
                {
                    Effect.Stop();
                    Effect.Unload();
                    Marshal.ReleaseComObject(Effect);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "EffectSlot: error unloading previous effect.");
                }
                Effect = null;
            }

            FreeNativeBuffers();

            // Allocate axis and direction arrays.
            _axisNative = Marshal.AllocHGlobal(Marshal.SizeOf<uint>());
            Marshal.WriteInt32(_axisNative, 0); // axis offset 0 = X axis

            _directionNative = Marshal.AllocHGlobal(Marshal.SizeOf<int>());
            Marshal.WriteInt32(_directionNative, 0);

            var diEffect = BuildDiEffect(durationUs, gain, 0, IntPtr.Zero);

            IntPtr effectPtr;
            int hr = device.CreateEffect(effectGuid, diEffect, out effectPtr, IntPtr.Zero);
            FreeAndClearDiEffect(diEffect);

            if (hr < 0 || effectPtr == IntPtr.Zero)
            {
                _logger.LogWarning("EffectSlot: CreateEffect for {Type} failed with HRESULT 0x{HR:X8}.", effectType, hr);
                FreeNativeBuffers();
                return;
            }

            Effect = (IDirectInputEffect)Marshal.GetObjectForIUnknown(effectPtr);
            Marshal.Release(effectPtr);
            EffectType = effectType;

            _logger.LogTrace("EffectSlot: created effect {Type}.", effectType);
        }

        /// <summary>
        /// Updates the type-specific parameters of an existing effect.
        /// </summary>
        internal void UpdateTypeSpecificParams<T>(T typeParams, IDirectInputEffect effect) where T : struct
        {
            if (_typeParamsNative != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(_typeParamsNative);
            }

            _typeParamsNative = Marshal.AllocHGlobal(Marshal.SizeOf<T>());
            Marshal.StructureToPtr(typeParams, _typeParamsNative, false);

            var diEffect = BuildDiEffect(DurationUs, Gain, (uint)Marshal.SizeOf<T>(), _typeParamsNative);
            int hr = effect.SetParameters(diEffect, DirectInputNative.DIEP_TYPESPECIFICPARAMS | DirectInputNative.DIEP_GAIN | DirectInputNative.DIEP_DURATION);
            FreeAndClearDiEffect(diEffect);

            if (hr < 0)
            {
                _logger.LogWarning("EffectSlot: SetParameters failed with HRESULT 0x{HR:X8}.", hr);
            }
        }

        /// <summary>
        /// Updates only the envelope parameters on an existing effect.
        /// </summary>
        internal void UpdateEnvelope(SetEnvelopeCommand envelope, IDirectInputEffect effect)
        {
            if (_envelopeNative == IntPtr.Zero)
            {
                _envelopeNative = Marshal.AllocHGlobal(Marshal.SizeOf<DiEnvelope>());
            }

            var diEnv = new DiEnvelope
            {
                dwSize         = (uint)Marshal.SizeOf<DiEnvelope>(),
                dwAttackLevel  = envelope.AttackLevel,
                dwAttackTime   = envelope.AttackTimeMs * 1000u,
                dwFadeLevel    = envelope.FadeLevel,
                dwFadeTime     = envelope.FadeTimeMs * 1000u,
            };
            Marshal.StructureToPtr(diEnv, _envelopeNative, false);

            var diEffect = BuildDiEffect(DurationUs, Gain, 0, IntPtr.Zero);

            // Manually set the envelope pointer in the native struct at the correct offset.
            // dwSize(4) + dwFlags(4) + dwDuration(4) + dwSamplePeriod(4) + dwGain(4) + dwTriggerButton(4) +
            // dwTriggerRepeatInterval(4) + cAxes(4) + rgdwAxes ptr(8) + rglDirection ptr(8) = offset 48
            Marshal.WriteIntPtr(diEffect, 48, _envelopeNative);

            int hr = effect.SetParameters(diEffect, DirectInputNative.DIEP_ENVELOPE);
            FreeAndClearDiEffect(diEffect);

            if (hr < 0)
            {
                _logger.LogWarning("EffectSlot: SetParameters (envelope) failed with HRESULT 0x{HR:X8}.", hr);
            }
        }

        private IntPtr BuildDiEffect(uint durationUs, uint gain, uint typeParamSize, IntPtr typeParamPtr)
        {
            IntPtr p = Marshal.AllocHGlobal(Marshal.SizeOf<DiEffect>());
            var diEffect = new DiEffect
            {
                dwSize                   = 80,
                dwFlags                  = DirectInputNative.DIEFF_CARTESIAN | DirectInputNative.DIEFF_OBJECTOFFSETS,
                dwDuration               = durationUs,
                dwSamplePeriod           = 0,
                dwGain                   = gain == 0 ? DirectInputNative.DI_FFNOMINALMAX : (uint)gain * 10000u / 255u,
                dwTriggerButton          = DirectInputNative.DIEB_NOTRIGGER,
                dwTriggerRepeatInterval  = TriggerRepeatUs,
                cAxes                    = 1,
                rgdwAxes                 = _axisNative,
                rglDirection             = _directionNative,
                lpEnvelope               = IntPtr.Zero,
                cbTypeSpecificParams     = typeParamSize,
                lpvTypeSpecificParams    = typeParamPtr,
                dwStartDelay             = 0,
            };
            Marshal.StructureToPtr(diEffect, p, false);
            return p;
        }

        private static void FreeAndClearDiEffect(IntPtr p)
        {
            if (p != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(p);
            }
        }

        private void FreeNativeBuffers()
        {
            if (_axisNative != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(_axisNative);
                _axisNative = IntPtr.Zero;
            }

            if (_directionNative != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(_directionNative);
                _directionNative = IntPtr.Zero;
            }

            if (_typeParamsNative != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(_typeParamsNative);
                _typeParamsNative = IntPtr.Zero;
            }

            if (_envelopeNative != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(_envelopeNative);
                _envelopeNative = IntPtr.Zero;
            }
        }

        public void Dispose()
        {
            if (Effect is not null)
            {
                try
                {
                    Effect.Stop();
                    Effect.Unload();
                    Marshal.ReleaseComObject(Effect);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "EffectSlot: error during dispose.");
                }
                Effect = null;
            }

            FreeNativeBuffers();
        }
    }
}
