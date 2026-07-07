using LiteDB;

namespace ChastiMaid.Models;

public class Chore
{
    [BsonId] public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public decimal Value { get; set; }
    public int FrequencyDays { get; set; } = 1;
    public int MinCompletionMinutes { get; set; }
    public DateTime? LastCompletedAt { get; set; }
}
