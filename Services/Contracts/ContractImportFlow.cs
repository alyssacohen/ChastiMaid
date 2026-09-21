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
public class ContractImportFlow(GameContractService service)
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
        var path = await ContractFileDialogs.PickOpenAsync(Resources.Get("ContractImportDialogTitle"));
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
            return Failed(string.Format(Resources.Get("ContractReadFailedStatus"), e.Message));
        }

        var result = service.Validate(json);
        if (result.Contract is not { } contract)
        {
            return Failed(Resources.Get("ContractRejectedStatus") + " " +
                          string.Join(" ", result.Issues.Where(i => i.IsFatal).Select(DescribeFatal)));
        }

        var report = Describe(result.Issues);
        var message = Resources.Get("ContractReplaceMessage");
        if (report.Length > 0)
        {
            message += "\n\n" + Resources.Get("ContractReplaceReportHeader") + "\n" + report;
        }

        if (hasUnsavedChanges)
        {
            message += "\n\n" + Resources.Get("ContractReplaceUnsavedNote");
        }

        var dialog = new ConfirmDialogViewModel(Resources.Get("ContractReplaceTitle"), message,
            Resources.Get("ContractReplaceConfirm"), Resources.Get("Cancel"));
        if (!await confirm(dialog))
        {
            return new ContractImportOutcome(ContractImportOutcomeKind.Cancelled);
        }

        service.Apply(result);

        var status = string.Format(Resources.Get("ContractLoadedStatus"),
            contract.Wardrobe!.Count, contract.OutfitRules!.Count, contract.Tasks!.Count);
        return new ContractImportOutcome(ContractImportOutcomeKind.Applied,
            report.Length > 0 ? status + "\n" + report : status);
    }

    private static ContractImportOutcome Failed(string message) =>
        new(ContractImportOutcomeKind.Failed, message);

    private static string DescribeFatal(ContractIssue issue) => issue.Kind switch
    {
        ContractIssueKind.UnsupportedSchemaVersion =>
            string.Format(Resources.Get("ContractFatalUnsupportedVersion"), issue.Subject),
        ContractIssueKind.MissingSection => string.Format(Resources.Get("ContractFatalMissingSection"), issue.Subject),
        ContractIssueKind.EmptyWardrobe => Resources.Get("ContractFatalEmptyWardrobe"),
        _ => Resources.Get("ContractFatalInvalidFile"),
    };

    /// <summary>One localized line per kind of adjustment (clamped, swapped, skipped); empty when nothing was touched.</summary>
    private static string Describe(IReadOnlyList<ContractIssue> issues)
    {
        var lines = new List<string>();
        AddLine(lines, "ContractReportClamped", issues.Where(i => i.Kind == ContractIssueKind.ValueClamped));
        AddLine(lines, "ContractReportSwapped", issues.Where(i => i.Kind == ContractIssueKind.RangeSwapped));
        AddLine(lines, "ContractReportDiscarded", issues.Where(i => i.Kind is ContractIssueKind.WardrobeItemDiscarded
            or ContractIssueKind.OutfitRuleDiscarded or ContractIssueKind.TaskDiscarded
            or ContractIssueKind.ConfigEntryDiscarded));
        return string.Join("\n", lines);
    }

    private static void AddLine(List<string> lines, string key, IEnumerable<ContractIssue> issues)
    {
        var subjects = issues.Select(i => i.Subject).Distinct().ToList();
        if (subjects.Count == 0)
        {
            return;
        }

        var shown = string.Join(", ", subjects.Take(MaxSubjectsPerLine));
        if (subjects.Count > MaxSubjectsPerLine)
        {
            shown += string.Format(Resources.Get("ContractReportMore"), subjects.Count - MaxSubjectsPerLine);
        }

        lines.Add(string.Format(Resources.Get(key), shown));
    }
}
