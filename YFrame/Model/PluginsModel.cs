using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YF_Manager;

namespace YFrame
{
    /// <summary>
    /// 插件-数据类型
    /// </summary>
    public class PluginsModel : ViewModelBase
    {

        private string _name;  
        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        private string _id;
        public string ID
        {
            get => _id;
            set => SetProperty(ref _id, value);
        }

        /// <summary>
        /// 状态  0: 关闭, 1:显示, 2:驻留
        /// </summary>
        private int _status;
        public int Status
        {
            get => _status;
            set => SetProperty(ref _status, value);
        }
    }
}
