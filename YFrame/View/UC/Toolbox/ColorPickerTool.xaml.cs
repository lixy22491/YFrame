using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using YF_Manager;
using YFrame.ViewModel.Toolbox;

namespace YFrame.View.UC.Toolbox
{
    /// <summary>
    /// 屏幕取色器工具视图（Code-behind）：
    /// 弹出区域选框覆盖窗框选截图，将截图回填 ViewModel 展示缩略图；
    /// 点击缩略图时换算坐标并从截图像素中取色。业务逻辑均在 ViewModel。
    /// </summary>
    public partial class ColorPickerTool : UserControl
    {
        /// <summary>
        /// 当前绑定的取色器 ViewModel
        /// </summary>
        private ColorPickerToolViewModel? _viewModel;

        public ColorPickerTool()
        {
            InitializeComponent();
            // DataContext 由 ToolboxService 在创建后赋值，故通过 DataContextChanged 挂接
            DataContextChanged += OnDataContextChanged;
        }

        /// <summary>
        /// DataContext 变化时挂接/退订 ViewModel 的拾取事件
        /// </summary>
        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (_viewModel != null)
                _viewModel.PickStarted -= OnPickStarted;

            _viewModel = DataContext as ColorPickerToolViewModel;
            if (_viewModel != null)
                _viewModel.PickStarted += OnPickStarted;
        }

        /// <summary>
        /// 响应开始拾取：弹出全屏区域选框覆盖窗，框选后截屏并回填
        /// </summary>
        private void OnPickStarted()
        {
            // 每次从当前 DataContext 获取 ViewModel，避免依赖字段的订阅时序问题
            if (DataContext is not ColorPickerToolViewModel vm) return;

            try
            {
                var overlay = new ColorPickerOverlayWindow(
                    onRegionSelected: bounds => CaptureAndSetSnapshot(vm, bounds),
                    onCancelled: () => vm.EndPick());

                // 不设置 Owner：主窗口为 AllowsTransparency=True 时，
                // 透明 owned window 受主窗口 z-order 限制可能被遮挡，独立 Topmost 窗口最可靠
                overlay.Show();
            }
            catch (Exception ex)
            {
                YF_Manager_Main.logger?.ErrorInfo("ColorPickerTool", "弹出框选覆盖窗失败 " + ex.Message);
                vm.EndPick();
            }
        }

        /// <summary>
        /// 截取指定屏幕区域并回填到 ViewModel（显示缩略图 + 存储像素数据）
        /// </summary>
        /// <param name="vm">取色器 ViewModel</param>
        /// <param name="bounds">屏幕物理像素区域</param>
        private static void CaptureAndSetSnapshot(ColorPickerToolViewModel vm, Rect bounds)
        {
            try
            {
                var (image, pixels, stride) = ScreenCaptureHelper.Capture(bounds);
                vm.SetSnapshot(image, pixels, stride);
            }
            catch (Exception ex)
            {
                YF_Manager_Main.logger?.ErrorInfo("ColorPickerTool", "截图失败 " + ex.Message);
            }
            finally
            {
                vm.EndPick();
            }
        }

        /// <summary>
        /// 点击截图缩略图取色：将控件内点击位置换算为截图像素坐标
        /// </summary>
        private void OnSnapshotMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not Image image) return;
            if (DataContext is not ColorPickerToolViewModel vm) return;
            if (vm.Snapshot is not BitmapSource snapshot) return;

            var pos = e.GetPosition(image);
            double srcW = snapshot.PixelWidth;
            double srcH = snapshot.PixelHeight;
            double actualW = image.ActualWidth;
            double actualH = image.ActualHeight;

            // Image 使用 Stretch=Uniform 居中缩放，计算实际绘制区域
            double scale = Math.Min(actualW / srcW, actualH / srcH);
            if (scale <= 0) return;
            double drawW = srcW * scale;
            double drawH = srcH * scale;
            double offsetX = (actualW - drawW) / 2;
            double offsetY = (actualH - drawH) / 2;

            int srcX = (int)((pos.X - offsetX) / scale);
            int srcY = (int)((pos.Y - offsetY) / scale);

            if (srcX >= 0 && srcY >= 0 && srcX < srcW && srcY < srcH)
                vm.PickColorAt(srcX, srcY);
        }

        /// <summary>
        /// 可编辑下拉框点击主体区域时也弹出下拉列表：
        /// WPF 默认仅点击右侧箭头（底部）才打开下拉，点击主体会进入文本编辑，
        /// 此处拦截点击并直接展开下拉列表，改善交互体验
        /// </summary>
        private void OnScaleComboPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is ComboBox combo && combo.IsEditable && !combo.IsDropDownOpen)
            {
                combo.IsDropDownOpen = true;
                e.Handled = true;
            }
        }
    }
}