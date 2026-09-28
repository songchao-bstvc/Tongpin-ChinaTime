using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace Tongpin
{
    // Use the original screen point so even coalesced/fast mouse moves retain their distance.
    public static class WindowDrag
    {
        public static void Attach(Window window, UIElement handle, Func<bool> allowed, Action<bool> busy, Action committed, Action clicked = null)
        {
            bool tracking = false, moved = false; Point start = new Point(); Native.RECT bounds = new Native.RECT();
            Action finish = () => {
                if (!tracking) return; tracking = false; busy(false);
                if (handle.IsMouseCaptured) handle.ReleaseMouseCapture();
                if (moved) committed();
            };
            handle.MouseLeftButtonDown += (sender, e) => {
                moved = false;
                if (!allowed()) return;
                start = window.PointToScreen(e.GetPosition(window));
                Native.GetWindowRect(new WindowInteropHelper(window).Handle, out bounds);
                tracking = true; busy(true); handle.CaptureMouse();
            };
            handle.MouseMove += (sender, e) => {
                if (!tracking || e.LeftButton != MouseButtonState.Pressed) return;
                var delta = window.PointToScreen(e.GetPosition(window)) - start;
                if (!moved && delta.Length < 6) return;
                moved = true;
                Native.SetWindowPos(new WindowInteropHelper(window).Handle, new IntPtr(-1), bounds.Left + (int)delta.X, bounds.Top + (int)delta.Y, 0, 0, 0x1 | 0x10);
            };
            handle.MouseLeftButtonUp += (sender, e) => { finish(); if (!moved) clicked?.Invoke(); };
            handle.LostMouseCapture += (sender, e) => finish();
        }
    }
}
