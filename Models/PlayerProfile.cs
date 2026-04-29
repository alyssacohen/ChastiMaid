using LiteDB;

namespace ChastiMaid.Models;

public enum Difficulty { Easy, Medium, Hard }

public class PlayerProfile
{
    [BsonId] public int Id { get; set; } = 1; // singleton
    public string PlayerName { get; set; } = "";
    public Difficulty Difficulty { get; set; } = Difficulty.Medium;
    public int AutoSaveIntervalMinutes { get; set; } = 10;
}
