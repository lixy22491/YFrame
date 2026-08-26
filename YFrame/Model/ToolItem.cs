using YF_Manager;

namespace YFrame.Model
{
    /// <summary>
    /// 工具箱工具项模型，描述一个内置小工具（ID、名称、描述）
    /// </summary>
    public class ToolItem : ViewModelBase
    {
        private string _id = string.Empty;
        /// <summary>
        /// 工具唯一标识（用于定位对应工具视图）
        /// </summary>
        public string ID
        {
            get => _id;
            set => SetProperty(ref _id, value);
        }

        private string _name = string.Empty;
        /// <summary>
        /// 工具显示名称
        /// </summary>
        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        private string _description = string.Empty;
        /// <summary>
        /// 工具功能简述
        /// </summary>
        public string Description
        {
            get => _description;
            set => SetProperty(ref _description, value);
        }
    }
}