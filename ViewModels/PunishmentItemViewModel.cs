using ChastiMaid.Models;

namespace ChastiMaid.ViewModels;

public class PunishmentItemViewModel : ViewModelBase
{
    public int PunishmentId { get; }
    public string Name { get; }
    public DateOnly AssignedDate { get; }
    public DateOnly? ExpiryDate { get; }
    public string AssignedLabel { get; }

    public PunishmentItemViewModel(ActivePunishment punishment)
    {
        PunishmentId = punishment.Id;
        Name = punishment.TemplateName;
        AssignedDate = DateOnly.FromDateTime(punishment.AssignedAt);
        ExpiryDate = null; // ActivePunishment has no explicit expiry date

        var assigned = AssignedDate.ToString("dd MMM");
        AssignedLabel = ExpiryDate.HasValue
            ? $"Assigned {assigned} · Expires {ExpiryDate.Value:dd MMM}"
            : $"Assigned {assigned} · No expiry";
    }
}
