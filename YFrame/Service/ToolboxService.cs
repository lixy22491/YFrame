using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using YFrame.Model;
using YFrame.View.UC.Toolbox;
using YFrame.ViewModel.Toolbox;

namespace YFrame
{
    /// <summary>
    /// 工具箱服务：维护内置工具注册表（ID → 视图工厂），
    /// 提供工具列表获取与按 ID 打开工具视图（缓存视图实例以保留工具使用状态）。
    /// </summary>
    public class ToolboxService
    {
        // 工具项列表（工具箱面板展示用）
        private readonly List<ToolItem> _tools = new();

        // 工具视图工厂字典：ID → 创建视图的委托
        private readonly Dictionary<string, Func<UserControl>> _factories = new();

        // 已打开的视图缓存：ID → 视图实例（单例缓存，保留状态）
        private readonly Dictionary<string, UserControl> _viewCache = new();

        /// <summary>
        /// 构造函数：注册全部内置工具
        /// </summary>
        public ToolboxService()
        {
            RegisterTool("color-picker", GetString("key_Toolbox_ColorPicker"), GetString("key_Toolbox_ColorPickerDesc"),
                () => new ColorPickerTool { DataContext = new ColorPickerToolViewModel() });
            RegisterTool("regex-tester", GetString("key_Toolbox_RegexTester"), GetString("key_Toolbox_RegexTesterDesc"),
                () => new RegexTesterTool { DataContext = new RegexTesterToolViewModel() });
            RegisterTool("md5-compare", GetString("key_Toolbox_Md5Compare"), GetString("key_Toolbox_Md5CompareDesc"),
                () => new Md5CompareTool { DataContext = new Md5CompareToolViewModel() });
        }

        /// <summary>
        /// 注册一个工具到注册表
        /// </summary>
        /// <param name="id">工具唯一标识</param>
        /// <param name="name">工具显示名称</param>
        /// <param name="description">工具功能描述</param>
        /// <param name="viewFactory">创建工具视图的工厂委托</param>
        private void RegisterTool(string id, string name, string description, Func<UserControl> viewFactory)
        {
            _tools.Add(new ToolItem { ID = id, Name = name, Description = description });
            _factories[id] = viewFactory;
        }

        /// <summary>
        /// 获取全部工具项列表
        /// </summary>
        /// <returns>工具项只读列表</returns>
        public IReadOnlyList<ToolItem> GetTools() => _tools;

        /// <summary>
        /// 按 ID 打开工具视图（首次创建并缓存，后续复用同一实例以保留使用状态）
        /// </summary>
        /// <param name="toolId">工具唯一标识</param>
        /// <returns>工具视图；ID 不存在时返回 null</returns>
        public UserControl? OpenTool(string toolId)
        {
            if (string.IsNullOrEmpty(toolId)) return null;
            if (_factories.TryGetValue(toolId, out var factory))
            {
                if (_viewCache.TryGetValue(toolId, out var cached))
                    return cached;

                var view = factory();
                _viewCache[toolId] = view;
                return view;
            }
            return null;
        }

        /// <summary>
        /// 从当前语言资源字典读取字符串（找不到时返回 key 本身）
        /// </summary>
        /// <param name="key">资源键</param>
        /// <returns>本地化字符串</returns>
        private static string GetString(string key)
            => Application.Current?.TryFindResource(key) as string ?? key;
    }
}