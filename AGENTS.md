# YFrame 项目参考（AI 快速理解用）

> **编写目的：** 供 AI 在新对话中快速理解此项目，避免重复读取所有代码。
> **生成日期：** 2026-07-22
> **最后更新：** 2026-07-30 — 新增 YF_PluginServer（插件服务器）+ PluginManagerWindow（插件管理器窗口）+ PluginManagerService + 插件热重载

---

## 一、项目概述

YFrame 是一个基于 **.NET 8.0 + WPF** 的**模块化插件桌面框架**，采用抽屉式 IDE 风格 Shell，通过**反射动态加载插件**，为各类开发者工具提供统一运行平台。

- **项目路径：** `C:\Users\Administrator\Desktop\code\C#\YFrame`
- **解决方案：** `YFrame.sln`（Visual Studio 2022）
- **语言：** C# 12.0，nullable enabled

### 解决方案组成

| 项目 | 路径 | 类型 | 输出 |
|------|------|------|------|
| YFrame | `YFrame\` | WPF Application | `YFrame.exe`（主框架 Shell） |
| YF_Manager | `YF_Manager\` | Class Library | `YF_Manager.dll`（共享基础设施库） |
| YFrame.Tests | `YFrame.Tests\` | xUnit Test Project | 单元测试（94 个用例） |
| YFrame.Installer | `YFrame.Installer\` | WPF Application | `YFrame.Installer.exe`（框架安装程序，仅安装本体） |

### 插件（位于上层目录 `C:\Users\Administrator\Desktop\code\C#\`）

| 插件 | 目录 | 功能 | 核心依赖 |
|------|------|------|----------|
| YF_AIHelper | `YF_AIHelper\` | 本地 LLM AI 对话助手 | LLamaSharp 0.24.0 + CUDA 12 |
| YF_Clicker | `YF_Clicker\` | 鼠标自动连点器 | WindowsInput |
| YF_HttpServer | `YF_HttpServer\` | 一键 HTTP 文件服务器 | HttpListener（内置） |
| YF_KMScript | `YF_KMScript\` | 中文 DSL 键鼠自动化脚本 | 无额外依赖 |
| YF_Penetration | `YF_Penetration\` | NAT 内网穿透（P2P 联机） | NatTraversal 自研库 |
| YF_ScreenOCRTranslate | `YF_ScreenOCRTranslate\` | 截图 OCR + 翻译 | PaddleOCRSharp + 百度翻译 API |
| YF_Serialport | `YF_Serialport\` | 串口调试助手（文本/HEX收发+预设命令） | System.IO.Ports 8.0.0 |
| YF_PluginServer | `YF_PluginServer\` | 插件HTTP服务器（列表+下载） | HttpListener（内置） |
| YF_TcpHelper | `YF_TcpHelper\` | TCP通讯助手（服务端/客户端+UDP广播发现） | System.Net.Sockets（内置） |

---

## 二、目录结构速览

```
YFrame/
├── YFrame.sln                          # VS 解决方案（含 YFrame + YF_Manager + YFrame.Tests 三项目）
├── README.md                           # 详细项目文档
│
├── YFrame/                             # 主框架 WPF 项目
│   ├── App.xaml                        # 应用入口 XAML（默认主题+语言资源字典、全局Style，无 StartupUri）
│   ├── App.xaml.cs                     # 入口逻辑：OnStartup构建DI容器，ChangeTheme() / ChangeLanguage()
│   ├── MainWindow.xaml                 # 主窗口布局（DockPanel三栏+Menu+StatusBar）
│   ├── MainWindow.xaml.cs              # 代码后置：构造函数注入ViewModel+Hook服务，OnSourceInitialized初始化
│   ├── ViewModel/
│   │   └── MainWindowViewModel.cs      # 核心 ViewModel（~750行，薄门面，委托给子服务）
│   ├── Service/                        # 服务层（从 ViewModel/Service 迁移）
│   │   ├── LogService.cs               # 日志面板服务（缓冲区管理、500行上限、通过 Mediator 接收日志消息）
│   │   ├── PluginService.cs            # 插件管理服务（插件切换、命令转发、热键路由、脚本操作）
│   │   ├── UserControlsService.cs      # 插件加载服务（200行，反射扫描/加载/实例化）
│   │   ├── PluginManagerService.cs     # 插件管理器服务（AOP单例，HTTP通信、下载/解压插件）
│   │   ├── HotkeyService.cs            # 全局热键服务（Win32 RegisterHotKey，AOP代理）
│   │   └── TrayIconService.cs          # 系统托盘图标服务（Win32 Shell_NotifyIcon，AOP代理）
│   ├── Model/
│   │   ├── PluginsModel.cs             # 插件列表模型（Name, ID, Status）
│   │   ├── CtrlDataModel.cs            # 运行时插件实例数据（UserControl, CommandHandler, Parameters）
│   │   └── RemotePluginInfo.cs         # 远程插件信息模型（插件管理器用，含下载状态）
│   ├── View/UC/
│   │   └── PerformanceMonitor.xaml/.cs # LiveCharts CPU/内存图表（5秒采样，30秒窗口）
│   ├── Common/
│   │   ├── Images/Logo.png             # 应用 Logo
│   │   ├── Images/Logo.ico             # 应用图标（.ico 格式）
│   │   ├── Themes/                     # 四套主题 XAML（Dark/Cream/LightBlue/Green） + ControlStyles
│   │   └── Language/                   # zh-CN.xaml + en-US.xaml
│   └── Properties/                     # 标准 WPF 配置
│
├── YF_Manager/                         # 共享框架库
│   ├── YF_Manager.cs                   # 静态入口：YF_Manager_Main 类 + 静态 logger
│   ├── BaseClass/ViewModelBase.cs      # 基类 ViewModel（实现 INotifyPropertyChanged + SetProperty）
│   ├── Interface/
│   │   ├── I_YF_Detail.cs              # 插件元数据接口（YF_ID, YF_Name）
│   │   └── I_YF_Command.cs             # 插件命令接口（ExecuteCommand, OnPluginCallback）
│   └── Common/
│       ├── YF_Di.cs                     # DI 容器全局持有者（IServiceProvider 静态引用）
│       ├── Config.cs                   # 全局常量（LogPath, PluginPath, TCP端口, PaddlePath）
│       ├── YF_Messenger.cs             # 轻量级消息中介（Mediator 模式核心，Register/Send/Unregister）
│       ├── YF_Messages.cs              # 8 种消息类型定义（LogAppend、PluginShown、HotkeyTriggered 等）
│       ├── Attributes/LogAttribute.cs  # [Log] 特性（LogLevel: Debug/Info/Warning/Error）
│       ├── Interceptors/LogInterceptor.cs  # Castle.Core IInterceptor（AOP 核心）
│       ├── Tools/
│       │   ├── YF_Manager_Log.cs       # 日志系统（HTML格式，按天/类型分文件，1MB轮转，最多999文件）
│       │   ├── YF_TcpHelper.cs         # 网络工具（GetLocalIP, GetDefaultGatewayIP）
│       │   └── YF_FileHelper.cs        # 文件工具（CopyDirectory, SetClipboardWithRetry, OpenFolder）
│       ├── YF_RelayCommand.cs          # ICommand实现（无参版 + 泛型版<T>）
│       └── YF_DelegateFunctionModel.cs # 委托类型声明（dvFunc_Vs, dvFunc_Vs_s）
│
├── YFrame.Tests/                       # 单元测试项目
│   ├── YFrame.Tests.csproj             # 引用 YF_Manager + YFrame，含 xUnit + coverlet
│   ├── YF_Manager/                     # YF_Manager 相关测试
│   │   ├── YF_RelayCommandTests.cs          # 6 个 — 无参 RelayCommand
│   │   ├── YF_RelayCommandGenericTests.cs   # 6 个 — 泛型 RelayCommand
│   │   ├── ConfigTests.cs                   # 7 个 — 配置常量
│   │   ├── YF_Manager_MainTests.cs          # 4 个 — YF_Manager_Main
│   │   ├── Common/
│   │   │   ├── YF_DelegateFunctionModelTests.cs  # 2 个 — 委托类型
│   │   │   ├── Attributes/LogAttributeTests.cs   # 5 个 — LogAttribute + LogLevel
│   │   │   └── Tools/
│   │   │       ├── YF_FileHelperTests.cs      # 15 个 — 文件系统操作
│   │   │       ├── YF_TcpHelperTests.cs       # 5 个 — 网络工具
│   │   │       └── YF_Manager_LogTests.cs     # 8 个 — 日志系统
│   │   └── Interface/
│   │       └── PluginEventArgsTests.cs        # 2 个 — 事件参数
│   ├── YFrame/                         # YFrame 主项目相关测试
│   │   ├── Service/
│   │   │   ├── LogServiceTests.cs             # 14 个 — 日志缓冲区管理
│   │   │   └── PluginServiceTests.cs          # 20 个 — 插件调度、命令路由
│   │   └── Model/
│   │       ├── PluginsModelTests.cs           # 8 个 — ViewModelBase 属性通知
│   │       └── CtrlDataModelTests.cs          # 5 个 — 插件实例数据模型
│
├── YFrame.Installer/                   # 框架安装程序（WPF 向导式，仅安装本体）
│   ├── YFrame.Installer.csproj         # 发布为自包含单文件，payload.zip 嵌入资源
│   ├── App.xaml/.cs                    # 入口逻辑
│   ├── MainWindow.xaml/.cs             # 主窗口（无边框，粒子背景动画，3步向导）
│   ├── Views/                          # WelcomePage / InstallConfigPage / ProgressPage / FinishPage
│   ├── ViewModels/                     # MainViewModel + RelayCommand + ViewModelBase
│   ├── Services/
│   │   ├── InstallService.cs           # 安装核心（文件复制、快捷方式、注册表）
│   │   └── PayloadExtractor.cs         # 从嵌入资源解压 payload.zip 到临时目录
│   ├── Models/InstallConfig.cs         # 安装配置模型
│   ├── Controls/                       # ParticleBackground + RainbowProgressBar
│   ├── Converters/Converters.cs        # Bool 转换器
│   ├── Resources/Logo.ico + payload.zip
│   └── CollectPayload.ps1              # 构建时从 YFrame bin 目录收集核心文件
│
├── Review/                             # 代码审查报告（多个HTML报告）
├── .github/workflows/
│   └── ci.yml                          # GitHub Actions CI 流水线（编译核心项目及运行单元测试）
├── .gitlab-ci.yml                      # GitLab CI 流水线（备选格式）
└── .gitignore                          # 排除 bin/obj/Log/Review 等
```

---

## 三、核心架构设计

### 3.1 依赖关系

```
YFrame.exe ──编译依赖──→ YF_Manager.dll ──→ Castle.Core 5.2.1 + Microsoft.Extensions.DI
    │                        │
    │ 运行时反射加载（无编译依赖） │  插件编译时引用
    ▼                        ▼
plugins/              所有插件项目
  ├── YF_AIHelper.dll ──→ YF_Manager.dll + LLamaSharp
  ├── YF_Clicker.dll ──→ YF_Manager.dll + WindowsInput
  ├── YF_HttpServer.dll ──→ YF_Manager.dll
  ├── YF_KMScript.dll ──→ YF_Manager.dll
  ├── YF_Penetration.dll ──→ YF_Manager.dll + NatTraversal 自研库
  ├── YF_PluginServer.dll ──→ YF_Manager.dll
  ├── YF_ScreenOCRTranslate.dll ──→ YF_Manager.dll + PaddleOCRSharp
  ├── YF_Serialport.dll ──→ YF_Manager.dll + System.IO.Ports
  └── YF_TcpHelper.dll ──→ YF_Manager.dll
```

**关键设计：** YFrame 与插件间**零编译时依赖**，完全通过 `I_YF_Detail` + `I_YF_Command` 接口契约通信。

### 3.2 插件契约接口

```csharp
// I_YF_Detail — 插件身份标识
interface I_YF_Detail { string YF_ID { get; } string YF_Name { get; } }

// I_YF_Command — 命令执行 + 事件回调
interface I_YF_Command {
    void ExecuteCommand(string command, object parameter = null);
    event EventHandler<PluginEventArgs> OnPluginCallback;
}
```

### 3.3 插件发现与加载流程

```
应用启动 → MainWindowViewModel.Init()
  → InitUI() → UserControlsService.LoadAndShowUserControl()
    1. 扫描 plugins/ 下所有子文件夹
    2. 查找 YF_*.dll 文件
    3. 过滤掉 YF_Manager.dll
    4. Assembly.LoadFrom(dllPath)
    5. 获取约定类型: {pluginName}.MainControl + {pluginName}.MainControlViewModel
    6. Activator.CreateInstance() 创建 ViewModel → 验证接口 → 存入 DctControls 字典
```

**懒加载：** 启动时仅扫描元数据（ID+Name），用户点击"显示"后才实例化 UserControl。

### 3.4 插件开发规范

| 约定项 | 规范 |
|--------|------|
| 命名空间 | 与 DLL 文件名相同（`YF_AIHelper.dll` → namespace `YF_AIHelper`） |
| 入口控件 | `{命名空间}.MainControl`，继承 `UserControl` |
| 视图模型 | `{命名空间}.MainControlViewModel`，实现 `I_YF_Detail` + `I_YF_Command` |
| 输出目录 | 编译到 `plugins/{命名空间}/` 目录下 |
| 必须引用 | `YF_Manager.dll` |

### 3.5 AOP 代理模式

**AOP 核心不变：** 被代理的方法必须声明为 `virtual`，有 `[Log]` 特性才被拦截记录。`CreateClassProxy<T>` + `LogInterceptor` 的机制完全保留。

**重构前：** 所有组件使用静态 `Lazy<T>` 单例模式：
```csharp
// 旧模式（已在 YFrame 项目中移除，YF_Manager 中仍保留以兼容插件）
private static readonly Lazy<T> _instance = new Lazy<T>(
    () => new ProxyGenerator().CreateClassProxy<T>(new LogInterceptor())
);
public static T Instance => _instance.Value;
```

**重构后（DI 模式）：** YFrame 项目的 AOP 代理由 DI 容器创建，通过属性注入填充依赖：
```csharp
// App.xaml.cs 中注册
services.AddSingleton(sp => {
    var proxy = new ProxyGenerator().CreateClassProxy<MainWindowViewModel>(
        new LogInterceptor()
    );
    proxy.InitializeDependencies(
        sp.GetRequiredService<YF_Manager_Log>(),
        sp.GetRequiredService<LogService>(),
        // ... 其余依赖
    );
    return proxy;
});
```

**采用 AOP 代理的类分布：**

| 层级 | 类 | 获取方式 |
|------|-----|---------|
| YF_Manager | `YF_Messenger`, `YF_FileHelper`, `YF_TcpHelper` | 保留 `static Instance`（插件兼容） |
| YFrame | `MainWindowViewModel`, `UserControlsService`, `HotkeyService`, `TrayIconService` | DI 容器解析，无 `Instance` |
| YFrame | `PluginManagerService` | AOP 单例（`static Instance`，对齐 YF_Manager 风格） |
| 各插件 | `MainControlViewModel` 等 | 各自使用 `static Lazy<T>` Instance（不受影响） |

### 3.6 Mediator 模式

**背景：** `MainWindowViewModel.cs` 原为 854 行的"上帝对象"，承担 UI 管理 + 命令绑定 + 热键路由 + 日志面板 + 脚本操作等多项职责，无法单元测试，修改风险高。

**解决方案：** 引入 Mediator 模式，将 ViewModel 拆分为一个薄门面 + 两个子服务，组件间通过 `YF_Messenger`（轻量消息中介）松耦合通信。

```
MainWindowViewModel（~440行，薄门面）
  │  保留：XAML 绑定属性 + 17 个命令属性 + AOP 代理
  │  删除：日志缓冲区、插件调度、热键路由、脚本命令的具体实现
  │
  ├──→ LogService ──→ YF_Messenger ←── PluginService
  │      ↑ 日志消息                    ↑ 插件/热键/脚本消息
  │      │                             │
  └──────┼─────────────────────────────┘
         完全不直接引用对方，只通过 Mediator 通信
```

**YF_Messenger**（位于 `YF_Manager/Common/YF_Messenger.cs`）核心 API：

```csharp
// 订阅消息
YF_Messenger.Instance.Register<LogAppendMessage>(msg => Console.WriteLine(msg.Text));
// 发送消息
YF_Messenger.Instance.Send(new LogAppendMessage("Hello"));
// 取消订阅
YF_Messenger.Instance.Unregister<LogAppendMessage>(handler);
```

**8 种消息类型**（位于 `YF_Manager/Common/YF_Messages.cs`，均为 `record` 类型）：

| 消息 | 发送方 | 接收方 | 场景 |
|------|--------|--------|------|
| `LogAppendMessage` | 任意组件 | LogService | 追加日志到面板 |
| `LogClearMessage` | ClearLogCommand | LogService | 清空日志面板 |
| `PluginShownMessage` | PluginService | 扩展点 | 插件切换通知 |
| `HotkeyTriggeredMessage` | HotkeyService 事件 | PluginService | Ctrl+Y 路由 |
| `ScriptCommandMessage` | 脚本按钮命令 | PluginService | 新建/打开/保存脚本 |
| `ThemeChangedMessage` | SetThemeCommand | 扩展点 | 主题切换通知 |
| `LanguageChangedMessage` | 语言切换命令 | 扩展点 | 语言切换通知 |
| `PanelSwitchMessage` | 面板切换命令 | 扩展点 | 侧边栏切换通知 |

**子系统职责划分：**

| 组件 | 文件 | 行数 | 职责 |
|------|------|------|------|
| MainWindowViewModel | `YFrame/ViewModel/MainWindowViewModel.cs` | 440 | 薄门面：XAML 绑定属性 + 命令声明 + AOP 入口（含 19 个命令：ReloadPlugins / PluginManager / 脚本操作等） |
| LogService | `YFrame/Service/LogService.cs` | 106 | 日志缓冲区（500行上限）、追加/清空、Mediator 订阅 |
| PluginService | `YFrame/Service/PluginService.cs` | 194 | 插件显示/切换、命令转发、热键路由、脚本操作、当前插件卸载 |

**可测试性对比：**

| | 重构前 | 重构后 |
|------|--------|--------|
| 测热键路由 | 需初始化 854 行 ViewModel | `new PluginService(mockLogger)` 即可 |
| 测日志缓冲区 | 同上 | `new LogService(mockLogger)` 即可 |
| 组件耦合 | 直接引用，紧耦合 | 仅通过 Mediator 通信，松耦合 |

### 3.7 日志系统

| 日志类型 | 目录 | 内容 |
|----------|------|------|
| InfoLog | `Log/InfoLog/` | 所有 Info 级别聚合 |
| DebugLog | `Log/DebugLog/` | 插件扫描、加载过程 |
| ErrorLog | `Log/ErrorLog/` | 异常详情、堆栈 |
| CommandLog | `Log/CommandLog/` | 框架→插件命令记录 |
| TcpLog | `Log/TcpLog/` | 网络通信日志 |
| InterceptorsLog | `Log/InterceptorsLog/` | AOP 拦截日志 |

- 文件格式：HTML（`.htm`），按日期命名（`yyyy-MM-dd.htm`）
- 单文件上限：1MB，超出自动轮转（`*_1.htm` ... `*_999.htm`）
- 线程安全：静态 `_fileLock`
- **UI 回显（Mediator 重构后）：** `YF_Manager_Log.d_LogWrite` → `YF_Messenger.Send(LogAppendMessage)` → `LogService.AppendLog()` → 回调更新 `MainWindowViewModel.LogText`
- **日志面板可清除：** 通过 `YF_Messenger.Send(LogClearMessage)` 或 `MainWindowViewModel.ClearLog()`

### 3.8 全局热键系统

```
MainWindow 加载
    │
    ▼
HotkeyService.Instance.Initialize(window)
    │  WindowInteropHelper → HwndSource.AddHook(WndProc)
    │
    ▼
用户点击工具栏"热键监控" → ToggleHotkeyCommand
    │
    ├── 开启 → HotkeyService.Instance.Register()
    │             RegisterHotKey(hWnd, HOTKEY_ID, MOD_CONTROL, VK_Y)
    │             状态栏显示 "热键Ctrl+Y: 已开启"
    │
    └── 关闭 → HotkeyService.Instance.Unregister()
                  UnregisterHotKey(hWnd, HOTKEY_ID)
                  状态栏显示 "热键Ctrl+Y: 已关闭"

Ctrl+Y 按下 → WndProc 拦截 WM_HOTKEY → OnHotkeyPressed 事件
    │
    ▼
HotkeyService 事件 → YF_Messenger.Send(HotkeyTriggeredMessage)
    │
    ▼
PluginService.OnHotkeyPressedInternal()
    │  根据当前激活插件 ID 分发命令:
    ├── "YF_ScreenOCRTranslate" → ExecuteCommand("CaptureScreen")
    ├── "YF_Clicker" → ExecuteCommand("ToggleClick")
    └── 其他插件 → ExecuteCommand("HotkeyTrigger")
```

**HotkeyService** 位于 `YFrame\Service\HotkeyService.cs`，遵循 AOP 单例模式。热键由**框架统一管理**而非各插件独立注册，避免热键冲突。热键路由由 PluginService 通过 Mediator 消息实现。

### 3.9 面板切换

主窗口的左右侧边栏支持**多标签页切换**：

| 面板 | 标签页 | 索引 | 说明 |
|------|--------|------|------|
| **左侧面板** | 插件列表 | 0 | VS Code 风格活动栏，垂直旋转文字，选中竖线指示 |
| **左侧面板** | 工具箱 | 1 | 工具箱内容区（待扩展） |
| **右侧面板** | 日志 | 0 | 实时日志输出，支持清除 |
| **右侧面板** | 参数 | 1 | 参数面板（待扩展） |

切换通过 `SwitchLeftPanelCommand` / `SwitchRightPanelCommand` 命令绑定，ViewModel 维护 `ActiveLeftPanel` / `ActiveRightPanel` 属性。面板切换会通过 `YF_Messenger` 发送 `PanelSwitchMessage`。

### 3.10 依赖注入（DI）

**背景：** 6 个 YFrame 核心组件使用静态 `Lazy<T>` 单例模式，内部大量硬编码 `XXX.Instance` 导致紧耦合，无法注入 Mock 进行单元测试。

**解决方案：** 引入 `Microsoft.Extensions.DependencyInjection`，采用**属性注入**策略兼容 Castle `CreateClassProxy` 的无参构造函数要求。

```
App.OnStartup 构建 DI 容器
    │
    ├── 注册 YF_Manager 层 AOP 服务（保留 static Instance，插件兼容）
    │   └── YF_Messenger / YF_FileHelper / YF_TcpHelper
    │
    ├── 注册 YFrame 层非 AOP 服务（构造函数注入）
    │   └── LogService(YF_Manager_Log, YF_Messenger)
    │   └── PluginService(YF_Manager_Log, YF_Messenger, UserControlsService)
    │
    ├── 注册 YFrame 层 AOP 服务（CreateClassProxy + 属性注入）
    │   └── MainWindowViewModel → InitializeDependencies(8个依赖)
    │   └── UserControlsService → InitializeDependencies(logger, 回调)
    │   └── HotkeyService / TrayIconService → 纯代理，无额外依赖
    │
    └── 注册 MainWindow（构造函数注入 ViewModel + Services）
```

**属性注入原理：** Castle `CreateClassProxy<T>` 要求在构造阶段调用无参 `new T()`。DI 容器在工厂方法中先创建代理，再通过 `InitializeDependencies(...)` 方法注入所有依赖。AOP 的 `virtual` + `[Log]` 机制完全不受影响。

**YF_Di 全局持有者：** 位于 `YF_Manager/Common/YF_Di.cs`，提供 `YF_Di.Provider`（`IServiceProvider`）和 `YF_Di.Get<T>()` 便捷方法，供插件按需解析服务。

**注入前后对比：**

| | 改造前 | 改造后 |
|------|--------|--------|
| `PluginService` 获取 `YF_Messenger` | `YF_Messenger.Instance` | 构造函数参数 `messenger` |
| `PluginService` 获取 `UserControlsService` | `UserControlsService.Instance` | 构造函数参数 `userControlsService` |
| `MainWindowViewModel` 获取所有依赖 | 8 处 `XXX.Instance` | `InitializeDependencies()` 一次设置 |
| 测试 PluginService | 需初始化全部单例链 | `new PluginService(logger, mockMessenger, mockUCService)` |

### 3.11 YFrame 安装程序

**YFrame.Installer** 是独立的 WPF 向导式安装程序，**仅安装框架本体**（不含插件和 AI 模型）。

**安装流程（3 步向导）：**

| 步骤 | 页面 | 说明 |
|------|------|------|
| 1 | WelcomePage | 欢迎页，粒子背景动画 |
| 2 | InstallConfigPage | 安装路径选择 + 桌面/开始菜单快捷方式选项 |
| 3 | ProgressPage | 彩虹渐变进度条 + 实时日志输出 |
| 完成 | FinishPage | 弹性勾号动画 + 安装成功提示 |

**Payload 机制：**

```
构建时：
  CollectPayload.ps1 → 从 YFrame\bin\ 收集核心文件（不含 plugins/、Log/、Config/、*.pdb）
       ↓
  System.IO.Compression.ZipFile → Resources\payload.zip
       ↓
  MSBuild 目标动态添加 EmbeddedResource → 嵌入 YFrame.Installer.dll

运行时：
  PayloadExtractor.GetPayloadPath()
       ↓ 优先
  从嵌入资源提取 payload.zip → %TEMP%\YFrame_Setup_xxx\
       ↓ 备用
  本地文件系统 payload/ 目录（VS 直接运行场景）
```

**关键设计：**
- **Debug 和 Release 均嵌入 payload.zip**，生成的 exe 可独立分发
- `PayloadExtractor` 优先从嵌入资源解压，文件系统仅为备用
- `CollectPayload.ps1` 仅收集核心文件 + `runtimes/` 子目录，不收集 `.pdb`、`plugins/`、`Log/`、`Config/`、`File/`
- MSBuild 目标 `PreparePayloadZip` 在 `BeforeTargets="BeforeBuild"` 执行，在目标内部动态添加 `EmbeddedResource`（解决条件求值时机问题）
- 安装到 `Program Files` 时自动请求管理员权限
- 快捷方式使用 `WScript.Shell` COM 创建 .lnk 文件
- 卸载信息写入 `HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\YFrame`

**生成可分发 exe：**
```powershell
# Debug（开发调试，含 payload.zip）
dotnet build "YFrame.Installer\YFrame.Installer.csproj" -c Debug

# Release（自包含单文件）
dotnet publish "YFrame.Installer\YFrame.Installer.csproj" -c Release
# 输出: Installer\YFrame.Installer.exe
```

---

## 四、CI/CD 流水线

### 配置文件

| 文件 | 格式 | 说明 |
|------|------|------|
| `.github/workflows/ci.yml` | GitHub Actions | **主用**，gitcode.com Actions 页面触发 |
| `.gitlab-ci.yml` | GitLab CI | **备用**，gitcode.com Pipeline 页面触发 |

### 工作流内容

```
push/PR to main → 自动触发
  └── Build Job (windows-latest)
       ├── dotnet restore YF_Manager/YF_Manager.csproj
       ├── dotnet restore YFrame/YFrame.csproj
       ├── dotnet restore YFrame.Tests/YFrame.Tests.csproj
       ├── dotnet build YF_Manager --configuration Release
       ├── dotnet build YFrame --configuration Release
       ├── dotnet build YFrame.Tests --configuration Release
       └── dotnet test YFrame.Tests --configuration Release
```

### 设计决策

- **编译核心项目及测试：** YF_Manager + YFrame + YFrame.Tests（KMScript 相关测试已移除，不再依赖外部 YF_KMScript 插件）
- **本地运行测试：** `dotnet test "YFrame\YFrame.Tests\YFrame.Tests.csproj"`
- **触发分支：** main / master，忽略 .md / Review / Log 目录变更
- **Windows Runner：** WPF 项目 `net8.0-windows` 只能在 Windows 上编译，gitcode.com 共享 runner 可能仅提供 Linux，需自行注册 Windows runner

---

## 五、主题与多语言

### 主题（4套）
| 主题 | 文件 | 主色调 |
|------|------|--------|
| 炭火暗夜 | `DarkGrayTheme.xaml` | 背景 #1E1E1E，强调色 #0078D4 |
| 素火明昼 | `CreamWhiteTheme.xaml` | 背景 #FFF5F5F8，强调色 #D94A1A |
| 冰火深蓝 | `LightBlueTheme.xaml` | 背景 #0B1526，强调色 #00CCF0 |
| 翠火青绿 | `GreenWhiteTheme.xaml` | 背景 #0A1410，强调色 #00E676 |

全局控件样式：`ControlStyles.xaml`（Button/TextBox/Label）

### 语言（2种）
- `zh-CN.xaml` 简体中文（默认）
- `en-US.xaml` English

切换方式：`App.ChangeLanguage("zh"/"en")` 或 `App.ChangeTheme("path")`，通过 `MergedDictionaries` 替换实现。主题/语言切换会通过 `YF_Messenger` 发送 `ThemeChangedMessage` / `LanguageChangedMessage`。

---

## 六、重要配置常量

| 常量 | 值 | 位置 |
|------|-----|------|
| 目标框架 | `net8.0-windows` | .csproj |
| 日志根目录 | `Log` | Config.cs |
| 插件目录 | `Plugins`（代码实际用 `"plugins"`） | Config.cs / UserControlsService.cs |
| 插件匹配 | `YF_*.dll` | UserControlsService.cs |
| 日志文件上限 | 1MB（`1024 * 1024`） | YF_Manager_Log.cs |
| 日志轮转上限 | 999 个文件 | YF_Manager_Log.cs |
| UI 日志行数 | 500 行 | LogService.cs（原在 MainWindowViewModel.cs） |
| 性能采样周期 | 5 秒 | PerformanceMonitor.cs |
| 图表数据窗口 | 6 点（30 秒） | PerformanceMonitor.cs |
| 全局热键 | Ctrl+Y (MOD_CONTROL=0x0002, VK_Y=0x59) | HotkeyService.cs |
| 热键 ID | 9001 | HotkeyService.cs |
| TCP 端口 | 服务器 8021，客户端 8022 | Config.cs |
| 插件服务器端口 | 9000 | Config.cs |
| 插件管理器地址 | http://127.0.0.1 | Config.cs |
| PaddleOCR 路径 | `plugins\YF_ScreenOCRTranslate\inference` | Config.cs |
| 窗口尺寸 | 800 x 1200 | MainWindow.xaml |
| 窗口标题 | "YF Tools" | MainWindow.xaml |
| C# 可空 | enabled | .csproj |
| 剪贴板重试 | 2 次 | YF_FileHelper.cs |

---

## 七、插件详情速查

### YF_AIHelper（AI 助手）
- **ID:** YF_AIHelper，名称："AI 助手"
- **模型:** DeepSeek-R1-Distill-Qwen-7B-Q4_K_M.gguf（本地 GGUF）
- **配置:** ContextSize=1024, GpuLayerCount=20
- **核心:** LLamaWeights → LLamaContext → InteractiveExecutor → 流式 InferAsync
- **UI:** 聊天界面，AI左/用户右气泡，消息自动滚动
- **初始化分离:** `Init(false)` 仅返回元数据不加载模型（避免文件占用），`Init(true)` 完整加载
- **已知问题:** KeyDown 事件未绑定到 XAML（Enter/Ctrl+Enter 功能不生效），`ExecuteCommand` 未区分命令类型，`Microsoft.Extensions.Configuration` 包已添加但未使用

### YF_Clicker（鼠标连点器）
- **ID:** YF_Clicker，名称："鼠标连点器"
- **核心依赖:** WindowsInput（InputSimulator 模拟鼠标点击）
- **功能:** 可设定点击间隔(ms)，后台线程连续点击，10 秒自动停止
- **启停方式:** 手动点击 UI 按钮 / 框架 Ctrl+Y 热键切换
- **状态:** UI 显示运行状态（绿/灰圆形指示器）+ 点击次数统计
- **回调:** 启停/完成时通过 `OnPluginCallback` 通知框架

### YF_HttpServer（Http 文件助手）
- **ID:** YF_HttpServer，名称："Http 文件助手"
- **核心:** HttpListener 监听端口（默认8000），后台线程运行
- **功能:** GET目录浏览+文件下载（含MIME），POST上传（X-FileName头）
- **UI:** IP/端口/路径配置 + 启停按钮 + 拖拽上传 + 防火墙/curl命令一键复制
- **自定义附加行为:** `DragDropBehavior` 实现MVVM拖放绑定
- **已知问题:** `_YF_FileHelper` 字段未初始化（OpenFolderCommand 会空引用），`ExecuteCommand` 未区分命令类型，`Run()` 中有调试残留代码

### YF_KMScript（脚本编辑器）
- **ID:** YF_KMScript，名称："脚本编辑器"
- **核心:** 自研中文DSL解释器（`ScriptInterpreter`，独立在 `Services/` 目录中，416行），OpenCV 模板匹配（`ImageMatcher`）
- **语法:** 定义/找图/点击/等待/循环/如果/否则/截图/输出，`//` 注释，Python 风格缩进（Tab 或 2 空格）
- **找图:** OpenCvSharp4 `CCOEFF_NORMED` 算法，`匹配阈值` 变量控制相似度
- **特性:** 后台执行 + `_shouldStop` 可中断 + 嵌套循环 + if/else 条件判断 + 区域截图选择窗口
- **新增服务:** `ImageMatcher`（OpenCV 找图）、`LogEntry`（日志模型）、`RegionSelectionWindow`（截图选区）、`ScreenCapture`（截图工具）
- **文件格式:** `.ys` 脚本文件
- **内部 RelayCommand:** 自定义简单 RelayCommand，未使用框架的 `YF_RelayCommand`

### YF_Penetration（NAT 内网穿透）
- **ID:** YF_Penetration，名称："NAT 内网穿透"
- **核心依赖:** 自研 NatTraversal 库（Client + Server + Shared 三层）
- **功能:** Host 创建房间 → 生成加入码 → Player 加入 → P2P 中继转发
- **模式:** Host / Player 两种角色，TCP / UDP 双协议中继
- **UI:** 双标签页切换（Host/Player），连接状态指示器，统计面板
- **状态:** 开发中，已具备基本房间创建/加入/中继功能

### YF_PluginServer（插件服务器）
- **ID:** YF_PluginServer，名称："插件服务器"
- **核心依赖:** HttpListener（内置）
- **功能:** 扫描本地 plugins/ 目录并启动 HTTP API 服务，提供 `GET /api/plugins` 插件列表（JSON 格式）和 `GET /api/plugins/{id}/download` 插件 ZIP 下载
- **API:** 返回 JSON 含插件 ID、名称、文件大小、文件数量等元数据
- **UI:** 服务器启停控制、本机 IP 和端口配置、本地插件列表展示（含文件数/大小/下载 API 路径）
- **配置持久化:** 端口号写入 `Config.PluginServerPort`

### YF_ScreenOCRTranslate（OCR 实时翻译）
- **ID:** YF_ScreenOCRTranslate，名称："OCR 实时翻译"
- **工作流:** Ctrl+Y热键 → 全屏截图选区 → PaddleOCR识别 → 百度翻译API → Canvas叠加显示
- **DPI:** 1.25倍缩放补偿（硬编码）
- **置信度:** >0.6过滤
- **模型:** PP-OCRv5 mobile（det/rec/cls）
- **已知问题:** 百度 API 密钥硬编码在源码中（appId/secretKey），`PaddleOCREngine` 未释放可能导致 GPU 内存泄漏，翻译 API 使用 HTTP 而非 HTTPS

### YF_Serialport（串口通讯助手）
- **ID:** YF_Serialport，名称："串口通讯助手"
- **核心依赖:** System.IO.Ports 8.0.0
- **功能:** 串口参数配置（端口/波特率/数据位/停止位/校验位/编码）、文本/HEX 双模式收发、预设命令管理（增删+JSON持久化）
- **架构:** ViewModel（870行）→ SerialPortService（457行，串口核心）+ PresetCommandService（292行，预设命令CRUD）
- **接收处理:** 文本模式用 StringBuilder 按 `\n` 换行累积输出；HEX 模式即时逐帧触发
- **预设命令:** 存储于 `Config\PortCommand\presets_index.json`，内存中用 `Dictionary<string, PresetCommand>` 缓存（忽略大小写）
- **已知问题:** `ExecuteCommand` 仅触发 `OnPluginCallback` 通知处理已开始，未解析命令参数

### YF_TcpHelper（TCP通讯助手）
- **ID:** YF_TcpHelper，名称："TCP通讯助手"
- **核心依赖:** 无额外 NuGet 依赖（使用 .NET 内置 `System.Net.Sockets`）
- **功能:** TCP 服务端（多客户端管理+广播发送）/ 客户端（连接+广播接收）双模式，UDP 广播自动发现
- **架构:** ViewModel（1031行）→ TcpServerService（721行，服务端TCP+UDP广播监听）+ TcpClientService（436行，客户端TCP+UDP广播发送）
- **广播发现流程:** 服务端 `UdpClient.Send(IPAddress.Broadcast)` 宣告 `IP:Port` → 客户端 `UdpClient` 接收 → 按分隔符解析 → 自动/手动反向 TCP 连接
- **并发安全:** `ConcurrentDictionary<string, ClientSession>` 管理客户端会话；UI 更新通过 `Dispatcher.Invoke` Marshal
- **接收处理:** 与串口助手相同的按行累积逻辑，500 行上限裁剪
- **已知问题:** 广播由 `TcpClientService.SendBroadcast` 发送但监听由 `TcpServerService.StartBroadcastListen` 接收，职责划分不够清晰；`ExecuteCommand` 未区分命令类型

---

## 八、关键文件路径映射

| 文件 | 路径 |
|------|------|
| 解决方案 | `YFrame/YFrame.sln` |
| README | `YFrame/README.md` |
| AGENTS | `YFrame/AGENTS.md` |
| App 入口 | `YFrame/YFrame/App.xaml.cs` |
| 主窗口 XAML | `YFrame/YFrame/MainWindow.xaml` |
| 核心 ViewModel | `YFrame/YFrame/ViewModel/MainWindowViewModel.cs` |
| 日志面板服务 | `YFrame/YFrame/Service/LogService.cs` |
| 插件管理服务 | `YFrame/YFrame/Service/PluginService.cs` |
| LogService 测试 | `YFrame/YFrame.Tests/YFrame/Service/LogServiceTests.cs` |
| PluginService 测试 | `YFrame/YFrame.Tests/YFrame/Service/PluginServiceTests.cs` |
| 插件加载服务 | `YFrame/YFrame/Service/UserControlsService.cs` |
| 插件管理器服务 | `YFrame/YFrame/Service/PluginManagerService.cs` |
| 热键服务 | `YFrame/YFrame/Service/HotkeyService.cs` |
| 托盘图标服务 | `YFrame/YFrame/Service/TrayIconService.cs` |
| 插件管理器窗口 | `YFrame/YFrame/View/PluginManagerWindow.xaml` + `.cs` |
| 插件管理器ViewModel | `YFrame/YFrame/ViewModel/PluginManagerViewModel.cs` |
| 插件元数据模型 | `YFrame/YFrame/Model/PluginsModel.cs` |
| 插件实例模型 | `YFrame/YFrame/Model/CtrlDataModel.cs` |
| 性能监视器 | `YFrame/YFrame/View/UC/PerformanceMonitor.xaml.cs` |
| 消息中介（Mediator） | `YFrame/YF_Manager/Common/YF_Messenger.cs` |
| 消息类型定义 | `YFrame/YF_Manager/Common/YF_Messages.cs` |
| DI 全局持有者 | `YFrame/YF_Manager/Common/YF_Di.cs` |
| 接口定义 | `YFrame/YF_Manager/Interface/I_YF_Detail.cs` + `I_YF_Command.cs` |
| AOP 拦截器 | `YFrame/YF_Manager/Common/Interceptors/LogInterceptor.cs` |
| 日志系统 | `YFrame/YF_Manager/Common/Tools/YF_Manager_Log.cs` |
| 文件工具 | `YFrame/YF_Manager/Common/Tools/YF_FileHelper.cs` |
| 日志特性 | `YFrame/YF_Manager/Common/Attributes/LogAttribute.cs` |
| 全局常量 | `YFrame/YF_Manager/Common/Config.cs` + `Tools/YF_ConfigHelper.cs` |
| RelayCommand | `YFrame/YF_Manager/Common/YF_RelayCommand.cs` |
| 工具类 | `YFrame/YF_Manager/Common/Tools/YF_TcpHelper.cs` |
| 主题 | `YFrame/YFrame/Common/Themes/*.xaml` |
| 语言 | `YFrame/YFrame/Common/Language/*.xaml` |
| 单元测试项目 | `YFrame/YFrame.Tests/` |
| CI 工作流（Actions） | `YFrame/.github/workflows/ci.yml` |
| CI 流水线（GitLab） | `YFrame/.gitlab-ci.yml` |
| 安装程序入口 | `YFrame/YFrame.Installer/App.xaml.cs` |
| 安装程序主窗口 | `YFrame/YFrame.Installer/MainWindow.xaml` |
| 安装核心服务 | `YFrame/YFrame.Installer/Services/InstallService.cs` |
| Payload 提取器 | `YFrame/YFrame.Installer/Services/PayloadExtractor.cs` |
| 安装程序 ViewModel | `YFrame/YFrame.Installer/ViewModels/MainViewModel.cs` |
| Payload 收集脚本 | `YFrame/YFrame.Installer/CollectPayload.ps1` |

---

## 九、开发注意事项

1. **添加新插件：** 在 `C:\Users\Administrator\Desktop\code\C#\` 下创建 `YF_XXX` 目录，按约定编写 `MainControl.xaml` + `MainControlViewModel.cs`，输出到 `plugins/YF_XXX/`
2. **修改主框架：** 编辑 `YFrame/` 和 `YF_Manager/` 下的代码，用 Visual Studio 2022 打开 `YFrame.sln`
3. **当前只支持 x64 / Any CPU** 配置
4. **AOP 代理要求方法为 virtual** 且有 `[Log]` 特性
5. **全局热键 Ctrl+Y 由框架 HotkeyService 统一管理**，插件无需各自注册热键，只需实现 `ExecuteCommand("ToggleClick" / "CaptureScreen" / "HotkeyTrigger")`
6. **DI 模式：**
   - YFrame 项目的服务（MainWindowViewModel / PluginService / LogService / UserControlsService / HotkeyService / TrayIconService）**不再使用 static Instance**
   - 所有依赖通过 DI 容器的构造函数注入（非 AOP 类）或 `InitializeDependencies()` 属性注入（AOP 类）
   - 如需在 YFrame 内部解析服务，使用 `YF_Di.Provider.GetRequiredService<T>()`
   - 插件端继续使用 `YF_Messenger.Instance` / `YF_FileHelper.Instance`（不受影响）
7. **Mediator 模式（重要）：**
   - 新增跨组件通信请使用 `YF_Messenger.Instance.Send()` / `Register()`，不要直接引用其他服务
   - MainWindowViewModel 是薄门面，复杂逻辑应放在 LogService 或 PluginService 中
   - PluginService 和 LogService **不需要 AOP**（已由 MainWindowViewModel 的 virtual 方法提供 AOP 入口）
8. **运行单元测试：** `dotnet test "YFrame\YFrame.Tests\YFrame.Tests.csproj"`
8. **CI/CD：**
   - CI 流水线编译核心项目（YF_Manager + YFrame）并运行 YFrame.Tests 单元测试
   - `.github/workflows/ci.yml` 和 `.gitlab-ci.yml` 二选一使用，gitcode.com 同时支持两种格式
   - WPF 项目需要 Windows Runner，gitcode.com 共享 runner 可能仅提供 Linux，需自行注册
 9. **已知问题：**
    - `DarkGrayTheme.xaml` 缺少图表相关资源键（其他三个主题有）
    - 目录名 `Plugins` vs `plugins` 大小写不一致
    - YF_AIHelper: KeyDown 未绑定到 XAML（Enter/Ctrl+Enter 不生效）
    - YF_HttpServer: `_YF_FileHelper` 未初始化（OpenFolderCommand 空引用）
    - YF_ScreenOCRTranslate: 百度 API 密钥硬编码，PaddleOCREngine 未释放
    - YF_Serialport: `ExecuteCommand` 未解析命令参数
    - YF_TcpHelper: 广播发送/监听职责分散在两个 Service 中，`ExecuteCommand` 未区分命令类型
10. **Logo.png 引用绝对路径**（`.csproj` 第22行），移植时需注意
11. **YF_FileHelper 现采用 AOP 单例模式**，新增 `OpenFolder()` 方法可打开任意文件夹（不存在则自动创建）
12. **测试项目 YFrame.Tests** 引用 YF_Manager + YFrame，需要联网环境（YF_TcpHelper 测试）和临时文件系统权限（YF_FileHelper 测试），其余测试均为纯逻辑测试
13. **YFrame.Installer：**
    - 仅安装框架本体（不含插件和 AI 模型），Debug 和 Release 均将 payload.zip 嵌入 exe
    - 独立分发时只需 `YFrame.Installer.exe` 一个文件，Payload 自动从嵌入资源解压
    - 安装流程 3 步：欢迎 → 配置安装路径和快捷方式 → 安装进度 → 完成
    - 收集脚本 `CollectPayload.ps1` 仅拷贝核心文件 + runtimes 目录，排除 plugins/、Log/、Config/、File/、*.pdb
14. **插件热重载：** 菜单栏→插件→重新加载插件，执行 `ReloadPlugins()`（`MainWindowViewModel.cs` → `PluginService.UnloadCurrentPlugin()` + `UserControlsService.ClearAllControls()` + `LoadAndShowUserControl()`）
15. **插件服务器与插件管理器：** `YF_PluginServer` 提供 HTTP API 分发插件，`PluginManagerWindow` 作为客户端连接服务器并下载/安装插件，服务器端口（`PluginServerPort`）和客户端地址（`PluginManagerServerURL`）均持久化到 `Config/config.conf`
