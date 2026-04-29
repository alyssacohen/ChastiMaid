using LiteDB;

namespace ChastiMaid.Models;

public class PlayerStats
{
    [BsonId] public int Id { get; set; } = 1; // singleton
    public int GamesCompleted { get; set; }
    public int GamesFailed { get; set; }
    public int TotalSessions { get; set; }
}
