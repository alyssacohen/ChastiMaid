using LiteDB;

namespace ChastiMaid.Core.Models;

/// <summary>
/// Represents a single clothing piece with its layering and slot properties.
/// This is a pure data model with no UI framework dependencies.
/// </summary>
public class OutfitItem
{
    public ObjectId Id { get; set; } = ObjectId.NewObjectId();
    public string Name { get; set; } = string.Empty;
    public int Weight { get; set; } = 10;
    public double Multiplier { get; set; } = 1.0;

    /// <summary>
    /// The layers this item can be worn on (e.g., "undies", "base", "outer").
    /// </summary>
    private HashSet<string>? _layers;

    public HashSet<string> Layers
    {
        get => _layers ??= [];
        set => _layers = value;
    }

    /// <summary>
    /// The body slots this item occupies (e.g., "chest", "legs", "feet").
    /// </summary>
    private HashSet<string>? _slots;

    public HashSet<string> Slots
    {
        get => _slots ??= [];
        set => _slots = value;
    }

    /// <summary>
    /// Optional tags for categorization and filtering (e.g., "leather", "formal").
    /// </summary>
    private HashSet<string>? _tags;
    public HashSet<string> Tags
    {
        get => _tags ??= [];
        set => _tags = value;
    }
    /// <summary>
    /// Default constructor ensures all collections are initialized.
    /// </summary>
    public OutfitItem()
    {
        Layers = [];
        Slots = [];
        Tags = [];
    }
}
