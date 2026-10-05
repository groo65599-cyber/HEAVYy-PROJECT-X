using System.Windows.Controls;
using HPXPirate.ViewModels;

namespace HPXPirate.Views;

public partial class SystemOptimizerView : UserControl
{
    public SystemOptimizerView()
    {
        InitializeComponent();
        DataContext = new SystemOptimizerViewModel(App.Host);
    }
}
