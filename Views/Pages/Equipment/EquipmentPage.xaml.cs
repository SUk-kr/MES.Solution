using System.Windows.Controls;
using MES.Solution.ViewModels;

namespace MES.Solution.Views.Pages
{
    public partial class EquipmentPage : Page
    {
        private readonly EquipmentViewModel _viewModel;

        public EquipmentPage()
        {
            InitializeComponent();
            _viewModel = new EquipmentViewModel();
            DataContext = _viewModel;
        }

        public void OnNavigatedFrom()
        {
            _viewModel?.Cleanup();
        }
    }
}