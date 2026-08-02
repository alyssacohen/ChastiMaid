using LiteDB;

namespace ChastiMaid.Models;

/// <summary>Per-kind base parameters for the permission (break request) system. Profile DB, one row per PermissionKind.</summary>
public class PermissionTypeConfig
{
    [BsonId] public int Id { get; set; }
    public PermissionKind Kind { get; set; }
    public int BaseGrantPercent { get; set; }
    public int DurationMinutes { get; set; }
    public int CooldownMinutes { get; set; }
}
