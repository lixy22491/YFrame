using System.ComponentModel;
using YFrame;
using YF_Manager;

namespace YFrame.Tests.YFrame.Model
{
    /// <summary>
    /// PluginsModel 单元测试
    /// </summary>
    public class PluginsModelTests
    {
        /// <summary>
        /// 设置 Name 属性时触发 PropertyChanged 事件
        /// </summary>
        [Fact]
        public void Name_Set_RaisesPropertyChanged()
        {
            var model = new PluginsModel();
            string? changedProperty = null;
            model.PropertyChanged += (s, e) => changedProperty = e.PropertyName;

            model.Name = "新插件";

            Assert.Equal("新插件", model.Name);
            Assert.Equal(nameof(PluginsModel.Name), changedProperty);
        }

        /// <summary>
        /// Name 设为相同值时不触发 PropertyChanged
        /// </summary>
        [Fact]
        public void Name_SetSameValue_DoesNotRaisePropertyChanged()
        {
            var model = new PluginsModel { Name = "插件A" };
            bool wasRaised = false;
            model.PropertyChanged += (s, e) => wasRaised = true;

            model.Name = "插件A"; // 相同值

            Assert.False(wasRaised);
        }

        /// <summary>
        /// 设置 ID 属性时触发 PropertyChanged
        /// </summary>
        [Fact]
        public void ID_Set_RaisesPropertyChanged()
        {
            var model = new PluginsModel();
            string? changedProperty = null;
            model.PropertyChanged += (s, e) => changedProperty = e.PropertyName;

            model.ID = "YF_NewPlugin";

            Assert.Equal("YF_NewPlugin", model.ID);
            Assert.Equal(nameof(PluginsModel.ID), changedProperty);
        }

        /// <summary>
        /// ID 设为相同值时不触发事件
        /// </summary>
        [Fact]
        public void ID_SetSameValue_DoesNotRaisePropertyChanged()
        {
            var model = new PluginsModel { ID = "YF_Plugin" };
            bool wasRaised = false;
            model.PropertyChanged += (s, e) => wasRaised = true;

            model.ID = "YF_Plugin";

            Assert.False(wasRaised);
        }

        /// <summary>
        /// 设置 Status 属性时触发 PropertyChanged
        /// </summary>
        [Fact]
        public void Status_Set_RaisesPropertyChanged()
        {
            var model = new PluginsModel();
            string? changedProperty = null;
            model.PropertyChanged += (s, e) => changedProperty = e.PropertyName;

            model.Status = 1;

            Assert.Equal(1, model.Status);
            Assert.Equal(nameof(PluginsModel.Status), changedProperty);
        }

        /// <summary>
        /// Status 的所有合法值：0=关闭, 1=显示, 2=驻留
        /// </summary>
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        public void Status_ValidValues_AreAccepted(int status)
        {
            var model = new PluginsModel { Status = status };
            Assert.Equal(status, model.Status);
        }

        /// <summary>
        /// 继承 ViewModelBase 基类（提供属性变更通知能力）
        /// </summary>
        [Fact]
        public void Inherits_ViewModelBase()
        {
            var model = new PluginsModel();
            Assert.IsAssignableFrom<ViewModelBase>(model);
        }

        /// <summary>
        /// 默认值验证
        /// </summary>
        [Fact]
        public void DefaultValues_AreExpected()
        {
            var model = new PluginsModel();

            Assert.Null(model.Name);
            Assert.Null(model.ID);
            Assert.Equal(0, model.Status);
        }
    }
}
