// SPDX-License-Identifier: GPL-3.0-only

namespace JoystickGremlin.Core.ProcessMonitor;

/// <summary>
/// No-op implementation of <see cref="IProcessEnumerator"/> used in tests and non-Windows builds.
/// Always returns an empty list.
/// </summary>
public sealed class NullProcessEnumerator : IProcessEnumerator
{
    /// <inheritdoc/>
    public IReadOnlyList<RunningProcessInfo> GetUserProcesses(bool includeAll = false) => [];
}
