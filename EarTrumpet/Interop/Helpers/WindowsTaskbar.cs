using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace EarTrumpet.Interop.Helpers
{
    public sealed class WindowsTaskbar
    {
        public struct State
        {
            public Position Location;
            public RECT Size;
            public Screen ContainingScreen;
            public bool IsAutoHideEnabled;

            public bool IsHorizontal
            {
                get { return Location == Position.Bottom || Location == Position.Top; }
            }
        }

        // Must match AppBarEdge enum
        public enum Position
        {
            Left = 0,
            Top = 1,
            Right = 2,
            Bottom = 3
        }

        [DllImport("user32.dll")]
        private static extern bool IsWindow(IntPtr hWnd);

        public static uint Dpi => User32.GetDpiForWindow(GetHwnd());

        // The taskbar the flyout should open next to. Defaults to the primary taskbar; when the flyout opens we pick the
        // taskbar on the monitor the mouse is on, so clicking the tray icon on a second monitor opens it there.
        private static IntPtr _targetHwnd;

        public static void UpdateTargetFromCursor()
        {
            var primary = GetHwnd();
            _targetHwnd = primary;
            try
            {
                var cursorScreen = Screen.FromPoint(Cursor.Position);
                if (primary != IntPtr.Zero && Screen.FromHandle(primary).Equals(cursorScreen))
                {
                    return;
                }

                // Taskbars on additional monitors are Shell_SecondaryTrayWnd windows.
                var hWnd = IntPtr.Zero;
                while ((hWnd = User32.FindWindowEx(IntPtr.Zero, hWnd, "Shell_SecondaryTrayWnd", IntPtr.Zero)) != IntPtr.Zero)
                {
                    if (Screen.FromHandle(hWnd).Equals(cursorScreen))
                    {
                        _targetHwnd = hWnd;
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"WindowsTaskbar UpdateTargetFromCursor: {ex}");
            }
        }

        private static IntPtr GetTargetHwnd()
        {
            return (_targetHwnd != IntPtr.Zero && IsWindow(_targetHwnd)) ? _targetHwnd : GetHwnd();
        }

        public static State Current
        {
            get
            {
                var hWnd = GetTargetHwnd();
                var isPrimary = hWnd == GetHwnd();
                var state = new State
                {
                    ContainingScreen = Screen.FromHandle(hWnd),
                };
                var appBarData = new APPBARDATA
                {
                    cbSize = (uint)Marshal.SizeOf(typeof(APPBARDATA)),
                    hWnd = hWnd
                };

                // SHAppBarMessage: Understands Taskbar auto-hide
                // state (the window is positioned across screens).
                // (GetTaskbarPos only ever describes the primary taskbar, so other monitors use the window rectangle.)
                if (isPrimary && Shell32.SHAppBarMessage(AppBarMessage.GetTaskbarPos, ref appBarData) != UIntPtr.Zero)
                {
                    state.Size = appBarData.rect;
                    state.Location = (Position)appBarData.uEdge;
                }
                else
                {
                    User32.GetWindowRect(hWnd, out state.Size);

                    if (state.ContainingScreen != null)
                    {
                        var screen = state.ContainingScreen;
                        if (state.Size.Bottom == screen.Bounds.Bottom && state.Size.Top == screen.Bounds.Top)
                        {
                            state.Location = (state.Size.Left == screen.Bounds.Left) ? Position.Left : Position.Right;
                        }
                        if (state.Size.Right == screen.Bounds.Right && state.Size.Left == screen.Bounds.Left)
                        {
                            state.Location = (state.Size.Top == screen.Bounds.Top) ? Position.Top : Position.Bottom;
                        }
                    }
                }

                var appBarState = (Shell32.AppBarState)Shell32.SHAppBarMessage(AppBarMessage.GetState, ref appBarData);
                state.IsAutoHideEnabled = appBarState.HasFlag(Shell32.AppBarState.ABS_AUTOHIDE);

                Trace.WriteLine($"WindowsTaskbar Current: Location={state.Location}, AutoHide={state.IsAutoHideEnabled}, Taskbar={hWnd}, Size={state.Size}, Monitor={state.ContainingScreen}");
                return state;
            }
        }

        public static IntPtr GetHwnd() => User32.FindWindow("Shell_TrayWnd", null);

        public static IntPtr GetTrayToolbarWindowHwnd()
        {
            IntPtr hWnd = GetHwnd();
            if (hWnd != IntPtr.Zero)
            {
                hWnd = User32.FindWindowEx(hWnd, IntPtr.Zero, "TrayNotifyWnd", IntPtr.Zero);
                if (hWnd != IntPtr.Zero)
                {
                    hWnd = User32.FindWindowEx(hWnd, IntPtr.Zero, "SysPager", IntPtr.Zero);
                    if (hWnd != IntPtr.Zero)
                    {
                        hWnd = User32.FindWindowEx(hWnd, IntPtr.Zero, "ToolbarWindow32", IntPtr.Zero);
                    }
                }
            }
            return hWnd;
        }
    }
}