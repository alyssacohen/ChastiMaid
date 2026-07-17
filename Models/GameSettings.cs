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

    // Hydration popup — minimum time between hydration reminders, independent of the general popup cycle
    public bool HydrationEnabled { get; set; } = true;
    public int HydrationMinIntervalMinutes { get; set; } = 60;

    // Curtsy popup — video recording duration
    public bool CurtsyEnabled { get; set; } = true;
    public int CurtsyVideoDurationSeconds { get; set; } = 5;

    // Motivational Quote popup — listening/typing window and speech recognition sensitivity
    public bool QuoteReadingEnabled { get; set; } = true;
    public int QuoteListenTimeoutSeconds { get; set; } = 20;
    public double QuoteRecognitionMinConfidence { get; set; } = 0.6;

    // Busy mode capture
    public int BusyCaptureMinIntervalSeconds { get; set; } = 3;
    public int BusyCaptureMaxIntervalSeconds { get; set; } = 10;

    // Kneel popup — total duration range (spec: 1-5 minutes) and verification-photo interval
    public bool KneelEnabled { get; set; } = true;
    public int KneelMinDurationSeconds { get; set; } = 60;
    public int KneelMaxDurationSeconds { get; set; } = 300;
    public int KneelPhotoMinIntervalSeconds { get; set; } = 3;
    public int KneelPhotoMaxIntervalSeconds { get; set; } = 10;

    // Plug and Play — enable/disable toggle (per-item eligibility/duration configured separately)
    public bool PlugAndPlayEnabled { get; set; }

    // Fine punishment — always active, cannot be disabled
    public decimal FineSeverityValue { get; set; } = 5; // $ per severity point

    // Cornertime punishment
    public bool CornertimeEnabled { get; set; } = true;
    public double CornertimeSeverityValue { get; set; } = 30; // seconds of required stillness per severity point
    public int CornertimeMaxOverrunSeconds { get; set; } = 120; // buffer beyond the requirement before it fails
    public int CornertimeCheckIntervalSeconds { get; set; } = 5; // how often a frame is compared for movement
    public int CornertimeMovementSensitivityPercent { get; set; } = 3; // % of changed pixels counted as movement
    public int CornertimeMovementPenaltySeconds { get; set; } = 15; // added to the deadline per detected movement

    // Writing Lines punishment
    public bool WritingLinesEnabled { get; set; } = true;
    public double WritingLinesSeverityValue { get; set; } = 3; // lines per severity point
    public int WritingLinesPenaltyLinesPerMistake { get; set; } = 1;

    // Forced Outfit punishment
    public bool ForcedOutfitEnabled { get; set; } = true;
    public double ForcedOutfitSeverityValue { get; set; } = 0.3; // sessions per severity point
    public List<int> ForcedOutfitClothingItemIds { get; set; } = [];
    public bool ForcedOutfitForceFullOutfit { get; set; }

    // Camera
    public int SelectedCameraIndex { get; set; }

    public decimal RollDebt(Random? rng = null)
    {
        rng ??= Random.Shared;
        return Math.Round(MinDebt + (decimal)rng.NextDouble() * (MaxDebt - MinDebt), 0);
    }
}
