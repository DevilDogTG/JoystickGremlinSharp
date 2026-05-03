// SPDX-License-Identifier: GPL-3.0-only

using FluentAssertions;
using JoystickGremlin.Core.ForceFeedback;
using Xunit;

namespace JoystickGremlin.Core.Tests.ForceFeedback;

public sealed class FfbCommandTests
{
    // ── DeviceControlCommand / DeviceGainCommand always have EBI=0 ────────────

    [Fact]
    public void DeviceControlCommand_AlwaysHasEffectBlockIndexZero()
    {
        var cmd = new DeviceControlCommand(FfbDeviceCommand.StopAll);

        cmd.EffectBlockIndex.Should().Be(0);
    }

    [Fact]
    public void DeviceGainCommand_AlwaysHasEffectBlockIndexZero()
    {
        var cmd = new DeviceGainCommand(128);

        cmd.EffectBlockIndex.Should().Be(0);
        cmd.Gain.Should().Be(128);
    }

    // ── Record equality ───────────────────────────────────────────────────────

    [Fact]
    public void SetConstantForceCommand_EqualityBasedOnValues()
    {
        var a = new SetConstantForceCommand(1, 5000);
        var b = new SetConstantForceCommand(1, 5000);
        var c = new SetConstantForceCommand(1, -5000);

        a.Should().Be(b);
        a.Should().NotBe(c);
    }

    [Fact]
    public void SetRampForceCommand_EqualityBasedOnValues()
    {
        var a = new SetRampForceCommand(2, 1000, -1000);
        var b = new SetRampForceCommand(2, 1000, -1000);
        var c = new SetRampForceCommand(2, 1000, 0);

        a.Should().Be(b);
        a.Should().NotBe(c);
    }

    [Fact]
    public void SetPeriodicCommand_EqualityBasedOnValues()
    {
        var a = new SetPeriodicCommand(3, 8000, 0, 0, 10000);
        var b = new SetPeriodicCommand(3, 8000, 0, 0, 10000);
        var c = new SetPeriodicCommand(3, 8000, 0, 0, 20000);

        a.Should().Be(b);
        a.Should().NotBe(c);
    }

    [Fact]
    public void SetConditionCommand_EqualityBasedOnValues()
    {
        var a = new SetConditionCommand(4, false, 0, 5000, 5000, 10000, 10000, 0);
        var b = new SetConditionCommand(4, false, 0, 5000, 5000, 10000, 10000, 0);
        var c = new SetConditionCommand(4, true, 0, 5000, 5000, 10000, 10000, 0);

        a.Should().Be(b);
        a.Should().NotBe(c);
    }

    [Fact]
    public void SetEnvelopeCommand_EqualityBasedOnValues()
    {
        var a = new SetEnvelopeCommand(5, 0, 0, 500, 500);
        var b = new SetEnvelopeCommand(5, 0, 0, 500, 500);
        var c = new SetEnvelopeCommand(5, 1000, 0, 500, 500);

        a.Should().Be(b);
        a.Should().NotBe(c);
    }

    [Fact]
    public void EffectOperationCommand_EqualityBasedOnValues()
    {
        var a = new EffectOperationCommand(1, FfbOperation.Start, 1);
        var b = new EffectOperationCommand(1, FfbOperation.Start, 1);
        var c = new EffectOperationCommand(1, FfbOperation.Stop, 1);

        a.Should().Be(b);
        a.Should().NotBe(c);
    }

    [Fact]
    public void DeviceControlCommand_EqualityBasedOnValues()
    {
        var a = new DeviceControlCommand(FfbDeviceCommand.StopAll);
        var b = new DeviceControlCommand(FfbDeviceCommand.StopAll);
        var c = new DeviceControlCommand(FfbDeviceCommand.Reset);

        a.Should().Be(b);
        a.Should().NotBe(c);
    }

    // ── EffectBlockIndex is set correctly on all commands ─────────────────────

    [Theory]
    [MemberData(nameof(GetCommandsWithExpectedEbi))]
    public void Command_HasExpectedEffectBlockIndex(FfbCommand command, byte expectedEbi)
    {
        command.EffectBlockIndex.Should().Be(expectedEbi);
    }

    public static TheoryData<FfbCommand, byte> GetCommandsWithExpectedEbi()
    {
        return new TheoryData<FfbCommand, byte>
        {
            {
                new SetEffectReportCommand(1, FfbEffectType.ConstantForce, 1000, 0, 0, 0, 255, 0, true, false, 90, 0),
                1
            },
            {
                new SetConstantForceCommand(2, 5000),
                2
            },
            {
                new SetRampForceCommand(3, 1000, -1000),
                3
            },
            {
                new SetPeriodicCommand(4, 8000, 0, 0, 10000),
                4
            },
            {
                new SetConditionCommand(5, false, 0, 5000, 5000, 10000, 10000, 0),
                5
            },
            {
                new SetEnvelopeCommand(6, 0, 0, 100, 100),
                6
            },
            {
                new EffectOperationCommand(7, FfbOperation.Start, 1),
                7
            },
            {
                new DeviceControlCommand(FfbDeviceCommand.StopAll),
                0
            },
            {
                new DeviceGainCommand(200),
                0
            },
        };
    }

    // ── SetEffectReportCommand carries effect type correctly ──────────────────

    [Theory]
    [MemberData(nameof(GetEffectTypes))]
    public void SetEffectReportCommand_StoresEffectType(FfbEffectType type)
    {
        var cmd = new SetEffectReportCommand(1, type, 1000, 0, 0, 0, 255, 0, true, false, 0, 0);

        cmd.EffectType.Should().Be(type);
    }

    public static TheoryData<FfbEffectType> GetEffectTypes()
    {
        var data = new TheoryData<FfbEffectType>();
        foreach (FfbEffectType val in Enum.GetValues<FfbEffectType>())
        {
            data.Add(val);
        }
        return data;
    }

    // ── DeviceControlCommand carries correct control value ────────────────────

    [Theory]
    [MemberData(nameof(GetDeviceCommands))]
    public void DeviceControlCommand_StoresControlValue(FfbDeviceCommand control)
    {
        var cmd = new DeviceControlCommand(control);

        cmd.Control.Should().Be(control);
        cmd.EffectBlockIndex.Should().Be(0);
    }

    public static TheoryData<FfbDeviceCommand> GetDeviceCommands()
    {
        var data = new TheoryData<FfbDeviceCommand>();
        foreach (FfbDeviceCommand val in Enum.GetValues<FfbDeviceCommand>())
        {
            data.Add(val);
        }
        return data;
    }
}
