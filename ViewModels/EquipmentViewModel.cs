using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using MES.Solution.Helpers;
using MySql.Data.MySqlClient;
using Dapper;
using System.Configuration;
using System.Threading.Tasks;
using System.Windows;
using LiveCharts;
using LiveCharts.Wpf;
using System.Windows.Media;
using System.Windows.Threading;

namespace MES.Solution.ViewModels
{
    public class EquipmentViewModel : INotifyPropertyChanged
    {
        private readonly string _connectionString;
        private readonly DispatcherTimer _refreshTimer;
        private bool _isLoading;

        public EquipmentViewModel()
        {
            _connectionString = ConfigurationManager.ConnectionStrings["MESDatabase"].ConnectionString;

            // 컬렉션 초기화
            EquipmentCards = new ObservableCollection<EquipmentCardModel>();
            MaintenanceSchedules = new ObservableCollection<MaintenanceScheduleModel>();
            Alerts = new ObservableCollection<AlertModel>();

            // 명령 초기화
            RefreshCommand = new RelayCommand(async () => await RefreshData());
            ManageScheduleCommand = new RelayCommand(ExecuteManageSchedule);

            // 타이머 설정 (30초마다 데이터 갱신)
            _refreshTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(30)
            };
            _refreshTimer.Tick += async (s, e) => await RefreshData();
            _refreshTimer.Start();

            // 초기 데이터 로드
            _ = LoadInitialData();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        // 컬렉션
        public ObservableCollection<EquipmentCardModel> EquipmentCards { get; }
        public ObservableCollection<MaintenanceScheduleModel> MaintenanceSchedules { get; }
        public ObservableCollection<AlertModel> Alerts { get; }

        // 명령
        public ICommand RefreshCommand { get; }
        public ICommand ManageScheduleCommand { get; }

        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                if (_isLoading != value)
                {
                    _isLoading = value;
                    OnPropertyChanged();
                }
            }
        }

        private async Task LoadInitialData()
        {
            try
            {
                IsLoading = true;
                await Task.WhenAll(
                    LoadEquipmentStatus(),
                    LoadMaintenanceSchedules(),
                    LoadAlerts()
                );
            }
            catch (Exception ex)
            {
                MessageBox.Show($"데이터 로드 중 오류가 발생했습니다: {ex.Message}",
                    "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task LoadEquipmentStatus()
        {
            using (var conn = new MySqlConnection(_connectionString))
            {
                // 첫 번째 쿼리 (설비 상태 모니터링)
                var sql = @"
    SELECT 
        equipment_code as EquipmentCode,
        equipment_company_name as Name,
        temperature as Temperature,
        humidity as Humidity,
        CASE 
            WHEN temperature > 28 THEN '경고'
            WHEN temperature < 18 THEN '경고'
            ELSE '정상'
        END as Status
    FROM dt_facility_management";


                var equipments = await conn.QueryAsync<EquipmentCardModel>(sql);

                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    EquipmentCards.Clear();
                    foreach (var equipment in equipments)
                    {
                        // 더미 차트 데이터 생성 (실제로는 DB에서 가져와야 함)
                        equipment.ChartData = new SeriesCollection
                        {
                            new LineSeries
                            {
                                Values = new ChartValues<double> { 20, 22, 23, 24, 25, 24, 23 },
                                PointGeometry = null,
                                Fill = new SolidColorBrush(Color.FromArgb(50, 30, 144, 255)),
                                Stroke = new SolidColorBrush(Color.FromRgb(30, 144, 255))
                            }
                        };
                        EquipmentCards.Add(equipment);
                    }
                });
            }
        }

        private async Task LoadMaintenanceSchedules()
        {
            using (var conn = new MySqlConnection(_connectionString))
            {
                // 두 번째 쿼리 (점검 일정)
                var sql = @"
    SELECT 
        equipment_code as EquipmentCode,
        equipment_company_name as EquipmentName,
        inspection_date as LastCheckDate,
        DATE_ADD(inspection_date, INTERVAL 
            CASE 
                WHEN inspection_frequency = '월간' THEN 30
                WHEN inspection_frequency = '분기' THEN 90
                ELSE 0
            END DAY) as NextCheckDate,
        equipment_contact_person as ResponsiblePerson,
        CASE 
            WHEN DATEDIFF(DATE_ADD(inspection_date, 
                INTERVAL CASE 
                    WHEN inspection_frequency = '월간' THEN 30
                    WHEN inspection_frequency = '분기' THEN 90
                    ELSE 0
                END DAY), CURRENT_DATE) < 0 
                THEN '지연'
            WHEN DATEDIFF(DATE_ADD(inspection_date, 
                INTERVAL CASE 
                    WHEN inspection_frequency = '월간' THEN 30
                    WHEN inspection_frequency = '분기' THEN 90
                    ELSE 0
                END DAY), CURRENT_DATE) <= 7 
                THEN '예정'
            ELSE '정상'
        END as Status
    FROM dt_facility_management
    ORDER BY NextCheckDate";

                var schedules = await conn.QueryAsync<MaintenanceScheduleModel>(sql);

                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    MaintenanceSchedules.Clear();
                    foreach (var schedule in schedules)
                    {
                        MaintenanceSchedules.Add(schedule);
                    }
                });
            }
        }

        private async Task LoadAlerts()
        {
            using (var conn = new MySqlConnection(_connectionString))
            {
                var sql = @"
    SELECT 
        equipment_company_name as EquipmentName,
        CASE 
            WHEN temperature > 28 THEN CONCAT(equipment_company_name, ' 온도 상승 (', temperature, '°C)')
            WHEN temperature < 18 THEN CONCAT(equipment_company_name, ' 온도 하강 (', temperature, '°C)')
            WHEN humidity > 70 THEN CONCAT(equipment_company_name, ' 습도 높음 (', humidity, '%)')
            WHEN humidity < 30 THEN CONCAT(equipment_company_name, ' 습도 낮음 (', humidity, '%)')
        END as Message,
        CURRENT_TIMESTAMP as Time
    FROM dt_facility_management
    WHERE temperature > 28 OR temperature < 18 OR humidity > 70 OR humidity < 30";

                var alerts = await conn.QueryAsync<AlertModel>(sql);

                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    Alerts.Clear();
                    foreach (var alert in alerts)
                    {
                        Alerts.Add(alert);
                    }
                });
            }
        }

        private async Task RefreshData()
        {
            await LoadInitialData();
        }

        private void ExecuteManageSchedule()
        {
            // TODO: 점검일정 관리 창 구현
            MessageBox.Show("점검일정 관리 기능은 추후 구현 예정입니다.", "알림",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void Cleanup()
        {
            _refreshTimer?.Stop();
        }
    }

    public class EquipmentCardModel
    {
        public string EquipmentCode { get; set; }
        public string Name { get; set; }
        public double Temperature { get; set; }
        public double Humidity { get; set; }
        public string Status { get; set; }
        public int ProductionCount { get; set; }
        public string OperationTime { get; set; }
        public SeriesCollection ChartData { get; set; }
    }

    public class MaintenanceScheduleModel
    {
        public string EquipmentCode { get; set; }
        public string EquipmentName { get; set; }
        public DateTime LastCheckDate { get; set; }
        public DateTime NextCheckDate { get; set; }
        public string ResponsiblePerson { get; set; }
        public string Status { get; set; }
    }

    public class AlertModel
    {
        public string EquipmentName { get; set; }
        public string Message { get; set; }
        public DateTime Time { get; set; }
    }
}