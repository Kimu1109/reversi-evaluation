using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using reversi_evaluation.Models;
using Avalonia.Media;

namespace reversi_evaluation.ViewModels;

public partial class HistoryCellViewModel : ObservableObject
{
    public int X { get; }
    public int Y { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOccupied))]
    [NotifyPropertyChangedFor(nameof(PieceBrush))]
    private CellState _state = CellState.None;

    [ObservableProperty]
    private bool _isRecommended = false;

    public bool IsOccupied => State != CellState.None;

    public IBrush PieceBrush => State switch
    {
        CellState.Black => Brushes.Black,
        CellState.White => Brushes.White,
        _ => Brushes.Transparent
    };

    public HistoryCellViewModel(int x, int y)
    {
        X = x;
        Y = y;
    }
}

public partial class EvaluationHistoryWindowViewModel : ObservableObject
{
    public ObservableCollection<PutHistory> PutHistories { get; }

    [ObservableProperty]
    private PutHistory? _selectedHistory;

    public ObservableCollection<HistoryCellViewModel> HistoryCells { get; } = new();

    public EvaluationHistoryWindowViewModel(ObservableCollection<PutHistory> putHistories)
    {
        PutHistories = putHistories;

        for (int y = 0; y < ReversiGame.BoardSize; y++)
        {
            for (int x = 0; x < ReversiGame.BoardSize; x++)
            {
                HistoryCells.Add(new HistoryCellViewModel(x, y));
            }
        }

        // 初期選択として、現在の手番、もしくは最後の履歴を選択
        SelectedHistory = PutHistories.LastOrDefault();
    }

    partial void OnSelectedHistoryChanged(PutHistory? value)
    {
        if (value == null)
        {
            foreach (var cell in HistoryCells)
            {
                cell.State = CellState.None;
                cell.IsRecommended = false;
            }
            return;
        }

        // 盤面を同期
        for (int i = 0; i < ReversiGame.TotalCells; i++)
        {
            HistoryCells[i].State = value.Board[i];
            HistoryCells[i].IsRecommended = false;
        }

        // 推奨手を解析してセット
        if (!string.IsNullOrEmpty(value.RecommendedMove))
        {
            var pos = ParseMove(value.RecommendedMove);
            if (pos.HasValue)
            {
                int index = pos.Value.y * ReversiGame.BoardSize + pos.Value.x;
                if (index >= 0 && index < ReversiGame.TotalCells)
                {
                    HistoryCells[index].IsRecommended = true;
                }
            }
        }
    }

    private static (int x, int y)? ParseMove(string move)
    {
        if (string.IsNullOrEmpty(move) || move.Length < 2) return null;
        char col = char.ToLower(move[0]);
        char row = move[1];
        if (col >= 'a' && col <= 'h' && row >= '1' && row <= '8')
        {
            return (col - 'a', row - '1');
        }
        return null;
    }
}
