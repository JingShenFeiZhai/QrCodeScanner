using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace MyQrCodeScanner.Modules
{
    /// <summary>
    /// WPF Topmost is not always enough for Store-packaged apps such as
    /// QuickLook. Re-assert topmost after the window is shown and, when
    /// QuickLook is running, place the popup directly above its window.
    /// </summary>
    public static class TopmostHelper
    {
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TOPMOST = 0x00000008;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_FRAMECHANGED = 0x0020;
        private const uint SWP_NOOWNERZORDER = 0x0200;
        private const uint GW_HWNDPREV = 3;
        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private static readonly Dictionary<Window, State> states = new Dictionary<Window, State>();

        private sealed class State
        {
            public int Count;
            public bool PreviousTopmost;
            public DispatcherTimer Timer;
            public EventHandler ClosedHandler;
        }

        public static void Attach(Window window)
        {
            if (window == null)
                return;

            lock (states)
            {
                State state;
                if (!states.TryGetValue(window, out state))
                {
                    state = new State
                    {
                        PreviousTopmost = window.Topmost,
                        Timer = new DispatcherTimer(DispatcherPriority.Background, window.Dispatcher)
                        {
                            Interval = TimeSpan.FromMilliseconds(700)
                        }
                    };
                    state.Timer.Tick += (s, e) => Apply(window);
                    state.ClosedHandler = (s, e) => DetachAll(window);
                    window.Closed += state.ClosedHandler;
                    states[window] = state;
                }

                state.Count++;
                window.Topmost = true;
                Apply(window);
                if (!state.Timer.IsEnabled)
                    state.Timer.Start();
            }
        }

        public static void Detach(Window window)
        {
            if (window == null)
                return;

            lock (states)
            {
                State state;
                if (!states.TryGetValue(window, out state))
                    return;

                state.Count--;
                if (state.Count > 0)
                    return;

                state.Timer.Stop();
                states.Remove(window);
                window.Closed -= state.ClosedHandler;
                try
                {
                    window.Topmost = state.PreviousTopmost;
                }
                catch
                {
                    // The window may already be closed.
                }
            }
        }

        private static void DetachAll(Window window)
        {
            lock (states)
            {
                State state;
                if (!states.TryGetValue(window, out state))
                    return;

                state.Timer.Stop();
                states.Remove(window);
            }
        }

        private static void Apply(Window window)
        {
            if (window == null || !window.IsVisible)
                return;

            IntPtr hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero)
                return;

            int style = GetWindowLong(hwnd, GWL_EXSTYLE);
            SetWindowLong(hwnd, GWL_EXSTYLE, style | WS_EX_TOPMOST);

            IntPtr quickLook = FindQuickLookWindow();
            uint flags = SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_NOOWNERZORDER;

            // Set topmost first. To place this window above QuickLook, insert
            // it after the window immediately above QuickLook; using QuickLook
            // itself as hWndInsertAfter would put this window below it.
            SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, flags | SWP_FRAMECHANGED);
            if (quickLook != IntPtr.Zero)
            {
                IntPtr aboveQuickLook = GetWindow(quickLook, GW_HWNDPREV);
                SetWindowPos(hwnd, aboveQuickLook != IntPtr.Zero ? aboveQuickLook : HWND_TOPMOST,
                    0, 0, 0, 0, flags);
            }
        }

        private static IntPtr FindQuickLookWindow()
        {
            HashSet<int> processIds = new HashSet<int>();
            foreach (Process process in Process.GetProcessesByName("QuickLook"))
            {
                try
                {
                    if (!process.HasExited)
                        processIds.Add(process.Id);
                }
                catch
                {
                }
                finally
                {
                    process.Dispose();
                }
            }

            if (processIds.Count == 0)
                return IntPtr.Zero;

            IntPtr foreground = GetForegroundWindow();
            uint foregroundPid;
            GetWindowThreadProcessId(foreground, out foregroundPid);
            if (processIds.Contains((int)foregroundPid))
                return foreground;

            IntPtr best = IntPtr.Zero;
            long bestArea = 0;
            EnumWindows((hwnd, lParam) =>
            {
                uint pid;
                GetWindowThreadProcessId(hwnd, out pid);
                if (!processIds.Contains((int)pid) || !IsWindowVisible(hwnd))
                    return true;

                RECT rect;
                if (!GetWindowRect(hwnd, out rect))
                    return true;

                long width = rect.Right - rect.Left;
                long height = rect.Bottom - rect.Top;
                long area = width > 0 && height > 0 ? width * height : 0;
                if (area > bestArea)
                {
                    bestArea = area;
                    best = hwnd;
                }
                return true;
            }, IntPtr.Zero);

            return best;
        }

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindow(IntPtr hWnd, uint command);

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int index);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int index, int value);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }
    }
}
