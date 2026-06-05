// SPDX-License-Identifier: GPL-3.0-only

namespace JoystickGremlin.Core.Update;

/// <summary>
/// Checks whether a newer application release is available.
/// </summary>
public interface IUpdateChecker
{
    /// <summary>
    /// Queries the release source for the latest published version and compares it
    /// against the running application version. Never throws on network or parsing
    /// failures — those surface as <see cref="UpdateCheckStatus.Failed"/> results.
    /// Caller-initiated cancellation propagates as <see cref="OperationCanceledException"/>.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the check.</param>
    /// <returns>The outcome of the check.</returns>
    Task<UpdateCheckResult> CheckForUpdateAsync(CancellationToken cancellationToken = default);
}
