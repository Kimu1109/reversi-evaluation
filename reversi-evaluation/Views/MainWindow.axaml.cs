using Avalonia.Controls;
using reversi_evaluation.ViewModels;
using Avalonia.Interactivity;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia;
using System;

namespace reversi_evaluation.Views;

public partial class MainWindow : Window
{
    public ReversiViewModel reversi = new();
    public MainWindow()
    {
        InitializeComponent();

        this.playerInfo.SetDataContext(reversi);
        this.reversiBoard.SetDataContext(reversi);
        this.statusBar.SetDataContext(reversi);

        reversi.InitEdaxAsync();
        reversi.ResetBoard();
    }
    public void ClickInitBoard(object sender, RoutedEventArgs e)
    {
        reversi.ResetBoard();
    }
    public void ClickVersionWindow(object sender, RoutedEventArgs e)
    {
        var win = new VersionWindow();
        win.Show(this);
    }
    public void ClickExitApp(object sender, RoutedEventArgs e)
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lifetime)
        {
            lifetime.Shutdown();
        }
    }
    public void ClickTurnSkip(object sender, RoutedEventArgs e)
    {
        reversi.Turn = reversi.TurnedTurn();
    }
    public void ClickWinRateChart(object sender, RoutedEventArgs e)
    {
        var win = new WinRateChart();
        win.SetDataContext(reversi);
        win.Show(this);
    }
    public void ClickHistoryBack(object sender, RoutedEventArgs e)
    {
        reversi.HistoryBack();
    }
    public void ClickHistoryForward(object sender, RoutedEventArgs e)
    {
        reversi.HistoryForward();
    }
}