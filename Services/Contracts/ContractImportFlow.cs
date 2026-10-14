using ChastiMaid.Localization;
using ChastiMaid.ViewModels;

namespace ChastiMaid.Services.Contracts;

/// <summary>How a contract import ended.</summary>
public enum ContractImportOutcomeKind
{
    Cancelled,
    Failed,
    Applied,
}

/// <summary>Result of <see cref="ContractImportFlow.RunAsync" />; <paramref name="Message" /> is localized, ready to show.</summary>
public record ContractImportOutcome(ContractImportOutcomeKind Kind, string Message = "");

/// <summary>
///     The whole "load a contract file" interaction — pick, read, validate, confirm the replacement, apply,
///     report — shared by Settings and the contract screen so both behave identically.
/// </summary>
public class ContractImportFlow(GameContractService service, ILocalizer L)
{
    private const int MaxSubjectsPerLine = 6;

    /// <summary>
    ///     Runs the flow. <paramref name="confirm" /> shows the replacement dialog and returns whether the user
    ///     accepted; nothing is applied before that. <paramref name="hasUnsavedChanges" /> adds a warning when
    ///     the caller will discard in-progress edits.
    /// </summary>
    public async Task<ContractImportOutcome> RunAsync(Func<ConfirmDialogViewModel, Task<bool>> confirm,
        bool hasUnsavedChanges = false)
    {
        var path = await ContractFileDialogs.PickOpenAsync(L.T("Import contract"));
        if (path is null)
        {
            return new ContractImportOutcome(ContractImportOutcomeKind.Cancelled);
        }

        string json;
        try
        {
            json = await File.ReadAllTextAsync(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return Failed(L.T("The file could not be read: {0}", e.Message));
        }

        var result = service.Validate(json);
        if (result.Contract is not { } contract)
        {
            return Failed(L.T("Nothing was changed.") + " " +
                          string.Join(" ", result.Issues.Where(i => i.IsFatal).Select(DescribeFatal)));
        }

        var report = Describe(result.Issues);
        var message = L.T("Loading this contract replaces your current settings, wardrobe, outfit rules, tasks, punishments and permissions. Your name, language, camera and autosave settings are kept.");
        if (report.Length > 0)
        {
            message += "\n\n" + L.T("While reading the file, some entries had to be corrected:") + "\n" + report;
        }

        if (hasUnsavedChanges)
        {
            message += "\n\n" + L.T("Unsaved changes in Settings will be lost.");
        }

        var dialog = new ConfirmDialogViewModel(L.T("Replace the current terms?"), message,
            L.T("Replace"), L.T("Cancel"));
        if (!await confirm(dialog))
        {
            return new ContractImportOutcome(ContractImportOutcomeKind.Cancelled);
        }

        service.Apply(result);

        var status = L.T("Contract loaded: {0} wardrobe items, {1} outfit rules and {2} tasks.",
            contract.Wardrobe!.Count, contract.OutfitRules!.Count, contract.Tasks!.Count);
        return new ContractImportOutcome(ContractImportOutcomeKind.Applied,
            report.Length > 0 ? status + "\n" + report : status);
    }

    private ContractImportOutcome Failed(string message) =>
        new(ContractImportOutcomeKind.Failed, message);

    private string DescribeFatal(ContractIssue issue) => issue.Kind switch
    {
        ContractIssueKind.UnsupportedSchemaVersion =>
            L.T("This contract uses an unsupported version ({0}).", issue.Subject),
        ContractIssueKind.MissingSection => L.T("The contract is missing a section: {0}.", issue.Subject),
        ContractIssueKind.EmptyWardrobe => L.T("The contract contains no usable wardrobe items."),
        _ => L.T("The file is not a valid contract."),
    };

    /// <summary>One localized line per kind of adjustment (clamped, swapped, skipped); empty when nothing was touched.</summary>
    private string Describe(IReadOnlyList<ContractIssue> issues)
    {
        var lines = new List<string>();
        if (Subjects(issues.Where(i => i.Kind == ContractIssueKind.ValueClamped)) is { } clamped)
        {
            lines.Add(L.T("Adjusted to the allowed range: {0}.", clamped));
        }

        if (Subjects(issues.Where(i => i.Kind == ContractIssueKind.RangeSwapped)) is { } swapped)
        {
            lines.Add(L.T("Minimum and maximum swapped: {0}.", swapped));
        }

        if (Subjects(issues.Where(i => i.Kind is ContractIssueKind.WardrobeItemDiscarded
                or ContractIssueKind.OutfitRuleDiscarded or ContractIssueKind.TaskDiscarded
                or ContractIssueKind.ConfigEntryDiscarded)) is { } discarded)
        {
            lines.Add(L.T("Skipped: {0}.", discarded));
        }

        return string.Join("\n", lines);
    }

    /// <summary>The issues' subjects as a short comma-separated list; null when there are none.</summary>
    private string? Subjects(IEnumerable<ContractIssue> issues)
    {
        var subjects = issues.Select(i => i.Subject).Distinct().ToList();
        if (subjects.Count == 0)
        {
            return null;
        }

        var shown = string.Join(", ", subjects.Take(MaxSubjectsPerLine));
        return subjects.Count > MaxSubjectsPerLine
            ? shown + L.T(" (+{0} more)", subjects.Count - MaxSubjectsPerLine)
            : shown;
    }
}
