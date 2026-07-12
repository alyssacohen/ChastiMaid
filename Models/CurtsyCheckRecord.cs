using LiteDB;

namespace ChastiMaid.Models;

/// <summary>A video recorded during a "curtsy" idle popup, pending future review.</summary>
public class CurtsyCheckRecord
{
    [BsonId] public int Id { get; set; }
    public string VideoPath { get; set; } = "";
    public DateTime RecordedAt { get; set; }

    // Consumed by a future review popup, not yet implemented
    public bool Reviewed { get; set; }
    public bool? IsCompliant { get; set; }
}
