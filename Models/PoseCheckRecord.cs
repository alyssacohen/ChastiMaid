using LiteDB;

namespace ChastiMaid.Models;

/// <summary>A photo taken during a "pose for camera" idle popup, pending future review.</summary>
public class PoseCheckRecord
{
    [BsonId] public int Id { get; set; }
    public int ItemId { get; set; }
    public string ItemName { get; set; } = "";
    public string PhotoPath { get; set; } = "";
    public DateTime CapturedAt { get; set; }

    // Consumed by a future review popup, not yet implemented
    public bool Reviewed { get; set; }
    public bool? IsCompliant { get; set; }
}
