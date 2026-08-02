using LiteDB;

namespace ChastiMaid.Models;

/// <summary>The currently active granted break, if any. Singleton within a save slot.</summary>
public class ActivePermissionBreak
{
    [BsonId] public int Id { get; set; } = 1;
    public PermissionKind Kind { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime MaxEndAt { get; set; }
}
