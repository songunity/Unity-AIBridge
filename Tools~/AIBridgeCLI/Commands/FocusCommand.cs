using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace AIBridgeCLI.Commands
{
    /// <summary>
    /// Focus command - brings Unity Editor window to foreground.
    /// This is a CLI-only command that doesn't require Unity to process it.
    /// </summary>
    public static class FocusCommand
    {
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

        private const int SW_RESTORE = 9;
        private const int SW_SHOW = 5;

        /// <summary>
        /// Find and focus the Unity Editor window for the current project.
        /// </summary>
        /// <returns>Result object with success status and message</returns>
        public static FocusResult Execute()
        {
            try
            {
                var unityProcesses = Process.GetProcessesByName("Unity");

                if (unityProcesses.Length == 0)
                {
                    return new FocusResult
                    {
                        Success = false,
                        Error = "Unity Editor is not running."
                    };
                }

                // Find the Unity process with a main window
                Process targetProcess = null;
                foreach (var process in unityProcesses)
                {
                    if (process.MainWindowHandle != IntPtr.Zero)
                    {
                        targetProcess = process;
                        break;
                    }
                }

                if (targetProcess == null)
                {
                    return new FocusResult
                    {
                        Success = false,
                        Error = "Unity Editor window not found."
                    };
                }

                var hwnd = targetProcess.MainWindowHandle;
                var windowTitle = targetProcess.MainWindowTitle;

                // If window is minimized, restore it
                if (IsIconic(hwnd))
                {
                    ShowWindow(hwnd, SW_RESTORE);
                }

                // Use AttachThreadInput trick to reliably set foreground window
                var foregroundHwnd = GetForegroundWindow();
                var currentThreadId = GetCurrentThreadId();
                GetWindowThreadProcessId(foregroundHwnd, out _);
                var foregroundThreadId = GetWindowThreadProcessId(foregroundHwnd, out _);

                if (currentThreadId != foregroundThreadId)
                {
                    AttachThreadInput(currentThreadId, foregroundThreadId, true);
                }

                SetForegroundWindow(hwnd);
                ShowWindow(hwnd, SW_SHOW);

                if (currentThreadId != foregroundThreadId)
                {
                    AttachThreadInput(currentThreadId, foregroundThreadId, false);
                }

                return new FocusResult
                {
                    Success = true,
                    ProcessId = targetProcess.Id,
                    WindowTitle = windowTitle
                };
            }
            catch (Exception ex)
            {
                return new FocusResult
                {
                    Success = false,
                    Error = ex.Message
                };
            }
        }
    }

    public class FocusResult
    {
        public bool Success { get; set; }
        public int ProcessId { get; set; }
        public string WindowTitle { get; set; }
        public string Error { get; set; }
    }
}
