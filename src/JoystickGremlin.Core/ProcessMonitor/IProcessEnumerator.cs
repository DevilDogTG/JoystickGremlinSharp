// SPDX-License-Identifier: GPL-3.0-only

namespace JoystickGremlin.Core.ProcessMonitor;

/// <summary>
/// Information about a running process, surfaced by the process picker.
/// </summary>
/// <param name="Pid">The process identifier.</param>
/// <param name="ProcessName">The OS process name (without extension), e.g. <c>"DCS"</c>.</param>
/// <param name="ExecutableName">The executable file name, e.g. <c>"DCS.exe"</c>.</param>
/// <param name="ExecutablePath">The full executable path (forward slashes), or empty if unavailable.</param>
/// <param name="WindowTitle">The main window title, or empty if none.</param>
/// <param name="LikelyGame">Whether the executable path looks like a game install (best-effort).</param>
public sealed record RunningProcessInfo(
    int Pid,
    string ProcessName,
    string ExecutableName,
    string ExecutablePath,
    string WindowTitle,
    bool LikelyGame);

/// <summary>
/// Enumerates running processes for the auto-load process picker.
/// </summary>
public interface IProcessEnumerator
{
    /// <summary>
    /// Returns the currently running processes.
    /// </summary>
    /// <param name="includeAll">
    /// When <c>false</c> (default), only user-facing processes with a visible main window are
    /// returned. When <c>true</c>, every process with a resolvable executable path is returned.
    /// </param>
    IReadOnlyList<RunningProcessInfo> GetUserProcesses(bool includeAll = false);
}
