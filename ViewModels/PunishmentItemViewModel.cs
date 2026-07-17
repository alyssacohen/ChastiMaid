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
    }

    public int PunishmentId { get; }
    public PunishmentKind Kind { get; }
    public string Name { get; }
    public DateOnly AssignedDate { get; }
    public string AssignedLabel { get; }
}
