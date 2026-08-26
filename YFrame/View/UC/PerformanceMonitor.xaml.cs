using YF_Manager;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using LiveCharts.Defaults;
using LiveCharts.Wpf;
using LiveCharts;
using System.Management;
using System.Diagnostics;
using System.Windows.Threading;
using System.ComponentModel;


namespace YFrame
{
    /// <summary>
    /// PerformanceMonitor.xaml 的交互逻辑
    /// </summary>
    public partial class PerformanceMonitor : UserControl
    {
        /// <summary>
        /// 内部属性通知辅助类，继承 ViewModelBase 以复用属性变更通知机制
        /// </summary>
        private sealed class PropertyNotifier : ViewModelBase
        {
            /// <summary>
            /// 对外触发指定属性的变更通知
            /// </summary>
            /// <param name="propertyName">属性名</param>
            public void Notify(string propertyName) => OnPropertyChanged(propertyName);
        }

        /// <summary>
        /// 属性通知辅助对象
        /// </summary>
        private readonly PropertyNotifier _notifier = new();

        /// <summary>
        /// 属性变更事件，转发到内部通知器，供 WPF 数据绑定监听
        /// </summary>
        public event PropertyChangedEventHandler? PropertyChanged
        {
            add => _notifier.PropertyChanged += value;
            remove => _notifier.PropertyChanged -= value;
        }

        public SeriesCollection SeriesCollection { get; set; }
        public string[] Labels { get; set; }
        public Func<double, string> YFormatter { get; set; }

        // CPU 内存计数器
        private PerformanceCounter cpuCounter;
        private PerformanceCounter ramCounter;

        // 总内存大小
        float totalMemoryMB;

        // 一分钟计数器
        int CounterTimes = 0;

        public PerformanceMonitor()
        {
            YF_Manager_Main.logger.LogInfo("性能监视器初始化-开始");
            InitializeComponent();

            // UI 线程：初始化图表（6个初始点，5秒采样，30秒窗口）
            // 从主题资源获取图表颜色
            var chartLine1 = (SolidColorBrush)TryFindResource("ChartLine1") ?? new SolidColorBrush(Colors.Orange);
            var chartLine2 = (SolidColorBrush)TryFindResource("ChartLine2") ?? new SolidColorBrush(Colors.DodgerBlue);
            var chartFill1 = (SolidColorBrush)TryFindResource("ChartFill1") ?? new SolidColorBrush(Color.FromArgb(0x15, 0xFF, 0xA5, 0x00));
            var chartFill2 = (SolidColorBrush)TryFindResource("ChartFill2") ?? new SolidColorBrush(Color.FromArgb(0x0F, 0x00, 0x90, 0xFF));

            SeriesCollection = new SeriesCollection
            {
                new LineSeries
                {
                    Title = "CPU",
                    Values = new ChartValues<ObservableValue>
                    {
                        new ObservableValue(0), new ObservableValue(0),
                        new ObservableValue(0), new ObservableValue(0),
                        new ObservableValue(0), new ObservableValue(0)
                    },
                    Stroke = chartLine1,
                    Fill = chartFill1,
                    PointGeometry = DefaultGeometries.Circle,
                    PointGeometrySize = 10,
                    PointForeground = chartLine1
                },
                new LineSeries
                {
                    Title = "内存",
                    Values = new ChartValues<ObservableValue>
                    {
                        new ObservableValue(0), new ObservableValue(0),
                        new ObservableValue(0), new ObservableValue(0),
                        new ObservableValue(0), new ObservableValue(0)
                    },
                    Stroke = chartLine2,
                    Fill = chartFill2,
                    PointGeometry = DefaultGeometries.Square,
                    PointGeometrySize = 10,
                    PointForeground = chartLine2
                }
            };
            Labels = new[] { "25秒前", "20秒前", "15秒前", "10秒前", "5秒前", "现在" };
            YFormatter = value => value.ToString("N0");
            DataContext = this;

            // 后台线程：执行耗时的 WMI 和 PerformanceCounter 初始化
            ThreadPool.QueueUserWorkItem(_ =>
            {
                InitializeCounters();

                // 初始化完成后回到 UI 线程启动 Timer
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    var timer = new DispatcherTimer
                    {
                        Interval = TimeSpan.FromSeconds(5)
                    };
                    timer.Tick += UpdatePerformanceData;
                    timer.Start();

                    YF_Manager_Main.logger.LogInfo("性能监视器初始化-完成");
                });
            });
        }

        private void InitializeCounters()
        {
            using (var searcher = new ManagementObjectSearcher(
                new ObjectQuery("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem")))
            {
                foreach (ManagementObject obj in searcher.Get())
                {
                    using (obj)
                    {
                        totalMemoryMB = Convert.ToInt64(obj["TotalPhysicalMemory"]) / (1024 * 1024);
                    }
                }
            }
            YF_Manager_Main.logger.LogInfo(
                $"总系统内存：{totalMemoryMB}MB, {(totalMemoryMB / 1024).ToString("0.0")}GB");

            cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            ramCounter = new PerformanceCounter("Memory", "Available MBytes");

            // 预热：丢弃首次无效采样
            cpuCounter.NextValue();
            ramCounter.NextValue();
            Thread.Sleep(100);
            cpuCounter.NextValue();
            ramCounter.NextValue();
        }

        // 刷新性能数据
        private void UpdatePerformanceData(object sender, EventArgs e)
        {

            try
            {
                // 取 CPU 使用率
                float cpuUsage = cpuCounter.NextValue();

                // 取可用内存（MB）
                float availableMemoryMB = ramCounter.NextValue();

                float usedMemoryMB = totalMemoryMB - availableMemoryMB;
                float memoryUsagePercent = (usedMemoryMB / totalMemoryMB) * 100;

                // 更新图表数据
                UpdateChartData(cpuUsage, memoryUsagePercent);

                // 通知 UI 更新标签
                _notifier.Notify(nameof(Labels));

                MainWindowViewModel.dlg_Show_Cpu_Memory(cpuUsage.ToString("0.0"), $"{(usedMemoryMB / 1024).ToString("0.0")}/{(totalMemoryMB / 1024).ToString("0.0")}");

                if (CounterTimes++ % 12 == 0)
                    YF_Manager_Main.logger.LogInfo($"" +
                        $"CPU:{cpuUsage.ToString("0.0")}%  " +
                        $"内存:{(usedMemoryMB / 1024).ToString("0.0")}GB/{(totalMemoryMB / 1024).ToString("0.0")}GB"
                        );
            }
            catch (Exception ex)
            {
                // 由于 Windows 性能计数器损坏 => cmd lodctr / R
                YF_Manager_Main.logger.ErrorInfo("UpdatePerformanceData", ex.Message);
            }
        }

        // 更新图表数据
        private void UpdateChartData(float cpuUsage, float memoryUsage)
        {
            if (SeriesCollection == null || SeriesCollection.Count < 2)
                return;

            try
            {
                // 获取 CPU 和内存数据序列
                var cpuSeries = SeriesCollection[0].Values as ChartValues<ObservableValue>;
                var memorySeries = SeriesCollection[1].Values as ChartValues<ObservableValue>;

                // 移除最旧数据点（保持6个点）
                if (cpuSeries.Count >= 6)
                {
                    cpuSeries.RemoveAt(0);
                    memorySeries.RemoveAt(0);
                }

                // 添加新数据点
                cpuSeries.Add(new ObservableValue(cpuUsage));
                memorySeries.Add(new ObservableValue(memoryUsage));
            }
            catch (Exception ex)
            {
                YF_Manager_Main.logger.ErrorInfo("UpdateChartData", ex.Message);
            }
        }

    }
}
