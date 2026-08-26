# YFrame 项目介绍

> **YFrame** — 基于 C# .NET 8.0 + WPF 的模块化桌面应用框架，采用抽屉式 IDE 风格外壳，通过反射动态加载插件，为各类开发者工具提供统一的运行平台。

> **技术关键词：** .NET 8.0 · WPF · MVVM · AOP · Castle.Core · 插件化架构 · 反射 · LiveCharts · LLamaSharp · PaddleOCR · System.IO.Ports · System.Net.Sockets
>
> **开发时间：** 2025-08-18  至 2026-07-30
---

## 目录

1. [架构总览](#1-架构总览)
2. [设计模式](#2-设计模式)
3. [项目结构](#3-项目结构)
4. [核心机制详解](#4-核心机制详解)
   - [4.1 插件系统](#41-插件系统)
   - [4.2 AOP 日志拦截](#42-aop-日志拦截)
   - [4.3 全局日志系统](#43-全局日志系统)
   - [4.4 主题与多语言](#44-主题与多语言)
   - [4.5 性能监控](#45-性能监控)
   - [4.6 命令与事件通信](#46-命令与事件通信)
5. [插件生态](#5-插件生态)
6. [技术栈详情](#6-技术栈详情)
7. [数据流与生命周期](#7-数据流与生命周期)

---

## 1. 架构总览

### 1.1 分层架构

```
┌────────────────────────────────────────────────────────────────┐
│                      表示层 (Presentation)                      │
│  ┌──────────────┐  ┌──────────────────┐  ┌──────────────────┐  │
│  │  MainWindow  │  │PerformanceMonitor│  │  插件 UserControl │  │
│  │  (Shell 窗口)│  │  (LiveCharts 图表)│  │  (动态加载的 UI)  │  │
│  └──────┬───────┘  └────────┬─────────┘  └────────┬─────────┘   │
│         │                   │                     │             │
├─────────┼───────────────────┼─────────────────────┼─────────────┤
│         │         视图模型层 (ViewModel)           │             │
│  ┌──────▼──────────┐  ┌─────▼──────────┐  ┌──────▼──────────┐   │
│  │ MainWindowVM    │  │ PerfMonitor    │  │ 插件 ViewModel  │   │
│  │ (单例 + AOP)    │  │ (后台线程采集)  │  │ (单例 + AOP)     │   │
│  └──────┬──────────┘  └─────┬──────────┘  └──────┬──────────┘   │
│         │                   │                     │             │
├─────────┼───────────────────┼─────────────────────┼─────────────┤
│         │             模型层 (Model)              │              │
│  ┌──────▼──────────┐                       ┌─────▼──────────┐    │
│  │  PluginsModel   │                       │  CtrlDataModel │    │
│  │  (插件列表项)    │                       │  (运行时实例)   │    │
│  └─────────────────┘                       └────────────────┘    │
├──────────────────────────────────────────────────────────────────┤
│                      服务层 (Service)                            │
│  ┌──────────────────────────────────────────────────────────────┐│
│  │  UserControlsService (AOP)  · 插件扫描·加载·实例化·生命周期     ││
│  │  PluginService              · 插件切换·命令转发·热键路由        ││
│  │  LogService                 · 日志缓冲区·Mediator 订阅         ││
│  │  PluginManagerService (AOP) · HTTP 通信·下载/安装插件          ││
│  │  HotkeyService (AOP)        · Win32 全局热键                  ││
│  │  TrayIconService (AOP)      · Shell_NotifyIcon 托盘           ││
│  └──────────────────────────────────────────────────────────────┘│
├──────────────────────────────────────────────────────────────────┤
│                   基础设施层 (Infrastructure)                     │
│  ┌──────────────┐ ┌──────────────┐ ┌──────────────┐              │
│  │ LogInterceptor│ │ YF_Manager_Log│ │ YF_RelayCommand│          │
│  │ (AOP 方法拦截) │ │ (文件日志系统) │ │ (ICommand 封装) │         │
│  └──────────────┘ └──────────────┘ └──────────────┘              │
│  ┌──────────────┐ ┌──────────────┐ ┌──────────────┐              │
│  │ I_YF_Detail  │ │ I_YF_Command │ │ YF_FileHelper│              │
│  │ (插件元数据)  │ │ (命令/回调)   │ │v                            │
│  └──────────────┘ └──────────────┘ └──────────────┘              │
└──────────────────────────────────────────────────────────────────┘
```

### 1.2 解决方案组成

解决方案 `YFrame.sln` 包含三个项目：

| 项目 | 类型 | 输出 | 说明 |
|------|------|------|------|
| **YFrame** | WPF Application (`WinExe`) | `YFrame.exe` | 主框架外壳，负责窗口管理、插件加载、主题/语言切换、性能监控 |
| **YF_Manager** | Class Library (`UseWPF`) | `YF_Manager.dll` | 共享基础设施库，定义插件契约接口、日志系统、AOP 拦截器、命令框架、消息中介 |
| **YFrame.Tests** | xUnit Test Project | — | 单元测试（115 个用例），覆盖 YF_Manager、YFrame |
| **YFrame.Installer** | WPF Application (`WinExe`) | `YFrame.Installer.exe` | 框架安装程序，向导式 3 步安装流程，仅安装框架本体（不含插件和 AI 模型），payload.zip 内嵌于 exe |

### 1.3 依赖关系

```
YFrame.exe ──→ YF_Manager.dll ──→ Castle.Core (AOP) + Microsoft.Extensions.DI
    │                                 │
    │  运行时反射加载                  │  编译时引用
    ▼                                 ▼
plugins/                       所有插件项目
  ├── YF_AIHelper.dll ──────────→ YF_Manager.dll + LLamaSharp
  ├── YF_Clicker.dll ───────────→ YF_Manager.dll + WindowsInput
  ├── YF_HttpServer.dll ────────→ YF_Manager.dll
  ├── YF_KMScript.dll ──────────→ YF_Manager.dll
  ├── YF_Penetration.dll ───────→ YF_Manager.dll + NatTraversal 自研库
  ├── YF_PluginServer.dll ──────→ YF_Manager.dll
  ├── YF_ScreenOCRTranslate.dll → YF_Manager.dll + PaddleOCRSharp
  ├── YF_Serialport.dll ─────────→ YF_Manager.dll + System.IO.Ports
  └── YF_TcpHelper.dll ─────────→ YF_Manager.dll
```

**关键设计决策：** YFrame 与插件之间**没有编译时依赖**。框架通过 `YF_Manager.dll` 中定义的接口契约（`I_YF_Detail`、`I_YF_Command`）与插件通信，插件在运行时通过反射被发现和加载，实现了完全的**编译时解耦**。

### 1.4 Shell UI 特性

主窗口采用抽屉式 IDE 风格布局，具有以下 UI 特性：

| 特性 | 说明 |
|------|------|
| **应用 Logo** | 窗口标题栏区域显示自定义 Logo 图标 |
| **窗口控制按钮** | 显示最大化 / 最小化 / 关闭按钮 |
| **侧边栏文字旋转** | 左侧插件列表文字垂直旋转显示（VS Code 风格活动栏） |
| **插件选中指示** | 选中插件以左侧强调色竖线指示（无背景高亮） |
| **多标签侧面板切换** | 左侧面板（插件列表 / 工具箱）和右侧面板（日志 / 参数）支持标签页切换 |
| **全局极窄滚动条** | 5px 宽滚动条，透明轨道 + 主题强调色滑块 + 悬停动画 |
| **性能监视器边框** | LiveCharts 图表区域带边框视觉分隔 |
| **全局热键监控** | 框架统一管理 Ctrl+Y 热键，工具栏一键启停 + 状态栏状态显示 |
| **日志面板管理** | 支持清除日志面板、一键打开日志文件夹 |

---

## 2. 设计模式

本项目综合运用了多种经典设计模式，构建了一个松耦合、可扩展的插件化架构。

### 2.1 模式总览

| 设计模式 | 应用位置 | 解决的问题 |
|----------|----------|-----------|
| **依赖注入 (DI)** | `App.xaml.cs` DI 容器 + 全部 6 个服务 | 消除 `XXX.Instance` 硬编码，松耦合，可单元测试 |
| **属性注入** | `MainWindowViewModel`、`UserControlsService` | 兼容 Castle `CreateClassProxy` 无参构造要求 |
| **代理模式 (Proxy) / AOP** | `LogInterceptor` + Castle.Core `ProxyGenerator` | 在不修改业务代码的前提下，透明地注入日志记录逻辑 |
| **观察者模式 (Observer)** | `ViewModelBase`（实现 `INotifyPropertyChanged`）+ 数据绑定、`OnPluginCallback` 事件 | View 与 ViewModel 解耦；插件向宿主回传数据 |
| **命令模式 (Command)** | `YF_RelayCommand` / `YF_RelayCommand<T>` | 将 UI 操作抽象为可绑定、可测试的命令对象 |
| **策略模式 (Strategy)** | 主题切换（`ResourceDictionary` 替换）、语言切换 | 运行时动态替换行为（外观/文本），无需修改代码 |
| **工厂模式 (Factory)** | `UserControlsService.TryLoadPlugin()` 反射创建实例 | 根据运行时发现的类型信息动态创建插件实例 |
| **外观模式 (Facade)** | `YF_Manager_Log` | 将文件 I/O、日志分类、轮转、UI 回传等复杂性封装为简单接口 |
| **模板方法 (Template Method)** | `LogInterceptor.Intercept()` | 定义日志拦截骨架（记录开始→记录参数→执行→记录结果），子步骤可定制 |
| **依赖倒置 (DIP)** | `I_YF_Detail`、`I_YF_Command` 接口 | 宿主依赖抽象接口而非具体插件实现 |
| **服务定位器 (Service Locator)** | `YF_Di` | 全局 `IServiceProvider` 持有者，供插件按需解析服务 |

### 2.2 依赖注入 + AOP 代理（核心组合模式）

这是整个框架中**最重要的组合模式**。早前所有组件使用静态 `Lazy<T>` 单例，现已迁移到 DI 容器管理：

```csharp
// YFrame 项目的 AOP 服务不再使用 static Instance，由 DI 容器创建
// App.xaml.cs 中注册：
services.AddSingleton(sp => {
    var proxy = new ProxyGenerator().CreateClassProxy<MainWindowViewModel>(new LogInterceptor());
    proxy.InitializeDependencies(/* 8个依赖 */);
    return proxy;
});

// YF_Manager 的 AOP 服务保留 static Instance，向后兼容插件：
// YF_Messenger.Instance / YF_FileHelper.Instance / YF_TcpHelper.Instance
```

**设计要点：**

1. **`CreateClassProxy<T>()`** — Castle.Core 动态生成代理类，覆盖所有 `virtual` 方法
2. **`LogInterceptor`** — 在方法调用前后自动注入日志逻辑（记录方法名、参数、返回值、耗时）
3. **属性注入** — 因为 Castle 需要无参构造函数来创建代理，依赖通过 `InitializeDependencies()` 方法注入
4. **构造函数注入** — 非 AOP 的普通服务（`LogService`、`PluginService`）直接构造函数注入

**采用 AOP 代理的 YFrame 类（DI 管理）：**

| 类 | 所属项目 | 角色 | 获取方式 |
|----|----------|------|----------|
| `MainWindowViewModel` | YFrame | 主窗口视图模型 | DI 容器解析 |
| `UserControlsService` | YFrame | 插件加载服务 | DI 容器解析 |
| `HotkeyService` | YFrame | 全局热键服务 | DI 容器解析 |
| `TrayIconService` | YFrame | 托盘图标服务 | DI 容器解析 |
| `PluginManagerService` | YFrame | 插件管理器服务（HTTP + 下载） | AOP 单例（`static Instance`） |

**YF_Manager 层（保留 static Instance，插件兼容）：**

| 类 | 所属项目 | 角色 |
|----|----------|------|
| `YF_Messenger` | YF_Manager | 消息中介 |
| `YF_FileHelper` | YF_Manager | 文件操作助手 |
| `YF_TcpHelper` | YF_Manager | 网络工具 |

### 2.3 MVVM 架构模式

```
┌──────────────────────┐
│       View (XAML)    │  ← 数据绑定 (DataContext)
│  MainWindow.xaml     │  ← ICommand 绑定 (Btn_Plugin_Show_Command)
│  插件 UserControl    │  ← 模板绑定 (ItemsControl + DataTemplate)
└──────────┬───────────┘
           │  DataContext = MainWindowViewModel.Instance
┌──────────▼───────────┐
│    ViewModel         │
│  · ViewModelBase          │ → 属性变更通知 View（继承基类）
│  · ICommand (RelayCommand)│ → 处理 View 的用户操作
│  · 业务逻辑              │ → 调用 Service 层
└──────────┬───────────┘
           │
┌──────────▼───────────┐
│       Model          │
│  PluginsModel        │ → 插件列表数据
│  CtrlDataModel       │ → 运行时插件实例数据
└──────────────────────┘
```

**数据绑定流：**
- View 通过 `{Binding LeftVisible}` 绑定到 ViewModel 属性
- ViewModel 通过 `OnPropertyChanged()` 通知 View 更新
- 按钮 Command 通过 `{Binding Btn_Plugin_Show_Command}` 绑定到 `YF_RelayCommand`
- 插件列表通过 `ItemsControl` + `DataTemplate` + `ObservableCollection<PluginsModel>` 动态渲染
- MainWindow 通过 DI 容器注入 ViewModel（`new MainWindow(vm, hotkey, tray)`），再赋给 `DataContext`

### 2.4 插件契约模式（接口隔离）

```
I_YF_Detail                  I_YF_Command
┌─────────────┐              ┌─────────────────────┐
│ YF_ID       │              │ ExecuteCommand()    │
│ YF_Name     │              │ OnPluginCallback    │
└─────────────┘              └─────────────────────┘
      ▲                              ▲
      │        插件 ViewModel 实现    │
      └──────────────┬───────────────┘
                     │
         ┌───────────┴───────────┐
         │ MainControlViewModel  │
         │(每个插件都实现这两个接口)│
         └───────────────────────┘
```

- **`I_YF_Detail`** — 提供插件身份标识（ID + Name），框架用它构建插件列表
- **`I_YF_Command`** — 提供命令执行入口（`ExecuteCommand`）和事件回调（`OnPluginCallback`），实现框架与插件的双向通信

### 2.5 策略模式 — 主题与语言切换

主题和语言系统采用纯策略模式，通过**运行时替换 `ResourceDictionary`** 实现：

```
Application.Current.Resources.MergedDictionaries
│
├── DarkGrayTheme.xaml  ←── 替换 ──→ CreamWhiteTheme / LightBlueTheme / GreenWhiteTheme
├── zh-CN.xaml          ←── 替换 ──→ en-US.xaml
└── ControlStyles.xaml  (全局控件样式，不随主题切换)
```

- 切换操作：移除旧字典 → 添加新字典
- XAML 中通过 `{DynamicResource key}` 引用资源，确保切换后自动刷新
- 无状态管理：主题和语言不保存在 ViewModel 中，完全由 `ResourceDictionary` 控制

---

## 3. 项目结构

```
YFrame/
├── YFrame.sln                          # Visual Studio 2022 解决方案
├── README.md                           # 项目简要说明
│
├── YFrame/                             # 主框架项目 (WPF Application)
│   ├── App.xaml                        # 应用启动配置（默认暗色主题 + 中文）
│   ├── App.xaml.cs                     # 入口逻辑 + ChangeTheme() / ChangeLanguage()
│   ├── MainWindow.xaml                 # 主窗口布局（三栏 DockPanel + Menu + StatusBar）
│   ├── MainWindow.xaml.cs              # 主窗口代码后置（设置 DataContext）
│   ├── ViewModel/
│   │   ├── MainWindowViewModel.cs      # 核心 ViewModel（薄门面，AOP）
│   │   ├── PluginManagerViewModel.cs   # 插件管理器 ViewModel（AOP，委托 Service）
│   │   └── Toolbox/                    # 工具箱工具 ViewModel（取色器/正则测试/MD5校验）
│   ├── Service/                        # 服务层
│   │   ├── UserControlsService.cs      # 插件加载服务（AOP，反射扫描/加载/实例化）
│   │   ├── PluginService.cs            # 插件管理服务（切换、命令转发、热键路由）
│   │   ├── PluginManagerService.cs     # 插件管理器服务（AOP 单例，HTTP 通信、下载/解压）
│   │   ├── LogService.cs               # 日志面板服务（缓冲区管理、Mediator 订阅）
│   │   ├── HotkeyService.cs            # 全局热键服务（AOP，Win32 RegisterHotKey）
│   │   ├── TrayIconService.cs          # 托盘图标服务（AOP，Shell_NotifyIcon）
│   │   └── ToolboxService.cs           # 工具箱工具服务（工具注册表 + 视图工厂缓存）
│   ├── Model/
│   │   ├── PluginsModel.cs             # 插件列表项模型
│   │   ├── CtrlDataModel.cs            # 运行时插件实例数据
│   │   ├── RemotePluginInfo.cs         # 远程插件信息模型（下载状态、进度）
│   │   └── ToolItem.cs                 # 工具箱工具项模型（ID、名称、描述）
│   ├── View/
│   │   ├── PluginManagerWindow.xaml/.cs  # 插件管理器窗口（连接服务器 + 下载/安装插件）
│   │   └── UC/
│   │       ├── PerformanceMonitor.xaml/.cs # CPU/内存实时监控图表（LiveCharts）
│   │       └── Toolbox/                  # 工具箱工具视图（取色器/正则测试/MD5校验 + 区域选框覆盖窗）
│   ├── Common/
│   │   ├── Images/
│   │   │   └── Logo.png               # 应用 Logo
│   │   ├── Themes/
│   │   │   ├── DarkGrayTheme.xaml      # 炭火暗夜（VS Code 暗色风，#1E1E1E）
│   │   │   ├── CreamWhiteTheme.xaml    # 素火明昼（暖白柔和，#FFF5F5F8）
│   │   │   ├── LightBlueTheme.xaml     # 冰火深蓝（深海蓝，#0B1526）
│   │   │   ├── GreenWhiteTheme.xaml    # 翠火青绿（暗绿基色，#0A1410）
│   │   │   └── ControlStyles.xaml      # 全局控件统一样式（Button / TextBox / Label）
│   │   └── Language/
│   │       ├── zh-CN.xaml              # 简体中文字符串资源
│   │       └── en-US.xaml              # 英文字符串资源
│
├── YF_Manager/                         # 共享框架库 (Class Library)
│   ├── YF_Manager.cs                   # 静态入口类（持有静态 logger 实例）
│   ├── BaseClass/ViewModelBase.cs      # 基类 ViewModel（实现 INotifyPropertyChanged + SetProperty）
│   ├── Interface/
│   │   ├── I_YF_Detail.cs              # 插件元数据接口（YF_ID, YF_Name）
│   │   └── I_YF_Command.cs             # 插件命令接口（ExecuteCommand, OnPluginCallback）
│   └── Common/
│       ├── Config.cs                   # 全局常量（日志路径、插件路径、TCP 端口、插件服务器端口等）
│       ├── YF_ConfigHelper.cs          # 配置文件读写助手（Config/config.conf 键值对持久化）
│       ├── Attributes/
│       │   └── LogAttribute.cs         # [Log] 自定义特性（Level + Message）
│       ├── Interceptors/
│       │   └── LogInterceptor.cs       # Castle.Core IInterceptor 实现（方法级日志拦截）
│       ├── Tools/
│       │   ├── YF_Manager_Log.cs       # 文件日志系统（HTML 格式、按天/类型分文件、1MB 轮转）
│       │   ├── YF_TcpHelper.cs         # 网络工具（获取网关 IP、本机 IP）
│       │   ├── YF_FileHelper.cs        # 文件操作助手（目录复制、剪贴板写入重试、资源管理器打开）
│       │   ├── Md5Hasher.cs            # MD5 哈希工具（字符串/文件分块异步计算 + 双文件对比）
│       │   └── RegexHelper.cs          # 正则测试辅助（模式校验、选项构建、匹配执行）
│       ├── YF_RelayCommand.cs          # ICommand 实现（无参版 + 泛型版）
│       └── YF_DelegateFunctionModel.cs # 委托类型声明
│
├── YFrame.Tests/                       # xUnit 单元测试项目
│   ├── YFrame.Tests.csproj             # 引用 YF_Manager + YFrame + YF_KMScript
│   ├── YF_Manager/                     # YF_Manager 相关测试
│   │   ├── YF_RelayCommandTests.cs      # 6 个 — 无参 RelayCommand
│   │   ├── YF_RelayCommandGenericTests.cs # 6 个 — 泛型 RelayCommand
│   │   ├── ConfigTests.cs               # 7 个 — 配置常量
│   │   ├── YF_Manager_MainTests.cs      # 4 个 — YF_Manager_Main
│   │   └── Common/
│   │       ├── YF_DelegateFunctionModelTests.cs  # 2 个 — 委托类型
│   │       ├── Attributes/LogAttributeTests.cs   # 5 个 — LogAttribute + LogLevel
│   │       └── Tools/
│   │           ├── YF_FileHelperTests.cs      # 15 个 — 文件系统操作
│   │           ├── YF_TcpHelperTests.cs       # 5 个 — 网络工具
│   │           ├── YF_Manager_LogTests.cs     # 8 个 — 日志系统
│   │           ├── Md5HasherTests.cs          # 10 个 — MD5 哈希计算
│   │           └── RegexHelperTests.cs        # 11 个 — 正则测试辅助
│   ├── YFrame/                         # YFrame 相关测试
│   │   ├── Service/
│   │   │   ├── LogServiceTests.cs      # 14 个 — 日志缓冲区管理
│   │   │   └── PluginServiceTests.cs   # 20 个 — 插件调度、命令路由
│   │   └── Model/
│   │       ├── PluginsModelTests.cs    # 8 个 — ViewModelBase 属性通知
│   │       └── CtrlDataModelTests.cs   # 5 个 — 插件实例数据模型
│   └── Plugins/KMScript/
│       └── ScriptInterpreterTests.cs   # 34 个 — DSL 脚本解析
│
├── YFrame.Installer/                   # 框架安装程序（WPF 向导式，仅安装本体）
│   ├── YFrame.Installer.csproj         # 自包含单文件发布，payload.zip 嵌入资源
│   ├── App.xaml/.cs                    # 入口逻辑
│   ├── MainWindow.xaml/.cs             # 主窗口（无边框，粒子动画，3步向导）
│   ├── Views/                          # WelcomePage / InstallConfigPage / ProgressPage / FinishPage
│   ├── ViewModels/                     # MainViewModel + RelayCommand + ViewModelBase
│   ├── Services/                       # InstallService（文件复制/快捷方式/注册表）+ PayloadExtractor
│   ├── Models/InstallConfig.cs         # 安装配置模型
│   ├── Controls/                       # ParticleBackground + RainbowProgressBar
│   ├── Converters/Converters.cs        # Bool 转换器
│   ├── Resources/Logo.ico + payload.zip
│   └── CollectPayload.ps1              # 构建时从 YFrame bin 收集核心文件
│
├── .github/workflows/
│   └── ci.yml                          # GitHub Actions CI 流水线
├── .gitlab-ci.yml                      # GitLab CI 流水线（备选）
└── Review/                             # 代码审查报告与项目文档
```

---

## 4. 核心机制详解

### 4.1 插件系统

插件系统是整个框架最核心的设计，实现了**完全动态的插件发现、加载、实例化和通信**。

#### 4.1.1 插件发现与加载流程

```
应用启动
    │
    ▼
MainWindowViewModel.Init()
    │
    ▼
UserControlsService.LoadAndShowUserControl()
    │
    ├── 1. 扫描 plugins/ 目录下所有子文件夹
    │      Directory.GetDirectories("plugins")
    │
    ├── 2. 在每个子文件夹中查找 YF_*.dll 文件
    │      Directory.GetFiles(item, "YF_*.dll")
    │
    ├── 3. 过滤掉 YF_Manager.dll（框架库，非插件）
    │      if (s == "YF_Manager") continue;
    │
    ├── 4. 反射加载程序集
    │      Assembly.LoadFrom(assemblyPath)
    │
    ├── 5. 获取约定的类型
    │      assembly.GetType($"{pluginName}.MainControl")       → UserControl
    │      assembly.GetType($"{pluginName}.MainControlViewModel") → ViewModel
    │
    └── 6. 实例化并注册
           Activator.CreateInstance(viewModelType)
           ├── 验证是否实现 I_YF_Detail（获取 ID 和 Name）
           └── 存入 DctControls 字典
```

#### 4.1.2 懒加载策略

插件加载分为两个阶段，最大化节约内存：

| 阶段 | 时机 | 操作 | 内存占用 |
|------|------|------|----------|
| **元数据扫描** | 应用启动 | 反射获取 `I_YF_Detail`（ID + Name），存入 `DctControls` | 仅元数据 |
| **实例化** | 用户点击"显示"按钮 | 创建 `UserControl` 实例，注入到 `Grid_Show_Array` | 完整插件实例 |

#### 4.1.3 插件开发规范

每个插件必须满足以下约定：

| 约定项 | 规范 |
|--------|------|
| **命名空间** | 与 DLL 文件名相同（如 `YF_AIHelper.dll` → namespace `YF_AIHelper`） |
| **入口控件** | `{命名空间}.MainControl`，继承 `UserControl` |
| **视图模型** | `{命名空间}.MainControlViewModel`，实现 `I_YF_Detail` + `I_YF_Command` |
| **输出目录** | 编译到 `plugins/{命名空间}/` 目录下 |
| **依赖** | 必须引用 `YF_Manager.dll` |

#### 4.1.4 双向通信机制

```
┌──────────────────────────────────────────────────────┐
│                    YFrame 宿主                       │
│                                                      │
│  MainWindowViewModel.SendCommand("command", param)   │
│      │                                               │
│      ▼                                               │
│  plugin.CommandHandler.ExecuteCommand("cmd", param)  │
│      │                                               │
│      │         ┌──────────────┐                      │
│      │  调用   │  插件 ViewModel │  事件回调          │
│      └───────→ │  (I_YF_Command)│ ─────────→         │
│                └──────────────┘           │          │
│                                           ▼          │
│                              OnPluginCallback 事件   │
│                              HandlePluginCallback()  │
└──────────────────────────────────────────────────────┘
```

### 4.2 AOP 日志拦截

框架采用 **Castle.Core DynamicProxy** 实现面向切面编程（AOP），在不侵入业务代码的前提下实现全自动的方法级日志记录。

#### 4.2.1 工作流程

```
调用者
  │
  ▼
代理对象 (Proxy) ──→ LogInterceptor.Intercept(invocation)
  │                         │
  │                         ├── 1. 检查方法是否有 [Log] 特性
  │                         │      · 无特性 → 直接执行原方法
  │                         │      · 有特性 → 继续拦截
  │                         │
  │                         ├── 2. 启动 Stopwatch 计时
  │                         │
  │                         ├── 3. 记录方法名、参数名和参数值
  │                         │
  │                         ├── 4. 调用 invocation.Proceed() 执行原方法
  │                         │      ├── 同步方法 → 直接等待完成
  │                         │      └── 异步方法 (Task/Task<T>)
  │                         │           → InterceptAsync() 等待完成
  │                         │
  │                         ├── 5. 记录返回值 + 执行耗时
  │                         │
  │                         └── 6. 异常时记录错误信息并重新抛出
  │
  ▼
返回结果
```

#### 4.2.2 异步方法处理

拦截器通过 `dynamic` 分发自动区分两种异步模式：

```csharp
bool isAsync = invocation.Method.ReturnType == typeof(Task) ||
               (invocation.Method.ReturnType.IsGenericType &&
                invocation.Method.ReturnType.GetGenericTypeDefinition() == typeof(Task<>));

if (isAsync)
    invocation.ReturnValue = InterceptAsync((dynamic)invocation.ReturnValue, ...);
```

- **`Task`（无返回值）** → `InterceptAsync(Task, ...)` → `await task` 后记录完成
- **`Task<T>`（有返回值）** → `InterceptAsync<T>(Task<T>, ...)` → `await task` 后记录返回值

#### 4.2.3 日志输出示例

```
[2026-07-09 10:30:00] 执行开始 | 初始化UI | 函数位置：YFrame.MainWindowViewModel.InitUI
[2026-07-09 10:30:00] 执行完成 | 初始化UI 耗时: 245ms | 函数位置：YFrame.MainWindowViewModel.InitUI
```

### 4.3 全局日志系统

#### 4.3.1 系统架构

```
任意组件
  │
  ├── logger.LogInfo(msg)    ──→ Write(InfoLog/*.htm)
  ├── logger.DebugInfo(msg)  ──→ Write(DebugLog/*.htm) + LogInfo()
  ├── logger.ErrorInfo(...)  ──→ Write(ErrorLog/*.htm) + LogInfo()
  ├── logger.CommandInfo(msg)──→ Write(CommandLog/*.htm) + LogInfo()
  ├── logger.TcpInfo(msg)    ──→ Write(TcpLog/*.htm)
  └── logger.InterceptorsLog  ──→ Write(InterceptorsLog/*.htm) + LogInfo()
       │
       └── d_LogWrite 委托 ──→ MainWindowViewModel.Show_Log() ──→ UI TextBox
```

#### 4.3.2 日志分类

| 日志类型 | 目录 | 内容 |
|----------|------|------|
| **InfoLog** | `Log/InfoLog/` | 所有 Info 级别日志的聚合（各类型日志也会写入此目录） |
| **DebugLog** | `Log/DebugLog/` | 调试信息（插件扫描、加载过程） |
| **ErrorLog** | `Log/ErrorLog/` | 错误信息（异常详情、堆栈信息） |
| **CommandLog** | `Log/CommandLog/` | 命令执行记录（框架→插件命令） |
| **TcpLog** | `Log/TcpLog/` | 网络通信日志（TCP 发现、连接） |
| **InterceptorsLog** | `Log/InterceptorsLog/` | AOP 拦截器日志（方法调用记录） |

#### 4.3.3 日志特性

- **文件格式：** HTML（`.htm`），带 `<HR>` 分隔线，方便浏览器查看
- **文件命名：** 按日期分文件（`yyyy-MM-dd.htm`），每天自动新建
- **自动轮转：** 单文件超过 1MB 时自动重命名为 `*_1.htm`、`*_2.htm` ... `*_999.htm`
- **线程安全：** 全局 `_fileLock` 对象保证多线程写入安全
- **UI 实时显示：** 通过静态委托 `d_LogWrite` 将日志推送到主窗口右侧面板
- **UI 容量控制：** 内存中保留最近 500 行日志，使用 `StringBuilder` 高效裁剪
- **日志自身容错：** 日志写入失败时输出到 `Debug.WriteLine()` 和 `Trace`，不会拖垮主程序

### 4.4 主题与多语言

#### 4.4.1 主题系统

```
App.ChangeTheme(themePath)
    │
    ├── 1. 在 MergedDictionaries 中查找含 "Theme" 的旧字典
    ├── 2. 移除旧字典
    └── 3. 加载新字典 → Application.Current.Resources.MergedDictionaries.Add(newTheme)
```

框架提供四套主题，启动时默认加载 `DarkGrayTheme.xaml` + `ControlStyles.xaml`：

| 主题 | 文件 | 中文名 | 主色调 |
|------|------|--------|--------|
| 炭火暗夜 | `DarkGrayTheme.xaml` | Ember Night | 背景 `#1E1E1E`，强调色 `#0078D4`（VS Code 风格暗色） |
| 素火明昼 | `CreamWhiteTheme.xaml` | Plain Fire | 背景 `#FFF5F5F8`，强调色 `#D94A1A`（暖橙红） |
| 冰火深蓝 | `LightBlueTheme.xaml` | Ice Deep Blue | 背景 `#0B1526`，强调色 `#00CCF0`（冰蓝） |
| 翠火青绿 | `GreenWhiteTheme.xaml` | Emerald Fire | 背景 `#0A1410`，强调色 `#00E676`（翠绿） |

**全局控件统一样式（`ControlStyles.xaml`）：**
- **Button** — 圆角边框 + 悬停透明度变化
- **TextBox** — 聚焦时显示主题强调色边框
- **Label** — 统一字体与颜色跟随主题
- **ScrollBar** — 5px 极窄滚动条，透明轨道 + 强调色滑块 + 悬停动画

XAML 中通过 `{DynamicResource key}` 引用资源，切换时所有绑定控件自动刷新。

#### 4.4.2 多语言系统

| 语言 | 文件 | 切换方式 |
|------|------|----------|
| 简体中文 | `zh-CN.xaml` | `App.ChangeLanguage("zh")` |
| English | `en-US.xaml` | `App.ChangeLanguage("en")` |

- 界面文本通过 `{DynamicResource key_XXX}` 绑定
- 切换时替换 `MergedDictionaries` 中的语言字典

### 4.5 性能监控

`PerformanceMonitor` 是一个独立的 `UserControl`，实时展示系统 CPU 和内存使用率。

#### 4.5.1 数据采集

| 指标 | 数据源 | 方式 |
|------|--------|------|
| **CPU** | `PerformanceCounter("Processor", "% Processor Time", "_Total")` | 先调用 `NextValue()` 预热，再采集实际值 |
| **内存** | WMI `Win32_ComputerSystem.TotalPhysicalMemory` + `PerformanceCounter("Memory", "Available MBytes")` | 总内存(GB) = WMI 总量 − 可用内存 |

#### 4.5.2 渲染机制

- **图表库：** LiveCharts.Wpf `CartesianChart`（折线图）
- **采样周期：** 5 秒/次（`ThreadPool` 后台线程 + `Dispatcher.Invoke` 回到 UI 线程）
- **数据窗口：** 滚动保留最近 30 秒（6 个数据点）
- **状态栏推送：** 通过静态委托 `dlg_Show_Cpu_Memory` 同步更新底部状态栏文字

### 4.6 命令与事件通信

#### 4.6.1 RelayCommand

框架实现了两个版本的 `ICommand`：

```
YF_RelayCommand              YF_RelayCommand<T>
┌────────────────┐           ┌──────────────────┐
│ Action _execute │           │ Action<T> _execute│
│ Func<bool>      │           │ Func<T,bool>      │
│ _canExecute     │           │ _canExecute       │
└────────────────┘           └──────────────────┘
```

- `CanExecuteChanged` 委托给 `CommandManager.RequerySuggested`，由 WPF 自动管理
- 构造函数中 `_execute` 参数通过 `ArgumentNullException` 防护

#### 4.6.2 事件驱动通信

```
MainWindowViewModel              UserControlsService           Plugin ViewModel
       │                               │                            │
       │ ShowUserControl(pluginId)     │                            │
       │──────────────────────────────→│                            │
       │                               │ Load assembly + create     │
       │                               │───────────────────────────→│
       │                               │                            │
       │                               │   Hook OnPluginCallback    │
       │                               │←───────────────────────────│
       │                               │                            │
       │                     HandlePluginCallback()                 │
       │←──────────────────────────────│                            │
       │                               │                            │
       │  SendCommand("cmd", param)    │                            │
       │──────────────────────────────────────────────────────────→ │
       │                               │       ExecuteCommand()     │
```

**三种通信路径：**
1. **宿主 → 插件：** `MainWindowViewModel.SendCommand()` → `I_YF_Command.ExecuteCommand()`
2. **插件 → 宿主：** `OnPluginCallback` 事件 → `UserControlsService.HandlePluginCallback()`
3. **任意组件 → UI 日志面板：** 静态委托 `d_LogWrite` → `MainWindowViewModel.Show_Log()`

---

### 4.7 全局热键系统

框架通过 `HotkeyService` 统一管理全局热键，而非各插件单独注册，解决热键重复或过多问题。

#### 4.7.1 架构设计

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
MainWindowViewModel.OnHotkeyPressed()
    │  根据当前激活插件 ID 分发命令:
    ├── "YF_ScreenOCRTranslate" → ExecuteCommand("CaptureScreen")
    ├── "YF_Clicker" → ExecuteCommand("ToggleClick")
    └── 其他插件 → ExecuteCommand("HotkeyTrigger")
```

#### 4.7.2 HotkeyService 核心实现

| 组件 | 说明 |
|------|------|
| **Win32 API** | `RegisterHotKey` / `UnregisterHotKey`（user32.dll） |
| **热键组合** | Ctrl + Y（`MOD_CONTROL=0x0002`，`VK_Y=0x59`） |
| **热键 ID** | `HOTKEY_ID = 9001` |
| **消息拦截** | `HwndSource.AddHook(WndProc)` 拦截 `WM_HOTKEY (0x0312)` |
| **事件通知** | `Action? OnHotkeyPressed` 事件通知订阅者 |
| **设计模式** | AOP 单例（`ProxyGenerator.CreateClassProxy<HotkeyService>`） |

#### 4.7.3 插件热键适配

插件只需在 `ExecuteCommand` 中实现对应命令，无需关心热键注册：

| 插件 | 命令 | 说明 |
|------|------|------|
| YF_ScreenOCRTranslate | `"CaptureScreen"` | 触发截图翻译流程 |
| YF_Clicker | `"ToggleClick"` | 切换连点器启停 |
| 其他插件 | `"HotkeyTrigger"` | 通用热键触发命令 |

---

### 4.8 CI/CD 持续集成

#### 4.8.1 工作流概览

```
git push → GitCode Actions/Pipeline 自动触发
  │
  └── Build Job (windows-latest)
       ├── 1. dotnet restore YF_Manager/YF_Manager.csproj
       ├── 2. dotnet restore YFrame/YFrame.csproj
       ├── 3. dotnet build YF_Manager --configuration Release
       └── 4. dotnet build YFrame --configuration Release
```

#### 4.8.2 配置文件

| 文件 | 格式 | 触发方式 |
|------|------|----------|
| `.github/workflows/ci.yml` | GitHub Actions | gitcode.com Actions 页面触发 |
| `.gitlab-ci.yml` | GitLab CI | gitcode.com Pipeline 页面触发 |

#### 4.8.3 设计说明

- **仅编译核心项目**（YF_Manager + YFrame），测试项目不参与 CI 编译
  - 原因：`YFrame.Tests` 依赖上层目录的 `YF_KMScript` 插件，该插件不在本仓库中
  - 本地运行测试：`dotnet test YFrame.Tests/YFrame.Tests.csproj`
- **需要 Windows Runner**：WPF 项目在 Linux 上无法编译，需自行注册 Windows runner
- **触发分支**：`main` / `master`，忽略 `.md`、`Review/`、`Log/` 目录变更
- **手动触发**：支持 `workflow_dispatch` 手动运行

---

## 5. 插件生态

当前已开发九款插件，覆盖 AI、网络、自动化、OCR 翻译、内网穿透、串口通讯、TCP 通讯、插件分发等场景。

### 5.1 插件总览

| 插件 | ID | 名称 | 核心依赖 | 一句话描述 |
|------|-----|------|----------|-----------|
| **YF_AIHelper** | `YF_AIHelper` | AI 助手 | LLamaSharp 0.24.0 + CUDA 12 | 本地 LLM 推理的 AI 对话助手 |
| **YF_Clicker** | `YF_Clicker` | 鼠标连点器 | WindowsInput | 可配置间隔的鼠标自动连点器 |
| **YF_HttpServer** | `YF_HttpServer` | Http 文件助手 | `HttpListener`（框架内置）| 一键启动的轻量级 HTTP 文件服务器 |
| **YF_KMScript** | `YF_KMScript` | 脚本编辑器 | OpenCvSharp4 | 中文 DSL 键鼠自动化脚本引擎 |
| **YF_Penetration** | `YF_Penetration` | NAT 内网穿透 | NatTraversal 自研库 | NAT 穿透 P2P 联机（房间制中继转发）|
| **YF_ScreenOCRTranslate** | `YF_ScreenOCRTranslate` | OCR 实时翻译 | PaddleOCRSharp + 百度翻译 API | 截图 OCR 识别 + 英译中 |
| **YF_Serialport** | `YF_Serialport` | 串口通讯助手 | System.IO.Ports 8.0.0 | 串口调试助手（文本/HEX收发+预设命令管理） |
| **YF_TcpHelper** | `YF_TcpHelper` | TCP通讯助手 | System.Net.Sockets（内置） | TCP服务端/客户端通讯+UDP广播自动发现 |
| **YF_PluginServer** | `YF_PluginServer` | 插件服务器 | `HttpListener`（框架内置） | HTTP API 插件分发服务（列表+下载） |

### 5.2 YF_AIHelper — AI 助手

**功能描述：** 基于 LLamaSharp 加载本地 GGUF 格式大语言模型，在本地完成推理，无需联网。支持 GPU 加速（CUDA 12）。

**技术实现：**
- 使用 `LLamaWeights.LoadFromFile()` 加载 GGUF 模型文件
- 通过 `LLamaContext` 创建推理上下文
- `ChatSession` 管理多轮对话历史
- 聊天消息通过 `ObservableCollection<ChatMessage>` 绑定到 UI 列表
- 发送消息支持按钮点击和 Enter 键（通过 `KeyBinding`）

**核心 NuGet 依赖：**
- `LLamaSharp` 0.24.0 — llama.cpp 的 .NET 绑定
- `LLamaSharp.Backend.Cuda12` 0.24.0 — CUDA 12 GPU 加速后端

### 5.3 YF_Clicker — 鼠标连点器

**功能描述：** 基于 WindowsInput 实现的鼠标自动连点工具。支持自定义点击间隔，通过后台线程连续点击，支持手动按钮控制和框架 Ctrl+Y 热键切换启停。

**技术实现：**
- `InputSimulator.Mouse.LeftButtonClick()` 模拟鼠标左键点击
- 后台 `Thread` 执行连续点击循环，10 秒自动停止
- `CancellationTokenSource` 实现安全中断
- UI 显示实时运行状态（绿/灰圆形指示器）和点击次数统计
- 通过 `OnPluginCallback` 事件向框架回传统计数据（启停状态、完成次数、耗时）

**启停方式：**
- 手动点击 UI 的"开始点击"/"停止点击"按钮
- 框架 Ctrl+Y 热键切换（需当前激活此插件）
- 10 秒超时自动停止

**核心 NuGet 依赖：**
- `WindowsInput` 6.4.0 — 模拟键盘鼠标输入

### 5.4 YF_HttpServer — HTTP 文件助手

**功能描述：** 基于 `HttpListener` 实现的一键式 HTTP 文件服务器。自动获取本机 IP，提供目录浏览、文件下载、拖拽上传和剪贴板复制命令等功能。

**技术实现：**
- `HttpListener` 监听指定端口（默认 8000），在后台线程运行
- `ProcessRequest()` 处理 GET 请求：生成 HTML 目录列表（`GenerateDirectoryListing()`）或返回文件内容（含 MIME 类型检测 `GetMimeType()`）
- `ProcessUploadRequest()` 处理 POST 请求：从 `X-FileName` 头获取文件名
- 支持拖拽文件上传（自定义依赖属性 `DropCommand`）
- 一键复制 `curl` 下载命令到剪贴板（通过 `YF_FileHelper.SetClipboardWithRetry()`）
- 文件操作（目录复制、剪贴板写入、打开文件夹）已提取到 `YF_Manager` 共享库的 `YF_FileHelper` 中
- 启动/停止按钮通过 `YF_RelayCommand` 的 `CanExecute` 控制互斥状态
- 自动通过 `YF_TcpHelper.GetLocalIP()` 获取本机 IP

### 5.5 YF_KMScript — 脚本编辑器

**功能描述：** 提供中文关键字 DSL 的键鼠自动化脚本编辑和解释执行环境。

**DSL 语法规则：**

| 关键字 | 语法 | 说明 |
|--------|------|------|
| `定义` | `定义 变量名 = 值` | 变量声明与赋值（支持字符串 `"..."` 和整数/浮点数） |
| `找图` | `找图 变量名` | OpenCV 模板匹配找图（返回坐标存入 `变量名位置`） |
| `点击` | `点击 变量名` | 模拟鼠标点击到指定坐标 |
| `等待` | `等待 毫秒` | 线程休眠（支持数字或变量名） |
| `循环` | `循环 N 次 ... 结束循环` | 循环结构（支持嵌套，`循环次数` 变量自动维护） |
| `如果` | `如果 条件 ... 否则 ...` | 条件判断（存在/等于/不等于/大于/小于/大于等于/小于等于） |
| `截图` | `截图 x y 宽 高 路径` | 截取屏幕指定区域保存为 PNG |
| `输出` | `输出 内容` | 打印到输出面板 |
| `//` | `// 注释内容` | 单行注释 |

**特殊变量：** `匹配阈值`（找图相似度，默认 0.80）、`循环次数`（当前循环迭代）

**技术实现：**
- `ScriptInterpreter` 类基于缩进语法树（Python 风格 Tab/2空格）解析脚本
- OpenCV `CCOEFF_NORMED` 算法实现模板匹配找图（`ImageMatcher` 服务）
- 变量存储在 `Dictionary<string, object>` 中，支持 string/int/double/Point
- 脚本在后台线程执行，支持通过 `_shouldStop` 标志中止
- 可视化编辑器 + 输出面板，提供运行/停止/清空/保存按钮
- 区域截图选择窗口（`RegionSelectionWindow`）支持鼠标拖拽框选
- Win32 `SetCursorPos` + `mouse_event` 实现鼠标输入模拟

### 5.6 YF_Penetration — NAT 内网穿透

**功能描述：** 基于自研 NatTraversal 库实现的 NAT 内网穿透工具，通过中转服务器实现 P2P 联机。支持 Host（建主）和 Player（加入）两种角色模式。

**技术实现：**
- `NatTunnelClient` 核心客户端类，管理连接生命周期
- Host 模式：创建房间 → 生成加入码 → 启动 TCP/UDP 中继循环
- Player 模式：使用加入码加入房间 → 启动本地代理 → `127.0.0.1` 端口转发
- 支持 UDP 中继和 TCP 传输两种转发模式
- 双标签页 UI 切换 Host/Player 角色，带连接状态实时指示器
- 统计数据面板：上传/下载字节数、会话数、回显测试
- 日志面板实时输出连接、中继、错误信息

**核心依赖：**
- NatTraversal.Client — NAT 穿透客户端核心库
- NatTraversal.Server — 中转服务端核心库
- NatTraversal.Shared — 共享类型定义库

### 5.7 YF_ScreenOCRTranslate — OCR 实时翻译

**功能描述：** 全局热键唤起截图 → PaddleOCR 文字识别 → 百度翻译 API 英译中 → 翻译结果叠加显示。五步流水线在数秒内完成。

**注意：** 热键 Ctrl+Y 现由框架 `HotkeyService` 统一管理，插件无需自行注册热键。

**完整工作流：**

```
用户按 Ctrl+Y（框架 HotkeyService 统一管理）
    │
    ▼
[1] 框架热键触发 → HotkeyService.OnHotkeyPressed
    │  MainWindowViewModel 向激活插件发送 "CaptureScreen" 命令
    │
    ▼
[2] 全屏截图覆盖层 (ScreenShot Window)
    │  · WindowState=Maximized, AllowsTransparency=True
    │  · 鼠标拖拽绘制选区矩形
    │  · SetWindowPos 确保置顶
    │
    ▼
[3] 区域截图 → Bitmap
    │
    ▼
[4] PaddleOCR 文字识别 (PaddleOCRSharp)
    │  · OCRModelConfig 配置 PP-OCRv5 模型
    │  · 返回 TextBlock 列表（文字 + 坐标）
    │
    ▼
[5] 百度翻译 API (TranslateService)
    │  · MD5(appid + q + salt + key) 签名
    │  · GET api.fanyi.baidu.com
    │  · Newtonsoft.Json 解析返回
    │
    ▼
[6] 结果显示 (ShowText Window)
    · 翻译文字 Canvas 叠加到原图位置
    · 1.25x DPI 缩放适配
```

**核心 NuGet 依赖：**
- `Paddle.Runtime.win_x64` 3.4.0 — Paddle 推理运行时
- `PaddleOCRSharp` 6.1.0 — PaddleOCR 的 .NET 封装
- `Newtonsoft.Json` 13.0.4 — JSON 序列化/反序列化
- Win32 API（`user32.dll`）：`RegisterHotKey`、`UnregisterHotKey`、`SetWindowPos`

### 5.8 YF_Serialport — 串口通讯助手

**功能描述：** 基于 `System.IO.Ports` 实现的串口调试助手。支持常用串口参数配置（端口/波特率/数据位/停止位/校验位/编码），文本/HEX 双模式收发，预设命令管理（增删+JSON 持久化）。

**技术实现：**
- `SerialPortService`（457行）封装 `System.IO.Ports.SerialPort`，管理串口打开/关闭/发送/接收，提供 5 个事件和 3 个公开属性
- `PresetCommandService`（292行）管理预设命令的 CRUD 操作，存储于 `Config\PortCommand\presets_index.json`，内存中用 `Dictionary<string, PresetCommand>` 缓存（键名忽略大小写）
- 文本接收模式使用 `StringBuilder` 缓冲区累积，按 `\n` 换行后再输出完整行；HEX 接收模式即时逐帧触发
- 发送支持文本（可自动追加换行）、HEX（空格分隔）、字节三种模式
- 预设命令通过 `System.Text.Json` 持久化，PrettyPrint 格式
- 串口收发数据自动记录到 TcpLog

**UI 功能：**
- 串口参数全配置：波特率（15 个常用值，默认 9600）、数据位（5~8，默认 8）、停止位（1/1.5/2，默认 1）、校验位（5 种）、编码（5 种，默认 UTF-8）
- 收发区 Text/HEX 模式独立切换，两个蓝色高亮切换按钮
- 连接后 `DtrEnable=true`、`RtsEnable=true`，读写超时 500ms
- 预设命令面板：名称输入 → 从发送区创建 → 列表显示（含模式标签+数据预览）→ 一键发送/删除

**核心 NuGet 依赖：**
- `System.IO.Ports` 8.0.0 — .NET 串口通讯实现

### 5.9 YF_TcpHelper — TCP通讯助手

**功能描述：** 基于 .NET 内置 `System.Net.Sockets` 实现的 TCP 通讯助手。支持 TCP 服务端（多客户端管理+广播发送）和客户端（连接+广播接收）两种模式，以及 UDP 广播自动发现功能。



**广播自动发现流程：**

```
服务端发送 UDP 广播宣告地址 → 客户端监听广播 → 按分隔符解析 IP:Port
    → 自动或手动反向建立 TCP 连接 → 双向文本通讯
```

**技术实现：**
- `TcpServerService`（721行）：TCP 异步监听（`AcceptTcpClientAsync`）+ 多客户端管理（`ConcurrentDictionary<string, ClientSession>`）+ UDP 广播监听与解析
- `TcpClientService`（436行）：TCP 连接/收发 + UDP 广播发送（`UdpClient.Send` 到 `IPAddress.Broadcast`）
- 接收区按行累积逻辑（`StringBuilder` 缓冲区 + `\n` 拆分），500 行上限裁剪
- ViewModel 定时广播使用 `System.Timers.Timer`（3 秒间隔），回调通过 `Dispatcher.Invoke` Marshal 到 UI 线程
- 自动反向连接：客户端收到广播后，若勾选 `AutoReverseConnect` 且未连接，自动填入 IP/Port 并连接

**UI 功能：**
- 双模式切换：服务端/客户端按钮，驱动对应配置面板可见性
- 服务端：监听端口配置 + UDP 广播（端口/内容/定时） + 已连接客户端列表（选中后点对点发送）
- 客户端：远程 IP/端口配置 + UDP 广播监听（端口/分隔符/自动反向连接） + 广播信息面板
- 收发区：文本模式，支持自动添加换行

**核心依赖：**
- `System.Net.Sockets` — .NET 内置，无需额外 NuGet 包

### 5.10 YF_PluginServer — 插件服务器

**功能描述：** 基于 `HttpListener` 实现的插件 HTTP 分发服务。扫描本地 `plugins/` 目录，通过 HTTP API 对外提供插件列表和 ZIP 下载功能。配合框架内置的 `PluginManagerWindow` 实现插件的远程分发与自动安装。


**HTTP API：**
| 接口 | 方法 | 说明 |
|------|------|------|
| `/api/plugins` | GET | 返回 JSON 格式的插件列表（ID、名称、文件大小、文件数量） |
| `/api/plugins/{id}/download` | GET | 返回插件文件夹的 ZIP 压缩包（流式下载） |

**技术实现：**
- `HttpListener` 监听指定端口（默认 9000），在后台线程运行
- `PluginServerService` 扫描 `plugins/` 目录，反射读取 `I_YF_Detail` 获取插件元数据
- ZIP 打包使用 `System.IO.Compression.ZipFile`
- 端口配置持久化到 `Config/config.conf`（`PluginServerPort` 键）
- ViewModel 通过 `ViewModelBase`（实现 `INotifyPropertyChanged`）驱动 UI，支持服务器启停、插件列表刷新、URL 复制

**UI 功能：**
- 本机 IP 自动获取 + 端口配置（读写配置文件）
- 服务器启停按钮 + 状态指示灯（绿/灰）
- 服务器地址展示 + 一键复制
- 本地插件列表展示（插件名称/ID、文件数量、占用空间、下载 API 路径）

**核心依赖：**
- `HttpListener` — .NET 内置，无需额外 NuGet 包
- `Castle.Core` 5.2.1 — AOP 日志拦截

---

## 6. 技术栈详情

### 6.1 框架与运行时

| 技术 | 版本/规格 | 用途 |
|------|-----------|------|
| **.NET** | 8.0 (`net8.0-windows`) | 目标框架 |
| **C#** | 12.0 | 编程语言（nullable enabled, implicit usings） |
| **WPF** | .NET 8.0 内置 | 桌面 UI 框架 |
| **Visual Studio** | 2022 (17.x) | 开发环境 |

### 6.2 核心 NuGet 依赖

| 包名 | 版本 | 所属项目 | 用途 |
|------|------|----------|------|
| **Castle.Core** | 5.2.1 | YF_Manager | AOP 动态代理，实现 `LogInterceptor` |
| **LiveCharts.Wpf** | 0.9.7 | YFrame | CPU/内存实时折线图 |
| **System.Management** | 9.0.8 | YFrame | WMI 查询系统物理内存 |

### 6.3 插件 NuGet 依赖

| 包名 | 版本 | 所属插件 | 用途 |
|------|------|----------|------|
| **LLamaSharp** | 0.24.0 | YF_AIHelper | llama.cpp .NET 绑定，本地 LLM 推理 |
| **LLamaSharp.Backend.Cuda12** | 0.24.0 | YF_AIHelper | CUDA 12 GPU 加速 |
| **Microsoft.Extensions.Configuration** | 9.0.8 | YF_AIHelper | 配置文件读取（预留） |
| **Microsoft.Extensions.Configuration.Json** | 9.0.8 | YF_AIHelper | JSON 配置文件支持（预留） |
| **WindowsInput** | 6.4.0 | YF_Clicker | 模拟键盘鼠标输入 |
| **OpenCvSharp4** | 4.13.0 | YF_KMScript | 计算机视觉，模板匹配找图 |
| **OpenCvSharp4.runtime.win** | 4.13.0 | YF_KMScript | OpenCV Windows 原生运行时 |
| **System.Drawing.Common** | 8.0.0 | YF_KMScript | 位图处理（截图、图片加载） |
| **Paddle.Runtime.win_x64** | 3.4.0 | YF_ScreenOCRTranslate | Paddle 推理运行时 |
| **PaddleOCRSharp** | 6.1.0 | YF_ScreenOCRTranslate | PaddleOCR .NET 封装（PP-OCRv5） |
| **Newtonsoft.Json** | 13.0.4 | YF_ScreenOCRTranslate | JSON 解析（百度翻译 API 响应） |
| **System.IO.Ports** | 8.0.0 | YF_Serialport | 串口通讯实现 |

### 6.4 系统 API

| API | 来源 | 使用位置 | 用途 |
|-----|------|----------|------|
| `RegisterHotKey` | `user32.dll` | YFrame (HotkeyService) | 注册全局热键 Ctrl+Y |
| `UnregisterHotKey` | `user32.dll` | YFrame (HotkeyService) | 注销全局热键 |
| `SetWindowPos` | `user32.dll` | YF_ScreenOCRTranslate | 截图覆盖层置顶 |
| `PerformanceCounter` | `System.Diagnostics` | YFrame | CPU 和内存使用率采集 |
| `ManagementObjectSearcher` | `System.Management` | YFrame | WMI 查询 `Win32_ComputerSystem` |
| `HttpListener` | `System.Net` | YF_HttpServer | HTTP 服务器核心 |
| `TcpClient` / `TcpListener` | `System.Net.Sockets` | YF_TcpHelper | TCP 通讯与 UDP 广播 |
| `SerialPort` | `System.IO.Ports` | YF_Serialport | 串口通讯 |

---

## 7. 数据流与生命周期

### 7.1 应用启动时序

```
Application.Startup
    │
    ├── 1. App 构造函数
    │      · 加载 DarkGrayTheme.xaml（默认暗色主题）+ ControlStyles.xaml（全局控件样式）
    │      · 加载 zh-CN.xaml（默认中文）
    │
    ├── 2. MainWindow 构造函数
    │      · DataContext = MainWindowViewModel.Instance
    │      · 首次访问 Instance → Lazy<T> 触发
    │        → ProxyGenerator.CreateClassProxy<MainWindowViewModel>()
    │        → 生成带 LogInterceptor 的代理对象
    │
    ├── 3. MainWindowViewModel.Instance.Init()
    │      · 创建 logger 实例
    │      · 调用 InitUI()
    │        ├── 设置 LeftVisible / RightVisible = true
    │        ├── 创建 PerformanceMonitor UserControl
    │        ├── 创建 Grid_Show_Array（插件容器）
    │        └── UserControlsService.Instance.LoadAndShowUserControl()
    │             └── 扫描 plugins/ → 反射加载 → 注册插件元数据
    │      · 调用 InitCommond()
    │        └── 创建所有 YF_RelayCommand 绑定
    │      · 设置静态委托
    │        ├── YF_Manager_Log.d_LogWrite = Show_Log（日志 → UI）
    │        └── dlg_Show_Cpu_Memory = Show_Cpu_Memory（性能 → 状态栏）
    │
    └── 4. MainWindow.Show()
           · 用户可见
           · PerformanceMonitor 后台线程开始采集
```

### 7.2 插件生命周期

```
                    ┌─────────────┐
                    │  应用启动    │
                    └──────┬──────┘
                           │
                           ▼
                    ┌─────────────┐
                    │ 元数据扫描   │  ← LoadAndShowUserControl()
                    │ 注册到列表   │     仅获取 I_YF_Detail (ID + Name)
                    └──────┬──────┘
                           │
                    ┌──────▼──────┐
                    │ 空闲等待     │  ← 插件 DLL 未加载到内存
                    └──────┬──────┘
                           │ 用户点击"显示"
                           ▼
                    ┌─────────────┐
                    │ 加载程序集   │  ← Assembly.LoadFrom(dllPath)
                    │ 创建实例     │  ← Activator.CreateInstance()
                    │ Hook 回调    │  ← OnPluginCallback += ...
                    └──────┬──────┘
                           │
                    ┌──────▼──────┐
                    │ 运行中       │  ← UserControl 显示在 Grid_Show_Array
                    │ 双向通信     │     宿主 ⇄ 插件（Command + Event）
                    └──────┬──────┘
                           │ 用户切换到其他插件 / 关闭
                           ▼
                    ┌─────────────┐
                    │ 隐藏/卸载    │  ← Children.Clear() 移除 UI
                    │ (实例保留)   │     CtrlDataModel 仍在字典中
                    └─────────────┘
```

### 7.3 日志数据流

```
业务代码调用                    文件系统                   UI
─────────────                  ────────                   ──
logger.LogInfo("msg")
    │
    ├──→ Write("Log/InfoLog/2026-07-09.htm")
    │       │
    │       ├── 文件存在? → 追加写入
    │       ├── 文件 > 1MB? → 重命名 + 新建
    │       └── 写入失败? → Debug.WriteLine() + Trace
    │
    └──→ d_LogWrite?.Invoke("msg")
            │
            ▼
         MainWindowViewModel.Show_Log("msg")
            │
            ├── lock(_logLock) → StringBuilder.AppendLine()
            ├── 超过 500 行 → 裁剪头部
            └── Dispatcher.Invoke → LogText = text
                    │
                    ▼
                UI TextBox 自动刷新 (数据绑定)
```

---





### *当前版本软件框架效果示例*
![image.png](https://raw.gitcode.com/user-images/assets/7353928/a7fe3ea5-5883-4906-a3ce-4bf435607487/image.png 'image.png')
![image.png](https://raw.gitcode.com/user-images/assets/7353928/7a3d49cd-7d16-47dd-96a7-b6e05b69dca7/image.png 'image.png')
![image.png](https://raw.gitcode.com/user-images/assets/7353928/1e6301c3-30cb-4fcc-85cd-61ad3e98a220/image.png 'image.png')
![image.png](https://raw.gitcode.com/user-images/assets/7353928/ac419378-a744-4cf2-8298-0917da853de1/image.png 'image.png')
![image.png](https://raw.gitcode.com/user-images/assets/7353928/e581be55-e346-4159-bc07-9a3aea52a541/image.png 'image.png')
![image.png](https://raw.gitcode.com/user-images/assets/7353928/bbd8545d-62cd-4da8-8589-a29c24d7afa8/image.png 'image.png')
![image.png](https://raw.gitcode.com/user-images/assets/7353928/c1cfee4f-e5b6-4eb9-b3fa-84a26714eb92/image.png 'image.png')
![image.png](https://raw.gitcode.com/user-images/assets/7353928/d8d749fd-a0a7-4474-9af0-61be8e7b2ede/image.png 'image.png')
![image.png](https://raw.gitcode.com/user-images/assets/7353928/ad77d098-1614-42ab-8318-3b474e4f17da/image.png 'image.png')
![image.png](https://raw.gitcode.com/user-images/assets/7353928/20ccf599-2299-4df4-a429-36785db3d1a4/image.png 'image.png')
![image.png](https://raw.gitcode.com/user-images/assets/7353928/c9e4c070-beb5-41e6-b34b-eb2a9bd1857e/image.png 'image.png')
![image.png](https://raw.gitcode.com/user-images/assets/7353928/f8b213c0-f0fe-4f16-a5f8-41763e2ae91e/image.png 'image.png')