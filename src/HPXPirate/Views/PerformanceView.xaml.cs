using System.Windows.Controls;
using HPXPirate.ViewModels;

namespace HPXPirate.Views;

public partial class PerformanceView : UserControl
{
    public PerformanceView()
    {
        InitializeComponent();
        DataContext = new PerformanceViewModel(App.Host);
    }
}
