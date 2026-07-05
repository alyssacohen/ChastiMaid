using LiteDB;

namespace ChastiMaid.Models;

public class GameSettings
{
    [BsonId] public int Id { get; set; } = 1; // singleton

    // Player
    public string PlayerName { get; set; } = "";
    public int AutoSaveIntervalMinutes { get; set; } = 10;

    // Localization: "system", "en", or "it". Requires a restart to take effect.
    public string Language { get; set; } = "system";

    // Game balance
    public decimal MinDebt { get; set; } = 500;
    public decimal MaxDebt { get; set; } = 1000;
    public double DailyInterestRate { get; set; } = 0.03;

    // Idle wage
    public decimal IdleWageAmount { get; set; } = 5;
    public int IdleWageIntervalMinutes { get; set; } = 30;

    // Popup intervals
    public int PopupMinIntervalSeconds { get; set; } = 180;
    public int PopupMaxIntervalSeconds { get; set; } = 600;
    public int PopupTimeoutSeconds { get; set; } = 15;

    // Busy mode capture
    public int BusyCaptureMinIntervalSeconds { get; set; } = 3;
    public int BusyCaptureMaxIntervalSeconds { get; set; } = 10;

    // Camera
    public int SelectedCameraIndex { get; set; }

    public decimal RollDebt(Random? rng = null)
    {
        rng ??= Random.Shared;
        return Math.Round(MinDebt + (decimal)rng.NextDouble() * (MaxDebt - MinDebt), 0);
    }
}
