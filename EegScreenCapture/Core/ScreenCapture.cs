using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace EegScreenCapture.Core
{
    /// <summary>
    /// Captures screenshots of specific screen regions
    /// </summary>
    public class ScreenCapture
    {
        [DllImport("user32.dll")]
        private static extern IntPtr GetDesktopWindow();

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindowDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern Int32 ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("gdi32.dll")]
        private static extern bool BitBlt(IntPtr hObject, int nXDest, int nYDest,
            int nWidth, int nHeight, IntPtr hObjectSource,
            int nXSrc, int nYSrc, int dwRop);

        private const int SRCCOPY = 0x00CC0020;

        /// <summary>
        /// Capture a specific region of the screen
        /// </summary>
        public static Bitmap CaptureRegion(Rectangle region)
        {
            var bitmap = new Bitmap(region.Width, region.Height, PixelFormat.Format24bppRgb);

            using (var graphics = Graphics.FromImage(bitmap))
            {
                var hdcDest = graphics.GetHdc();
                var hdcSrc = GetWindowDC(GetDesktopWindow());

                BitBlt(hdcDest, 0, 0, region.Width, region.Height,
                       hdcSrc, region.X, region.Y, SRCCOPY);

                graphics.ReleaseHdc(hdcDest);
                ReleaseDC(GetDesktopWindow(), hdcSrc);
            }

            return bitmap;
        }

        /// <summary>
        /// Capture the entire primary screen
        /// </summary>
        public static Bitmap CaptureFullScreen()
        {
            // Get primary screen dimensions using Win32
            int width = GetSystemMetrics(0); // SM_CXSCREEN
            int height = GetSystemMetrics(1); // SM_CYSCREEN
            var bounds = new Rectangle(0, 0, width, height);
            return CaptureRegion(bounds);
        }

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);
    }
}
