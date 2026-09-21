using ChastiMaid.Models;

namespace ChastiMaid.Services.Contracts;

/// <summary>What a validation problem is about; the UI turns each kind into localized text.</summary>
public enum ContractIssueKind
{
    // Fatal: the whole file is rejected and nothing is changed.
    InvalidFile,
    UnsupportedSchemaVersion,
    MissingSection,
    EmptyWardrobe,

    // Reported: the file is still usable after the fix described.
    ValueClamped,
    RangeSwapped,
    WardrobeItemDiscarded,
    OutfitRuleDiscarded,
    TaskDiscarded,
    ConfigEntryDiscarded,
}

/// <summary>One validation problem. <paramref name="Subject" /> is the setting, item, tag pair or section concerned.</summary>
public record ContractIssue(ContractIssueKind Kind, string Subject)
{
    /// <summary>True when the issue makes the whole file unusable.</summary>
    public bool IsFatal => Kind <= ContractIssueKind.EmptyWardrobe;
}

/// <summary>
///     Outcome of validating a contract file: the normalized contract (null when a fatal issue was found)
///     and every problem found, fatal or not.
/// </summary>
public record ContractValidationResult(GameContract? Contract, IReadOnlyList<ContractIssue> Issues)
{
    /// <summary>True when the contract can be applied.</summary>
    public bool IsValid => Contract is not null;
}
