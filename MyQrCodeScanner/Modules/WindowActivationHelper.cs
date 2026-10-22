using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace MyQrCodeScanner.Modules
{
    /// <summary>
    /// Restores the last active window from the tray. WPF Activate alone can
    /// be ignored by the Windows foreground lock, so restore and foreground
    /// activation are also performed through Win32.
    /// </summary>
    public static class WindowActivationHelper
    {
        private const uint SW_RESTORE = 9;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_SHOWWINDOW = 0x0040;
        private static readonly IntPtr HWND_TOP = IntPtr.Zero;
        private static readonly object SyncRoot = new object();
        private static Window lastActiveWindow;

        public static void Track(Window window)
        {
            if (window == null)
                return;

            window.Activated += (s, e) =>
            {
                lock (SyncRoot)
                    lastActiveWindow = window;
            };

            window.Closed += (s, e) =>
            {
                lock (SyncRoot)
                {
                    if (lastActiveWindow == window)
                        lastActiveWindow = null;
                }
            };
        }

        public static void ShowLastActiveWindow()
        {
            Window target;
            lock (SyncRoot)
                target = lastActiveWindow;

            if (target == null || !target.IsLoaded)
                target = FindFallbackWindow();

            if (target == null && Application.Current != null)
                target = Application.Current.MainWindow;

            ShowAndActivate(target);
        }

        public static void ShowAndActivate(Window window)
        {
            if (window == null)
                return;

            if (!window.Dispatcher.CheckAccess())
            {
                window.Dispatcher.BeginInvoke(new Action(() => ShowAndActivate(window)));
                return;
            }

            try
            {
                if (window.WindowState == WindowState.Minimized)
                    window.WindowState = WindowState.Normal;

                if (!window.IsVisible)
                    window.Show();

                window.Activate();
                window.Focus();

                IntPtr hwnd = new WindowInteropHelper(window).Handle;
                if (hwnd == IntPtr.Zero)
                    return;

                ShowWindow(hwnd, SW_RESTORE);
                SetWindowPos(hwnd, HWND_TOP, 0, 0, 0, 0,
                    SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
                BringWindowToTop(hwnd);
                ForceForeground(hwnd);

                // Let WPF finish restoring the window, then apply foreground
                // activation once more.
                window.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
                {
                    try
                    {
                        if (!window.IsVisible)
                            return;
                        window.Activate();
                        window.Focus();
                        ForceForeground(hwnd);
                    }
                    catch
                    {
                    }
                }));
            }
            catch
            {
            }
        }

        private static Window FindFallbackWindow()
        {
            if (Application.Current == null)
                return null;

            Window active = Application.Current.Windows.Cast<Window>()
                .FirstOrDefault(w => w.IsVisible && w.IsActive);
            if (active != null)
                return active;

            return Application.Current.Windows.Cast<Window>()
                .FirstOrDefault(w => w.IsVisible && w != Application.Current.MainWindow)
                ?? Application.Current.MainWindow;
        }

        private static void ForceForeground(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero)
                return;

            IntPtr foreground = GetForegroundWindow();
            if (foreground == hwnd)
                return;

            uint foregroundThread = GetWindowThreadProcessId(foreground, out _);
            uint currentThread = GetCurrentThreadId();
            bool attached = false;

            try
            {
                if (foregroundThread != 0 && foregroundThread != currentThread)
                    attached = AttachThreadInput(currentThread, foregroundThread, true);
                SetForegroundWindow(hwnd);
            }
            finally
            {
                if (attached)
                    AttachThreadInput(currentThread, foregroundThread, false);
            }
        }

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, uint command);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

        [DllImport("user32.dll")]
        private static extern bool BringWindowToTop(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

        [DllImport("user32.dll")]
        private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool attach);
    }
}
