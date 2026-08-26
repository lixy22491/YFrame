using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using YF_Manager;

namespace YFrame.ViewModel.Toolbox
{
    /// <summary>
    /// 正则表达式测试工具 ViewModel，封装模式选项、匹配执行与结果展示
    /// 匹配逻辑委托给 YF_Manager.RegexHelper，便于单元测试
    /// </summary>
    public class RegexTesterToolViewModel : ViewModelBase
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
        private string _pattern = string.Empty;

        /// <summary>
        /// 正则模式输入
        /// </summary>
        public string Pattern
        {
            get => _pattern;
            set => SetProperty(ref _pattern, value);
        }

        private string _inputText = string.Empty;

        /// <summary>
        /// 待匹配的测试文本
        /// </summary>
        public string InputText
        {
            get => _inputText;
            set => SetProperty(ref _inputText, value);
        }

        private bool _ignoreCase;

        /// <summary>
        /// 忽略大小写选项
        /// </summary>
        public bool IgnoreCase
        {
            get => _ignoreCase;
            set => SetProperty(ref _ignoreCase, value);
        }

        private bool _multiline;

        /// <summary>
        /// 多行模式选项（^ $ 匹配每行首尾）
        /// </summary>
        public bool Multiline
        {
            get => _multiline;
            set => SetProperty(ref _multiline, value);
        }

        private bool _singleline;

        /// <summary>
        /// 单行模式选项（. 匹配换行符）
        /// </summary>
        public bool Singleline
        {
            get => _singleline;
            set => SetProperty(ref _singleline, value);
        }

        private bool _ignorePatternWhitespace;

        /// <summary>
        /// 忽略模式空白选项
        /// </summary>
        public bool IgnorePatternWhitespace
        {
            get => _ignorePatternWhitespace;
            set => SetProperty(ref _ignorePatternWhitespace, value);
        }

        /// <summary>
        /// 匹配结果列表
        /// </summary>
        public ObservableCollection<RegexMatchResult> Matches { get; } = new();

        private int _matchCount;

        /// <summary>
        /// 匹配结果数量汇总文本
        /// </summary>
        public string MatchSummary => string.Format(R("key_Toolbox_RegexMatchSummary"), _matchCount);

        private string _statusText = string.Empty;

        /// <summary>
        /// 状态栏文本
        /// </summary>
        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }

        private Brush _statusBrush = Brushes.Gray;

        /// <summary>
        /// 状态栏颜色（正常绿/错误红/默认灰）
        /// </summary>
        public Brush StatusBrush
        {
            get => _statusBrush;
            set => SetProperty(ref _statusBrush, value);
        }

        /// <summary>
        /// 执行匹配命令
        /// </summary>
        public ICommand TestCommand { get; }

        /// <summary>
        /// 清空结果命令
        /// </summary>
        public ICommand ClearCommand { get; }

        public RegexTesterToolViewModel()
        {
            TestCommand = new YF_RelayCommand(ExecuteTest);
            ClearCommand = new YF_RelayCommand(Clear);
            _statusText = R("key_Toolbox_RegexInit");
        }

        /// <summary>
        /// 执行正则匹配：校验模式合法性，执行匹配并刷新结果列表
        /// </summary>
        private void ExecuteTest()
        {
            Matches.Clear();
            _matchCount = 0;
            OnPropertyChanged(nameof(MatchSummary));

            // 先校验模式是否合法
            if (!YF_RegexHelper.IsValidPattern(Pattern, out string? errorMessage))
            {
                StatusText = RF("key_Toolbox_RegexInvalid", errorMessage ?? "Invalid");
                StatusBrush = Brushes.Red;
                return;
            }

            var options = YF_RegexHelper.BuildOptions(IgnoreCase, Multiline, Singleline, IgnorePatternWhitespace);

            // 将 Windows 换行(\r\n)与旧式换行(\r)归一化为 \n：
            string normalizedInput = InputText.Replace("\r\n", "\n").Replace('\r', '\n');
            var results = YF_RegexHelper.MatchAll(Pattern, normalizedInput, options);

            foreach (var r in results)
                Matches.Add(r);
            _matchCount = Matches.Count;
            OnPropertyChanged(nameof(MatchSummary));

            if (_matchCount == 0)
            {
                StatusText = R("key_Toolbox_RegexNoMatch");
                StatusBrush = Brushes.Orange;
            }
            else
            {
                StatusText = RF("key_Toolbox_RegexSuccess", _matchCount);
                StatusBrush = Brushes.Green;
            }
        }

        /// <summary>
        /// 清空输入、选项与匹配结果，恢复初始状态
        /// </summary>
        private void Clear()
        {
            Pattern = string.Empty;
            InputText = string.Empty;
            IgnoreCase = false;
            Multiline = false;
            Singleline = false;
            IgnorePatternWhitespace = false;
            Matches.Clear();
            _matchCount = 0;
            OnPropertyChanged(nameof(MatchSummary));
            StatusText = R("key_Toolbox_RegexInit");
            StatusBrush = Brushes.Gray;
        }
    }
}