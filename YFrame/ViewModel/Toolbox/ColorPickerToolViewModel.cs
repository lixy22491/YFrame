using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using YF_Manager;

namespace YFrame.ViewModel.Toolbox
{
    /// <summary>
    /// 屏幕取色器工具 ViewModel：
    /// 框选截图后展示缩略图，点击缩略图从截图像素中取色并记录到历史。
    /// 框选交互由 View 层负责（全屏区域选框覆盖窗），截图与像素数据通过 SetSnapshot 回填。
    /// </summary>
    public class ColorPickerToolViewModel : ViewModelBase
    {
        /// <summary>
        /// 历史取色记录项
        /// </summary>
        public class PickedColor
        {
            /// <summary>
            /// 取到的颜色
            /// </summary>
            public Color Color { get; init; }

            /// <summary>
            /// 拾取位置坐标（截图像素坐标）
            /// </summary>
            public string Position { get; init; } = string.Empty;

            /// <summary>
            /// HEX 色值
            /// </summary>
            public string Hex => $"#{Color.R:X2}{Color.G:X2}{Color.B:X2}";

            /// <summary>
            /// 颜色画刷（供历史列表色块绑定）
            /// </summary>
            public Brush Brush => new SolidColorBrush(Color);
        }

        // 请求启动框选事件，由 View 订阅并弹出全屏区域选框覆盖窗
        public event Action? PickStarted;

        private Color _currentColor = Colors.White;

        /// <summary>
        /// 当前拾取到的颜色（驱动预览色块）
        /// </summary>
        public Color CurrentColor
        {
            get => _currentColor;
            set
            {
                if (SetProperty(ref _currentColor, value))
                {
                    OnPropertyChanged(nameof(HexText));
                    OnPropertyChanged(nameof(RgbText));
                    OnPropertyChanged(nameof(CurrentBrush));
                }
            }
        }

        /// <summary>
        /// 当前颜色画刷（供预览色块绑定）
        /// </summary>
        public Brush CurrentBrush => new SolidColorBrush(_currentColor);

        /// <summary>
        /// 当前颜色 HEX 文本（#RRGGBB）
        /// </summary>
        public string HexText => $"#{_currentColor.R:X2}{_currentColor.G:X2}{_currentColor.B:X2}";

        /// <summary>
        /// 当前颜色 RGB 文本
        /// </summary>
        public string RgbText => $"RGB({_currentColor.R}, {_currentColor.G}, {_currentColor.B})";

        private int _mouseX;
        private int _mouseY;

        /// <summary>
        /// 当前拾取坐标（截图像素坐标）
        /// </summary>
        public string PositionText => $"X:{_mouseX}  Y:{_mouseY}";

        private BitmapSource? _snapshot;

        /// <summary>
        /// 框选得到的截图缩略图（供左上区域显示与点击取色）
        /// </summary>
        public BitmapSource? Snapshot
        {
            get => _snapshot;
            private set => SetProperty(ref _snapshot, value);
        }

        /// <summary>
        /// 是否已加载截图（控制提示可见性）
        /// </summary>
        public bool IsSnapshotVisible => Snapshot != null;

        // 截图 BGRA 像素数据与行字节数（点击取色时按坐标读取）
        private byte[]? _pixels;
        private int _stride;

        private bool _isPicking;

        /// <summary>
        /// 是否正在框选截图中
        /// </summary>
        public bool IsPicking
        {
            get => _isPicking;
            set => SetProperty(ref _isPicking, value);
        }

        /// <summary>
        /// 历史取色记录列表
        /// </summary>
        public ObservableCollection<PickedColor> History { get; } = new();

        /// <summary>
        /// 开始框选截图命令
        /// </summary>
        public ICommand StartPickCommand { get; }

        /// <summary>
        /// 复制当前 HEX 色值命令
        /// </summary>
        public ICommand CopyHexCommand { get; }

        /// <summary>
        /// 复制当前 RGB 色值命令
        /// </summary>
        public ICommand CopyRgbCommand { get; }

        /// <summary>
        /// 清空历史记录命令
        /// </summary>
        public ICommand ClearHistoryCommand { get; }

        public ColorPickerToolViewModel()
        {
            StartPickCommand = new YF_RelayCommand(StartPick, () => !IsPicking);
            CopyHexCommand = new YF_RelayCommand(() => CopyToClipboard(HexText));
            CopyRgbCommand = new YF_RelayCommand(() => CopyToClipboard(RgbText));
            ClearHistoryCommand = new YF_RelayCommand(() => History.Clear());

            // 从配置文件读取屏幕缩放比例（默认 125），与截图翻译插件共用同一配置键
            int scale = 125;
            if (int.TryParse(YF_Manager.Config.ScreenScale, out int savedScale))
                scale = Math.Clamp(savedScale, 100, 200);
            _screenScaleText = scale.ToString();
            ScreenScaleFactor = scale / 100.0;
        }

        /// <summary>
        /// 缩放下拉框可选预设值（单位：百分比），与截图翻译插件一致
        /// </summary>
        public List<int> ScreenScaleOptions { get; } = new() { 100, 125, 150, 175, 200 };

        private string _screenScaleText = "125";

        /// <summary>
        /// 缩放百分比文本（可编辑下拉框，取值 100-200），变更时保存到 Config.ScreenScale
        /// </summary>
        public string ScreenScaleText
        {
            get => _screenScaleText;
            set
            {
                // 解析用户输入，校验范围 100-200，合法则保存配置
                if (int.TryParse(value, out int scale) && scale >= 100 && scale <= 200)
                {
                    if (_screenScaleText != scale.ToString())
                    {
                        _screenScaleText = scale.ToString();
                        OnPropertyChanged();
                        SaveScreenScale(scale);
                    }
                }
                else
                {
                    // 非法输入：恢复显示为当前有效值（百分比）
                    _screenScaleText = ((int)Math.Round(ScreenScaleFactor * 100)).ToString();
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// 当前屏幕缩放因子（百分比/100，默认 1.25），供框选坐标换算使用
        /// </summary>
        public double ScreenScaleFactor { get; private set; } = 1.25;

        /// <summary>
        /// 保存屏幕缩放比例到 YF_Manager 配置文件并更新内存缩放因子
        /// </summary>
        /// <param name="scale">缩放百分比（100-200）</param>
        private void SaveScreenScale(int scale)
        {
            YF_Manager.Config.ScreenScale = scale.ToString();
            ScreenScaleFactor = scale / 100.0;
            YF_Manager_Main.logger?.LogInfo($"屏幕缩放比例已设置为: {scale}%");
        }

        /// <summary>
        /// 启动框选：置为拾取中状态并通知 View 弹出区域选框覆盖窗
        /// </summary>
        private void StartPick()
        {
            IsPicking = true;
            PickStarted?.Invoke();
        }

        /// <summary>
        /// 回填框选截图与像素数据（由 View 层截屏后调用）
        /// </summary>
        /// <param name="image">截图位图（BGRA32）</param>
        /// <param name="pixels">BGRA 像素数组</param>
        /// <param name="stride">每行字节数</param>
        public void SetSnapshot(BitmapSource? image, byte[]? pixels, int stride)
        {
            Snapshot = image;
            _pixels = pixels;
            _stride = stride;
            OnPropertyChanged(nameof(IsSnapshotVisible));
        }

        /// <summary>
        /// 点击截图取色：根据截图像素坐标读取颜色并记录
        /// </summary>
        /// <param name="x">截图 X 像素坐标</param>
        /// <param name="y">截图 Y 像素坐标</param>
        public void PickColorAt(int x, int y)
        {
            if (_pixels == null || _stride <= 0) return;
            if (Snapshot == null || x < 0 || y < 0 || x >= Snapshot.PixelWidth || y >= Snapshot.PixelHeight)
                return;

            int index = y * _stride + x * 4;
            if (index + 2 >= _pixels.Length) return;

            // BGRA 字节顺序：B,G,R,A
            byte b = _pixels[index];
            byte g = _pixels[index + 1];
            byte r = _pixels[index + 2];
            var color = Color.FromRgb(r, g, b);

            CurrentColor = color;
            _mouseX = x;
            _mouseY = y;
            OnPropertyChanged(nameof(PositionText));

            // 加入历史（最多保留 20 条，最新的在最前）
            History.Insert(0, new PickedColor { Color = color, Position = $"({x}, {y})" });
            if (History.Count > 20)
                History.RemoveAt(History.Count - 1);
        }

        /// <summary>
        /// 取消/结束框选
        /// </summary>
        public void EndPick()
        {
            IsPicking = false;
        }

        /// <summary>
        /// 将文本写入剪贴板
        /// </summary>
        /// <param name="text">要复制的文本</param>
        private static void CopyToClipboard(string text)
        {
            try
            {
                Clipboard.SetText(text);
            }
            catch (Exception ex)
            {
                YF_Manager_Main.logger?.ErrorInfo("ColorPickerTool", "复制到剪贴板失败 " + ex.Message);
            }
        }
    }
}