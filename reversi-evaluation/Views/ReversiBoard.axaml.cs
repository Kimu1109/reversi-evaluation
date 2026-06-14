using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using reversi_evaluation.ViewModels;

namespace reversi_evaluation.Views;

public partial class ReversiBoard : UserControl
{
    public ReversiBoard()
    {
        InitializeComponent();
    }
    public void SetDataContext(ReversiViewModel data)
    {
        DataContext = data;
    }
}