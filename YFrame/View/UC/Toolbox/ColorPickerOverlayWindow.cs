using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace YFrame.View.UC.Toolbox
{
    /// <summary>
    /// 全屏半透明区域选框覆盖窗（参考截图翻译插件的框选交互）：
    /// 覆盖主屏，半透明遮罩 + 鼠标拖拽绘制红色选择框，
    /// 松开鼠标后回调选区（物理像素坐标），ESC 取消。
    /// 用于取色器框选截图区域。
    /// </summary>
    public class ColorPickerOverlayWindow : Window
    {
        // ===== Win32 P/Invoke（强制全屏置顶，避免 Maximized + AllowsTransparency 兼容问题） =====
        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        // 选区绘制画布（显式 Transparent 背景确保透明区域可命中鼠标事件）
        private readonly Canvas _canvas = new() { Background = Brushes.Transparent };

        // 正在绘制的选择框
        private Rectangle? _selectionRect;

        // 拖拽起点（画布逻辑坐标）
        private Point _startPoint;

        // 选区完成回调（物理像素坐标 Rect）
        private readonly Action<Rect> _onRegionSelected;

        // 取消回调
        private readonly Action _onCancelled;

        /// <summary>
        /// 创建并展示选区覆盖窗
        /// </summary>
        /// <param name="onRegionSelected">框选完成回调（物理像素坐标 Rect）</param>
        /// <param name="onCancelled">按 ESC 取消回调</param>
        public ColorPickerOverlayWindow(Action<Rect> onRegionSelected, Action onCancelled)
        {
            _onRegionSelected = onRegionSelected;
            _onCancelled = onCancelled;

            // 覆盖主屏（全屏半透明遮罩）
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = new SolidColorBrush(Color.FromArgb(110, 0, 0, 0));
            Topmost = true;
            ShowInTaskbar = false;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = 0;
            Top = 0;
            Width = SystemParameters.PrimaryScreenWidth;
            Height = SystemParameters.PrimaryScreenHeight;
            Cursor = Cursors.Cross;

            // 顶部操作提示
            var hint = new TextBlock
            {
                Text = Application.Current?.TryFindResource("key_Toolbox_OverlayHint") as string
                       ?? "拖拽框选截图区域 · ESC 取消",
                FontSize = 14,
                Foreground = Brushes.White,
                Background = new SolidColorBrush(Color.FromArgb(180, 30, 30, 30)),
                Padding = new Thickness(14, 7, 14, 7),
                Margin = new Thickness(16, 14, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top
            };
            _canvas.Children.Add(hint);

            Content = _canvas;

            // 键盘：ESC 取消
            PreviewKeyDown += OnPreviewKeyDown;
        }

        /// <summary>
        /// 窗口显示后强制置顶全屏（按实际物理分辨率，自动适配 2K/4K 等高 DPI 屏幕）
        /// </summary>
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            // 按当前窗口的实际 DPI 将逻辑分辨率换算为物理像素，确保覆盖整个屏幕
            var dpi = VisualTreeHelper.GetDpi(this);
            int physWidth = (int)Math.Round(SystemParameters.PrimaryScreenWidth * dpi.DpiScaleX);
            int physHeight = (int)Math.Round(SystemParameters.PrimaryScreenHeight * dpi.DpiScaleY);

            // HWND_TOPMOST = -1，SWP_SHOWWINDOW = 0x0040
            SetWindowPos(new System.Windows.Interop.WindowInteropHelper(this).Handle,
                new IntPtr(-1), 0, 0, physWidth, physHeight, 0x0040);

            // 订阅鼠标拖拽事件
            _canvas.MouseLeftButtonDown += OnMouseDown;
            _canvas.MouseMove += OnMouseMove;
            _canvas.MouseLeftButtonUp += OnMouseUp;
        }

        /// <summary>
        /// 鼠标按下：记录起点并创建选择框
        /// </summary>
        private void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            _startPoint = e.GetPosition(_canvas);

            _selectionRect = new Rectangle
            {
                Stroke = Brushes.Red,
                StrokeThickness = 2,
                Fill = new SolidColorBrush(Color.FromArgb(20, 255, 0, 0))
            };
            Canvas.SetLeft(_selectionRect, _startPoint.X);
            Canvas.SetTop(_selectionRect, _startPoint.Y);
            _canvas.Children.Add(_selectionRect);
        }

        /// <summary>
        /// 鼠标移动：实时更新选择框位置与大小
        /// </summary>
        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (_selectionRect == null) return;

            var current = e.GetPosition(_canvas);
            Canvas.SetLeft(_selectionRect, Math.Min(_startPoint.X, current.X));
            Canvas.SetTop(_selectionRect, Math.Min(_startPoint.Y, current.Y));
            _selectionRect.Width = Math.Abs(current.X - _startPoint.X);
            _selectionRect.Height = Math.Abs(current.Y - _startPoint.Y);
        }

        /// <summary>
        /// 鼠标松开：换算物理坐标并回调选区
        /// </summary>
        private void OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_selectionRect == null) return;

            var current = e.GetPosition(_canvas);
            double left = Math.Min(_startPoint.X, current.X);
            double top = Math.Min(_startPoint.Y, current.Y);
            double width = Math.Abs(current.X - _startPoint.X);
            double height = Math.Abs(current.Y - _startPoint.Y);

            // 逻辑坐标 → 物理像素坐标：使用用户配置的屏幕缩放因子（Config.ScreenScale，默认 125）
            // 与截图翻译插件一致，可在线编辑下拉框调整（100-200）
            double factor = GetScreenScaleFactor();
            var bounds = new Rect(left * factor, top * factor, width * factor, height * factor);

            Close();
            _onRegionSelected?.Invoke(bounds);
        }

        /// <summary>
        /// 读取配置中的屏幕缩放因子（百分比/100，默认 1.25，范围 100-200）
        /// </summary>
        /// <returns>屏幕缩放因子</returns>
        private static double GetScreenScaleFactor()
        {
            if (int.TryParse(YF_Manager.Config.ScreenScale, out int scale))
                return Math.Clamp(scale, 100, 200) / 100.0;
            return 1.25;
        }

        /// <summary>
        /// ESC 键：取消框选
        /// </summary>
        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Close();
                _onCancelled?.Invoke();
            }
        }
    }
}