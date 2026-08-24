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

    // Snapshot of the worn outfit (+ active Plug-and-Play item, if any) at capture time, used by the
    // Self-check popup to tell whether a queried item belonged to that session's outfit. Records saved
    // before this field existed default to an empty list, so HasOutfitSnapshot marks them ineligible for
    // self-check instead of being misread as "nothing was worn".
    public List<int> OutfitItemIdsAtCapture { get; set; } = [];
    public bool HasOutfitSnapshot { get; set; }

    public bool Reviewed { get; set; }
    public bool? IsCompliant { get; set; }
}
