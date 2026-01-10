using LiteDB;

namespace ChastiMaid.Core.Models;

public class OutfitRule
{
    public ObjectId Id { get; set; } = ObjectId.NewObjectId();
    public ObjectId ItemId { get; set; }
    public string Relation { get; set; } = "requires";
    public ObjectId RelatedItemId { get; set; }
}
