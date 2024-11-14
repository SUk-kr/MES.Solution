using Dapper;
using MES.Solution.Helpers;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Configuration;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows;

namespace MES.Solution.ViewModels
{
    public class ShipmentViewModel : INotifyPropertyChanged
    {
        private readonly string _connectionString;
        private DateTime _startDate = DateTime.Now.AddDays(1 - DateTime.Now.Day); //이번달 1일
        private DateTime _endDate = DateTime.Now.AddDays(1 - DateTime.Now.Day).AddMonths(1).AddDays(-1);//이번달 말일
        private string _selectedCompany = "전체";
        private string _selectedProduct = "전체";
        private ShipmentModel _selectedShipment;

        public ShipmentViewModel()
        {
            _connectionString = ConfigurationManager.ConnectionStrings["MESDatabase"].ConnectionString;

            // 컬렉션 초기화
            Shipments = new ObservableCollection<ShipmentModel>();
            Companies = new ObservableCollection<string>();
            Products = new ObservableCollection<string>();

            // 명령 초기화
            SearchCommand = new RelayCommand(async () => await ExecuteSearch());
            AddCommand = new RelayCommand(ExecuteAdd);
            ExportCommand = new RelayCommand(ExecuteExport);
            ViewDetailCommand = new RelayCommand<ShipmentModel>(ExecuteViewDetail);

            // 초기 데이터 로드
            LoadInitialData();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        // 컬렉션
        public ObservableCollection<ShipmentModel> Shipments { get; }
        public ObservableCollection<string> Companies { get; }
        public ObservableCollection<string> Products { get; }

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

        public string SelectedCompany
        {
            get => _selectedCompany;
            set
            {
                if (_selectedCompany != value)
                {
                    _selectedCompany = value;
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

        public ShipmentModel SelectedShipment
        {
            get => _selectedShipment;
            set
            {
                if (_selectedShipment != value)
                {
                    _selectedShipment = value;
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
                await LoadCompanies();
                await LoadProducts();
                await ExecuteSearch();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"데이터 로드 중 오류가 발생했습니다: {ex.Message}",
                    "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task LoadCompanies()
        {
            using (var conn = new MySqlConnection(_connectionString))
            {
                var sql = "SELECT DISTINCT company_name FROM dt_shipment ORDER BY company_name";
                var companies = await conn.QueryAsync<string>(sql);

                Companies.Clear();
                Companies.Add("전체");
                foreach (var company in companies)
                {
                    Companies.Add(company);
                }
            }
        }

        private async Task LoadProducts()
        {
            using (var conn = new MySqlConnection(_connectionString))
            {
                var sql = @"
SELECT DISTINCT p.product_name 
FROM dt_shipment s
JOIN dt_product p ON s.product_code = p.product_code 
ORDER BY p.product_name";
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
    s.shipment_number as ShipmentNumber,
    s.company_code as CompanyCode,
    s.company_name as CompanyName,
    s.product_code as ProductCode,
    p.product_name as ProductName,
    s.production_date as ProductionDate,
    s.shipment_date as ShipmentDate,
    s.shipment_quantity as ShipmentQuantity,
    s.inventory_quantity as InventoryQuantity,
    s.employee_name as EmployeeName
FROM dt_shipment s
JOIN dt_product p ON s.product_code = p.product_code
WHERE s.shipment_date BETWEEN @StartDate AND @EndDate";

                    var parameters = new DynamicParameters();
                    parameters.Add("@StartDate", StartDate.Date);
                    parameters.Add("@EndDate", EndDate.Date);

                    if (!string.IsNullOrEmpty(SelectedCompany) && SelectedCompany != "전체")
                    {
                        sql += " AND s.company_name = @CompanyName";
                        parameters.Add("@CompanyName", SelectedCompany);
                    }

                    if (!string.IsNullOrEmpty(SelectedProduct) && SelectedProduct != "전체")
                    {
                        sql += " AND p.product_name = @ProductName";
                        parameters.Add("@ProductName", SelectedProduct);
                    }

                    sql += " ORDER BY s.shipment_date DESC, s.shipment_number";

                    var result = await conn.QueryAsync<ShipmentModel>(sql, parameters);

                    Shipments.Clear();
                    foreach (var shipment in result)
                    {
                        Shipments.Add(shipment);
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
            // TODO: 출하 등록 창 구현
            MessageBox.Show("출하 등록 기능은 추후 구현 예정입니다.");
        }

        private void ExecuteExport()
        {
            // TODO: 엑셀 내보내기 기능 구현
            MessageBox.Show("엑셀 내보내기 기능은 추후 구현 예정입니다.");
        }

        private void ExecuteViewDetail(ShipmentModel shipment)
        {
            if (shipment != null)
            {
                // TODO: 상세 정보 창 구현
                MessageBox.Show($"출하번호 {shipment.ShipmentNumber}의 상세 정보 창은 추후 구현 예정입니다.");
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

    public class ShipmentModel
    {
        public string ShipmentNumber { get; set; }
        public string CompanyCode { get; set; }
        public string CompanyName { get; set; }
        public string ProductCode { get; set; }
        public string ProductName { get; set; }
        public DateTime ProductionDate { get; set; }
        public DateTime ShipmentDate { get; set; }
        public int ShipmentQuantity { get; set; }
        public int InventoryQuantity { get; set; }
        public string EmployeeName { get; set; }
    }
}
