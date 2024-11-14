using System.Windows.Controls;
using MES.Solution.ViewModels;

namespace MES.Solution.Views.Pages
{
    public partial class InventoryPage : Page
    {
        private readonly InventoryViewModel _viewModel;

        public InventoryPage()
        {
            InitializeComponent();
            _viewModel = new InventoryViewModel();
            DataContext = _viewModel;
        }
    }
}