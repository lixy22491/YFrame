using Castle.DynamicProxy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using System.Configuration;
using System.Data;
using System.IO;
using System.Windows;
using YF_Manager;

namespace YFrame
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// 通过 OnStartup 构建 DI 容器并启动主窗口
    /// 支持 --uninstall 命令行参数以执行卸载流程
    /// </summary>
    public partial class App : Application
    {
        public static YF_Manager_Log logger = new YF_Manager_Log("App", "Interaction App");

        /// <summary>
        /// 应用启动入口：处理 --uninstall 或构建 DI 容器启动主窗口
        /// </summary>
        protected override void OnStartup(StartupEventArgs e)
        {
            // 控制面板卸载时传入 --uninstall 参数
            if (e.Args.Length > 0 && (e.Args.Contains("--uninstall") || e.Args.Contains("-uninstall")))
            {
                HandleUninstall(e.Args);
                Shutdown();
                return;
            }

            // 调用父类 OnStartup，确保 WPF 标准行为执行
            base.OnStartup(e);

            // 初始化静态 logger（供 LogInterceptor 等底层组件使用）
            YF_Manager_Main.logger = new YF_Manager_Log("主控类", "YF_Manager");

            // 构建 DI 容器
            var services = new ServiceCollection();
            ConfigureServices(services);
            var provider = services.BuildServiceProvider();

            // 设置全局 DI 持有者，供插件和底层库解析
            YF_Di.Provider = provider;

            // 从 DI 容器获取主窗口（ViewModel 通过属性注入已完成初始化）
            var mainWindow = provider.GetRequiredService<MainWindow>();
            mainWindow.Show();
        }

        /// <summary>
        /// 注册所有服务到 DI 容器
        /// </summary>
        private static void ConfigureServices(ServiceCollection services)
        {
            // ===== YF_Manager 层：AOP 单例服务（为保持插件兼容性） =====
            // 注册服务
            services.AddSingleton(_ => YF_Messenger.Instance);
            services.AddSingleton(_ => YF_FileHelper.Instance);
            services.AddSingleton<YF_Manager_Log>(sp =>
                new YF_Manager_Log("主框架", "YF_Frame", sp.GetRequiredService<YF_FileHelper>()));

            // ===== YFrame 层：非 AOP 服务（直接构造函数注入） =====
            services.AddSingleton(sp => new LogService(
                sp.GetRequiredService<YF_Manager_Log>(),
                sp.GetRequiredService<YF_Messenger>()
            ));

            // UserControlsService：AOP 代理 + 依赖注入
            services.AddSingleton(sp =>
            {
                var proxy = new ProxyGenerator().CreateClassProxy<UserControlsService>(
                    new LogInterceptor()
                );
                // 暂时不设回调，等 PluginService 创建后再设置
                proxy.InitializeDependencies(
                    sp.GetRequiredService<YF_Manager_Log>(),
                    null! // 回调稍后由 MainWindowViewModel 注入
                );
                return proxy;
            });

            // PluginService：依赖 YF_Manager_Log + YF_Messenger + UserControlsService
            services.AddSingleton(sp => new PluginService(
                sp.GetRequiredService<YF_Manager_Log>(),
                sp.GetRequiredService<YF_Messenger>(),
                sp.GetRequiredService<UserControlsService>()
            ));

            // HotkeyService：AOP 代理
            services.AddSingleton(_ =>
                new ProxyGenerator().CreateClassProxy<HotkeyService>(new LogInterceptor())
            );

            // TrayIconService：AOP 代理
            services.AddSingleton(_ =>
                new ProxyGenerator().CreateClassProxy<TrayIconService>(new LogInterceptor())
            );

            // ToolboxService：工具箱工具注册表（普通单例，无 AOP 需求）
            services.AddSingleton<ToolboxService>();

            // ===== MainWindowViewModel：AOP 代理 + 全部依赖注入 =====
            services.AddSingleton(sp =>
            {
                // 解析所有子服务
                var logService = sp.GetRequiredService<LogService>();
                var pluginService = sp.GetRequiredService<PluginService>();
                var userControlsService = sp.GetRequiredService<UserControlsService>();
                var hotkeyService = sp.GetRequiredService<HotkeyService>();
                var trayIconService = sp.GetRequiredService<TrayIconService>();
                var messenger = sp.GetRequiredService<YF_Messenger>();
                var fileHelper = sp.GetRequiredService<YF_FileHelper>();

                // 设置 UserControlsService 的回调（将其转发到 PluginService）
                userControlsService.OnPluginCallback = (pluginId, evt) =>
                    pluginService.HandlePluginCallback(pluginId, evt);

                // 创建 MainWindowViewModel 的 AOP 代理
                var proxy = new ProxyGenerator().CreateClassProxy<MainWindowViewModel>(
                    new LogInterceptor()
                );

                // 属性注入所有依赖
                proxy.InitializeDependencies(
                    sp.GetRequiredService<YF_Manager_Log>(),
                    logService,
                    pluginService,
                    userControlsService,
                    hotkeyService,
                    trayIconService,
                    messenger,
                    fileHelper,
                    sp.GetRequiredService<ToolboxService>()
                );

                return proxy;
            });

            // ===== MainWindow：通过构造函数注入 ViewModel =====
            services.AddSingleton(sp =>
            {
                var vm = sp.GetRequiredService<MainWindowViewModel>();
                var hotkeyService = sp.GetRequiredService<HotkeyService>();
                var trayIconService = sp.GetRequiredService<TrayIconService>();
                return new MainWindow(vm, hotkeyService, trayIconService);
            });
        }

        /// <summary>
        /// 切换语言
        /// </summary>
        /// <param name="lang">zh / en</param>
        public static void ChangeLanguage(string lang)
        {
            try
            {
                var dict = new ResourceDictionary();
                dict.Source = lang switch
                {
                    "en" => new Uri("Common/Language/en-US.xaml", UriKind.Relative),
                    _ => new Uri("Common/Language/zh-CN.xaml", UriKind.Relative)
                };

                // 移除旧的语言资源
                var oldDict = Current.Resources.MergedDictionaries
                    .FirstOrDefault(d => d.Source?.ToString().Contains("en-US") == true);
                if (oldDict == null)
                    oldDict = Current.Resources.MergedDictionaries
                        .FirstOrDefault(d => d.Source?.ToString().Contains("zh-CN") == true);
                if (oldDict != null)
                    Current.Resources.MergedDictionaries.Remove(oldDict);

                // 添加新语言资源
                Current.Resources.MergedDictionaries.Add(dict);
            }
            catch (Exception ex)
            {
                logger.ErrorInfo("ChangeLanguage", ex.Message);
            }
        }

        /// <summary>
        /// 切换主题
        /// </summary>
        /// <param name="themePath">主题文件相对路径，如 "Common/Themes/DarkGrayTheme.xaml"</param>
        public static void ChangeTheme(string themePath)
        {
            try
            {
                // 移除旧的主题资源（仅匹配 /Themes/*Theme.xaml，避免误删 ControlStyles.xaml）
                var merged = Application.Current.Resources.MergedDictionaries;                int oldIndex = -1;
                for (int i = 0; i < merged.Count; i++)
                {
                    var src = merged[i].Source?.ToString();
                    if (src != null && src.Contains("/Themes/") && src.EndsWith("Theme.xaml"))
                    {
                        oldIndex = i;
                        break;
                    }
                }
                if (oldIndex >= 0)
                    merged.RemoveAt(oldIndex);

                // 在原位置插入新主题，保持 MergedDictionaries 顺序不变
                var newTheme = new ResourceDictionary { Source = new Uri(themePath, UriKind.Relative) };
                merged.Insert(oldIndex >= 0 ? oldIndex : 0, newTheme);
            }
            catch (Exception ex)
            {
                logger.ErrorInfo("ChangeTheme", ex.Message);
                MessageBox.Show($"主题切换失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 处理 --uninstall 命令行参数：执行卸载流程
        /// </summary>
        /// <param name="args">命令行参数数组</param>
        private static void HandleUninstall(string[] args)
        {
            // 获取当前可执行文件所在目录作为安装路径
            var installPath = AppDomain.CurrentDomain.BaseDirectory;
            var exePath = Path.Combine(installPath, "YFrame.exe");
            bool isQuiet = args.Contains("--quiet") || args.Contains("-quiet");

            try
            {
                // 确认卸载（非静默模式时弹窗询问）
                if (!isQuiet)
                {
                    var result = MessageBox.Show(
                        "确定要卸载 YFrame 工具集吗？\n\n这将删除所有框架文件。\n安装的插件将保留在配置目录中。",
                        "YFrame 卸载",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question,
                        MessageBoxResult.No);

                    if (result != MessageBoxResult.Yes)
                    {
                        MessageBox.Show("已取消卸载。", "YFrame 卸载", MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }
                }

                // 删除桌面快捷方式
                var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                var desktopShortcut = Path.Combine(desktopPath, "YFrame 工具集.lnk");
                if (File.Exists(desktopShortcut))
                    File.Delete(desktopShortcut);

                // 删除开始菜单项
                var startMenuPath = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
                var appStartMenuDir = Path.Combine(startMenuPath, "YFrame");
                if (Directory.Exists(appStartMenuDir))
                    Directory.Delete(appStartMenuDir, true);

                // 删除注册表卸载信息
                try
                {
                    using var parentKey = Registry.CurrentUser.OpenSubKey(
                        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", writable: true);
                    parentKey?.DeleteSubKey("YFrame", throwOnMissingSubKey: false);
                }
                catch { }

                // 删除安装目录（延迟删除：创建一个临时批处理脚本来自删除）
                // 因为当前 exe 正在运行，无法直接删除自身目录
                var batchPath = Path.Combine(Path.GetTempPath(), "YFrame_Uninstall.bat");
                var batchContent = $"@echo off{Environment.NewLine}" +
                    $":loop{Environment.NewLine}" +
                    $"timeout /t 1 /nobreak >nul{Environment.NewLine}" +
                    $"if exist \"{exePath}\" goto loop{Environment.NewLine}" +
                    $"rmdir /s /q \"{installPath}\"{Environment.NewLine}" +
                    $"del \"%~f0\"{Environment.NewLine}";

                File.WriteAllText(batchPath, batchContent);

                // 启动批处理并以隐藏窗口运行
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c \"{batchPath}\"",
                    UseShellExecute = true,
                    CreateNoWindow = true,
                    WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden
                };
                System.Diagnostics.Process.Start(psi);

                if (!isQuiet)
                {
                    MessageBox.Show(
                        "YFrame 工具集已成功卸载。\n" +
                        "快捷方式和注册表信息已清除。\n" +
                        "安装目录将在下次系统启动后自动删除。",
                        "卸载完成",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                if (!isQuiet)
                {
                    MessageBox.Show(
                        $"卸载过程中发生错误:\n{ex.Message}",
                        "卸载失败",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
        }
    }
}
