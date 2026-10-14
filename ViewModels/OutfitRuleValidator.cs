using ChastiMaid.Localization;
using ChastiMaid.Models;
using ChastiMaid.Services;

namespace ChastiMaid.ViewModels;

public record RuleValidationOutcome(bool CanFinalize, bool IsViolation, bool HasWarning, string StatusMessage);

/// <summary>
///     Applies tag-based Requires/Incompatible rules to a chip grid's selection: marks unmet-Requires targets
///     as <see cref="ChipState.Required" />, disables chips that would violate an Incompatible pair, and
///     builds the status message. Shared by <see cref="ChangeOutfitViewModel" /> and
///     <see cref="ForcedOutfitPresetEditorViewModel" />. Must run after
///     <see cref="OutfitChipComposer.ApplyStructuralReset" />, since it only touches chips still
///     in <see cref="ChipState.Normal" />.
/// </summary>
public class OutfitRuleValidator(List<OutfitRule> allRules, ILocalizer l)
{
    private readonly OutfitRuleEngine _engine = new(allRules);

    public RuleValidationOutcome Apply(IEnumerable<ChipViewModel> allChipsSource)
    {
        var allChips = allChipsSource.ToList();
        var selectedItems = allChips.Where(c => c.State is ChipState.Selected).Select(c => c.Item).ToList();
        var violations = new List<string>();
        var infos = new List<string>();
        var canFinalize = true;

        // Incompatible (bidirectional): a rule fires if selected items cover both its tags.
        foreach (var rule in allRules.Where(r => r.Relation == OutfitRelation.Incompatible))
        {
            var sourceSelected = selectedItems.Where(i => _engine.HasTag(i, rule.SourceTag)).ToList();
            var relatedSelected = selectedItems.Where(i => _engine.HasTag(i, rule.RelatedTag)).ToList();

            if (sourceSelected.Count > 0 && relatedSelected.Count > 0)
            {
                var sourceNames = string.Join(", ", sourceSelected.Select(i => i.Name).Distinct());
                var relatedNames = string.Join(", ", relatedSelected.Select(i => i.Name).Distinct());
                // translators: {0} and {1} are lists of clothing item names
                violations.Add(l.T("{0} and {1} cannot be worn together.", sourceNames, relatedNames));
            }
            else if (sourceSelected.Count > 0)
            {
                foreach (var chip in allChips.Where(c =>
                             c.State is ChipState.Normal && _engine.HasTag(c.Item, rule.RelatedTag)))
                {
                    chip.State = ChipState.Disabled;
                }
            }
            else if (relatedSelected.Count > 0)
            {
                foreach (var chip in allChips.Where(c =>
                             c.State is ChipState.Normal && _engine.HasTag(c.Item, rule.SourceTag)))
                {
                    chip.State = ChipState.Disabled;
                }
            }
        }

        // Requires (directional: source tag requires one of the related tags), grouped by distinct source tag.
        var processedSourceTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rule in allRules.Where(r => r.Relation == OutfitRelation.Requires))
        {
            var sourceItems = selectedItems.Where(i => _engine.HasTag(i, rule.SourceTag)).ToList();
            if (sourceItems.Count == 0 || !processedSourceTags.Add(rule.SourceTag))
            {
                continue;
            }

            var requiredTags = allRules
                .Where(r => r.Relation == OutfitRelation.Requires && OutfitRuleEngine.TagsEqual(r.SourceTag, rule.SourceTag))
                .Select(r => r.RelatedTag)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var satisfying = selectedItems.Where(i => requiredTags.Any(t => _engine.HasTag(i, t))).ToList();
            var sourceNames = string.Join(", ", sourceItems.Select(i => i.Name).Distinct());

            if (satisfying.Count > 0)
            {
                var satisfierNames = string.Join(", ", satisfying.Select(i => i.Name).Distinct());
                // translators: {0} and {2} are lists of clothing item names, {1} a list of tags
                infos.Add(l.T("{0} requires one of: {1}. {2} selected — requirement satisfied.",
                    sourceNames, string.Join(", ", requiredTags), satisfierNames));
            }
            else
            {
                foreach (var chip in allChips.Where(c =>
                             c.State is ChipState.Normal && requiredTags.Any(t => _engine.HasTag(c.Item, t))))
                {
                    chip.State = ChipState.Required;
                }

                // translators: {0} is a list of clothing item names, {1} a list of tags
                violations.Add(l.T("{0} requires one of: {1}.", sourceNames, string.Join(", ", requiredTags)));
                canFinalize = false;
            }
        }

        return new RuleValidationOutcome(
            canFinalize,
            violations.Count > 0,
            violations.Count > 0 || infos.Count > 0,
            string.Join(" ", violations.Concat(infos)));
    }
}
