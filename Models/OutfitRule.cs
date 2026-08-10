using LiteDB;

namespace ChastiMaid.Models;

public enum OutfitRelation
{
    Requires,
    Incompatible,
}

public class OutfitRule
{
    [BsonId] public int Id { get; set; }
    public string SourceTag { get; set; } = "";
    public string RelatedTag { get; set; } = "";
    public OutfitRelation Relation { get; set; }
}
