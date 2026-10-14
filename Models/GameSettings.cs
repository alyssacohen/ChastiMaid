using System.Text.Json.Serialization;
using ChastiMaid.Localization;
using LiteDB;

namespace ChastiMaid.Models;

/// <summary>
///     Game settings singleton. Properties marked <c>[JsonIgnore]</c> are specific to the machine or the person
///     running the app (identity, language, autosave, camera) and are therefore excluded from the exported game
///     contract (see <c>GameContractService</c>); LiteDB persistence is unaffected. Every other property is part
///     of the game and travels with the contract.
/// </summary>
public class GameSettings
{
    [JsonIgnore] [BsonId] public int Id { get; set; } = 1; // singleton

    // Player
    [JsonIgnore] public string PlayerName { get; set; } = "";
    [JsonIgnore] public int AutoSaveIntervalMinutes { get; set; } = 10;

    // Summon voice — comma-separated list of address terms the employer may use when calling the
    // player to a popup (in addition to PlayerName), e.g. "pet, maid, girl"
    public string Appellatives { get; set; } = "";

    // Localization: "en" or "it" ("system" is only the unset default, resolved and persisted at startup).
    [JsonIgnore] public string Language { get; set; } = "system";

    // Game balance
    public decimal MinDebt { get; set; } = 500;
    public decimal MaxDebt { get; set; } = 1000;
    public double DailyInterestRate { get; set; } = 0.03;

    // Idle wage
    public decimal IdleWageAmount { get; set; } = 5;
    public int IdleWageIntervalMinutes { get; set; } = 30;

    // Popup intervals
    public int PopupMinIntervalSeconds { get; set; } = 180;
    public int PopupMaxIntervalSeconds { get; set; } = 1200;
    public int PopupTimeoutSeconds { get; set; } = 15;

    // Hydration popup — minimum time between hydration reminders, independent of the general popup cycle
    public bool HydrationEnabled { get; set; } = true;
    public int HydrationMinIntervalMinutes { get; set; } = 30;

    // Curtsy popup — video recording duration
    public bool CurtsyEnabled { get; set; } = true;
    public int CurtsyVideoDurationSeconds { get; set; } = 5;

    // Motivational Quote popup — listening/typing window and speech recognition sensitivity
    public bool QuoteReadingEnabled { get; set; } = true;
    public int QuoteListenTimeoutSeconds { get; set; } = 45;
    public int QuoteMatchMinSimilarityPercent { get; set; } = 82;

    // Busy mode capture
    public int BusyCaptureMinIntervalSeconds { get; set; } = 300;
    public int BusyCaptureMaxIntervalSeconds { get; set; } = 600;

    // Kneel popup — total duration range (spec: 1-5 minutes) and verification-photo interval
    public bool KneelEnabled { get; set; } = true;
    public int KneelMinDurationSeconds { get; set; } = 60;
    public int KneelMaxDurationSeconds { get; set; } = 300;
    public int KneelPhotoMinIntervalSeconds { get; set; } = 5;
    public int KneelPhotoMaxIntervalSeconds { get; set; } = 30;

    // Plug and Play — enable/disable toggle (per-item eligibility/duration configured separately)
    public bool PlugAndPlayEnabled { get; set; } = true;

    // Diaper Play — halves the Hydration interval and (once a permission system exists) denies
    // toilet permission for the session, whenever the confirmed outfit includes a "diaper"-tagged item.
    public bool DiaperPlayEnabled { get; set; } = true;

    // Fine punishment — always active, cannot be disabled
    public decimal FineSeverityValue { get; set; } = 10; // $ per severity point

    // Cornertime punishment
    public bool CornertimeEnabled { get; set; } = true;
    public double CornertimeSeverityValue { get; set; } = 30; // seconds of required stillness per severity point
    public int CornertimeMinRequiredSeconds { get; set; } = 60; // floor for the initial required duration
    public int CornertimeMaxOverrunSeconds { get; set; } = 180; // fixed buffer beyond the initial requirement before it fails
    public int CornertimeMovementCooldownMinSeconds { get; set; } = 10; // min seconds between two movement penalties (motion is checked continuously)
    public int CornertimeMovementCooldownMaxSeconds { get; set; } = 30; // max seconds between two movement penalties
    public int CornertimeMovementSensitivityPercent { get; set; } = 3; // % of changed pixels counted as movement
    public int CornertimeMovementPenaltyMinSeconds { get; set; } = 10; // min seconds added to the required duration per detected movement
    public int CornertimeMovementPenaltyMaxSeconds { get; set; } = 30; // max seconds added to the required duration per detected movement
    public bool CornertimeVoiceEnabled { get; set; } = true; // spoken narration (the completion chime always plays)
    public int CornertimeMaxRequiredSeconds { get; set; } = 900; // ceiling for a single Cornertime punishment; severity beyond it spills into further punishments

    // Writing Lines punishment
    public bool WritingLinesEnabled { get; set; } = true;
    public double WritingLinesSeverityValue { get; set; } = 3; // lines per severity point
    public int WritingLinesPenaltyLinesPerMistake { get; set; } = 1;
    public int WritingLinesMinLines { get; set; } = 5;
    public int WritingLinesMaxLines { get; set; } = 150;
    public bool WritingLinesNoIdleEnabled { get; set; } = true;
    public int WritingLinesNoIdleTimeoutSeconds { get; set; } = 5;
    public int WritingLinesNoIdleLinesToAdd { get; set; } = 2;
    public bool WritingLinesInterruptionsEnabled { get; set; } = true;
    public int WritingLinesInterruptionMinIntervalSeconds { get; set; } = 60;
    public int WritingLinesInterruptionMaxIntervalSeconds { get; set; } = 180;

    // Forced Outfit punishment — the concrete outfit presets live in ForcedOutfitPreset (per-profile collection)
    public bool ForcedOutfitEnabled { get; set; } = true;
    public double ForcedOutfitMaxHours { get; set; } = 30; // ceiling for a single Forced Outfit punishment; severity beyond it spills into further punishments

    // Camera
    [JsonIgnore] public int SelectedCameraIndex { get; set; }

    public decimal RollDebt(Random? rng = null)
    {
        rng ??= Random.Shared;
        return Math.Round(MinDebt + (decimal)rng.NextDouble() * (MaxDebt - MinDebt), 0);
    }

    public const int AppellativeMaxLength = 30;
    public const int AppellativeMaxCount = 10;

    // Entries over AppellativeMaxLength or beyond AppellativeMaxCount are silently dropped — the
    // same rule both the Settings warning (SettingsViewModel.AppellativesWarning) and the runtime
    // summon phrase (DashboardViewModel.PlaySummonBell) use, so what the player is warned about
    // is exactly what gets excluded.
    /// <summary>What <see cref="ParseAppellatives" /> will drop from <paramref name="appellatives" />; null when nothing.</summary>
    public static string? AppellativesWarning(string? appellatives, ILocalizer l)
    {
        var raw = (appellatives ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (raw.Length > AppellativeMaxCount)
        {
            return l.T("Only the first {0} are used.", AppellativeMaxCount);
        }

        var tooLong = raw.FirstOrDefault(a => a.Length > AppellativeMaxLength);
        return tooLong is null
            ? null
            : l.T("\"{0}\" is over {1} characters and will be ignored.", tooLong, AppellativeMaxLength);
    }

    public IReadOnlyList<string> ParseAppellatives()
    {
        // ponytail: LiteDB round-trips an empty string field as null, so a freshly seeded profile
        // (never saved through Settings) reads back Appellatives == null, not "".
        return (Appellatives ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(a => a.Length <= AppellativeMaxLength)
            .Take(AppellativeMaxCount)
            .ToList();
    }
}
