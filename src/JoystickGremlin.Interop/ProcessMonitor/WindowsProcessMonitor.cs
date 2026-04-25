// SPDX-License-Identifier: GPL-3.0-only

using System.Runtime.InteropServices;
using JoystickGremlin.Core.ProcessMonitor;
using Microsoft.Extensions.Logging;

namespace JoystickGremlin.Interop.ProcessMonitor;

/// <summary>
/// Windows implementation of <see cref="IProcessMonitor"/>.
/// Polls the foreground window via <c>GetForegroundWindow</c> and
/// <c>QueryFullProcessImageName</c> once per second, raising
/// <see cref="ForegroundProcessChanged"/> only when the active process changes.
/// </summary>
public sealed class WindowsProcessMonitor : IProcessMonitor
{
    private const uint ProcessQueryLimitedInformation = 0x1000;

    private readonly ILogger<WindowsProcessMonitor> _logger;
    private CancellationTokenSource? _cts;
    private int _currentPid;

    /// <inheritdoc/>
    public event EventHandler<string>? ForegroundProcessChanged;

    /// <summary>
    /// Initializes a new instance of <see cref="WindowsProcessMonitor"/>.
    /// </summary>
    public WindowsProcessMonitor(ILogger<WindowsProcessMonitor> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public void Start()
    {
        if (_cts is not null) return;
        _cts = new CancellationTokenSource();
        _ = PollAsync(_cts.Token);
        _logger.LogInformation("WindowsProcessMonitor started.");
    }

    /// <inheritdoc/>
    public void Stop()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        _logger.LogInformation("WindowsProcessMonitor stopped.");
    }

    /// <inheritdoc/>
    public void Dispose() => Stop();

    private async Task PollAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                var hwnd = NativeMethods.GetForegroundWindow();
                if (hwnd == IntPtr.Zero) continue;

                NativeMethods.GetWindowThreadProcessId(hwnd, out var pid);
                if (pid == _currentPid) continue;

                _currentPid = pid;

                var path = QueryProcessPath(pid);
                _logger.LogTrace("Foreground process changed: PID={Pid} Path={Path}", pid, path);
                ForegroundProcessChanged?.Invoke(this, path);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ProcessMonitor polling loop failed.");
        }
    }

    private static string QueryProcessPath(int pid)
    {
        var handle = NativeMethods.OpenProcess(ProcessQueryLimitedInformation, false, pid);
        if (handle == IntPtr.Zero) return string.Empty;

        try
        {
            var buffer = new char[1024];
            var size = (uint)buffer.Length;
            if (!NativeMethods.QueryFullProcessImageName(handle, 0, buffer, ref size))
                return string.Empty;

            // Normalize path separators to forward slashes, consistent with Python reference.
            return new string(buffer, 0, (int)size).Replace('\\', '/');
        }
        finally
        {
            NativeMethods.CloseHandle(handle);
        }
    }

    // ─── P/Invoke declarations ────────────────────────────────────────────────

    private static partial class NativeMethods
    {
        [DllImport("user32.dll")]
        internal static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        internal static extern uint GetWindowThreadProcessId(IntPtr hWnd, out int lpdwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, int dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool QueryFullProcessImageName(
            IntPtr hProcess,
            uint   dwFlags,
            char[] lpExeName,
            ref uint lpdwSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool CloseHandle(IntPtr hObject);
    }
}
