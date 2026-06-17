using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace reversi_evaluation.Models;

public class ReversiSaveData
{
    [JsonPropertyName("appName")]
    [JsonPropertyOrder(-4)]
    public string AppName { get; set; } = "reversi_evaluation";

    [JsonPropertyName("repository")]
    [JsonPropertyOrder(-3)]
    public string Repository { get; set; } = "https://github.com/Kimu1109/reversi-evaluation";

    [JsonPropertyName("player1Name")]
    [JsonPropertyOrder(-2)]
    public string Player1Name { get; set; } = "";

    [JsonPropertyName("player2Name")]
    [JsonPropertyOrder(-1)]
    public string Player2Name { get; set; } = "";

    [JsonPropertyName("savedAt")]
    [JsonPropertyOrder(0)]
    public System.DateTime SavedAt { get; set; }

    [JsonPropertyName("history")]
    [JsonPropertyOrder(1)]
    public List<PutHistoryItem> History { get; set; } = new();
}

public class PutHistoryItem
{
    [JsonPropertyName("board")]
    public List<CellState> Board { get; set; } = new();

    [JsonPropertyName("turn")]
    public CellState Turn { get; set; }

    [JsonPropertyName("turnCount")]
    public int TurnCount { get; set; }

    [JsonPropertyName("winRateBlack")]
    public int WinRateBlack { get; set; }

    [JsonPropertyName("winRateWhite")]
    public int WinRateWhite { get; set; }

    [JsonPropertyName("isGameEnd")]
    public bool IsGameEnd { get; set; }

    [JsonPropertyName("recommendedMove")]
    public string? RecommendedMove { get; set; }
}
