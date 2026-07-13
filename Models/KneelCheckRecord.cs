using LiteDB;

namespace ChastiMaid.Models;

/// <summary>Photos taken while kneeling before the camera during an idle popup, pending future review.</summary>
public class KneelCheckRecord
{
    [BsonId] public int Id { get; set; }
    public List<string> PhotoPaths { get; set; } = [];
    public DateTime StartedAt { get; set; }

    // Consumed by a future review popup, not yet implemented
    public bool Reviewed { get; set; }
    public bool? IsCompliant { get; set; }
}
