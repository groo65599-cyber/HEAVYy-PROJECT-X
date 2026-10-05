using System.Windows.Controls;
using HPXPirate.ViewModels;

namespace HPXPirate.Views;

public partial class GameProfilesView : UserControl
{
    public GameProfilesView()
    {
        InitializeComponent();
        DataContext = new GameProfilesViewModel(App.Host);
    }
}
