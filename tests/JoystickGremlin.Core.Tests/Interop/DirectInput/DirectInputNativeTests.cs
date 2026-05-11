// SPDX-License-Identifier: GPL-3.0-only

using JoystickGremlin.Interop.DirectInput;

namespace JoystickGremlin.Core.Tests.Interop.DirectInput;

public sealed class DirectInputNativeTests
{
    [Fact]
    public void IidIdirectInput8W_MatchesOfficialComInterfaceGuid()
    {
        DirectInputNative.IID_IDirectInput8W
            .Should().Be(new Guid("BF798031-483A-4DA2-AA99-5D64ED369700"));
    }
}
