using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Threading;
using System.Windows;
using LiveCharts;
using LiveCharts.Wpf;
using System.Windows.Media;
using MySql.Data.MySqlClient;
using Dapper;
using System.Configuration;
using System.Threading.Tasks;
using System.Linq;

namespace MES.Solution.ViewModels
{
    public class DashboardViewModel : INotifyPropertyChanged
    {
        private readonly string _connectionString;
        private readonly DispatcherTimer _refreshTimer;

        // 상태 필드
        private string _todayProduction;
        private double _todayProductionRate;
        private string _weeklyProduction;
        private double _weeklyProductionRate;
        private double _equipmentOperationRate;
        private double _achievementRate;

        public DashboardViewModel()
        {
            _connectionString = ConfigurationManager.ConnectionStrings["MESDatabase"].ConnectionString;

            // 데이터 컬렉션 초기화
            ProductionStatus = new ObservableCollection<ProductionStatusModel>();
            ProductionTrendSeries = new SeriesCollection();
            InventoryStatusSeries = new SeriesCollection();

            // 타이머 설정 (30초마다 새로고침)
            _refreshTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(30)
            };
            _refreshTimer.Tick += async (s, e) => await RefreshDataAsync();
            _refreshTimer.Start();

            // 초기 데이터 로드
            _ = InitializeDataAsync();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        // 속성들
        public string TodayProduction
        {
            get => _todayProduction;
            set
            {
                if (_todayProduction != value)
                {
                    _todayProduction = value;
                    OnPropertyChanged();
                }
            }
        }

        public double TodayProductionRate
        {
            get => _todayProductionRate;
            set
            {
                if (_todayProductionRate != value)
                {
                    _todayProductionRate = value;
                    OnPropertyChanged();
                }
            }
        }

        public string WeeklyProduction
        {
            get => _weeklyProduction;
            set
            {
                if (_weeklyProduction != value)
                {
                    _weeklyProduction = value;
                    OnPropertyChanged();
                }
            }
        }

        public double WeeklyProductionRate
        {
            get => _weeklyProductionRate;
            set
            {
                if (_weeklyProductionRate != value)
                {
                    _weeklyProductionRate = value;
                    OnPropertyChanged();
                }
            }
        }

        public double EquipmentOperationRate
        {
            get => _equipmentOperationRate;
            set
            {
                if (_equipmentOperationRate != value)
                {
                    _equipmentOperationRate = value;
                    OnPropertyChanged();
                }
            }
        }

        public double AchievementRate
        {
            get => _achievementRate;
            set
            {
                if (_achievementRate != value)
                {
                    _achievementRate = value;
                    OnPropertyChanged();
                }
            }
        }

        // 컬렉션
        public ObservableCollection<ProductionStatusModel> ProductionStatus { get; }
        public SeriesCollection ProductionTrendSeries { get; }
        public SeriesCollection InventoryStatusSeries { get; }
        public string[] ProductionTrendLabels { get; private set; }

        // 포매터
        public Func<double, string> ProductionFormatter { get; } = value => value.ToString("N0");

        private async Task InitializeDataAsync()
        {
            try
            {
                await Task.WhenAll(
                    LoadProductionStatusAsync(),
                    LoadProductionTrendAsync(),
                    LoadInventoryStatusAsync()
                );
            }
            catch (Exception ex)
            {
                // 로그 기록 또는 에러 처리
                System.Diagnostics.Debug.WriteLine($"Data initialization error: {ex.Message}");
            }
        }

        private async Task LoadProductionStatusAsync()
        {
            using (var conn = new MySqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                var sql = @"
                    SELECT 
                        p.product_name as ProductName,
                        pp.planned_quantity as PlannedQuantity,
                        pp.production_quantity as ProductionQuantity,
                        ROUND(pp.production_quantity * 100.0 / pp.planned_quantity, 1) as AchievementRate,
                        pp.status as Status
                    FROM dt_production_plan pp
                    JOIN dt_product p ON pp.product_code = p.product_code
                    WHERE pp.production_date = CURDATE()
                    ORDER BY pp.planned_quantity DESC";

                var result = await conn.QueryAsync<ProductionStatusModel>(sql);

                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    ProductionStatus.Clear();
                    foreach (var item in result)
                    {
                        ProductionStatus.Add(item);
                    }
                });
            }
        }

        private async Task LoadProductionTrendAsync()
        {
            using (var conn = new MySqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                var sql = @"
                    SELECT 
                        DATE_FORMAT(production_date, '%Y-%m-%d') as Date,
                        SUM(production_quantity) as Quantity
                    FROM dt_production_plan
                    WHERE production_date >= DATE_SUB(CURDATE(), INTERVAL 7 DAY)
                    GROUP BY production_date
                    ORDER BY production_date";

                var result = await conn.QueryAsync<ProductionTrendModel>(sql);
                var data = result.ToList();

                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    ProductionTrendSeries.Clear();
                    ProductionTrendSeries.Add(new LineSeries
                    {
                        Title = "생산량",
                        Values = new ChartValues<double>(data.Select(x => (double)x.Quantity)),
                        PointGeometry = DefaultGeometries.Circle,
                        Stroke = new SolidColorBrush(Color.FromRgb(24, 90, 189))
                    });

                    ProductionTrendLabels = data.Select(x => x.Date).ToArray();
                    OnPropertyChanged(nameof(ProductionTrendLabels));
                });
            }
        }

        private async Task LoadInventoryStatusAsync()
        {
            using (var conn = new MySqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                var sql = @"
                    SELECT 
                        p.product_name as ProductName,
                        i.quantity as Quantity
                    FROM dt_inventory i
                    JOIN dt_product p ON i.product_code = p.product_code
                    ORDER BY i.quantity DESC
                    LIMIT 5";

                var result = await conn.QueryAsync<InventoryStatusModel>(sql);
                var data = result.ToList();

                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    InventoryStatusSeries.Clear();
                    foreach (var item in data)
                    {
                        InventoryStatusSeries.Add(new PieSeries
                        {
                            Title = item.ProductName,
                            Values = new ChartValues<double> { item.Quantity },
                            DataLabels = true
                        });
                    }
                });
            }
        }

        private async Task RefreshDataAsync()
        {
            await InitializeDataAsync();
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void Cleanup()
        {
            _refreshTimer.Stop();
        }
    }

    public class ProductionStatusModel
    {
        public string ProductName { get; set; }
        public int PlannedQuantity { get; set; }
        public int ProductionQuantity { get; set; }
        public decimal AchievementRate { get; set; }
        public string Status { get; set; }
    }

    public class ProductionTrendModel
    {
        public string Date { get; set; }
        public int Quantity { get; set; }
    }

    public class InventoryStatusModel
    {
        public string ProductName { get; set; }
        public int Quantity { get; set; }
    }
}