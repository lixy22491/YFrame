using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace YF_Manager
{
    /// <summary>
    /// 基类 ViewModel，实现 INotifyPropertyChanged，简化属性通知
    /// </summary>
    public abstract class ViewModelBase : INotifyPropertyChanged
    {
        /// <summary>
        /// 属性变化事件，UI 通过该事件感知数据变化并刷新绑定
        /// </summary>
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// 触发属性变化通知
        /// </summary>
        /// <param name="propertyName">属性名，默认取调用处的属性名</param>
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        /// <summary>
        /// 设置属性值并自动触发通知
        /// </summary>
        /// <typeparam name="T">属性类型</typeparam>
        /// <param name="field">后备字段（引用传递）</param>
        /// <param name="value">要设置的新值</param>
        /// <param name="propertyName">属性名，默认取调用处的属性名</param>
        /// <returns>值是否发生了改变</returns>
        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            // 值未变化则不通知，避免无谓刷新
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}
