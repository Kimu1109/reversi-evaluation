using System;
using System.Collections.Generic;
using System.Linq;

namespace reversi_evaluation.Models;

public enum CellState
{
    None = 0,
    Black = 1,
    White = -1
}

public class ReversiGame
{
    public const int BoardSize = 8;
    public const int TotalCells = BoardSize * BoardSize;

    private readonly CellState[] _board = new CellState[TotalCells];
    
    public CellState[] Board => _board.ToArray();

    public CellState Turn { get; private set; } = CellState.Black;
    public bool IsGameEnd { get; private set; }
    public int TurnCount { get; private set; }

    private static readonly (int dx, int dy)[] Directions =
    [
        (-1, 0), (-1, -1), (0, -1), (1, -1), (1, 0), (1, 1), (0, 1), (-1, 1)
    ];

    public ReversiGame()
    {
        Reset();
    }

    public void Reset()
    {
        Array.Fill(_board, CellState.None);
        SetCell(3, 3, CellState.White);
        SetCell(4, 4, CellState.White);
        SetCell(3, 4, CellState.Black);
        SetCell(4, 3, CellState.Black);

        Turn = CellState.Black;
        TurnCount = 0;
        IsGameEnd = false;
    }

    public void SetBoard(CellState[] board, CellState turn, int turnCount, bool isGameEnd)
    {
        if (board.Length != TotalCells) throw new ArgumentException("Invalid board size");
        Array.Copy(board, _board, TotalCells);
        Turn = turn;
        TurnCount = turnCount;
        IsGameEnd = isGameEnd;
    }

    public bool TryPut(int x, int y, bool execute = true)
    {
        if (IsGameEnd || !IsInBoard(x, y) || GetCell(x, y) != CellState.None)
            return false;

        bool canPut = false;
        foreach (var (dx, dy) in Directions)
        {
            if (CheckDirection(x, y, dx, dy, out int count))
            {
                if (!execute) return true;
                canPut = true;
                FlipDirection(x, y, dx, dy, count);
            }
        }

        if (canPut && execute)
        {
            SetCell(x, y, Turn);
            AdvanceTurn();
        }

        return canPut;
    }

    private bool CheckDirection(int x, int y, int dx, int dy, out int count)
    {
        count = 0;
        int nx = x + dx;
        int ny = y + dy;

        if (!IsInBoard(nx, ny) || GetCell(nx, ny) != Opposite(Turn))
            return false;

        for (int i = 2; i < BoardSize; i++)
        {
            nx = x + dx * i;
            ny = y + dy * i;

            if (!IsInBoard(nx, ny)) break;

            var state = GetCell(nx, ny);
            if (state == CellState.None) break;
            if (state == Turn)
            {
                count = i - 1;
                return true;
            }
        }

        return false;
    }

    private void FlipDirection(int x, int y, int dx, int dy, int count)
    {
        for (int i = 1; i <= count; i++)
        {
            SetCell(x + dx * i, y + dy * i, Turn);
        }
    }

    private void AdvanceTurn()
    {
        TurnCount++;
        var nextTurn = Opposite(Turn);
        if (CanMove(nextTurn))
        {
            Turn = nextTurn;
        }
        else if (CanMove(Turn))
        {
            // Skip nextTurn, stay with current Turn
        }
        else
        {
            IsGameEnd = true;
        }
    }

    public bool CanMove(CellState player)
    {
        for (int y = 0; y < BoardSize; y++)
        {
            for (int x = 0; x < BoardSize; x++)
            {
                if (CheckMovePossible(x, y, player)) return true;
            }
        }
        return false;
    }

    private bool CheckMovePossible(int x, int y, CellState player)
    {
        if (GetCell(x, y) != CellState.None) return false;

        foreach (var (dx, dy) in Directions)
        {
            int nx = x + dx;
            int ny = y + dy;
            if (IsInBoard(nx, ny) && GetCell(nx, ny) == Opposite(player))
            {
                for (int i = 2; i < BoardSize; i++)
                {
                    int tx = x + dx * i;
                    int ty = y + dy * i;
                    if (!IsInBoard(tx, ty)) break;
                    var state = GetCell(tx, ty);
                    if (state == CellState.None) break;
                    if (state == player) return true;
                }
            }
        }
        return false;
    }

    public static CellState Opposite(CellState state) => state switch
    {
        CellState.Black => CellState.White,
        CellState.White => CellState.Black,
        _ => CellState.None
    };

    private bool IsInBoard(int x, int y) => x >= 0 && x < BoardSize && y >= 0 && y < BoardSize;
    public CellState GetCell(int x, int y) => _board[y * BoardSize + x];
    private void SetCell(int x, int y, CellState state) => _board[y * BoardSize + x] = state;

    public int CountCells(CellState state) => _board.Count(c => c == state);
}