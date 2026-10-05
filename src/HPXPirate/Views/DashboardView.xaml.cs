using System.Windows.Controls;
using HPXPirate.ViewModels;

namespace HPXPirate.Views;

public partial class DashboardView : UserControl
{
    public DashboardView()
    {
        InitializeComponent();
        DataContext = new DashboardViewModel(App.Host);
    }
}
