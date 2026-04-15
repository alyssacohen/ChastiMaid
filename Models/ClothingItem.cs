using LiteDB;

namespace ChastiMaid.Models;

public class ClothingItem
{
    [BsonId] public int Id { get; set; }
    public string Name { get; set; } = "";
    public List<string> Layers { get; set; } = [];
    public List<string> Slots { get; set; } = [];
    public double Multiplier { get; set; } = 1.0;
    public double Weight { get; set; } = 1.0;
    public List<string> Tags { get; set; } = [];
}
