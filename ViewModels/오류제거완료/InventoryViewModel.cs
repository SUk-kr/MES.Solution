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
using System.Linq;

namespace MES.Solution.ViewModels
{
    public class InventoryViewModel : INotifyPropertyChanged
    {
        private readonly string _connectionString;
        private string _productNameFilter;
        private string _selectedProductGroup;
        private string _selectedQuantityFilter;

        public InventoryViewModel()
        {
            _connectionString = ConfigurationManager.ConnectionStrings["MESDatabase"].ConnectionString;

            // 컬렉션 초기화
            Inventories = new ObservableCollection<InventoryModel>();
            ProductGroups = new ObservableCollection<string>();
            QuantityFilters = new ObservableCollection<string>
            {
                "전체",
                "안전재고 미만",
                "재고 없음"
            };
            InventoryChartData = new SeriesCollection();
            ValueChartData = new SeriesCollection();

            // 명령 초기화
            SearchCommand = new RelayCommand(async () => await ExecuteSearch());
            RegisterReceiptCommand = new RelayCommand(ExecuteRegisterReceipt);
            RegisterShipmentCommand = new RelayCommand(ExecuteRegisterShipment);
            AdjustInventoryCommand = new RelayCommand(ExecuteAdjustInventory);
            ExportCommand = new RelayCommand(ExecuteExport);
            ViewDetailCommand = new RelayCommand<InventoryModel>(ExecuteViewDetail);

            // 초기 데이터 로드
            _ = LoadInitialData();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        // 속성
        public string ProductNameFilter
        {
            get => _productNameFilter;
            set
            {
                if (_productNameFilter != value)
                {
                    _productNameFilter = value;
                    OnPropertyChanged();
                }
            }
        }

        public string SelectedProductGroup
        {
            get => _selectedProductGroup;
            set
            {
                if (_selectedProductGroup != value)
                {
                    _selectedProductGroup = value;
                    OnPropertyChanged();
                }
            }
        }

        public string SelectedQuantityFilter
        {
            get => _selectedQuantityFilter;
            set
            {
                if (_selectedQuantityFilter != value)
                {
                    _selectedQuantityFilter = value;
                    OnPropertyChanged();
                }
            }
        }

        // 컬렉션
        public ObservableCollection<InventoryModel> Inventories { get; }
        public ObservableCollection<string> ProductGroups { get; }
        public ObservableCollection<string> QuantityFilters { get; }
        public SeriesCollection InventoryChartData { get; }
        public SeriesCollection ValueChartData { get; }
        public string[] ChartLabels { get; private set; }

        // 명령
        public ICommand SearchCommand { get; }
        public ICommand RegisterReceiptCommand { get; }
        public ICommand RegisterShipmentCommand { get; }
        public ICommand AdjustInventoryCommand { get; }
        public ICommand ExportCommand { get; }
        public ICommand ViewDetailCommand { get; }

        // 포매터
        public Func<double, string> QuantityFormatter => value => value.ToString("N0");

        private async Task LoadInitialData()
        {
            try
            {
                await Task.WhenAll(
                    LoadProductGroups(),
                    ExecuteSearch()
                );
            }
            catch (Exception ex)
            {
                MessageBox.Show($"데이터 로드 중 오류가 발생했습니다: {ex.Message}",
                    "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task LoadProductGroups()
        {
            using (var conn = new MySqlConnection(_connectionString))
            {
                var sql = "SELECT DISTINCT product_group FROM dt_product ORDER BY product_group";
                var groups = await conn.QueryAsync<string>(sql);

                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    ProductGroups.Clear();
                    ProductGroups.Add("전체");
                    foreach (var group in groups)
                    {
                        ProductGroups.Add(group);
                    }
                });
            }
        }

        private async Task LoadChartData()
        {
            using (var conn = new MySqlConnection(_connectionString))
            {
                // 재고 수량 차트 데이터
                var quantitySql = @"
                    SELECT 
                        p.product_group,
                        SUM(i.inventory_quantity) as TotalQuantity
                    FROM dt_inventory_management i
                    JOIN dt_product p ON i.product_code = p.product_code
                    GROUP BY p.product_group
                    ORDER BY TotalQuantity DESC";

                var quantityData = await conn.QueryAsync<GroupQuantityModel>(quantitySql);

                // 재고 금액 차트 데이터
                var valueSql = @"
                    SELECT 
                        p.product_group,
                        SUM(i.inventory_quantity * p.price) as TotalValue
                    FROM dt_inventory_management i
                    JOIN dt_product p ON i.product_code = p.product_code
                    GROUP BY p.product_group
                    ORDER BY TotalValue DESC";

                var valueData = await conn.QueryAsync<GroupValueModel>(valueSql);

                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    // 수량 차트 업데이트
                    InventoryChartData.Clear();
                    var quantityValues = new ChartValues<double>();
                    var labels = new System.Collections.Generic.List<string>();

                    foreach (var item in quantityData)
                    {
                        quantityValues.Add(item.TotalQuantity);
                        labels.Add(item.ProductGroup);
                    }

                    InventoryChartData.Add(new ColumnSeries
                    {
                        Title = "재고수량",
                        Values = quantityValues,
                        Fill = new SolidColorBrush(Color.FromRgb(30, 144, 255))
                    });

                    ChartLabels = labels.ToArray();
                    OnPropertyChanged(nameof(ChartLabels));

                    // 금액 차트 업데이트
                    ValueChartData.Clear();
                    foreach (var item in valueData)
                    {
                        ValueChartData.Add(new PieSeries
                        {
                            Title = item.ProductGroup,
                            Values = new ChartValues<double> { item.TotalValue },
                            DataLabels = true,
                            LabelPoint = point => $"{point.Y:N0}원 ({point.Participation:P1})"
                        });
                    }
                });
            }
        }

        private async Task ExecuteSearch()
        {
            try
            {
                using (var conn = new MySqlConnection(_connectionString))
                {
                    // 안전재고량 포함
                    //var sql = @"
                    //    SELECT 
                    //        i.product_code as ProductCode,
                    //        p.product_group as ProductGroup,
                    //        p.product_name as ProductName,
                    //        i.inventory_quantity as CurrentQuantity,
                    //        p.min_stock_quantity as SafetyStock,
                    //        p.unit as Unit,
                    //        p.price as UnitPrice,
                    //        i.inventory_quantity * p.price as TotalValue
                    //    FROM dt_inventory_management i
                    //    JOIN dt_product p ON i.product_code = p.product_code
                    //    WHERE 1=1";

                    var sql = @"
                        SELECT 
                            i.product_code as ProductCode,
                            p.product_group as ProductGroup,
                            p.product_name as ProductName,
                            i.inventory_quantity as CurrentQuantity,
                            p.unit as Unit,
                            p.price as UnitPrice,
                            i.inventory_quantity * p.price as TotalValue
                        FROM dt_inventory_management i
                        JOIN dt_product p ON i.product_code = p.product_code
                        WHERE 1=1";
                    //안전재고량 미포함

                    var parameters = new DynamicParameters();

                    if (!string.IsNullOrEmpty(SelectedProductGroup) && SelectedProductGroup != "전체")
                    {
                        sql += " AND p.product_group = @ProductGroup";
                        parameters.Add("@ProductGroup", SelectedProductGroup);
                    }

                    if (!string.IsNullOrEmpty(ProductNameFilter))
                    {
                        sql += " AND p.product_name LIKE @ProductName";
                        parameters.Add("@ProductName", $"%{ProductNameFilter}%");
                    }

                    if (!string.IsNullOrEmpty(SelectedQuantityFilter))
                    {
                        switch (SelectedQuantityFilter)
                        {
                            //case "안전재고 미만":
                            //    sql += " AND i.inventory_quantity < p.min_stock_quantity";
                            //    break;
                            case "재고 없음":
                                sql += " AND i.inventory_quantity = 0";
                                break;
                        }
                    }

                    sql += " ORDER BY p.product_group, p.product_name";

                    var result = await conn.QueryAsync<InventoryModel>(sql, parameters);

                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        Inventories.Clear();
                        foreach (var item in result)
                        {
                            Inventories.Add(item);
                        }
                    });

                    await LoadChartData();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"검색 중 오류가 발생했습니다: {ex.Message}",
                    "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExecuteRegisterReceipt()
        {
            MessageBox.Show("입고 등록 기능은 추후 구현 예정입니다.", "알림",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ExecuteRegisterShipment()
        {
            MessageBox.Show("출고 등록 기능은 추후 구현 예정입니다.", "알림",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ExecuteAdjustInventory()
        {
            MessageBox.Show("재고 조정 기능은 추후 구현 예정입니다.", "알림",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ExecuteExport()
        {
            MessageBox.Show("엑셀 다운로드 기능은 추후 구현 예정입니다.", "알림",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ExecuteViewDetail(InventoryModel inventory)
        {
            if (inventory != null)
            {
                MessageBox.Show($"제품코드 {inventory.ProductCode}의 상세 정보 창은 추후 구현 예정입니다.", "알림",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class InventoryModel
    {
        public string ProductCode { get; set; }
        public string ProductGroup { get; set; }
        public string ProductName { get; set; }
        public int CurrentQuantity { get; set; }
        public int SafetyStock { get; set; }
        public string Unit { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalValue { get; set; }
    }

    public class GroupQuantityModel
    {
        public string ProductGroup { get; set; }
        public double TotalQuantity { get; set; }
    }

    public class GroupValueModel
    {
        public string ProductGroup { get; set; }
        public double TotalValue { get; set; }
    }
}