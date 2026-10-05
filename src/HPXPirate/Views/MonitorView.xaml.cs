using System.Windows.Controls;
using HPXPirate.ViewModels;

namespace HPXPirate.Views;

public partial class MonitorView : UserControl
{
    public MonitorView()
    {
        InitializeComponent();
        DataContext = new MonitorViewModel(App.Host);
    }
}
