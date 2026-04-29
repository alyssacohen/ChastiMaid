using LiteDB;

namespace ChastiMaid.Models;

public class GameSettings
{
    [BsonId] public int Id { get; set; } = 1; // singleton

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
}
