using LiteDB;

namespace ChastiMaid.Models;

/// <summary>Active cooldown for a permission kind inside a save slot. At most one entry per PermissionKind.</summary>
public class PermissionCooldown
{
    [BsonId] public int Id { get; set; }
    public PermissionKind Kind { get; set; }
    public DateTime CooldownUntil { get; set; }
}
