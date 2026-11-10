using EarTrumpet.Extensions;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Text;

namespace EarTrumpet.Interop.Helpers
{
    public class IconHelper
    {
        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromPoint(System.Drawing.Point pt, uint dwFlags);

        [DllImport("shcore.dll")]
        private static extern int GetDpiForMonitor(IntPtr hMonitor, int dpiType, out uint dpiX, out uint dpiY);

        /// <summary>
        /// Highest effective DPI across all connected monitors. The shell scales a single tray HICON to
        /// every taskbar, so rendering for the sharpest monitor (and letting the shell scale down) avoids
        /// the blur caused by scaling a small icon up on a high-DPI second monitor.
        /// </summary>
        public static uint GetMaxMonitorDpi()
        {
            uint max = 96;
            try
            {
                foreach (var screen in System.Windows.Forms.Screen.AllScreens)
                {
                    var hMonitor = MonitorFromPoint(new System.Drawing.Point(screen.Bounds.Left + 1, screen.Bounds.Top + 1), 2 /*MONITOR_DEFAULTTONEAREST*/);
                    if (GetDpiForMonitor(hMonitor, 0 /*MDT_EFFECTIVE_DPI*/, out uint dpiX, out _) == 0 && dpiX > max)
                    {
                        max = dpiX;
                    }
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"IconHelper GetMaxMonitorDpi: {ex}");
            }
            return max;
        }

        /// <summary>
        /// Draws a Segoe Fluent Icons / Segoe MDL2 Assets glyph into a crisp, anti-aliased, transparent icon of the exact pixel size.
        /// </summary>
        public static Icon RenderGlyphIcon(string glyph, int size, Color color)
        {
            string fontName = null;
            using (var fonts = new InstalledFontCollection())
            {
                foreach (var family in fonts.Families)
                {
                    if (family.Name == "Segoe Fluent Icons") { fontName = family.Name; break; }
                    if (family.Name == "Segoe MDL2 Assets") { fontName = family.Name; }
                }
            }
            if (fontName == null) throw new InvalidOperationException("No Segoe icon font installed");

            using (var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb))
            {
                using (var g = Graphics.FromImage(bitmap))
                using (var font = new Font(fontName, size * 0.95f, FontStyle.Regular, GraphicsUnit.Pixel))
                using (var brush = new SolidBrush(color))
                using (var format = new StringFormat(StringFormat.GenericTypographic) { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                {
                    g.Clear(Color.Transparent);
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.TextRenderingHint = TextRenderingHint.AntiAlias;
                    g.DrawString(glyph, font, brush, new RectangleF(0, 0, size, size), format);
                }
                return Icon.FromHandle(bitmap.GetHicon()).AsDisposableIcon();
            }
        }

        public static Icon LoadIconForTaskbar(string path, uint dpi)
        {
            Icon icon = null;
            if (path.StartsWith("pack://"))
            {
                using (var stream = System.Windows.Application.GetResourceStream(new Uri(path)).Stream)
                {
                    icon = new Icon(stream, new Size(
                        User32.GetSystemMetricsForDpi(User32.SystemMetrics.SM_CXICON, dpi),
                        User32.GetSystemMetricsForDpi(User32.SystemMetrics.SM_CYICON, dpi)));
                }
            }
            else
            {
                var iconPath = new StringBuilder(path);
                int iconIndex = Shlwapi.PathParseIconLocationW(iconPath);
                icon = LoadIconResource(iconPath.ToString(), iconIndex,
                    User32.GetSystemMetricsForDpi(User32.SystemMetrics.SM_CXSMICON, dpi),
                    User32.GetSystemMetricsForDpi(User32.SystemMetrics.SM_CYSMICON, dpi));
            }
            Trace.WriteLine($"IconHelper LoadIconForTaskbar {path} {icon?.Width}x{icon?.Height}");
            return icon;
        }

        public static Icon LoadIconResource(string path, int iconOrdinal, int cx, int cy)
        {
            using (var hModule = Kernel32.LoadLibraryEx(path, IntPtr.Zero, Kernel32.LoadLibraryFlags.LOAD_LIBRARY_AS_DATAFILE | Kernel32.LoadLibraryFlags.LOAD_LIBRARY_AS_IMAGE_RESOURCE))
            {
                var groupResInfo = Kernel32.FindResourceW(hModule, new IntPtr(iconOrdinal), Kernel32.RT_GROUP_ICON);
                var groupResData = Kernel32.LockResource(Kernel32.LoadResource(hModule, groupResInfo));
                var iconId = User32.LookupIconIdFromDirectoryEx(groupResData, true, cx, cy, User32.LoadImageFlags.LR_DEFAULTCOLOR);

                var iconResInfo = Kernel32.FindResourceW(hModule, new IntPtr(iconId), Kernel32.RT_ICON);
                var iconResData = Kernel32.LockResource(Kernel32.LoadResource(hModule, iconResInfo));
                var iconResSize = Kernel32.SizeofResource(hModule, iconResInfo);
                var iconHandle = User32.CreateIconFromResourceEx(iconResData, iconResSize, true, User32.IconCursorVersion.Default, cx, cy, User32.LoadImageFlags.LR_DEFAULTCOLOR);
                return Icon.FromHandle(iconHandle).AsDisposableIcon();
            }
        }

        public static Icon ColorIcon(Icon originalIcon, double fillPercent, System.Windows.Media.Color newColor)
        {
            using (var bitmap = originalIcon.ToBitmap())
            {
                for (int y = 0; y < bitmap.Height; y++)
                {
                    for (int x = 0; x < bitmap.Width * fillPercent; x++)
                    {
                        var pixel = bitmap.GetPixel(x, y);

                        if (pixel.R > 220)
                        {
                            bitmap.SetPixel(x, y, Color.FromArgb(pixel.A, newColor.R, newColor.G, newColor.B));
                        }
                    }
                }

                return Icon.FromHandle(bitmap.GetHicon()).AsDisposableIcon();
            }
        }
    }
}
