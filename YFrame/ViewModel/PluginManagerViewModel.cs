using System.Collections.ObjectModel;
using System.Net.Http;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using YF_Manager;
using YF_Model = YFrame.Model;

namespace YFrame.ViewModel
{
    /// <summary>
    /// 插件管理器 ViewModel（AOP 代理），负责 UI 状态和命令路由
    /// HTTP 通信与下载逻辑委托给 PluginManagerService
    /// </summary>
    public class PluginManagerViewModel : ViewModelBase
    {

        #region 依赖服务

        private readonly Service.PluginManagerService _service = Service.PluginManagerService.Instance;

        #endregion

        #region 语言资源辅助

        /// <summary>
        /// 从当前语言资源字典获取翻译字符串
        /// </summary>
        private static string R(string key) => Application.Current?.TryFindResource(key) as string ?? key;

        /// <summary>
        /// 带格式参数的资源字符串
        /// </summary>
        private static string RF(string key, params object[] args)
        {
            var fmt = Application.Current?.TryFindResource(key) as string ?? key;
            return string.Format(fmt, args);
        }

        #endregion

        #region 绑定属性 — 服务器连接状态

        private string _serverURL = "";

        /// <summary>服务器地址（用户输入）</summary>
        public string ServerURL
        {
            get => _serverURL;
            set => SetProperty(ref _serverURL, value);
        }

        private bool _isConnected;

        /// <summary>是否已连接到服务器</summary>
        public bool IsConnected
        {
            get => _isConnected;
            set
            {
                if (SetProperty(ref _isConnected, value))
                {
                    OnPropertyChanged(nameof(CanConnect));
                    OnPropertyChanged(nameof(IsEmpty));
                }
            }
        }

        /// <summary>连接按钮是否可用</summary>
        public bool CanConnect => !IsConnected;

        private string _statusText = "";

        /// <summary>连接状态文本</summary>
        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }

        private Brush _statusBrush = Brushes.Gray;

        /// <summary>连接状态指示灯颜色</summary>
        public Brush StatusBrush
        {
            get => _statusBrush;
            set => SetProperty(ref _statusBrush, value);
        }

        #endregion

        #region 绑定属性 — 插件列表

        /// <summary>远程插件列表数据源</summary>
        public ObservableCollection<YF_Model.RemotePluginInfo> RemotePlugins { get; } = new();

        private string _pluginCountText = "";

        /// <summary>底部状态栏插件计数文本</summary>
        public string PluginCountText
        {
            get => _pluginCountText;
            set => SetProperty(ref _pluginCountText, value);
        }

        private string _downloadStatusText = "";

        /// <summary>底部状态栏下载状态文本</summary>
        public string DownloadStatusText
        {
            get => _downloadStatusText;
            set => SetProperty(ref _downloadStatusText, value);
        }


        /// <summary>空列表提示是否可见</summary>
        public bool IsEmpty => !IsConnected || RemotePlugins.Count == 0;

        #endregion

        #region 命令

        public ICommand ConnectCommand { get; }
        public ICommand DisconnectCommand { get; }
        public ICommand DownloadCommand { get; }

        #endregion

        #region 构造函数

        public PluginManagerViewModel()
        {
            var savedUrl = Config.PluginManagerServerURL;
            var savedPort = Config.PluginServerPort;
            ServerURL = _service.BuildFullUrl(savedUrl, savedPort);
            StatusText = R("key_PluginManager_NotConnected");

            ConnectCommand = new YF_RelayCommand<object>(_ => Connect(), _ => CanConnect);
            DisconnectCommand = new YF_RelayCommand<object>(_ => Disconnect(), _ => IsConnected);
            DownloadCommand = new YF_RelayCommand<YF_Model.RemotePluginInfo>(plugin => DownloadPlugin(plugin));

            RemotePlugins.CollectionChanged += (_, _) => OnPropertyChanged(nameof(IsEmpty));
        }

        #endregion

        #region 连接/断开（AOP 入口）

        /// <summary>
        /// 连接到插件服务器，获取插件列表
        /// </summary>
        [Log(Level = LogLevel.Info, Message = "连接插件服务器")]
        public virtual async void Connect()
        {
            var url = _service.NormalizeUrl(ServerURL);
            if (string.IsNullOrEmpty(url))
            {
                SetStatus(R("key_PluginManager_EnterAddress"), Brushes.Orange);
                return;
            }
            ServerURL = url;

            // 持久化地址
            Config.PluginManagerServerURL = url;
            SetStatus(R("key_PluginManager_Connecting"), Brushes.DodgerBlue);

            try
            {
                var plugins = await _service.FetchPluginListAsync(url);

                RemotePlugins.Clear();
                foreach (var p in plugins)
                    RemotePlugins.Add(p);

                IsConnected = true;
                SetStatus(RF("key_PluginManager_Connected", RemotePlugins.Count), Brushes.Green);
                PluginCountText = RF("key_PluginManager_AvailablePlugins", RemotePlugins.Count);
            }
            catch (HttpRequestException ex)
            {
                SetStatus(RF("key_PluginManager_ConnectFailed", ex.Message), Brushes.Red);
            }
            catch (TaskCanceledException)
            {
                SetStatus(R("key_PluginManager_Timeout"), Brushes.Red);
            }
            catch (Exception ex)
            {
                SetStatus(RF("key_PluginManager_Error", ex.Message), Brushes.Red);
            }
        }

        /// <summary>
        /// 断开连接，取消所有下载并清空列表
        /// </summary>
        [Log(Level = LogLevel.Info, Message = "断开插件服务器")]
        public virtual void Disconnect()
        {
            foreach (var plugin in RemotePlugins)
            {
                plugin.Cts?.Cancel();
                plugin.Cts?.Dispose();
                plugin.Cts = null;
                plugin.IsDownloading = false;
            }

            RemotePlugins.Clear();
            IsConnected = false;
            SetStatus(R("key_PluginManager_NotConnected"), Brushes.Gray);
            PluginCountText = "";
            DownloadStatusText = "";
        }

        #endregion

        #region 下载（AOP 入口）

        /// <summary>
        /// 下载指定插件
        /// </summary>
        [Log(Level = LogLevel.Info, Message = "下载插件")]
        public virtual async void DownloadPlugin(YF_Model.RemotePluginInfo? pluginInfo)
        {
            if (pluginInfo == null) return;

            pluginInfo.IsDownloading = true;
            pluginInfo.DownloadProgress = 0;
            pluginInfo.Cts = new CancellationTokenSource();
            DownloadStatusText = RF("key_PluginManager_Downloading", pluginInfo.PluginName);

            var progress = new Progress<int>(p =>
            {
                Application.Current.Dispatcher.InvokeAsync(() => pluginInfo.DownloadProgress = p);
            });

            try
            {
                await _service.InstallPluginAsync(pluginInfo.FolderName, progress, pluginInfo.Cts.Token);

                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    pluginInfo.IsDownloading = false;
                    pluginInfo.IsInstalled = true;
                    pluginInfo.DownloadProgress = 100;
                    DownloadStatusText = RF("key_PluginManager_InstallComplete", pluginInfo.PluginName);
                });
            }
            catch (OperationCanceledException)
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    pluginInfo.IsDownloading = false;
                    pluginInfo.DownloadProgress = 0;
                    DownloadStatusText = RF("key_PluginManager_DownloadCancelled", pluginInfo.PluginName);
                });
            }
            catch (Exception ex)
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    pluginInfo.IsDownloading = false;
                    pluginInfo.DownloadProgress = 0;
                    DownloadStatusText = RF("key_PluginManager_DownloadFailed", ex.Message);
                    MessageBox.Show(
                        RF("key_PluginManager_DownloadFailed", ex.Message),
                        R("key_PluginManager_Title"),
                        MessageBoxButton.OK, MessageBoxImage.Error);
                });
            }
            finally
            {
                pluginInfo.Cts?.Dispose();
                pluginInfo.Cts = null;
            }
        }

        #endregion

        #region 状态更新

        /// <summary>
        /// 更新状态文本和指示灯颜色
        /// </summary>
        private void SetStatus(string text, Brush color)
        {
            StatusText = text;
            StatusBrush = color;
        }

        #endregion
    }
}
