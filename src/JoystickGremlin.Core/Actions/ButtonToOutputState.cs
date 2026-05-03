// SPDX-License-Identifier: GPL-3.0-only

namespace JoystickGremlin.Core.Actions;

/// <summary>
/// Enum representing the four cardinal directional buttons.
/// </summary>
public enum DirectionalInput
{
    /// <summary>Up button (typically hat/D-Pad up).</summary>
    Up = 0,

    /// <summary>Down button.</summary>
    Down = 1,

    /// <summary>Left button.</summary>
    Left = 2,

    /// <summary>Right button.</summary>
    Right = 3
}

/// <summary>
/// Represents the current pressed state of four directional buttons (Up/Down/Left/Right).
/// Uses bit flags internally for efficient state tracking.
/// </summary>
[Flags]
public enum DirectionalButtonState
{
    /// <summary>No buttons pressed.</summary>
    None = 0,

    /// <summary>Up button pressed.</summary>
    Up = 1 << 0,

    /// <summary>Down button pressed.</summary>
    Down = 1 << 1,

    /// <summary>Left button pressed.</summary>
    Left = 1 << 2,

    /// <summary>Right button pressed.</summary>
    Right = 1 << 3,

    /// <summary>All buttons pressed (bitmask for combinations).</summary>
    All = Up | Down | Left | Right
}

/// <summary>
/// Provides helper methods for directional button state management and output calculation.
/// </summary>
public static class ButtonToOutputState
{
    /// <summary>
    /// Updates the directional button state based on a button press or release event.
    /// </summary>
    /// <param name="currentState">The current button state.</param>
    /// <param name="input">The directional input (Up, Down, Left, Right).</param>
    /// <param name="isPressed">True if the button is pressed, false if released.</param>
    /// <returns>The updated state.</returns>
    public static DirectionalButtonState UpdateState(
        DirectionalButtonState currentState,
        DirectionalInput input,
        bool isPressed)
    {
        var flag = (DirectionalButtonState)(1 << (int)input);
        return isPressed ? (currentState | flag) : (currentState & ~flag);
    }

    /// <summary>
    /// Calculates the Hat/POV direction (in degrees) based on the current button state.
    /// </summary>
    /// <remarks>
    /// Hat direction follows the convention:
    /// - 0° = Up
    /// - 90° = Right
    /// - 180° = Down
    /// - 270° = Left
    /// - -1 = Center (no buttons pressed or invalid combination)
    /// 
    /// Simultaneous cardinal presses (e.g., Up+Left) produce diagonal directions.
    /// </remarks>
    /// <param name="state">The current directional button state.</param>
    /// <returns>Hat direction in degrees (0–35999) or -1 for center/neutral.</returns>
    public static int CalculateHatDegrees(DirectionalButtonState state)
    {
        return state switch
        {
            DirectionalButtonState.None => -1,

            DirectionalButtonState.Up => 0,
            DirectionalButtonState.Right => 9000,
            DirectionalButtonState.Down => 18000,
            DirectionalButtonState.Left => 27000,

            // Diagonals
            DirectionalButtonState.Up | DirectionalButtonState.Right => 4500,
            DirectionalButtonState.Right | DirectionalButtonState.Down => 13500,
            DirectionalButtonState.Down | DirectionalButtonState.Left => 22500,
            DirectionalButtonState.Left | DirectionalButtonState.Up => 31500,

            // Three directions (invalid but center for safety)
            _ when state == (DirectionalButtonState.Up | DirectionalButtonState.Right | DirectionalButtonState.Down) => -1,
            _ when state == (DirectionalButtonState.Up | DirectionalButtonState.Right | DirectionalButtonState.Left) => -1,
            _ when state == (DirectionalButtonState.Up | DirectionalButtonState.Down | DirectionalButtonState.Left) => -1,
            _ when state == (DirectionalButtonState.Right | DirectionalButtonState.Down | DirectionalButtonState.Left) => -1,

            // All four directions (center)
            DirectionalButtonState.All => -1,

            // Opposite directions (center)
            _ when state == (DirectionalButtonState.Up | DirectionalButtonState.Down) => -1,
            _ when state == (DirectionalButtonState.Left | DirectionalButtonState.Right) => -1,

            // Fallback: any unknown combination centers
            _ => -1
        };
    }

    /// <summary>
    /// Calculates the axis value for a single axis based on directional button state.
    /// Supports X-axis (Left/Right) and Y-axis (Up/Down) calculations.
    /// </summary>
    /// <remarks>
    /// Y-axis (vertical):
    /// - Up pressed: +1.0
    /// - Down pressed: -1.0
    /// - Both or neither: 0.0
    ///
    /// X-axis (horizontal):
    /// - Right pressed: +1.0
    /// - Left pressed: -1.0
    /// - Both or neither: 0.0
    /// </remarks>
    /// <param name="state">The current directional button state.</param>
    /// <param name="isYAxis">True to calculate Y-axis (Up/Down), false for X-axis (Left/Right).</param>
    /// <returns>Axis value in range [-1.0, 1.0].</returns>
    public static double CalculateAxisValue(DirectionalButtonState state, bool isYAxis)
    {
        if (isYAxis)
        {
            bool hasUp = (state & DirectionalButtonState.Up) != 0;
            bool hasDown = (state & DirectionalButtonState.Down) != 0;

            if (hasUp && hasDown)
                return 0.0;
            return hasUp ? 1.0 : (hasDown ? -1.0 : 0.0);
        }
        else
        {
            bool hasLeft = (state & DirectionalButtonState.Left) != 0;
            bool hasRight = (state & DirectionalButtonState.Right) != 0;

            if (hasLeft && hasRight)
                return 0.0;
            return hasRight ? 1.0 : (hasLeft ? -1.0 : 0.0);
        }
    }
}
