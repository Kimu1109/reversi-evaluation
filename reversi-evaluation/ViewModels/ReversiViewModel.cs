namespace reversi_evaluation.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using Avalonia.Media;
using System;
using System.Text;
using System.IO;
using System.Threading.Tasks;
using reversi_evaluation.Evaluations;
using reversi_evaluation.Models;
using System.Collections.Generic;
using System.Linq;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.Kernel;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using LiveChartsCore.Drawing;
using Microsoft.Extensions.Logging;

public partial class PutHistory : ObservableObject
{
    public PutHistory(
        CellState[] board,
        CellState turn,
        int turnCount,
        int winRateBlack,
        int winRateWhite,
        bool isGameEnd
    )
    {
        Board = board.ToList();
        WinRateBlack = winRateBlack;
        WinRateWhite = winRateWhite;
        Turn = turn;
        TurnCount = turnCount;
        IsGameEnd = isGameEnd;
    }
    public List<CellState> Board { get; }
    public int WinRateBlack { get; }
    public int WinRateWhite { get; }
    public CellState Turn { get; }
    public int TurnCount { get; }
    public bool IsGameEnd { get; }
}

public partial class CellViewModel : ObservableObject
{
    public CellViewModel(int x, int y)
    {
        X = x;
        Y = y;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOccupied))]
    [NotifyPropertyChangedFor(nameof(PieceBrush))]
    private CellState _state = CellState.None;

    public bool IsOccupied => State != CellState.None;

    public IBrush PieceBrush => State switch
    {
        CellState.Black => Brushes.Black,
        CellState.White => Brushes.White,
        _ => Brushes.Transparent
    };

    public int X { get; }
    public int Y { get; }
}

public partial class ReversiViewModel : ObservableObject, IAsyncDisposable
{
    private readonly ReversiGame _game = new();
    private EdaxEvaluation? _edax;

    [ObservableProperty]
    private string _player1Name = "プレイヤー1";

    [ObservableProperty]
    private string _player2Name = "プレイヤー2";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PlayerStatus))]
    [NotifyPropertyChangedFor(nameof(BlackBarWidth))]
    private int _blackPercentage = 50;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PlayerStatus))]
    [NotifyPropertyChangedFor(nameof(WhiteBarWidth))]
    private int _whitePercentage = 50;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EdaxResult))]
    private int _edaxScore = 0;

    public string EdaxResult => EdaxRunning ?
        "解析中" :
        EdaxScore switch
        {
            int.MinValue => "失敗",
            _ => EdaxScore.ToString()
        };

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EdaxResult))]
    private bool _edaxRunning = false;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BlackCount))]
    [NotifyPropertyChangedFor(nameof(WhiteCount))]
    private int _turnCount = 0;

    public int BlackBarWidth => (int)(BlackPercentage * 0.01 * 700);
    public int WhiteBarWidth => (int)(WhitePercentage * 0.01 * 700 + 2);

    public ObservableCollection<PutHistory> PutHistories { get; } = new();

    [ObservableProperty]
    private ISeries[] _series;
    
    [ObservableProperty]
    private Axis[] _xAxes = [
        new Axis { MinLimit = 0, Labeler = value => $"{value + 1:0}手目" }
    ];

    [ObservableProperty]
    private Axis[] _yAxes = [
        new Axis { MinLimit = 0, MaxLimit = 100, Labeler = value => $"{value:0}%" }
    ];

    [ObservableProperty]
    private RectangularSection[] _sections = [
        new RectangularSection
        {
            Yi = 50, Yj = 50,
            Stroke = new SolidColorPaint { Color = SKColors.LightGray, StrokeThickness = 3 }
        }
    ];

    public string PlayerStatus
    {
        get
        {
            if (IsGameEnd)
            {
                int b = BlackCount;
                int w = WhiteCount;
                if (b > w) return "黒の勝ち";
                if (w > b) return "白の勝ち";
                return "引き分け";
            }
            int diff = Math.Abs(BlackPercentage - WhitePercentage);
            if (diff < 10) return "互角";
            
            string leader = BlackPercentage > WhitePercentage ? "黒" : "白";
            return diff < 25 ? $"{leader}がやや優勢" : $"{leader}が優勢";
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TurnString))]
    public CellState _turn = CellState.Black;

    public string TurnString => Turn switch
    {
        CellState.White => "白",
        CellState.Black => "黒",
        _ => "不明"
    };

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGameEndString))]
    [NotifyPropertyChangedFor(nameof(PlayerStatus))]
    private bool _isGameEnd = false;

    public string IsGameEndString => IsGameEnd ? "Y" : "N";

    public int BlackCount => _game.CountCells(CellState.Black);
    public int WhiteCount => _game.CountCells(CellState.White);

    public ObservableCollection<CellViewModel> BoardCells { get; } = new();

    public ReversiViewModel()
    {
        for (int y = 0; y < ReversiGame.BoardSize; y++)
        {
            for (int x = 0; x < ReversiGame.BoardSize; x++)
            {
                BoardCells.Add(new CellViewModel(x, y));
            }
        }

        Series = [
            new LineSeries<PutHistory>
            {
                Name = "白勝率",
                Values = PutHistories,
                Stroke = new SolidColorPaint(SKColors.SkyBlue, 8),
                Fill = new SolidColorPaint(SKColor.Empty),
                Mapping = (h, i) => new Coordinate(h.TurnCount, h.WinRateWhite)
            },
            new LineSeries<PutHistory>
            {
                Name = "黒勝率",
                Values = PutHistories,
                Stroke = new SolidColorPaint(SKColors.Black, 8),
                Fill = new SolidColorPaint(SKColor.Empty),
                Mapping = (h, i) => new Coordinate(h.TurnCount, h.WinRateBlack)
            }
        ];
    }

    public async Task InitEdaxAsync()
    {
        string path;
        if (OperatingSystem.IsWindows())
            path = Path.Combine(AppContext.BaseDirectory, "edax", "wEdax-x86-64.exe");
        else if (OperatingSystem.IsLinux())
            path = Path.Combine(AppContext.BaseDirectory, "edax", "lEdax-x86-64");
        else
            return;

        try
        {
            _edax = await EdaxEvaluation.StartAsync(path, Path.Combine(AppContext.BaseDirectory, "edax"));
            await _edax.SetLevelAsync(8);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Edax initialization failed: {ex.Message}");
        }
    }

    public void ResetBoard()
    {
        _game.Reset();
        SyncFromModel();
        
        BlackPercentage = 50;
        WhitePercentage = 50;
        EdaxScore = 0;

        PutHistories.Clear();
        RecordHistory();
    }

    private void SyncFromModel()
    {
        var board = _game.Board;
        for (int i = 0; i < ReversiGame.TotalCells; i++)
        {
            BoardCells[i].State = board[i];
        }
        Turn = _game.Turn;
        TurnCount = _game.TurnCount;
        IsGameEnd = _game.IsGameEnd;
        
        OnPropertyChanged(nameof(BlackCount));
        OnPropertyChanged(nameof(WhiteCount));
    }

    private void RecordHistory()
    {
        PutHistories.Add(new PutHistory(
            _game.Board,
            Turn,
            TurnCount,
            BlackPercentage,
            WhitePercentage,
            IsGameEnd
        ));
    }

    public string ToEdaxSetBoardString()
    {
        var sb = new StringBuilder(65);
        var board = _game.Board;
        for (int i = 0; i < ReversiGame.TotalCells; i++)
        {
            sb.Append(board[i] switch
            {
                CellState.None => '-',
                CellState.Black => '*',
                CellState.White => 'O',
                _ => '-'
            });
        }
        sb.Append(Turn == CellState.Black ? '*' : 'O');
        return sb.ToString();
    }

    public void HistoryBack()
    {
        var history = PutHistories.FirstOrDefault(h => h.TurnCount == TurnCount - 1);
        if (history != null) ApplyHistory(history);
    }

    public void HistoryForward()
    {
        var history = PutHistories.FirstOrDefault(h => h.TurnCount == TurnCount + 1);
        if (history != null) ApplyHistory(history);
    }

    private void ApplyHistory(PutHistory history)
    {
        _game.SetBoard(history.Board.ToArray(), history.Turn, history.TurnCount, history.IsGameEnd);
        BlackPercentage = history.WinRateBlack;
        WhitePercentage = history.WinRateWhite;
        SyncFromModel();
    }

    [RelayCommand]
    private async Task ClickCell(CellViewModel clickedCell)
    {
        if (EdaxRunning || IsGameEnd) return;

        if (_game.TryPut(clickedCell.X, clickedCell.Y))
        {
            EdaxRunning = true;
            SyncFromModel();

            if (!IsGameEnd && _edax != null)
            {
                await UpdateEvaluationAsync();
            }

            // Remove future histories if we are branching
            for (int i = PutHistories.Count - 1; i >= 0; i--)
            {
                if (PutHistories[i].TurnCount >= TurnCount)
                    PutHistories.RemoveAt(i);
            }
            
            RecordHistory();
            EdaxRunning = false;
        }
    }

    private async Task UpdateEvaluationAsync()
    {
        if (_edax == null) return;

        EdaxScore = await _edax.GetHintScoreAsync(ToEdaxSetBoardString());
        if (EdaxScore == int.MinValue)
        {
            Console.WriteLine("Edax evaluation failed.");
            return;
        }

        double winRate = WinRateCalculator.ToWinRate(EdaxScore, _game.CountCells(CellState.None));
        if (Turn == CellState.Black)
        {
            BlackPercentage = (int)winRate;
            WhitePercentage = 100 - (int)winRate;
        }
        else
        {
            BlackPercentage = 100 - (int)winRate;
            WhitePercentage = (int)winRate;
        }
    }

    public CellState TurnedTurn() => ReversiGame.Opposite(Turn);

    public void SkipTurn()
    {
        _game.SetBoard(_game.Board, TurnedTurn(), TurnCount, _game.IsGameEnd);
        SyncFromModel();
    }

    public async ValueTask DisposeAsync()
    {
        if (_edax != null)
        {
            await _edax.DisposeAsync();
        }
    }
}