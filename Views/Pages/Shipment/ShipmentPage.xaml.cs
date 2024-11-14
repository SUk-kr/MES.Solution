using MES.Solution.ViewModels;
using System.Windows.Controls;

namespace MES.Solution.Views.Pages.Shipment
{
    /// <summary>
    /// ShipmentPage.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class ShipmentPage : Page
    {
        private ShipmentViewModel _viewModel;
        public ShipmentPage()
        {
            InitializeComponent();
            _viewModel = new ShipmentViewModel();
            DataContext = _viewModel;
        }

        public void OnNavigatedFrom()
        {
            _viewModel?.Cleanup();
        }
    }
}
