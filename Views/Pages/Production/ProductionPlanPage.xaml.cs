using System.Windows.Controls;
using MES.Solution.ViewModels;

namespace MES.Solution.Views.Pages
{
    public partial class ProductionPlanPage : Page
    {
        private readonly ProductionPlanViewModel _viewModel;

        public ProductionPlanPage()
        {
            InitializeComponent();
            _viewModel = new ProductionPlanViewModel();
            DataContext = _viewModel;
        }

        public void OnNavigatedFrom()
        {
            _viewModel?.Cleanup();
        }
    }
}