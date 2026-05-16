// SPDX-License-Identifier: GPL-3.0-only

using JoystickGremlin.Core.Actions.Keyboard;

namespace JoystickGremlin.Core.Tests.Actions;

public sealed class StaticKeyNameCatalogTests
{
    [Fact]
    public void AvailableKeys_ContainsAllArrowKeys()
    {
        var catalog = new StaticKeyNameCatalog();

        catalog.AvailableKeys.Should().Contain(["Up", "Down", "Left", "Right"]);
    }

    [Fact]
    public void AvailableKeys_ContainsCommonModifiers()
    {
        var catalog = new StaticKeyNameCatalog();

        catalog.AvailableKeys.Should().Contain(["LShift", "RShift", "LControl", "RControl", "LAlt", "RAlt"]);
    }

    [Fact]
    public void AvailableKeys_IsDistinctAndSortedCaseInsensitive()
    {
        var catalog = new StaticKeyNameCatalog();
        var keys = catalog.AvailableKeys;

        keys.Distinct(StringComparer.OrdinalIgnoreCase).Count().Should().Be(keys.Count);
        keys.Should().BeInAscendingOrder((a, b) => StringComparer.OrdinalIgnoreCase.Compare(a, b));
    }

    [Fact]
    public void AvailableKeys_IncludesAlphabetDigitsAndFunctionKeys()
    {
        var catalog = new StaticKeyNameCatalog();

        catalog.AvailableKeys.Should().Contain(["A", "Z", "D0", "D9", "F1", "F12"]);
    }
}
