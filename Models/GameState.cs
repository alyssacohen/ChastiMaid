using LiteDB;

namespace ChastiMaid.Models;

public class GameState
{
    [BsonId] public int Id { get; set; } = 1; // singleton per save
    public string SlotName { get; set; } = "";
    public decimal InitialDebt { get; set; }
    public decimal CurrentDebt { get; set; }
    public Difficulty Difficulty { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime LastSessionAt { get; set; }
    public bool IsCompleted { get; set; }
}
