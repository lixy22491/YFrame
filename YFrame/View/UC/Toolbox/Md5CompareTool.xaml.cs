using System.IO;
using System.Windows;
using System.Windows.Controls;
using YFrame.ViewModel.Toolbox;

namespace YFrame.View.UC.Toolbox
{
    /// <summary>
    /// MD5 文件校验工具视图（Code-behind）：
    /// 业务逻辑在 Md5CompareToolViewModel，此处仅处理文件拖拽落地事件
    /// </summary>
    public partial class Md5CompareTool : UserControl
    {
        public Md5CompareTool()
        {
            InitializeComponent();
            // 两个文件选择区支持拖拽
            Loaded += OnLoaded;
        }

        /// <summary>
        /// 加载后挂接拖放事件
        /// </summary>
        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            foreach (var child in FindVisualChildren<Border>(this))
            {
                if (child.Tag is string tag && (tag == "A" || tag == "B"))
                {
                    child.DragOver += OnDragOver;
                    child.Drop += OnDrop;
                }
            }
        }

        /// <summary>
        /// 拖拽经过时若为文件则允许放置
        /// </summary>
        private static void OnDragOver(object sender, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        }

        /// <summary>
        /// 文件放入：根据目标区域 Tag 将路径写入对应文件位
        /// </summary>
        private void OnDrop(object sender, DragEventArgs e)
        {
            if (DataContext is not Md5CompareToolViewModel vm) return;
            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;

            if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
            {
                bool isA = (sender as Border)?.Tag as string == "A";
                vm.SetFile(files[0], isA);
            }
        }

        /// <summary>
        /// 遍历可视树查找指定类型的子元素
        /// </summary>
        /// <typeparam name="T">目标元素类型</typeparam>
        /// <param name="parent">父元素</param>
        /// <returns>匹配的子元素序列</returns>
        private static System.Collections.Generic.IEnumerable<T> FindVisualChildren<T>(DependencyObject parent) where T : DependencyObject
        {
            int count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                if (child is T typed)
                    yield return typed;
                foreach (var descendant in FindVisualChildren<T>(child))
                    yield return descendant;
            }
        }
    }
}