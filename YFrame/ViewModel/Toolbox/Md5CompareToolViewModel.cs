using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using YF_Manager;

namespace YFrame.ViewModel.Toolbox
{
    /// <summary>
    /// MD5 文件校验工具 ViewModel：
    /// 支持单文件 MD5 计算与双文件内容对比（版本校验），
    /// 计算委托给 YF_Manager.Md5Hasher，异步分块读取并上报进度
    /// </summary>
    public class Md5CompareToolViewModel : ViewModelBase
    {
        /// <summary>
        /// 从当前语言资源字典获取字符串
        /// </summary>
        /// <param name="key">资源键</param>
        /// <returns>本地化字符串</returns>
        private static string R(string key) => Application.Current?.TryFindResource(key) as string ?? key;

        /// <summary>
        /// 带格式参数的语言资源字符串
        /// </summary>
        /// <param name="key">资源键</param>
        /// <param name="args">格式化参数</param>
        /// <returns>本地化并格式化后的字符串</returns>
        private static string RF(string key, params object[] args)
            => string.Format(Application.Current?.TryFindResource(key) as string ?? key, args);

        private string _fileAPath = string.Empty;

        /// <summary>
        /// 第一个文件路径
        /// </summary>
        public string FileAPath
        {
            get => _fileAPath;
            set => SetProperty(ref _fileAPath, value);
        }

        private string _fileBPath = string.Empty;

        /// <summary>
        /// 第二个文件路径（可选，用于对比）
        /// </summary>
        public string FileBPath
        {
            get => _fileBPath;
            set => SetProperty(ref _fileBPath, value);
        }

        private string _fileAInfo = string.Empty;

        /// <summary>
        /// 文件 A 信息（大小 + 修改时间）
        /// </summary>
        public string FileAInfo
        {
            get => _fileAInfo;
            set => SetProperty(ref _fileAInfo, value);
        }

        private string _fileBInfo = string.Empty;

        /// <summary>
        /// 文件 B 信息（大小 + 修改时间）
        /// </summary>
        public string FileBInfo
        {
            get => _fileBInfo;
            set => SetProperty(ref _fileBInfo, value);
        }

        private string _fileAHash = string.Empty;

        /// <summary>
        /// 文件 A 的 MD5 值
        /// </summary>
        public string FileAHash
        {
            get => _fileAHash;
            set => SetProperty(ref _fileAHash, value);
        }

        private string _fileBHash = string.Empty;

        /// <summary>
        /// 文件 B 的 MD5 值
        /// </summary>
        public string FileBHash
        {
            get => _fileBHash;
            set => SetProperty(ref _fileBHash, value);
        }

        private double _progress;

        /// <summary>
        /// 计算进度（0.0 ~ 1.0）
        /// </summary>
        public double Progress
        {
            get => _progress;
            set
            {
                if (SetProperty(ref _progress, value))
                    OnPropertyChanged(nameof(ProgressPercent));
            }
        }

        /// <summary>
        /// 进度百分比文本
        /// </summary>
        public string ProgressPercent => $"{_progress * 100:F0}%";

        private bool _isComputing;

        /// <summary>
        /// 是否正在计算中（计算期间禁用按钮）
        /// </summary>
        public bool IsComputing
        {
            get => _isComputing;
            set => SetProperty(ref _isComputing, value);
        }

        private string _resultText = string.Empty;

        /// <summary>
        /// 结果状态文本
        /// </summary>
        public string ResultText
        {
            get => _resultText;
            set => SetProperty(ref _resultText, value);
        }

        private Brush _resultBrush = Brushes.Gray;

        /// <summary>
        /// 结果状态颜色（一致绿/不一致红/计算中橙/默认灰）
        /// </summary>
        public Brush ResultBrush
        {
            get => _resultBrush;
            set => SetProperty(ref _resultBrush, value);
        }

        /// <summary>
        /// 选择文件 A 命令
        /// </summary>
        public ICommand SelectFileACommand { get; }

        /// <summary>
        /// 选择文件 B 命令
        /// </summary>
        public ICommand SelectFileBCommand { get; }

        /// <summary>
        /// 计算 MD5 命令
        /// </summary>
        public ICommand ComputeCommand { get; }

        /// <summary>
        /// 复制文件 A MD5 命令
        /// </summary>
        public ICommand CopyHashACommand { get; }

        /// <summary>
        /// 复制文件 B MD5 命令
        /// </summary>
        public ICommand CopyHashBCommand { get; }

        /// <summary>
        /// 清空所有输入与结果命令
        /// </summary>
        public ICommand ClearCommand { get; }

        public Md5CompareToolViewModel()
        {
            SelectFileACommand = new YF_RelayCommand(() => SelectFile(isA: true), () => !IsComputing);
            SelectFileBCommand = new YF_RelayCommand(() => SelectFile(isA: false), () => !IsComputing);
            ComputeCommand = new YF_RelayCommand(ComputeAsync, () => !IsComputing);
            CopyHashACommand = new YF_RelayCommand(() => CopyToClipboard(FileAHash), () => FileAHash.Length > 0);
            CopyHashBCommand = new YF_RelayCommand(() => CopyToClipboard(FileBHash), () => FileBHash.Length > 0);
            ClearCommand = new YF_RelayCommand(Clear);
            _resultText = R("key_Toolbox_Md5Init");
        }

        /// <summary>
        /// 弹出文件选择框并记录所选文件路径及信息
        /// </summary>
        /// <param name="isA">true 选择文件 A，false 选择文件 B</param>
        private void SelectFile(bool isA)
        {
            var dialog = new OpenFileDialog
            {
                Title = isA ? "选择第一个文件" : "选择第二个文件",
                Filter = "所有文件 (*.*)|*.*",
                CheckFileExists = true
            };
            if (dialog.ShowDialog() == true)
            {
                if (isA)
                {
                    FileAPath = dialog.FileName;
                    FileAInfo = GetFileInfoText(dialog.FileName);
                    FileAHash = string.Empty;
                }
                else
                {
                    FileBPath = dialog.FileName;
                    FileBInfo = GetFileInfoText(dialog.FileName);
                    FileBHash = string.Empty;
                }
                ResultText = R("key_Toolbox_Md5Ready");
                ResultBrush = Brushes.Gray;
            }
        }

        /// <summary>
        /// 拖拽文件路径赋值（供 View 的拖放事件调用）
        /// </summary>
        /// <param name="path">拖入的文件路径</param>
        /// <param name="isA">true 赋值给文件 A，false 赋值给文件 B</param>
        public void SetFile(string path, bool isA)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;
            if (isA)
            {
                FileAPath = path;
                FileAInfo = GetFileInfoText(path);
                FileAHash = string.Empty;
            }
            else
            {
                FileBPath = path;
                FileBInfo = GetFileInfoText(path);
                FileBHash = string.Empty;
            }
            ResultText = R("key_Toolbox_Md5Ready");
            ResultBrush = Brushes.Gray;
        }

        /// <summary>
        /// 异步计算 MD5：单文件只算 A，双文件分别计算并对比
        /// </summary>
        private async void ComputeAsync()
        {
            // 校验文件 A 必须存在
            if (string.IsNullOrEmpty(FileAPath) || !File.Exists(FileAPath))
            {
                ResultText = R("key_Toolbox_Md5NeedFileA");
                ResultBrush = Brushes.Red;
                return;
            }

            bool hasFileB = !string.IsNullOrEmpty(FileBPath) && File.Exists(FileBPath);

            IsComputing = true;
            Progress = 0;
            ResultText = R("key_Toolbox_Md5Computing");
            ResultBrush = Brushes.Orange;
            CommandManager.InvalidateRequerySuggested();

            // 进度报告：IProgress 在 UI 上下文回调，自动更新 Progress
            var progress = new Progress<double>(p => Progress = p);

            try
            {
                FileAHash = await Md5Hasher.ComputeFileAsync(FileAPath, progress);

                if (hasFileB)
                {
                    FileBHash = await Md5Hasher.ComputeFileAsync(FileBPath, progress);

                    // 双文件对比（不区分大小写）
                    bool isEqual = string.Equals(FileAHash, FileBHash, StringComparison.OrdinalIgnoreCase);
                    ResultText = isEqual
                        ? R("key_Toolbox_Md5Equal")
                        : R("key_Toolbox_Md5Diff");
                    ResultBrush = isEqual ? Brushes.Green : Brushes.Red;
                }
                else
                {
                    ResultText = RF("key_Toolbox_Md5Done", FileAPath);
                    ResultBrush = Brushes.Green;
                }
            }
            catch (Exception ex)
            {
                ResultText = RF("key_Toolbox_Md5Failed", ex.Message);
                ResultBrush = Brushes.Red;
            }
            finally
            {
                IsComputing = false;
                Progress = 1.0;
                CommandManager.InvalidateRequerySuggested();
            }
        }

        /// <summary>
        /// 清空所有输入与结果
        /// </summary>
        private void Clear()
        {
            FileAPath = string.Empty;
            FileBPath = string.Empty;
            FileAInfo = string.Empty;
            FileBInfo = string.Empty;
            FileAHash = string.Empty;
            FileBHash = string.Empty;
            Progress = 0;
            ResultText = R("key_Toolbox_Md5Init");
            ResultBrush = Brushes.Gray;
        }

        /// <summary>
        /// 生成文件信息文本（大小 + 修改时间）
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <returns>如 "1.5 MB | 2026-08-26 10:30"</returns>
        private static string GetFileInfoText(string filePath)
        {
            try
            {
                var fi = new FileInfo(filePath);
                return $"{FormatSize(fi.Length)} | {fi.LastWriteTime:yyyy-MM-dd HH:mm}";
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// 格式化字节数为可读字符串
        /// </summary>
        /// <param name="bytes">字节数</param>
        /// <returns>如 "1.5 MB"</returns>
        private static string FormatSize(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
            if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
            return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
        }

        /// <summary>
        /// 将文本写入剪贴板
        /// </summary>
        /// <param name="text">要复制的文本</param>
        private static void CopyToClipboard(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            try
            {
                Clipboard.SetText(text);
            }
            catch (Exception ex)
            {
                YF_Manager_Main.logger?.ErrorInfo("Md5CompareTool", "复制到剪贴板失败 " + ex.Message);
            }
        }
    }
}