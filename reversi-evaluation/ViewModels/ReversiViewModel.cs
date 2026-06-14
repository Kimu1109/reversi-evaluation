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
using System.Collections.Generic;
using System.Linq;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.Kernel;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using LiveChartsCore.Drawing;

public enum CellState
{
    None = 0,
    Black = 1,
    White = -1
}
public partial class PutHistory : ObservableObject
{
    public PutHistory(
        CellViewModel[] Board,
        CellState Turn,
        int TurnCount,
        int WinRateBlack,
        int WinRateWhite,
        bool IsGameEnd
    ){

        foreach(var cell in Board)
        {
            this.Board.Add(cell.State);
        }
        this.WinRateBlack = WinRateBlack;
        this.WinRateWhite = WinRateWhite;
        this.Turn = Turn;
        this.TurnCount = TurnCount;
        this.IsGameEnd = IsGameEnd;
    }
    public List<CellState> Board {get;} = new();
    public int WinRateBlack {get;}
    public int WinRateWhite {get;}
    public CellState Turn {get;}
    public int TurnCount {get;}
    public bool IsGameEnd {get;}
}
public partial class CellViewModel : ObservableObject
{
    public CellViewModel(int X, int Y)
    {
        this.X = X;
        this.Y = Y;
    }

    // 1. バッキングフィールドに属性をつける
    // [ObservableProperty] により、裏で「State」というプロパティが自動生成されます
    // [NotifyPropertyChangedFor] で、Stateが変わったときに関連プロパティも再描画させます
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOccupied))]
    [NotifyPropertyChangedFor(nameof(PieceBrush))]
    private CellState _state = CellState.None;

    // 2. Stateに依存するプロパティ（これらは get のみでOK）
    public bool IsOccupied => State != 0;

    public IBrush PieceBrush => State switch
    {
        CellState.Black => Brushes.Black,
        CellState.White => Brushes.White,
        _ => Brushes.Transparent
    };

    public int X {get; }
    public int Y {get; }
}
public partial class ReversiViewModel : ObservableObject
{
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

    private EdaxEvaluation? _edax;

    public ObservableCollection<PutHistory> PutHistories { get; } = new();

    //* FOR LIVE CHARTS 2
    [ObservableProperty]
    private ISeries[] _series;
    [ObservableProperty]
    private Axis[] _xAxes =
    [
        new Axis
        {
            MinLimit = 0,
            Labeler = value => $"{value + 1:0}手目"
        }
    ];
    [ObservableProperty]
    private Axis[] _yAxes =
    [
        new Axis
        {
            MinLimit = 0,
            MaxLimit = 100,
            Labeler = value => $"{value:0}%"
        }
    ];
    [ObservableProperty]
    private RectangularSection[] _sections =
    [
        new RectangularSection
        {
            Yi = 50,
            Yj = 50,
            Stroke = new SolidColorPaint
            {
                Color = SKColors.LightGray,
                StrokeThickness = 3
            }
        }
    ];

    public string PlayerStatus
    {
        get
        {
            if (IsGameEnd)
            {
                var b = BlackCount;
                var w = WhiteCount;
                if(b > w)
                    return "黒の勝ち";
                else if(w > b)
                    return "白の勝ち";
                else
                    return "引き分け";
            }
            var diff = Math.Abs(BlackPercentage - WhitePercentage);
            if(diff < 10)
                return "互角";

            if(diff < 25)
                if(BlackPercentage > WhitePercentage)
                    return "黒がやや優勢";
                else
                    return "白がやや優勢";
            
            if(BlackPercentage > WhitePercentage)
                return "黒が優勢";
            else
                return "白が優勢";
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TurnString))]
    public CellState _turn = CellState.Black;
    public string TurnString => Turn switch
    {
        CellState.White => "白",
        CellState.Black => "黒",
        _ => "ありえない値" 
    };

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGameEndString))]
    [NotifyPropertyChangedFor(nameof(PlayerStatus))]
    private bool _isGameEnd = false;
    public string IsGameEndString => IsGameEnd ? "Y" : "N";

    public int BlackCount => CountCell(CellState.Black);
    public int WhiteCount => CountCell(CellState.White);

    public ObservableCollection<CellViewModel> BoardCells { get; } = new();


    //!LOGIC部分
    public ReversiViewModel()
    {
        for(int y = 0; y < 8; y++)
        {
            for(int x = 0; x < 8; x++)
            {
                BoardCells.Add(new CellViewModel(x, y));
            }
        }

        Series =
        [
            new LineSeries<PutHistory>
            {
                Name = "白勝率",
                Values = PutHistories,
                Stroke = new SolidColorPaint(SKColors.SkyBlue, 8),
                Fill = new SolidColorPaint(SKColor.Empty),
                Mapping = (h, i) => new Coordinate(
                    h.TurnCount,
                    h.WinRateWhite)
            },

            new LineSeries<PutHistory>
            {
                Name = "黒勝率",
                Values = PutHistories,
                Stroke = new SolidColorPaint(SKColors.Black, 8),
                Fill = new SolidColorPaint(SKColor.Empty),
                Mapping = (h, i) => new Coordinate(
                    h.TurnCount,
                    h.WinRateBlack)
            }
        ];
    }
    public async void InitEdaxAsync()
    {
        string path;
        if (OperatingSystem.IsWindows())
            path = AppContext.BaseDirectory + "edax\\wEdax-x86-64.exe";
        else if(OperatingSystem.IsLinux())
            path = AppContext.BaseDirectory + "edax/lEdax-x86-64";
        else
            return;

        _edax = await EdaxEvaluation.StartAsync(path, AppContext.BaseDirectory + "edax");
        await _edax.SetLevelAsync(8);
    }
    public void ClearBoard()
    {
        for(int i = 0; i < 64; i++)
        {
            BoardCells[i].State = CellState.None;
        }
    }
    public bool IsSkip(CellState turn)
    {
        for(int x = 0; x < 8; x++)
        {
            for(int y = 0; y < 8; y++)
            {
                if(TryPut(x, y, turn, false))
                {
                    return false;
                }
            }
        }
        return true;
    }
    public void ResetBoard()
    {
        ClearBoard();
        BoardCellsAt(3, 3).State = CellState.White;
        BoardCellsAt(4, 4).State = CellState.White;
        BoardCellsAt(3, 4).State = CellState.Black;
        BoardCellsAt(4, 3).State = CellState.Black;

        BlackPercentage = 50;
        WhitePercentage = 50;

        EdaxScore = 0;
        TurnCount = 0;

        IsGameEnd = false;

        PutHistories.Clear();
        PutHistories.Add(new PutHistory(
            BoardCells.ToArray(),
            Turn,
            TurnCount,
            BlackPercentage,
            WhitePercentage,
            IsGameEnd
        ));
    }
    public bool TryPut(int x, int y, CellState state, bool is_put)
    {

        //前提条件
        if(state == CellState.None)
            return false;

        if(!IsInBoard(x, y))
            return false;

        if(BoardCellsAt(x, y).State != CellState.None)
            return false;



        bool result = false;

        //8方向繰り返す
        for(int i = 0; i < 8; i++)
        {
            var move_x = 0;
            var move_y = 0;

            //方向を設定
            switch (i)
            {
                case 0:
                    move_x = -1;
                    move_y = 0;
                    break;
                case 1:
                    move_x = -1;
                    move_y = -1;
                    break;
                case 2:
                    move_x = 0;
                    move_y = -1;
                    break;
                case 3:
                    move_x = 1;
                    move_y = -1;
                    break;
                case 4:
                    move_x = 1;
                    move_y = 0;
                    break;
                case 5:
                    move_x = 1;
                    move_y = 1;
                    break;
                case 6:
                    move_x = 0;
                    move_y = 1;
                    break;
                case 7:
                    move_x = -1;
                    move_y = 1;
                    break;
            }

            //前提条件
            if(!IsInBoard(x + move_x, y + move_y))
                continue;
            if((int)BoardCellsAt(x + move_x, y + move_y).State != -(int)state)
                continue;

            //その方向に伸ばす
            for(int p = 2; p < 8; p++)
            {
                if(!IsInBoard(x + move_x * p, y + move_y * p))
                    break;

                var cell = BoardCellsAt(x + move_x * p, y + move_y * p);

                if(cell.State == CellState.None)
                    break;

                if(cell.State == state)
                {
                    if(!is_put) return true;

                    //置けるなら置く
                    result = true;
                    for(int q = 0; q < p; q++)
                    {
                        BoardCellsAt(x + move_x * q, y + move_y * q).State = state;
                    }

                    break;
                }
            }
        }

        return result;

    }
    public string ToEdaxSetBoardString()
    {
        var sb = new StringBuilder(65);

        // A1 -> H8 の row-major
        for (int rank = 0; rank < 8; rank++)
        {
            for (int file = 0; file < 8; file++)
            {
                sb.Append(BoardCellsAt(file, rank).State switch
                {
                    CellState.None => '-',
                    CellState.Black => '*',
                    CellState.White => 'O',
                    _ => '-'
                });
            }
        }

        sb.Append(Turn == CellState.Black ? '*' : 'O');
        return sb.ToString();
    }
    private bool IsInBoard(int x, int y) => x >= 0 && x <= 7 && y >= 0 && y <= 7;

    public CellViewModel BoardCellsAt(int x, int y) => BoardCells[y * 8 + x];
    public int CountCell(CellState target)
    {
        int count = 0;
        for(int i = 0; i < 64; i++)
        {
            if(BoardCells[i].State == target) count++;
        }
        return count;
    }
    public CellState TurnedTurn()
    {
        return (CellState)(-(int)Turn);
    }
    public void HistoryBack()
    {
        var history = PutHistories.Where(h => h.TurnCount == TurnCount - 1).ToArray();
        if (history.Length > 0)
            ApplyHistory(history[0]);
    }
    public void HistoryForward()
    {
        var history = PutHistories.Where(h => h.TurnCount == TurnCount + 1).ToArray();
        if (history.Length > 0)
            ApplyHistory(history[0]);
    }
    private void ApplyHistory(PutHistory history)
    {
        for(int i = 0; i < 64; i++)
        {
            BoardCells[i].State = history.Board[i];
        }
        Turn = history.Turn;
        TurnCount = history.TurnCount;
        BlackPercentage = history.WinRateBlack;
        WhitePercentage = history.WinRateWhite;
        IsGameEnd = history.IsGameEnd;
    }

    // マスがクリックされたときの処理
    [RelayCommand]
    private async void ClickCell(CellViewModel clickedCell)
    {
        if(!EdaxRunning && TryPut(clickedCell.X, clickedCell.Y, Turn, true))
        {
            EdaxRunning = true;

            Turn = TurnedTurn();
            if (IsSkip(Turn))
            {
                if (IsSkip(TurnedTurn()))
                {
                    IsGameEnd = true;
                    Console.WriteLine("GAME END!");
                }
                Turn = TurnedTurn();
            }
            TurnCount++;

            if (!IsGameEnd)
            {
                if(_edax is not null)
                {
                    EdaxScore = await _edax.GetHintScoreAsync(this.ToEdaxSetBoardString());
                    if(EdaxScore == int.MinValue)
                    {
                        BlackPercentage = 0;
                        WhitePercentage = 0;
                        Console.WriteLine("EDAX FAILED!");
                    }
                    else
                    {
                        double winRate = WinRateCalculator.ToWinRate(EdaxScore, CountCell(CellState.None));
                        if(Turn == CellState.Black)
                        {
                            BlackPercentage = (int)winRate;
                            WhitePercentage = 100 - (int)winRate;
                        }
                        else
                        {
                            BlackPercentage = 100 - (int)winRate;
                            WhitePercentage = (int)winRate;
                        }
                        Console.WriteLine($"Score:{EdaxScore}, WinRate:{winRate}");
                    }
                }
                else
                {
                    Console.WriteLine("EDAX IS NOT STARTED!");
                }
            }

            for (int i = PutHistories.Count - 1; i >= 0; i--)
            {
                if (PutHistories[i].TurnCount >= TurnCount)
                    PutHistories.RemoveAt(i);
            }
            
            PutHistories.Add(new PutHistory(
                BoardCells.ToArray(),
                Turn,
                TurnCount,
                BlackPercentage,
                WhitePercentage,
                IsGameEnd
            ));



            EdaxRunning = false;
        }
    }
}