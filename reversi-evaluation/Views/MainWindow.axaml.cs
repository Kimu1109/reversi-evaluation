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

        // Initializing Edax in the background
        _ = reversi.InitEdaxAsync();
        reversi.ResetBoard(false);

        this.Closed += async (s, e) => await reversi.DisposeAsync();
    }
    public void ClickInitBoard(object sender, RoutedEventArgs e)
    {
        reversi.ResetBoard(false);
    }
    public void ClickInitFlipBoard(object sender, RoutedEventArgs e)
    {
        reversi.ResetBoard(true);
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
        reversi.SkipTurn();
    }
    public void ClickWinRateChart(object sender, RoutedEventArgs e)
    {
        var win = new WinRateChart()
        {
            DataContext = reversi
        };
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
    public void ClickEvaluationHistory(object sender, RoutedEventArgs e)
    {
        var win = new EvaluationHistoryWindow
        {
            DataContext = reversi
        };

        win.Show(this);
    }
    public async void ClickSaveHistory(object sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new Avalonia.Platform.Storage.FilePickerSaveOptions
        {
            Title = "履歴を保存",
            DefaultExtension = "json",
            ShowOverwritePrompt = true,
            FileTypeChoices = new[]
            {
                new Avalonia.Platform.Storage.FilePickerFileType("JSON Files") { Patterns = new[] { "*.json" } }
            }
        });

        if (file != null)
        {
            try
            {
                var json = reversi.SaveToSaveDataJson();
                await using var stream = await file.OpenWriteAsync();
                await using var writer = new System.IO.StreamWriter(stream);
                await writer.WriteAsync(json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to save history: {ex.Message}");
            }
        }
    }
    public async void ClickLoadHistory(object sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions
        {
            Title = "履歴を読み込み",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new Avalonia.Platform.Storage.FilePickerFileType("JSON Files") { Patterns = new[] { "*.json" } }
            }
        });

        if (files != null && files.Count > 0)
        {
            try
            {
                await using var stream = await files[0].OpenReadAsync();
                var saveData = await System.Text.Json.JsonSerializer.DeserializeAsync<reversi_evaluation.Models.ReversiSaveData>(stream);
                if (saveData != null)
                {
                    reversi.LoadFromSaveData(saveData);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to load history: {ex.Message}");
            }
        }
    }
}