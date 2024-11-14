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
using System.Linq;

namespace MES.Solution.ViewModels
{
    public class WorkOrderViewModel : INotifyPropertyChanged
    {
        private readonly string _connectionString;
        private DateTime _workDate = DateTime.Now.AddDays(1 - DateTime.Now.Day);//시작날짜
        private string _selectedShift = "전체";
        private string _selectedLine = "전체";
        private WorkOrderModel _selectedWorkOrder;

        public WorkOrderViewModel()
        {
            _connectionString = ConfigurationManager.ConnectionStrings["MESDatabase"].ConnectionString;

            // 컬렉션 초기화
            WorkOrders = new ObservableCollection<WorkOrderModel>();
            Shifts = new ObservableCollection<string> { "전체", "주간1", "주간2", "야간1", "야간2" };
            ProductionLines = new ObservableCollection<string> { "전체", "라인1", "라인2", "라인3" };

            // 명령 초기화
            SearchCommand = new RelayCommand(async () => await ExecuteSearch());
            AddCommand = new RelayCommand(ExecuteAdd);
            MoveUpCommand = new RelayCommand(ExecuteMoveUp, CanExecuteMove);
            MoveDownCommand = new RelayCommand(ExecuteMoveDown, CanExecuteMove);
            SaveSequenceCommand = new RelayCommand(ExecuteSaveSequence, CanExecuteSaveSequence);

            // 초기 데이터 로드
            LoadInitialData();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        // 속성
        public DateTime WorkDate
        {
            get => _workDate;
            set
            {
                if (_workDate != value)
                {
                    _workDate = value;
                    OnPropertyChanged();
                }
            }
        }

        public string SelectedShift
        {
            get => _selectedShift;
            set
            {
                if (_selectedShift != value)
                {
                    _selectedShift = value;
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

        public WorkOrderModel SelectedWorkOrder
        {
            get => _selectedWorkOrder;
            set
            {
                if (_selectedWorkOrder != value)
                {
                    _selectedWorkOrder = value;
                    OnPropertyChanged();
                    // 선택된 작업지시 변경 시 이동 버튼 상태 갱신
                    (MoveUpCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    (MoveDownCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    (SaveSequenceCommand as RelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        // 컬렉션
        public ObservableCollection<WorkOrderModel> WorkOrders { get; }
        public ObservableCollection<string> Shifts { get; }
        public ObservableCollection<string> ProductionLines { get; }

        // 명령
        public ICommand SearchCommand { get; }
        public ICommand AddCommand { get; }
        public ICommand MoveUpCommand { get; }
        public ICommand MoveDownCommand { get; }
        public ICommand SaveSequenceCommand { get; }

        private async void LoadInitialData()
        {
            try
            {
                await ExecuteSearch();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"데이터 로드 중 오류가 발생했습니다: {ex.Message}",
                    "오류", MessageBoxButton.OK, MessageBoxImage.Error);
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
        pp.work_order_code AS WorkOrderNumber,
        pp.production_date AS ProductionDate,
        pp.product_code AS ProductCode,
        dp.product_name AS ProductName,
        pp.order_quantity AS OrderQuantity,
        pp.production_quantity AS ProductionQuantity,
        pp.work_shift AS Shift,
        pp.process_status AS Status,
        pp.production_line AS ProductionLine
    FROM dt_production_plan pp
    JOIN dt_product dp ON pp.product_code = dp.product_code
    WHERE pp.production_date = @WorkDate";
                    //dt_product에서 product_name 불러와야함

                    var parameters = new DynamicParameters();
                    parameters.Add("@WorkDate", WorkDate);

                    if (!string.IsNullOrEmpty(SelectedShift) && SelectedShift != "전체")
                    {
                        sql += " AND work_shift = @Shift";
                        parameters.Add("@Shift", SelectedShift);
                    }

                    if (!string.IsNullOrEmpty(SelectedLine) && SelectedLine != "전체")
                    {
                        sql += " AND production_line = @ProductionLine";
                        parameters.Add("@ProductionLine", SelectedLine);
                    }



                    sql += " ORDER BY WorkOrderNumber";

                    var result = await conn.QueryAsync<WorkOrderModel>(sql, parameters);

                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        WorkOrders.Clear();
                        foreach (var order in result)
                        {
                            WorkOrders.Add(order);
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"작업지시 조회 중 오류가 발생했습니다: {ex.Message}",
                    "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExecuteAdd()
        {
            // TODO: 작업지시 등록 창 구현
            MessageBox.Show("작업지시 등록 기능은 추후 구현 예정입니다.");
        }

        private bool CanExecuteMove()
        {
            return SelectedWorkOrder != null;
        }

        private void ExecuteMoveUp()
        {
            if (SelectedWorkOrder == null) return;

            var index = WorkOrders.IndexOf(SelectedWorkOrder);
            if (index > 0)
            {
                WorkOrders.Move(index, index - 1);
                UpdateSequenceNumbers();
            }
        }

        private void ExecuteMoveDown()
        {
            if (SelectedWorkOrder == null) return;

            var index = WorkOrders.IndexOf(SelectedWorkOrder);
            if (index < WorkOrders.Count - 1)
            {
                WorkOrders.Move(index, index + 1);
                UpdateSequenceNumbers();
            }
        }

        private void UpdateSequenceNumbers()
        {
            for (int i = 0; i < WorkOrders.Count; i++)
            {
                WorkOrders[i].Sequence = i + 1;
            }
        }

        private bool CanExecuteSaveSequence()
        {
            return WorkOrders.Count > 0;
        }

        private async void ExecuteSaveSequence()
        {
            try
            {
                using (var conn = new MySqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    using (var transaction = conn.BeginTransaction())
                    {
                        try
                        {
                            foreach (var order in WorkOrders)
                            {
                                var sql = @"
    UPDATE dt_production_plan 
    SET work_order_sequence = @Sequence 
    WHERE work_order_code = @WorkOrderNumber";

                                await conn.ExecuteAsync(sql, new
                                {
                                    order.Sequence,
                                    order.WorkOrderNumber
                                }, transaction);
                            }

                            transaction.Commit();
                            MessageBox.Show("작업 순서가 저장되었습니다.", "알림",
                                MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                        catch
                        {
                            transaction.Rollback();
                            throw;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"작업 순서 저장 중 오류가 발생했습니다: {ex.Message}",
                    "오류", MessageBoxButton.OK, MessageBoxImage.Error);
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

    public class WorkOrderModel : INotifyPropertyChanged
    {
        private int _sequence;
        private bool _isSelected;

        public string WorkOrderNumber { get; set; }
        public DateTime ProductionDate { get; set; }
        public string ProductCode { get; set; }
        public string ProductName { get; set; }
        public int OrderQuantity { get; set; }
        public int ProductionQuantity { get; set; }
        public string Shift { get; set; }
        public string Status { get; set; }
        public string ProductionLine { get; set; }

        public int Sequence
        {
            get => _sequence;
            set
            {
                if (_sequence != value)
                {
                    _sequence = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}