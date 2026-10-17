using System.Globalization;
using System.Windows.Input;
using ChastiMaid.Localization;
using ReactiveUI;
using ReactiveUI.SourceGenerators;

namespace ChastiMaid.ViewModels;

/// <summary>
///     Weight or Multiplier of the Wardrobe targets: the value they share, or nothing when they differ. Numbers are
///     shown in the invariant format (decimal point) whatever the language.
/// </summary>
public partial class WardrobeNumberFieldViewModel : ViewModelBase
{
    /// <summary>Spinner step, the one the Wardrobe fields have always used.</summary>
    public const double Step = 0.1;

    public const string Format = "0.0#";

    private readonly Func<WardrobeEditViewModel, double> _get;
    private readonly ILocalizer _l;
    private readonly Action<WardrobeEditViewModel, double> _set;
    private readonly Func<IReadOnlyList<WardrobeEditViewModel>> _targets;
    [Reactive] private bool _isMixed;
    [Reactive] private string _rangeText = "";
    private decimal? _value;

    public WardrobeNumberFieldViewModel(Func<IReadOnlyList<WardrobeEditViewModel>> targets,
        Func<WardrobeEditViewModel, double> get, Action<WardrobeEditViewModel, double> set, ILocalizer l)
    {
        _targets = targets;
        _get = get;
        _set = set;
        _l = l;
        StepUpCommand = ReactiveCommand.Create(() => StepBy(Step));
        StepDownCommand = ReactiveCommand.Create(() => StepBy(-Step));
    }

    /// <summary>
    ///     The value every target shares, or null when they differ. A number ≥ 0 is written to every target; null (a
    ///     cleared field) or a negative number changes nothing and stays as typed until <see cref="EndEdit" />.
    /// </summary>
    public decimal? Value
    {
        get => _value;
        set
        {
            _value = value;
            if (value is { } v && v >= 0)
            {
                foreach (var target in _targets())
                {
                    _set(target, (double)v);
                }
            }
        }
    }

    public string Placeholder => IsMixed ? _l.T("Different values") : "";

    public ICommand StepUpCommand { get; }
    public ICommand StepDownCommand { get; }

    /// <summary>Adds <paramref name="delta" /> to each target's own value (also when they differ), never below 0.</summary>
    public void StepBy(double delta)
    {
        foreach (var target in _targets())
        {
            _set(target, Math.Max(0, Math.Round(_get(target) + delta, 2)));
        }

        Refresh(true);
    }

    /// <summary>Drops whatever was typed and shows the targets' value again (focus lost, selection changed).</summary>
    public void EndEdit()
    {
        Refresh(true);
    }

    /// <summary>
    ///     Recomputes the shared value and the range. Without <paramref name="force" /> the field is only updated when
    ///     the value really changed, so a number being typed is not reformatted under the cursor.
    /// </summary>
    public void Refresh(bool force = false)
    {
        var values = _targets().Select(_get).ToList();
        var mixed = values.Distinct().Count() > 1;
        decimal? shared = values.Count > 0 && !mixed ? (decimal)values[0] : null;

        IsMixed = mixed;
        RangeText = mixed
            // translators: under a number field when the selected items differ, e.g. "from 0.5 to 1.0"
            ? _l.T("from {0} to {1}", values.Min().ToString(Format, CultureInfo.InvariantCulture),
                values.Max().ToString(Format, CultureInfo.InvariantCulture))
            : "";
        this.RaisePropertyChanged(nameof(Placeholder));

        if (force || shared != _value)
        {
            _value = shared;
            this.RaisePropertyChanged(nameof(Value));
        }
    }
}
