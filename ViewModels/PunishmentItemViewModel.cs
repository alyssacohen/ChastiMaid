using ChastiMaid.Localization;
using ChastiMaid.Models;

namespace ChastiMaid.ViewModels;

public class PunishmentItemViewModel : ViewModelBase
{
    public PunishmentItemViewModel(ActivePunishment punishment, ILocalizer L, bool canSoften = false,
        bool canCancel = false, bool isActiveForcedOutfit = false)
    {
        CanSoften = canSoften;
        CanCancel = canCancel;
        PunishmentId = punishment.Id;
        Kind = punishment.Kind;
        Name = Kind switch
        {
            PunishmentKind.Cornertime => L.T("Cornertime"),
            PunishmentKind.WritingLines => L.T("Writing Lines"),
            PunishmentKind.ForcedOutfit => L.T("Forced Outfit"),
            _ => throw new ArgumentOutOfRangeException(nameof(punishment)),
        };
        AssignedDate = DateOnly.FromDateTime(punishment.AssignedAt);

        AssignedLabel = L.T("Assigned {0}", AssignedDate.ToString("dd MMM"));

        ReasonLabel = ReasonText(punishment.Reason, L);

        DetailLabel = Kind switch
        {
            PunishmentKind.WritingLines when punishment.WritingLinesTotalLines is { } lines =>
                L.N("{0} line", "{0} lines", lines, lines),
            PunishmentKind.Cornertime when punishment.CornertimeRequiredSeconds is { } seconds =>
                L.T("{0} min", (int)Math.Ceiling(seconds / 60.0)),
            PunishmentKind.ForcedOutfit when punishment.ForcedOutfitPresetName is { } name => name,
            _ => null,
        };

        ActionLabel = Kind switch
        {
            PunishmentKind.ForcedOutfit when isActiveForcedOutfit =>
                L.T("{0} remaining", FormatRemainingTime(punishment, L)),
            PunishmentKind.ForcedOutfit => L.T("Awaiting its turn"),
            _ => L.T("Start"),
        };
    }

    /// <summary>Formats the remaining Forced Outfit time as whole hours, or minutes when under an hour.</summary>
    public static string FormatRemainingTime(ActivePunishment punishment, ILocalizer L)
    {
        var remainingSeconds = Math.Max(0,
            (punishment.ForcedOutfitRequiredSeconds ?? 0) - punishment.ForcedOutfitElapsedSeconds);
        var minutes = Math.Max(1, (int)Math.Ceiling(remainingSeconds / 60.0));
        var hours = (int)Math.Ceiling(remainingSeconds / 3600.0);
        return remainingSeconds < 3600
            ? L.N("{0} minute", "{0} minutes", minutes, minutes)
            : L.N("{0} hour", "{0} hours", hours, hours);
    }

    /// <summary>
    ///     Why a punishment was assigned. <see cref="ActivePunishment.Reason" /> is a stable identifier stored in the
    ///     save (see the AssignPunishment callers); unknown or missing reasons show nothing.
    /// </summary>
    private static string? ReasonText(string? reason, ILocalizer L) => reason switch
    {
        "PunishmentReasonPopupLate" => L.T("For keeping me waiting."),
        "PunishmentReasonImpossibleQuestionFailed" => L.T("For an unsatisfactory answer to my question."),
        "PunishmentReasonMotivationalQuoteFailed" => L.T("For fumbling your lines, tardily."),
        "PunishmentReasonChoreTooEarly" => L.T("For unseemly haste in your duties."),
        "PunishmentReasonOccasionalTaskTooEarly" => L.T("For declaring a task done when it plainly was not."),
        "PunishmentReasonChoreTooSlow" => L.T("For dawdling well past what the task required."),
        "PunishmentReasonOccasionalTaskTooSlow" => L.T("For treating a simple task as an excuse for idleness."),
        "PunishmentReasonCornertimeFailed" => L.T("For failing to remain still in the corner."),
        "PunishmentReasonPermissionOverrun" => L.T("For overstaying a granted respite."),
        "PunishmentReasonSelfCheckWronglyHidden" => L.T("For denying what the camera plainly required of you."),
        "PunishmentReasonSelfCheckWronglyVisible" => L.T("For lying through your teeth."),
        _ => null,
    };

    public int PunishmentId { get; }
    public PunishmentKind Kind { get; }

    /// <summary>Whether the player owns a SOFTEN_PUNISHMENT card, so the panel offers it for this row.</summary>
    public bool CanSoften { get; }

    /// <summary>Whether the player owns a CANCEL_PUNISHMENT card, so the panel offers it for this row.</summary>
    public bool CanCancel { get; }

    public string Name { get; }
    public DateOnly AssignedDate { get; }
    public string AssignedLabel { get; }

    /// <summary>Why this punishment was assigned (e.g. "Failed a check-in"). Null for saves predating this field.</summary>
    public string? ReasonLabel { get; }

    /// <summary>Kind-specific quantity (lines to write, minutes of cornertime). Null when not applicable.</summary>
    public string? DetailLabel { get; }

    /// <summary>Label for the row's action button. ForcedOutfit has no manual action, so it shows the countdown instead.</summary>
    public string ActionLabel { get; }
}
