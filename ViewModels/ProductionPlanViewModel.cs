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

namespace MES.Solution.ViewModels
{
    public class ProductionPlanViewModel : INotifyPropertyChanged
    {
        private readonly string _connectionString;
        private DateTime _startDate = DateTime.Now.AddDays(1 - DateTime.Now.Day);//시작날짜
        private DateTime _endDate = DateTime.Today;
        private string _selectedLine = "전체";
        private string _selectedProduct = "전체";
        private string _selectedStatus = "전체";
        private ProductionPlanModel _selectedPlan;

        public ProductionPlanViewModel()
        {
            _connectionString = ConfigurationManager.ConnectionStrings["MESDatabase"].ConnectionString;

            // 컬렉션 초기화
            ProductionPlans = new ObservableCollection<ProductionPlanModel>();
            ProductionLines = new ObservableCollection<string> { "전체", "라인1", "라인2", "라인3" };
            Products = new ObservableCollection<string>();
            Statuses = new ObservableCollection<string> { "전체", "대기", "작업중", "완료", "지연" };

            // 명령 초기화
            SearchCommand = new RelayCommand(async () => await ExecuteSearch());
            AddCommand = new RelayCommand(ExecuteAdd);
            ExportCommand = new RelayCommand(ExecuteExport);
            ViewDetailCommand = new RelayCommand<ProductionPlanModel>(ExecuteViewDetail);

            // 초기 데이터 로드
            LoadInitialData();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        // 컬렉션
        public ObservableCollection<ProductionPlanModel> ProductionPlans { get; }
        public ObservableCollection<string> ProductionLines { get; }
        public ObservableCollection<string> Products { get; }
        public ObservableCollection<string> Statuses { get; }

        // 속성
        public DateTime StartDate
        {
            get => _startDate;
            set
            {
                if (_startDate != value)
                {
                    _startDate = value;
                    OnPropertyChanged();
                }
            }
        }

        public DateTime EndDate
        {
            get => _endDate;
            set
            {
                if (_endDate != value)
                {
                    _endDate = value;
                    OnPropertyChanged();
                }
            }
        }

        public string SelectedLine
        {
            get => _selectedLine;
            set
            {
                if (_selectedLine != value)
                {
                    _selectedLine = value;
                    OnPropertyChanged();
                }
            }
        }

        public string SelectedProduct
        {
            get => _selectedProduct;
            set
            {
                if (_selectedProduct != value)
                {
                    _selectedProduct = value;
                    OnPropertyChanged();
                }
            }
        }

        public string SelectedStatus
        {
            get => _selectedStatus;
            set
            {
                if (_selectedStatus != value)
                {
                    _selectedStatus = value;
                    OnPropertyChanged();
                }
            }
        }

        public ProductionPlanModel SelectedPlan
        {
            get => _selectedPlan;
            set
            {
                if (_selectedPlan != value)
                {
                    _selectedPlan = value;
                    OnPropertyChanged();
                }
            }
        }

        // 명령
        public ICommand SearchCommand { get; }
        public ICommand AddCommand { get; }
        public ICommand ExportCommand { get; }
        public ICommand ViewDetailCommand { get; }

        private async void LoadInitialData()
        {
            try
            {
                await LoadProducts();
                await ExecuteSearch();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"데이터 로드 중 오류가 발생했습니다: {ex.Message}",
                    "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task LoadProducts()
        {
            using (var conn = new MySqlConnection(_connectionString))
            {
                var sql = @"
SELECT DISTINCT pd.product_name 
FROM dt_production_plan p
JOIN dt_product pd ON p.product_code = pd.product_code 
ORDER BY pd.product_name";
                var products = await conn.QueryAsync<string>(sql);

                Products.Clear();
                Products.Add("전체");
                foreach (var product in products)
                {
                    Products.Add(product);
                }
            }
        }

        private async Task ExecuteSearch()
        {
            try
            {
                using (var conn = new MySqlConnection(_connectionString))
                {
                    var sql = @"
SELECT 
    p.work_order_code as PlanNumber,
    p.production_date as PlanDate,
    p.production_line as ProductionLine,
    p.product_code as ProductCode,
    pd.product_name as ProductName,
    p.order_quantity as PlannedQuantity,
    p.production_quantity as ProductionQuantity,
p.process_status as status,
    ROUND(p.production_quantity * 100.0 / p.order_quantity, 1) as AchievementRate
FROM dt_production_plan p
JOIN dt_product pd ON p.product_code = pd.product_code
WHERE p.production_date BETWEEN @StartDate AND @EndDate";

                    var parameters = new DynamicParameters();
                    parameters.Add("@StartDate", StartDate);
                    parameters.Add("@EndDate", EndDate);

                    if (!string.IsNullOrEmpty(SelectedLine) && SelectedLine != "전체")
                    {
                        sql += " AND p.production_line = @ProductionLine";
                        parameters.Add("@ProductionLine", SelectedLine);
                    }

                    if (!string.IsNullOrEmpty(SelectedProduct) && SelectedProduct != "전체")
                    {
                        sql += " AND pd.product_name = @ProductName";
                        parameters.Add("@ProductName", SelectedProduct);
                    }

                    if (!string.IsNullOrEmpty(SelectedStatus) && SelectedStatus != "전체")
                    {
                        sql += " AND p.process_status = @Status";
                        parameters.Add("@Status", SelectedStatus);
                    }

                    sql += " ORDER BY production_date DESC, work_order_code";

                    var result = await conn.QueryAsync<ProductionPlanModel>(sql, parameters);

                    ProductionPlans.Clear();
                    foreach (var plan in result)
                    {
                        ProductionPlans.Add(plan);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"검색 중 오류가 발생했습니다: {ex.Message}",
                    "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExecuteAdd()
        {
            // TODO: 생산계획 등록 창 구현
            MessageBox.Show("생산계획 등록 기능은 추후 구현 예정입니다.");
        }

        private void ExecuteExport()
        {
            // TODO: 엑셀 내보내기 기능 구현
            MessageBox.Show("엑셀 내보내기 기능은 추후 구현 예정입니다.");
        }

        private void ExecuteViewDetail(ProductionPlanModel plan)
        {
            if (plan != null)
            {
                // TODO: 상세 정보 창 구현
                MessageBox.Show($"계획번호 {plan.PlanNumber}의 상세 정보 창은 추후 구현 예정입니다.");
            }
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void Cleanup()
        {
            // 리소스 정리 필요시 여기에 구현
        }
    }

    public class ProductionPlanModel
    {
        public string PlanNumber { get; set; }
        public DateTime PlanDate { get; set; }
        public string ProductionLine { get; set; }
        public string ProductCode { get; set; }
        public string ProductName { get; set; }
        public int PlannedQuantity { get; set; }
        public int ProductionQuantity { get; set; }
        public decimal AchievementRate { get; set; }
        public string Status { get; set; }
    }
}