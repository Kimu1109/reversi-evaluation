using System;
using System.Globalization;
using Avalonia.Data.Converters;
using reversi_evaluation.Models;

namespace reversi_evaluation.Converters;

public class CellStateConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is CellState state)
        {
            return state switch
            {
                CellState.Black => "黒",
                CellState.White => "白",
                _ => "なし"
            };
        }
        return value;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
