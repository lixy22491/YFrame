using LiveCharts.Defaults;
using LiveCharts;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using YF_Manager;
using System.IO;
using YFrame.Model;

namespace YFrame
{
    /// <summary>
    /// 主窗口
    /// </summary>
    public class MainWindowViewModel : ViewModelBase, I_YF_Detail
    {

        #region 绑定属性（XAML 绑定点，保留在此处）

        private bool _leftVisible;  // 左抽屉显示状态
        public bool LeftVisible
        {
            get => _leftVisible;
            set => SetProperty(ref _leftVisible, value);
        }

        private bool _rightVisible; // 右抽屉显示状态
        public bool RightVisible
        {
            get => _rightVisible;
            set => SetProperty(ref _rightVisible, value);
        }

        private string _txt_Cpu = "--%";  // CPU显示状态
        public string Txt_Cpu
        {
            get => _txt_Cpu;
            set => SetProperty(ref _txt_Cpu, value);
        }

        private string _txt_Memory = "--GB";  // 内存显示状态
        public string Txt_Memory
        {
            get => _txt_Memory;
            set => SetProperty(ref _txt_Memory, value);
        }

        private string _logText = string.Empty;  // 日志显示
        public string LogText
        {
            get => _logText;
            set => SetProperty(ref _logText, value);
        }

        private UserControl _performance_Monitor_View = null!;  // 性能监视器
        public UserControl Performance_Monitor_View
        {
            get => _performance_Monitor_View;
            set => SetProperty(ref _performance_Monitor_View, value);
        }

        private Grid _grid_Show_Array = null!;  // 用户控件(插件)显示列表
        public Grid Grid_Show_Array
        {
            get => _grid_Show_Array;
            set => SetProperty(ref _grid_Show_Array, value);
        }

        private bool _isFullScreen;  // 是否处于全屏状态
        public bool IsFullScreen
        {
            get => _isFullScreen;
            set => SetProperty(ref _isFullScreen, value);
        }

        private bool _isHotkeyEnabled;  // 热键监控是否开启
        public bool IsHotkeyEnabled
        {
            get => _isHotkeyEnabled;
            set
            {
                if (SetProperty(ref _isHotkeyEnabled, value))
                    OnPropertyChanged(nameof(HotkeyStatusText));
            }
        }

        // 状态栏热键状态文本
        public string HotkeyStatusText => IsHotkeyEnabled ? "已开启" : "已关闭";

        /// <summary>
        /// 左侧面板激活页：0=插件列表, 1=工具箱
        /// </summary>
        private int _activeLeftPanel;
        public int ActiveLeftPanel
        {
            get => _activeLeftPanel;
            set => SetProperty(ref _activeLeftPanel, value);
        }

        /// <summary>
        /// 右侧面板激活页：0=日志, 1=参数
        /// </summary>
        private int _activeRightPanel;
        public int ActiveRightPanel
        {
            get => _activeRightPanel;
            set => SetProperty(ref _activeRightPanel, value);
        }

        public ObservableCollection<PluginsModel> lsPlugins { get; } = new ObservableCollection<PluginsModel>(); // 插件列表

        private PluginsModel _selectedPlugin = null!;
        /// <summary>
        /// 当前在插件列表中选中的插件（VS 风格列表交互）
        /// </summary>
        public PluginsModel SelectedPlugin
        {
            get => _selectedPlugin;
            set
            {
                if (SetProperty(ref _selectedPlugin, value) && value != null)
                    ShowPlugin(value.ID);
            }
        }

        /// <summary>
        /// 工具箱工具列表（左侧工具箱面板数据源）
        /// </summary>
        public ObservableCollection<ToolItem> Tools { get; } = new ObservableCollection<ToolItem>();

        private ToolItem? _selectedTool;

        /// <summary>
        /// 当前在工具箱中选中的工具（选中即打开到主工作区）
        /// </summary>
        public ToolItem? SelectedTool
        {
            get => _selectedTool;
            set
            {
                if (SetProperty(ref _selectedTool, value) && value != null)
                    SelectTool(value.ID);
            }
        }

        private UserControl? _activeToolView;

        /// <summary>
        /// 当前在主工作区打开的工具视图（null 表示未打开任何工具）
        /// </summary>
        public UserControl? ActiveToolView
        {
            get => _activeToolView;
            set => SetProperty(ref _activeToolView, value);
        }

        private bool _isToolActive;

        /// <summary>
        /// 是否正在显示工具箱工具（用于主工作区叠加切换，true 时覆盖插件区）
        /// </summary>
        public bool IsToolActive
        {
            get => _isToolActive;
            set => SetProperty(ref _isToolActive, value);
        }

        #endregion

        #region 绑定命令（XAML 绑定点，委托给子服务）

        public ICommand Btn_Exit_Command { get; set; } = null!;                  // 退出事件
        public ICommand ToggleLeftToolWindowCommand { get; set; } = null!;       // 左侧抽屉事件
        public ICommand ToggleRightToolWindowCommand { get; set; } = null!;      // 右侧抽屉事件
        public ICommand SetThemeCommand { get; set; } = null!;                   // 主题切换事件（参数为主题路径）
        public ICommand Title_Move_Command { get; set; } = null!;                // 窗体拖拽移动事件
        public ICommand Btn_Minimize_Command { get; set; } = null!;              // 窗体最小化事件
        public ICommand ToggleChineseCommand { get; set; } = null!;              // 中文切换事件
        public ICommand ToggleEnglishCommand { get; set; } = null!;              // 英文切换事件
        public ICommand Btn_Plugin_Show_Command { get; set; } = null!;           // 插件显示事件
        public ICommand SwitchLeftPanelCommand { get; set; } = null!;            // 左侧面板切换事件
        public ICommand SwitchRightPanelCommand { get; set; } = null!;           // 右侧面板切换事件
        public ICommand ToggleHotkeyCommand { get; set; } = null!;               // 热键监控开关事件
        public ICommand OpenLogFolderCommand { get; set; } = null!;              // 打开日志文件夹事件
        public ICommand ClearLogCommand { get; set; } = null!;                   // 清除日志事件
        public ICommand NewScriptCommand { get; set; } = null!;                  // 新建脚本事件
        public ICommand OpenScriptCommand { get; set; } = null!;                 // 打开脚本事件
        public ICommand SaveScriptCommand { get; set; } = null!;                 // 保存脚本事件
        public ICommand Btn_About_Command { get; set; } = null!;                 // 关于事件
        public ICommand PluginManagerCommand { get; set; } = null!;              // 插件管理器事件
        public ICommand ReloadPluginsCommand { get; set; } = null!;              // 重新加载所有插件事件
        public ICommand ToggleFullScreenCommand { get; set; } = null!;           // 全屏切换事件
        public ICommand SelectToolCommand { get; set; } = null!;                // 打开工具箱工具事件（参数为工具ID）
        public ICommand CloseToolCommand { get; set; } = null!;                 // 关闭工具箱工具事件

        #endregion

        #region 全局委托（保持向后兼容）

        // 委托-显示CPU、内存信息
        public static YF_DelegateFunctionModel.dvFunc_Vs_s dlg_Show_Cpu_Memory = null!;

        #endregion

        #region I_YF_Detail 实现

        public string YF_ID => "YF_Frame";
        public string YF_Name => "主框架";

        #endregion

        #region DI 注入的依赖（通过 InitializeDependencies 属性注入）

        // 日志对象
        private YF_Manager_Log _logger = null!;
        private LogService _logService = null!;
        private PluginService _pluginService = null!;
        private UserControlsService _userControlsService = null!;
        private HotkeyService _hotkeyService = null!;
        private TrayIconService _trayIconService = null!;
        private YF_Messenger _messenger = null!;
        private YF_FileHelper _fileHelper = null!;
        private ToolboxService _toolboxService = null!;

        /// <summary>
        /// 设置依赖项（由 DI 容器创建 AOP 代理后调用）
        /// Castle CreateClassProxy 需要无参构造函数，故用此方法做属性注入
        /// </summary>
        public void InitializeDependencies(
            YF_Manager_Log logger,
            LogService logService,
            PluginService pluginService,
            UserControlsService userControlsService,
            HotkeyService hotkeyService,
            TrayIconService trayIconService,
            YF_Messenger messenger,
            YF_FileHelper fileHelper,
            ToolboxService toolboxService)
        {
            _logger = logger;
            _logService = logService;
            _pluginService = pluginService;
            _userControlsService = userControlsService;
            _hotkeyService = hotkeyService;
            _trayIconService = trayIconService;
            _messenger = messenger;
            _fileHelper = fileHelper;
            _toolboxService = toolboxService;
        }

        #endregion

        #region 构造函数 + 初始化

        public MainWindowViewModel() { }

        /// <summary>
        /// 初始化入口
        /// </summary>
        [Log(Level = LogLevel.Info, Message = "主框架初始化")]
        public virtual void Init()
        {
            _logger.LogInfo("主框架初始化-开始");

            _logService.OnLogTextChanged = text => LogText = text;

            InitUI();
            InitCommond();

            // 日志委托接入 Mediator，由 LogService 统一处理
            YF_Manager_Log.d_LogWrite = msg =>
            {
                _messenger.Send(new LogAppendMessage(msg));
            };

            // 订阅全局热键事件
            _hotkeyService.OnHotkeyPressed += () =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    // 热键消息经 Mediator 转发，PluginService 订阅处理
                    _messenger.Send(new HotkeyTriggeredMessage());
                });
            };

            // 订阅托盘图标服务事件
            _trayIconService.OnShowWindow += () =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (Application.Current.MainWindow is MainWindow mainWindow)
                    {
                        mainWindow.Show();
                        mainWindow.WindowState = WindowState.Normal;
                        mainWindow.Activate();
                    }
                });
            };
            _trayIconService.OnExitApplication += () =>
            {
                Application.Current.Dispatcher.Invoke(() => ExitApplication());
            };

            // 委托绑定：性能监视器回调
            dlg_Show_Cpu_Memory = Show_Cpu_Memory;

            _logger.LogInfo("主框架初始化-完成");
        }

        /// <summary>
        /// 初始化 UI 元素
        /// </summary>
        [Log(Level = LogLevel.Info, Message = "初始化UI")]
        public virtual void InitUI()
        {
            try
            {
                LeftVisible = true;
                RightVisible = true;

                Performance_Monitor_View = new PerformanceMonitor();
                Grid_Show_Array = new Grid();

                // 通知 PluginService 插件显示区域已就绪
                _pluginService.SetGridShowArea(Grid_Show_Array);

                // 加载插件列表
                _userControlsService.LoadAndShowUserControl();
                foreach (var item in _userControlsService.DctControls)
                {
                    lsPlugins.Add(new PluginsModel
                    {
                        Name = item.Value.Name,
                        ID = item.Key,
                        Status = 0
                    });
                }

                // 填充工具箱工具列表
                foreach (var tool in _toolboxService.GetTools())
                    Tools.Add(tool);
            }
            catch (Exception ex)
            {
                _logger.ErrorInfo("InitUI", ex.Message);
            }
        }

        /// <summary>
        /// 初始化命令绑定
        /// </summary>
        [Log(Level = LogLevel.Info, Message = "初始化命令绑定")]
        public virtual void InitCommond()
        {
            try
            {
                // ===== 面板切换（委托给 Mediator） =====
                ToggleLeftToolWindowCommand = new YF_RelayCommand(() =>
                {
                    LeftVisible = !LeftVisible;
                    _logger.LogInfo("左侧边栏-" + (LeftVisible ? "开" : "关"));
                });

                ToggleRightToolWindowCommand = new YF_RelayCommand(() =>
                {
                    RightVisible = !RightVisible;
                    _logger.LogInfo("右侧边栏-" + (RightVisible ? "开" : "关"));
                });

                // ===== 窗口管理 =====
                Btn_Exit_Command = new YF_RelayCommand(() => ExitApplication());
                Btn_Minimize_Command = new YF_RelayCommand(() =>
                {
                    Application.Current.MainWindow.WindowState = WindowState.Minimized;
                    _logger.LogInfo("窗体最小化");
                });
                Title_Move_Command = new YF_RelayCommand<object>(param =>
                {
                    try
                    {
                        if (param is Border border)
                            Window.GetWindow(border)?.DragMove();
                        else if (param is FrameworkElement element)
                            Window.GetWindow(element)?.DragMove();
                    }
                    catch (Exception ex)
                    {
                        _logger.ErrorInfo("Title_Move_Command", ex.Message);
                    }
                });

                // ===== 主题/语言（通过 Mediator 通知所有订阅者） =====
                SetThemeCommand = new YF_RelayCommand<string>(themePath =>
                {
                    var fileName = Path.GetFileNameWithoutExtension(themePath);
                    var themeDisplayName = fileName switch
                    {
                        "DarkGrayTheme" => "炭火暗夜",
                        "CreamWhiteTheme" => "素火明昼",
                        "LightBlueTheme" => "冰火深蓝",
                        "GreenWhiteTheme" => "翠火青绿",
                        _ => fileName
                    };
                    App.ChangeTheme(themePath);
                    _logger.LogInfo("主题切换-" + themeDisplayName);
                    _messenger.Send(new ThemeChangedMessage(themeDisplayName, themePath));
                });
                ToggleChineseCommand = new YF_RelayCommand(() =>
                {
                    App.ChangeLanguage("zh");
                    _logger.LogInfo("语言切换-中文");
                    _messenger.Send(new LanguageChangedMessage("zh"));
                });
                ToggleEnglishCommand = new YF_RelayCommand(() =>
                {
                    App.ChangeLanguage("en");
                    _logger.LogInfo("语言切换-英文");
                    _messenger.Send(new LanguageChangedMessage("en"));
                });

                // ===== 插件管理（委托给 PluginService） =====
                Btn_Plugin_Show_Command = new YF_RelayCommand<string>(parameter =>
                    _pluginService.ShowPlugin(parameter));

                // ===== 面板切换（通过 Mediator） =====
                SwitchLeftPanelCommand = new YF_RelayCommand<string>(panelIndex =>
                {
                    if (int.TryParse(panelIndex, out var idx))
                    {
                        ActiveLeftPanel = idx;
                        _messenger.Send(new PanelSwitchMessage("Left", idx));
                    }
                });
                SwitchRightPanelCommand = new YF_RelayCommand<string>(panelIndex =>
                {
                    if (int.TryParse(panelIndex, out var idx))
                    {
                        ActiveRightPanel = idx;
                        _messenger.Send(new PanelSwitchMessage("Right", idx));
                    }
                });

                // ===== 热键开关 =====
                ToggleHotkeyCommand = new YF_RelayCommand(() => ToggleHotkey());

                // ===== 日志操作 =====
                OpenLogFolderCommand = new YF_RelayCommand(() => OpenLogFolder());
                ClearLogCommand = new YF_RelayCommand(() =>
                {
                    _messenger.Send(new LogClearMessage());
                });

                // ===== 脚本操作（通过 Mediator → PluginService） =====
                NewScriptCommand = new YF_RelayCommand(() =>
                    _messenger.Send(new ScriptCommandMessage("New")));
                OpenScriptCommand = new YF_RelayCommand(() =>
                    _messenger.Send(new ScriptCommandMessage("Open")));
                SaveScriptCommand = new YF_RelayCommand(() =>
                    _messenger.Send(new ScriptCommandMessage("Save")));

                // ===== 关于 =====
                Btn_About_Command = new YF_RelayCommand(() => ShowAbout());

                // ===== 插件管理器 =====
                PluginManagerCommand = new YF_RelayCommand(() => ShowPluginManager());

                // ===== 重新加载所有插件 =====
                ReloadPluginsCommand = new YF_RelayCommand(() => ReloadPlugins());

                // ===== 全屏切换 =====
                ToggleFullScreenCommand = new YF_RelayCommand(() => ToggleFullScreen());

                // ===== 工具箱工具 =====
                SelectToolCommand = new YF_RelayCommand<string>(toolId => SelectTool(toolId));
                CloseToolCommand = new YF_RelayCommand(() => CloseTool());
            }
            catch (Exception ex)
            {
                _logger.ErrorInfo("InitCommond", ex.Message);
            }
        }

        #endregion

        #region 热键管理（保留逻辑，因为涉及 HWND 操作）

        [Log(Level = LogLevel.Info, Message = "热键开关切换")]
        public virtual void ToggleHotkey()
        {
            try
            {
                if (!IsHotkeyEnabled)
                {
                    if (_hotkeyService.Register())
                    {
                        IsHotkeyEnabled = true;
                        _logger.LogInfo("全局热键 Ctrl+Y 注册成功");
                    }
                    else
                    {
                        _logger.ErrorInfo("ToggleHotkey", "全局热键 Ctrl+Y 注册失败");
                    }
                }
                else
                {
                    if (_hotkeyService.Unregister())
                    {
                        IsHotkeyEnabled = false;
                        _logger.LogInfo("全局热键 Ctrl+Y 注销成功");
                    }
                    else
                    {
                        _logger.ErrorInfo("ToggleHotkey", "全局热键 Ctrl+Y 注销失败");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.ErrorInfo("ToggleHotkey", ex.Message);
            }
        }

        /// <summary>
        /// 委托给 PluginService 的热键路由（现走 Mediator，保留此方法兼容旧调用）
        /// </summary>
        [Log(Level = LogLevel.Info, Message = "热键触发（兼容）")]
        public virtual void OnHotkeyPressed()
        {
            _pluginService.OnHotkeyPressed();
        }

        #endregion

        #region 日志操作

        /// <summary>
        /// 打开日志文件夹
        /// </summary>
        [Log(Level = LogLevel.Info, Message = "打开日志文件夹")]
        public virtual void OpenLogFolder()
        {
            try
            {
                string logPath = Path.GetFullPath(Config.LogPath);
                _fileHelper.OpenFolder(logPath);
                _logger.LogInfo("打开日志文件夹: " + logPath);
            }
            catch (Exception ex)
            {
                _logger.ErrorInfo("OpenLogFolder", ex.Message);
            }
        }

        /// <summary>
        /// 清除日志
        /// </summary>
        [Log(Level = LogLevel.Info, Message = "清除日志")]
        public virtual void ClearLog()
        {
            LogText = _logService.ClearLog();
            _logger.LogInfo("日志面板已清除");
        }

        /// <summary>
        /// 追加日志到面板
        /// </summary>
        public virtual void Show_Log(string msg)
        {
            _logService.AppendLog(msg);
        }

        #endregion

        #region 插件操作（委托给 PluginService）

        [Log(Level = LogLevel.Info, Message = "重新加载所有插件")]
        public virtual void ReloadPlugins()
        {
            try
            {
                _logger.LogInfo("开始重新加载所有插件...");

                // 卸载当前显示的插件 UI
                _pluginService.UnloadCurrentPlugin();

                // 清空列表与字典
                lsPlugins.Clear();
                _userControlsService.ClearAllControls();

                // 重新扫描加载
                _userControlsService.LoadAndShowUserControl();

                // 重新填充插件列表
                foreach (var item in _userControlsService.DctControls)
                {
                    lsPlugins.Add(new PluginsModel
                    {
                        Name = item.Value.Name,
                        ID = item.Key,
                        Status = 0
                    });
                }

                // 重置选中项，避免指向已清除的旧对象
                SelectedPlugin = null;
                _logger.LogInfo($"插件重新加载完成，共 {lsPlugins.Count} 个插件");
                _messenger.Send(new LogAppendMessage($"插件已重新加载，共 {lsPlugins.Count} 个"));
            }
            catch (Exception ex)
            {
                _logger.ErrorInfo("ReloadPlugins", ex.Message);
            }
        }

        [Log(Level = LogLevel.Info, Message = "显示插件")]
        public virtual void ShowPlugin(string pluginId)
        {
            // 显示插件时关闭工具箱工具视图，避免工具覆盖插件区
            if (IsToolActive)
            {
                ActiveToolView = null;
                IsToolActive = false;
            }
            _pluginService.ShowPlugin(pluginId);
        }

        [Log(Level = LogLevel.Info, Message = "发送命令")]
        public virtual void SendCommand(string command, object parameter = null!)
        {
            _pluginService.SendCommand(command, parameter);
        }

        [Log(Level = LogLevel.Info, Message = "处理插件回调")]
        public virtual void HandlePluginCallback(string pluginId, PluginEventArgs e)
        {
            _pluginService.HandlePluginCallback(pluginId, e);
        }

        #endregion

        #region 脚本操作（委托给 PluginService）

        [Log(Level = LogLevel.Info, Message = "新建脚本")]
        public virtual void ExecuteNewScript()
        {
            _pluginService.ExecuteScriptCommand("New");
        }

        [Log(Level = LogLevel.Info, Message = "打开脚本")]
        public virtual void ExecuteOpenScript()
        {
            _pluginService.ExecuteScriptCommand("Open");
        }

        [Log(Level = LogLevel.Info, Message = "保存脚本")]
        public virtual void ExecuteSaveScript()
        {
            _pluginService.ExecuteScriptCommand("Save");
        }

        #endregion

        #region 工具箱操作（委托给 ToolboxService）

        /// <summary>
        /// 打开指定工具箱工具到主工作区（叠加显示，不清空插件区）
        /// </summary>
        /// <param name="toolId">工具唯一标识</param>
        [Log(Level = LogLevel.Info, Message = "打开工具箱工具")]
        public virtual void SelectTool(string toolId)
        {
            try
            {
                var view = _toolboxService.OpenTool(toolId);
                if (view == null)
                {
                    _logger.ErrorInfo("SelectTool", $"工具箱中不存在该工具: {toolId}");
                    return;
                }
                ActiveToolView = view;
                IsToolActive = true;
                _logger.LogInfo($"工具箱工具已打开: {toolId}");
            }
            catch (Exception ex)
            {
                _logger.ErrorInfo("SelectTool", ex.Message);
            }
        }

        /// <summary>
        /// 关闭当前工具箱工具，恢复显示插件区内容
        /// </summary>
        [Log(Level = LogLevel.Info, Message = "关闭工具箱工具")]
        public virtual void CloseTool()
        {
            ActiveToolView = null;
            IsToolActive = false;
            _logger.LogInfo("工具箱工具已关闭");
        }

        #endregion

        #region 窗口管理

        [Log(Level = LogLevel.Info, Message = "显示关于窗口")]
        public virtual void ShowAbout()
        {
            try
            {
                var aboutWindow = new View.AboutWindow
                {
                    Owner = Application.Current.MainWindow
                };
                aboutWindow.ShowDialog();
                _logger.LogInfo("关于窗口已打开");
            }
            catch (Exception ex)
            {
                _logger.ErrorInfo("ShowAbout", ex.Message);
            }
        }

        [Log(Level = LogLevel.Info, Message = "显示插件管理器窗口")]
        public virtual void ShowPluginManager()
        {
            try
            {
                var pluginManagerWindow = new View.PluginManagerWindow
                {
                    Owner = Application.Current.MainWindow
                };
                pluginManagerWindow.ShowDialog();
                _logger.LogInfo("插件管理器窗口已打开");
            }
            catch (Exception ex)
            {
                _logger.ErrorInfo("ShowPluginManager", ex.Message);
            }
        }

        [Log(Level = LogLevel.Info, Message = "窗体移动")]
        public virtual void Move_Window(object sender, MouseButtonEventArgs e)
        {
            try
            {
                if (e.OriginalSource is Border)
                {
                    var window = Window.GetWindow((DependencyObject)sender);
                    window?.DragMove();
                }
            }
            catch (Exception ex)
            {
                _logger.ErrorInfo("Move_Window", ex.Message);
            }
        }

        /// <summary>
        /// 委托：刷新性能数据到 UI
        /// </summary>
        public virtual void Show_Cpu_Memory(string cpu, string memory)
        {
            try
            {
                Txt_Cpu = cpu + "%";
                Txt_Memory = memory + "GB";
            }
            catch (Exception ex)
            {
                _logger.ErrorInfo("Show_Cpu_Memory", ex.Message);
            }
        }

        /// <summary>
        /// 切换全屏显示：委托给主窗口的 Win32 实现
        /// </summary>
        [Log(Level = LogLevel.Info, Message = "切换全屏")]
        public virtual void ToggleFullScreen()
        {
            try
            {
                if (Application.Current.MainWindow is MainWindow mainWindow)
                {
                    mainWindow.ToggleFullScreen();
                    IsFullScreen = !IsFullScreen;
                    _logger.LogInfo(IsFullScreen ? "进入全屏模式" : "退出全屏模式");
                }
            }
            catch (Exception ex)
            {
                _logger.ErrorInfo("ToggleFullScreen", ex.Message);
            }
        }

        [Log(Level = LogLevel.Info, Message = "最小化到系统托盘")]
        public virtual void MinimizeToTray()
        {
            try
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (Application.Current.MainWindow is MainWindow mainWindow)
                        mainWindow.Hide();
                });
                _logger.LogInfo("最小化到系统托盘");
            }
            catch (Exception ex)
            {
                _logger.ErrorInfo("MinimizeToTray", ex.Message);
            }
        }

        [Log(Level = LogLevel.Info, Message = "退出应用程序")]
        public virtual void ExitApplication()
        {
            try
            {
                _trayIconService.MarkExiting();
                _logger.LogInfo("退出应用程序");
                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                _logger.ErrorInfo("ExitApplication", ex.Message);
            }
        }

        #endregion
    }
}
