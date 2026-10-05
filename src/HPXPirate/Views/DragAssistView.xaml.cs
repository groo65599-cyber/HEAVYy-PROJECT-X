using System.Windows.Controls;
using HPXPirate.ViewModels;

namespace HPXPirate.Views;

public partial class DragAssistView : UserControl
{
    public DragAssistView()
    {
        InitializeComponent();
        DataContext = new DragAssistViewModel(App.Host);
    }
}
