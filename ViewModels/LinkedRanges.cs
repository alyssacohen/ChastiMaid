using System.ComponentModel;
using System.Reflection;

namespace ChastiMaid.ViewModels;

/// <summary>
///     Keeps minimum/maximum property pairs ordered: a minimum raised above its maximum pushes the maximum up, a
///     maximum lowered below its minimum pushes the minimum down. Edits are only recorded as they arrive and resolved by
///     <see cref="Commit" /> once the edit ends (focus left the field, a spinner step, a save), because a NumericUpDown
///     writes every keystroke: typing 150 into a maximum would otherwise drag its minimum down to 1, then 15.
/// </summary>
public sealed class LinkedRanges
{
    private readonly object _owner;
    private readonly (PropertyInfo Min, PropertyInfo Max)[] _pairs;

    // Pairs (by minimum name) whose maximum was the side edited last.
    private readonly HashSet<string> _maxEditedLast = [];

    public LinkedRanges(INotifyPropertyChanged owner, params (string Min, string Max)[] pairs)
    {
        _owner = owner;
        _pairs = [..pairs.Select(p => (Prop(p.Min), Prop(p.Max)))];
        owner.PropertyChanged += (_, e) => Edited(e.PropertyName);
    }

    /// <summary>For every pair out of order, the side not edited last follows the other one.</summary>
    public void Commit()
    {
        foreach (var (min, max) in _pairs)
        {
            var minValue = (IComparable)min.GetValue(_owner)!;
            var maxValue = max.GetValue(_owner)!;
            if (minValue.CompareTo(maxValue) <= 0)
            {
                continue;
            }

            if (_maxEditedLast.Contains(min.Name))
            {
                min.SetValue(_owner, maxValue);
            }
            else
            {
                max.SetValue(_owner, minValue);
            }
        }
    }

    private void Edited(string? name)
    {
        foreach (var (min, max) in _pairs)
        {
            if (name == max.Name)
            {
                _maxEditedLast.Add(min.Name);
            }
            else if (name == min.Name)
            {
                _maxEditedLast.Remove(min.Name);
            }
        }
    }

    private PropertyInfo Prop(string name) =>
        _owner.GetType().GetProperty(name) ?? throw new InvalidOperationException($"Unknown property {name}");
}
