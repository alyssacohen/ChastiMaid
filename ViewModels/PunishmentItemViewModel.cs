using ChastiMaid.Localization;
using ChastiMaid.Models;

namespace ChastiMaid.ViewModels;

public class PunishmentItemViewModel : ViewModelBase
{
    public PunishmentItemViewModel(ActivePunishment punishment)
    {
        PunishmentId = punishment.Id;
        Kind = punishment.Kind;
        Name = Resources.Get(Kind switch
        {
            PunishmentKind.Cornertime => "PunishmentKindCornertime",
            PunishmentKind.WritingLines => "PunishmentKindWritingLines",
            PunishmentKind.ForcedOutfit => "PunishmentKindForcedOutfit",
            _ => throw new ArgumentOutOfRangeException(nameof(punishment)),
        });
        AssignedDate = DateOnly.FromDateTime(punishment.AssignedAt);

        AssignedLabel = string.Format(Resources.Get("PunishmentAssignedFormat"), AssignedDate.ToString("dd MMM"));

        ReasonLabel = punishment.Reason is { } reasonKey ? Resources.Get(reasonKey) : null;

        DetailLabel = Kind switch
        {
            PunishmentKind.WritingLines when punishment.WritingLinesTotalLines is { } lines =>
                string.Format(Resources.Get("PunishmentDetailWritingLinesFormat"), lines),
            PunishmentKind.Cornertime when punishment.CornertimeRequiredSeconds is { } seconds =>
                string.Format(Resources.Get("PunishmentDetailCornertimeFormat"), (int)Math.Ceiling(seconds / 60.0)),
            _ => null,
        };
    }

    public int PunishmentId { get; }
    public PunishmentKind Kind { get; }
    public string Name { get; }
    public DateOnly AssignedDate { get; }
    public string AssignedLabel { get; }

    /// <summary>Why this punishment was assigned (e.g. "Failed a check-in"). Null for saves predating this field.</summary>
    public string? ReasonLabel { get; }

    /// <summary>Kind-specific quantity (lines to write, minutes of cornertime). Null when not applicable.</summary>
    public string? DetailLabel { get; }
}
