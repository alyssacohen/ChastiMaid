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
    public int ItemId { get; set; }
    public int RelatedItemId { get; set; }
    public OutfitRelation Relation { get; set; }
}
