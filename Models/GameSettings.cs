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
    public int QuoteListenTimeoutSeconds { get; set; } = 45;
    public int QuoteMatchMinSimilarityPercent { get; set; } = 82;

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

    // Diaper Play — halves the Hydration interval and (once a permission system exists) denies
    // toilet permission for the session, whenever the confirmed outfit includes a "diaper"-tagged item.
    public bool DiaperPlayEnabled { get; set; }

    // Fine punishment — always active, cannot be disabled
    public decimal FineSeverityValue { get; set; } = 5; // $ per severity point

    // Cornertime punishment
    public bool CornertimeEnabled { get; set; } = true;
    public double CornertimeSeverityValue { get; set; } = 30; // seconds of required stillness per severity point
    public int CornertimeMinRequiredSeconds { get; set; } = 60; // floor for the initial required duration
    public int CornertimeMaxOverrunSeconds { get; set; } = 120; // fixed buffer beyond the initial requirement before it fails
    public int CornertimeMovementCooldownMinSeconds { get; set; } = 10; // min seconds between two movement penalties (motion is checked continuously)
    public int CornertimeMovementCooldownMaxSeconds { get; set; } = 30; // max seconds between two movement penalties
    public int CornertimeMovementSensitivityPercent { get; set; } = 3; // % of changed pixels counted as movement
    public int CornertimeMovementPenaltyMinSeconds { get; set; } = 10; // min seconds added to the required duration per detected movement
    public int CornertimeMovementPenaltyMaxSeconds { get; set; } = 20; // max seconds added to the required duration per detected movement
    public bool CornertimeVoiceEnabled { get; set; } = true; // spoken narration (the completion chime always plays)
    public int CornertimeMaxRequiredSeconds { get; set; } = 600; // ceiling applied when assigning or aggravating a Cornertime punishment

    // Writing Lines punishment
    public bool WritingLinesEnabled { get; set; } = true;
    public double WritingLinesSeverityValue { get; set; } = 3; // lines per severity point
    public int WritingLinesPenaltyLinesPerMistake { get; set; } = 1;
    public int WritingLinesMinLines { get; set; } = 20;
    public int WritingLinesMaxLines { get; set; } = 150;

    // Forced Outfit punishment — the concrete outfit presets live in ForcedOutfitPreset (per-profile collection)
    public bool ForcedOutfitEnabled { get; set; } = true;
    public int ForcedOutfitMaxSessions { get; set; } = 10; // ceiling applied when assigning or aggravating a Forced Outfit punishment

    // Camera
    public int SelectedCameraIndex { get; set; }

    public decimal RollDebt(Random? rng = null)
    {
        rng ??= Random.Shared;
        return Math.Round(MinDebt + (decimal)rng.NextDouble() * (MaxDebt - MinDebt), 0);
    }
}
