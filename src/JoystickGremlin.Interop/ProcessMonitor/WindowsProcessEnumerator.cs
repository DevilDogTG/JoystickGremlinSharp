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
                if (process.Id == ownPid)
                {
                    continue;
                }

                // Cheap filter first: only query the path for processes we'll actually return.
                if (!includeAll && process.MainWindowHandle == IntPtr.Zero)
                {
                    continue;
                }

                var path = QueryProcessPath(process.Id);
                if (string.IsNullOrEmpty(path))
                {
                    continue;
                }

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

    /// <summary>
    /// Returns <see cref="Process.ProcessName"/>, treating exited/inaccessible processes as
    /// empty strings so a single bad process can't tear down the whole enumeration.
    /// </summary>
    private static string SafeProcessName(Process process)
    {
        try { return process.ProcessName; }
        catch { return string.Empty; }
    }

    /// <summary>Returns the file-name segment of a forward-slash-normalized path.</summary>
    private static string GetFileName(string normalizedPath)
    {
        var slash = normalizedPath.LastIndexOf('/');
        return slash >= 0 ? normalizedPath[(slash + 1)..] : normalizedPath;
    }

    /// <summary>
    /// Resolves the full executable path for a PID via <c>QueryFullProcessImageName</c>.
    /// Uses <c>PROCESS_QUERY_LIMITED_INFORMATION</c> so the call works without elevation, and
    /// returns an empty string instead of throwing on processes the caller cannot open.
    /// </summary>
    private static string QueryProcessPath(int pid)
    {
        var handle = NativeMethods.OpenProcess(ProcessQueryLimitedInformation, false, pid);
        if (handle == IntPtr.Zero)
        {
            return string.Empty;
        }

        try
        {
            var buffer = new char[1024];
            var size = (uint)buffer.Length;
            if (!NativeMethods.QueryFullProcessImageName(handle, 0, buffer, ref size))
            {
                return string.Empty;
            }

            // Normalize path separators to forward slashes, consistent with WindowsProcessMonitor.
            return new string(buffer, 0, (int)size).Replace('\\', '/');
        }
        finally
        {
            NativeMethods.CloseHandle(handle);
        }
    }

    // ─── P/Invoke declarations ────────────────────────────────────────────────

    /// <summary>Win32 entry points used by <see cref="WindowsProcessEnumerator"/>.</summary>
    private static class NativeMethods
    {
        /// <summary>kernel32!OpenProcess — opens an existing local process object.</summary>
        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, int dwProcessId);

        /// <summary>kernel32!QueryFullProcessImageNameW — retrieves the full executable path for a process handle.</summary>
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool QueryFullProcessImageName(
            IntPtr hProcess,
            uint   dwFlags,
            char[] lpExeName,
            ref uint lpdwSize);

        /// <summary>kernel32!CloseHandle — closes an open object handle.</summary>
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool CloseHandle(IntPtr hObject);
    }
}
