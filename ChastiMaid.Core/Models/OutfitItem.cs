using CommunityToolkit.Mvvm.ComponentModel;
using LiteDB;

namespace ChastiMaid.Core.Models;

/// <summary>
/// Represents a single clothing piece with its layering and slot properties.
/// </summary>
public partial class OutfitItem : ObservableObject
{
    public ObjectId Id { get; set; } = ObjectId.NewObjectId();

    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private int _weight = 10;
    [ObservableProperty] private double _multiplier = 1.0;

    // We store Tags as a HashSet for logic, but we'll provide a string helper for the UI
    public HashSet<string> Layers { get; set; } = new();

    public HashSet<string> Slots { get; set; } = new();
    public HashSet<string> Tags { get; set; } = new();

    public OutfitItem()
    {
        Layers = new HashSet<string>();
        Slots = new HashSet<string>();
        Tags = new HashSet<string>();
    }

    // Helper for UI Binding to handle the Tags HashSet as a single string
    public string TagsText
    {
        get
        {
            if (Tags == null) return string.Empty;
            return string.Join(", ", Tags);
        }
        set
        {
            var input = value ?? string.Empty;

            Tags = new HashSet<string>(
            input.Split(',', System.StringSplitOptions.RemoveEmptyEntries)
                 .Select(t => t.Trim())
            );

            OnPropertyChanged(nameof(TagsText));
        }
    }
}
