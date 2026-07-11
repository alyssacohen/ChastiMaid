using LiteDB;

namespace ChastiMaid.Models;

public class Session
{
    [BsonId] public int Id { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public decimal DebtAtStart { get; set; }
    public decimal DebtAtEnd { get; set; }
    public bool SafeUnlocked { get; set; }
}
