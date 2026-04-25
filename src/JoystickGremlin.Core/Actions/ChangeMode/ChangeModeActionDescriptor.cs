// SPDX-License-Identifier: GPL-3.0-only

using System.Text.Json.Nodes;
using JoystickGremlin.Core.Events;
using JoystickGremlin.Core.Modes;
using Microsoft.Extensions.Logging;

namespace JoystickGremlin.Core.Actions.ChangeMode;

/// <summary>
/// Descriptor for the change-mode action.
/// Switches the active mode via <see cref="IModeManager"/> when a button is pressed.
/// <para>
/// Configuration keys:
/// <list type="bullet">
///   <item><c>targetMode</c> — name of the mode to switch to (required).</item>
/// </list>
/// </para>
/// </summary>
public sealed class ChangeModeActionDescriptor : IActionDescriptor
{
    private readonly IModeManager _modeManager;
    private readonly ILogger<ChangeModeActionDescriptor> _logger;

    /// <summary>Action tag registered in <see cref="IActionRegistry"/>.</summary>
    public const string ActionTag = "change-mode";

    /// <inheritdoc/>
    public string Tag => ActionTag;

    /// <inheritdoc/>
    public string Name => "Change Mode";

    /// <summary>
    /// Initializes a new instance of <see cref="ChangeModeActionDescriptor"/>.
    /// </summary>
    public ChangeModeActionDescriptor(IModeManager modeManager, ILogger<ChangeModeActionDescriptor> logger)
    {
        _modeManager = modeManager;
        _logger = logger;
    }

    /// <inheritdoc/>
    public IActionFunctor CreateFunctor(JsonObject? configuration)
    {
        var targetMode = configuration?["targetMode"]?.GetValue<string>() ?? string.Empty;
        return new ChangeModeFunctor(_modeManager, targetMode, _logger);
    }

    private sealed class ChangeModeFunctor : IActionFunctor
    {
        private readonly IModeManager _modeManager;
        private readonly string _targetMode;
        private readonly ILogger _logger;

        internal ChangeModeFunctor(IModeManager modeManager, string targetMode, ILogger logger)
        {
            _modeManager = modeManager;
            _targetMode = targetMode;
            _logger = logger;
        }

        public Task ExecuteAsync(InputEvent inputEvent, CancellationToken cancellationToken = default)
        {
            // Only switch on button press (value >= 0.5 = pressed).
            if (inputEvent.Value < 0.5) return Task.CompletedTask;

            if (string.IsNullOrWhiteSpace(_targetMode))
            {
                _logger.LogWarning("Change-mode action has no target mode configured.");
                return Task.CompletedTask;
            }

            try
            {
                _modeManager.SwitchTo(_targetMode);
                _logger.LogInformation("Mode switched to {Mode}", _targetMode);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Change-mode action failed switching to {Mode}", _targetMode);
            }

            return Task.CompletedTask;
        }
    }
}
