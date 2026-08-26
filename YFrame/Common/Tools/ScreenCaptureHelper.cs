using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace YFrame
{
    /// <summary>
    /// 屏幕区域截图帮助类：
    /// 通过 GDI P/Invoke（BitBlt）截取屏幕指定区域，转换为 WPF BitmapSource 与 BGRA 像素数组，
    /// 供取色器在截图缩略图上点击取色使用。不依赖 System.Drawing.Common，零第三方依赖。
    /// </summary>
    public static class ScreenCaptureHelper
    {
        // ===== Win32 P/Invoke =====
        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int w, int h);

        [DllImport("gdi32.dll")]
        private static extern IntPtr SelectObject(IntPtr hdc, IntPtr h);

        [DllImport("gdi32.dll")]
        private static extern bool BitBlt(IntPtr hdcDest, int x, int y, int w, int h, IntPtr hdcSrc, int x1, int y1, int rop);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr h);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteDC(IntPtr hdc);

        // 光栅操作码：SRCCOPY（直接复制源像素）
        private const int SRCCOPY = 0x00CC0020;

        /// <summary>
        /// 截取屏幕指定区域（物理像素坐标）
        /// </summary>
        /// <param name="bounds">要截取的屏幕区域（物理像素坐标，X/Y 为左上角）</param>
        /// <returns>三元组：位图（WPF BitmapSource，BGRA32）、像素数据（BGRA 字节数组）、行字节数</returns>
        public static (BitmapSource? Image, byte[]? Pixels, int Stride) Capture(Rect bounds)
        {
            int x = (int)Math.Round(bounds.X);
            int y = (int)Math.Round(bounds.Y);
            int w = (int)Math.Round(bounds.Width);
            int h = (int)Math.Round(bounds.Height);
            if (w <= 0 || h <= 0)
                return (null, null, 0);

            IntPtr screenDC = GetDC(IntPtr.Zero);
            IntPtr memDC = CreateCompatibleDC(screenDC);
            IntPtr hBitmap = CreateCompatibleBitmap(screenDC, w, h);
            IntPtr oldBitmap = SelectObject(memDC, hBitmap);

            try
            {
                // 从屏幕 DC 复制选区到内存位图
                BitBlt(memDC, 0, 0, w, h, screenDC, x, y, SRCCOPY);

                // 转为 WPF 位图源
                var source = Imaging.CreateBitmapSourceFromHBitmap(
                    hBitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());

                // 统一转换为 BGRA32，保证像素字节顺序固定（B,G,R,A）
                var bgra = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
                bgra.Freeze();

                // 拷贝像素数组（供点击取色时按坐标读取颜色）
                int stride = w * 4;
                var pixels = new byte[stride * h];
                bgra.CopyPixels(pixels, stride, 0);

                return (bgra, pixels, stride);
            }
            finally
            {
                // 清理 GDI 对象
                SelectObject(memDC, oldBitmap);
                DeleteObject(hBitmap);
                DeleteDC(memDC);
                ReleaseDC(IntPtr.Zero, screenDC);
            }
        }
    }
}