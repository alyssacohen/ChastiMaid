using LiteDB;

namespace ChastiMaid.Models;

/// <summary>
///     One owned copy of a catalogue card, held in the player's inventory for the current save. A copy is out of the
///     shared pool for as long as this row exists; consuming the card deletes the row and returns the copy to the pool.
/// </summary>
public class InventoryCard
{
    [BsonId] public int Id { get; set; }
    public CardType Type { get; set; }
    public int Variant { get; set; }

    [BsonIgnore] public CardId CardId => new(Type, Variant);
}
