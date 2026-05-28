// SPDX-License-Identifier: GPL-3.0-only

using System.Diagnostics;
using System.Runtime.InteropServices;
using JoystickGremlin.Core.ProcessMonitor;
using Microsoft.Extensions.Logging;

namespace JoystickGremlin.Interop.ProcessMonitor;

/// <summary>
/// Windows implementation of <see cref="IProcessEnumerator"/>.
/// Lists running processes via <see cref="Process.GetProcesses()"/>, resolving each executable
/// path with <c>QueryFullProcessImageName</c> (low-privilege, so it does not throw on processes
/// the caller cannot fully open). User-facing processes (those with a visible main window) are
/// returned by default; likely games are flagged and sorted first.
/// </summary>
public sealed class WindowsProcessEnumerator : IProcessEnumerator
{
    private const uint ProcessQueryLimitedInformation = 0x1000;

    private readonly ILogger<WindowsProcessEnumerator> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="WindowsProcessEnumerator"/>.
    /// </summary>
    public WindowsProcessEnumerator(ILogger<WindowsProcessEnumerator> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public IReadOnlyList<RunningProcessInfo> GetUserProcesses(bool includeAll = false)
    {
        var ownPid = Environment.ProcessId;
        var byPath = new Dictionary<string, RunningProcessInfo>(StringComparer.OrdinalIgnoreCase);

        foreach (var process in Process.GetProcesses())
        {
            try
            {
                if (process.Id == ownPid) continue;

                // Cheap filter first: only query the path for processes we'll actually return.
                if (!includeAll && process.MainWindowHandle == IntPtr.Zero) continue;

                var path = QueryProcessPath(process.Id);
                if (string.IsNullOrEmpty(path)) continue;

                var exeName = GetFileName(path);
                string windowTitle;
                try { windowTitle = process.MainWindowTitle ?? string.Empty; }
                catch { windowTitle = string.Empty; }

                var info = new RunningProcessInfo(
                    Pid:            process.Id,
                    ProcessName:    SafeProcessName(process),
                    ExecutableName: exeName,
                    ExecutablePath: path,
                    WindowTitle:    windowTitle,
                    LikelyGame:     GameHeuristics.IsLikelyGame(path));

                // Collapse multiple instances of the same executable into one entry,
                // preferring the instance that has a window title.
                if (!byPath.TryGetValue(path, out var existing)
                    || (string.IsNullOrEmpty(existing.WindowTitle) && !string.IsNullOrEmpty(windowTitle)))
                {
                    byPath[path] = info;
                }
            }
            catch (Exception ex)
            {
                // Process may have exited or be inaccessible — skip it.
                _logger.LogTrace(ex, "Skipping process during enumeration.");
            }
            finally
            {
                process.Dispose();
            }
        }

        return byPath.Values
            .OrderByDescending(p => p.LikelyGame)
            .ThenBy(p => string.IsNullOrEmpty(p.WindowTitle) ? p.ExecutableName : p.WindowTitle,
                    StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string SafeProcessName(Process process)
    {
        try { return process.ProcessName; }
        catch { return string.Empty; }
    }

    private static string GetFileName(string normalizedPath)
    {
        var slash = normalizedPath.LastIndexOf('/');
        return slash >= 0 ? normalizedPath[(slash + 1)..] : normalizedPath;
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

            // Normalize path separators to forward slashes, consistent with WindowsProcessMonitor.
            return new string(buffer, 0, (int)size).Replace('\\', '/');
        }
        finally
        {
            NativeMethods.CloseHandle(handle);
        }
    }

    // ─── P/Invoke declarations ────────────────────────────────────────────────

    private static class NativeMethods
    {
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
