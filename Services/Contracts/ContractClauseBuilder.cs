using System.Globalization;
using ChastiMaid.Converters;
using ChastiMaid.Localization;
using ChastiMaid.Models;

namespace ChastiMaid.Services.Contracts;

/// <summary>One numbered clause of the contract as shown on screen.</summary>
public record ContractClause(int Number, string Title, IReadOnlyList<string> Paragraphs)
{
    /// <summary>The numbered heading, e.g. "3. The Wardrobe".</summary>
    public string Heading => $"{Number}. {Title}";
}

/// <summary>
///     Turns a <see cref="GameContract" /> into its numbered clauses. Pure: the same contract always yields the
///     same text, so the screen only has to re-run it when the contract changes. Numbers are interpolated;
///     appellatives never are (only their count is stated), so Italian sentences stay grammatical.
/// </summary>
public sealed class ContractClauseBuilder(ILocalizer L)
{
    /// <summary>Builds the clauses in reading order, numbered from 1. The variable-severity clause is always last.</summary>
    public IReadOnlyList<ContractClause> Build(GameContract contract)
    {
        var s = contract.Settings ?? new GameSettings();
        var wardrobe = contract.Wardrobe ?? [];
        var rules = contract.OutfitRules ?? [];
        var tasks = contract.Tasks ?? [];
        var presets = contract.ForcedOutfitPresets ?? [];
        var plugAndPlay = contract.PlugAndPlayItems ?? [];
        var diaperPlay = contract.DiaperPlayItems ?? [];
        var chastity = contract.ChastityDeviceItems ?? [];
        var permissions = contract.PermissionTypes ?? [];
        var appellatives = s.ParseAppellatives().Count;

        List<(string Title, List<string> Paragraphs)> clauses =
        [
            (L.T("Forms of Address"),
                [L.T("The Maid shall address the Lady of the House as “Madame”, and by no other name.")]),
            (L.T("Debt and Interest"),
            [
                L.T("Upon acceptance, Madame shall determine the Maid's debt, at her own discretion, at a sum between {0} and {1}.",
                    Money(s.MinDebt), Money(s.MaxDebt)),
                L.T("Interest accrues daily at {0}%. Madame regrets that arithmetic is not open to negotiation.",
                    Num(s.DailyInterestRate * 100)),
            ]),
            (L.T("Wages"),
            [
                L.T("While the Maid is idle, Madame shall pay her {0} every {1}. “Idle” is, in this household, a generously loose term.",
                    Money(s.IdleWageAmount), Span(s.IdleWageIntervalMinutes * 60)),
            ]),
            (L.T("The Wardrobe"), WardrobeParagraphs(wardrobe, rules, s, plugAndPlay, diaperPlay, chastity)),
            (L.T("Standing Tasks"), TaskParagraphs(tasks)),
            (L.T("Summons and Check-ins"), CheckinParagraphs(s)),
            (L.T("Corrective Measures"), PunishmentParagraphs(s, presets)),
            (L.T("Requests for Leave"), PermissionParagraphs(permissions)),
            (L.T("Sundry Provisions"),
            [
                s.CornertimeVoiceEnabled
                    ? L.T("Madame shall narrate the Maid's Cornertime aloud.")
                    : L.T("The Maid's Cornertime shall be observed in silence."),
                appellatives > 0
                    ? L.N("Madame keeps {0} form of address in reserve for summoning the Maid.",
                        "Madame keeps {0} forms of address in reserve for summoning the Maid.", appellatives, appellatives)
                    : L.T("Madame will summon the Maid by name alone."),
            ]),

            // Fixed text: the mechanic lives in SeverityBalance and is deliberately not a setting, so the clause
            // names the factors loosely and promises no figure. See docs/design/new-game-contract.md.
            (L.T("Variable Severity"),
                [L.T("The severity of punishments is not fixed. It shall vary with the Maid's conduct: how faithfully the household's routines are kept, how many matters remain outstanding, how often Madame's choice of attire has been declined, and how much of the debt still stands. Madame does not disclose her methods. She merely observes.")]),
        ];

        return clauses.Select((c, i) => new ContractClause(i + 1, c.Title, c.Paragraphs)).ToList();
    }

    private List<string> WardrobeParagraphs(List<ClothingItem> wardrobe, List<OutfitRule> rules, GameSettings s,
        List<PlugAndPlayItemConfig> plugAndPlay, List<DiaperPlayItemConfig> diaperPlay,
        List<ChastityDeviceItemConfig> chastity)
    {
        var perLayer = WardrobeLayer.All
            .Select(l => (DisplayName: L.T(l.DisplayName), Count: wardrobe.Count(i => i.Layers.Contains(l.Order))))
            .Where(l => l.Count > 0)
            .Select(l => $"{l.DisplayName} {l.Count}");

        var paragraphs = new List<string>
        {
            L.N("The wardrobe comprises {0} garment: {1}.", "The wardrobe comprises {0} garments: {1}.",
                wardrobe.Count, wardrobe.Count, string.Join(", ", perLayer)),
            rules.Count == 0
                ? L.T("No rules restrict how garments may be combined. Madame has faith in the Maid's taste — for the present.")
                : L.N("{0} rule governs which garments may be worn together: {1} requiring, {2} forbidding.",
                    "{0} rules govern which garments may be worn together: {1} requiring, {2} forbidding.",
                    rules.Count, rules.Count, rules.Count(r => r.Relation == OutfitRelation.Requires),
                    rules.Count(r => r.Relation == OutfitRelation.Incompatible)),
            s.PlugAndPlayEnabled && plugAndPlay.Count > 0
                ? L.N("Plug and Play is in force for {0} garment, to be worn for between {1} and {2}.",
                    "Plug and Play is in force for {0} garments, each to be worn for between {1} and {2}.",
                    plugAndPlay.Count, plugAndPlay.Count,
                    Span(plugAndPlay.Min(c => c.MinDurationMinutes) * 60),
                    Span(plugAndPlay.Max(c => c.MaxDurationMinutes) * 60))
                : L.T("Plug and Play is not in force."),
            s.DiaperPlayEnabled && diaperPlay.Count > 0
                ? L.N("Diaper Play is in force for {0} garment. While it is worn, the interval between hydration reminders is halved.",
                    "Diaper Play is in force for {0} garments. While one is worn, the interval between hydration reminders is halved.",
                    diaperPlay.Count, diaperPlay.Count)
                : L.T("Diaper Play is not in force."),
        };

        if (chastity.Count > 0)
        {
            paragraphs.Add(L.N("{0} item is designated as a chastity device.",
                "{0} items are designated as chastity devices.", chastity.Count, chastity.Count));
        }

        return paragraphs;
    }

    private List<string> TaskParagraphs(List<Chore> tasks) =>
    [
        tasks.Count == 0
            ? L.T("No standing tasks are set. Madame finds this improbable, and suspects an oversight.")
            : L.N("Madame sets {0} standing task, paid upon completion. Were the Maid to complete it, they would earn {1}.",
                "Madame sets {0} standing tasks, each paid upon completion. Were the Maid to complete every one, they would earn {1}.",
                tasks.Count, tasks.Count, Money(tasks.Sum(t => t.Value))),
    ];

    private List<string> CheckinParagraphs(GameSettings s) =>
    [
        L.T("Madame may summon the Maid at intervals of between {0} and {1}; the Maid shall answer within {2}.",
            Span(s.PopupMinIntervalSeconds), Span(s.PopupMaxIntervalSeconds), Span(s.PopupTimeoutSeconds)),
        s.HydrationEnabled
            ? L.T("Hydration reminders shall come no more often than once every {0}.",
                Span(s.HydrationMinIntervalMinutes * 60))
            : L.T("The Maid is excused from hydration reminders."),
        s.CurtsyEnabled
            ? L.T("The Maid may be required to curtsy, recorded on video for {0}.", Span(s.CurtsyVideoDurationSeconds))
            : L.T("No curtsy will be required of the Maid."),
        s.QuoteReadingEnabled
            ? L.T("The Maid may be given a motivational quotation to read aloud, with {0} in which to do so. Fidelity to the text of at least {1}% will satisfy Madame.",
                Span(s.QuoteListenTimeoutSeconds), s.QuoteMatchMinSimilarityPercent)
            : L.T("The Maid shall not be asked to recite."),
        s.KneelEnabled
            ? L.T("The Maid may be asked to kneel for between {0} and {1}, photographed at intervals of between {2} and {3}.",
                Span(s.KneelMinDurationSeconds), Span(s.KneelMaxDurationSeconds),
                Span(s.KneelPhotoMinIntervalSeconds), Span(s.KneelPhotoMaxIntervalSeconds))
            : L.T("The Maid shall not be asked to kneel."),
        L.T("While the Maid is occupied, photographs shall be taken at intervals of between {0} and {1}.",
            Span(s.BusyCaptureMinIntervalSeconds), Span(s.BusyCaptureMaxIntervalSeconds)),
    ];

    private List<string> PunishmentParagraphs(GameSettings s, List<ForcedOutfitPreset> presets)
    {
        var paragraphs = new List<string>
        {
            L.T("Every infraction carries a fine of {0} per point of severity.", Money(s.FineSeverityValue)),
        };

        if (s.CornertimeEnabled)
        {
            paragraphs.Add(L.T("Cornertime: the Maid shall stand perfectly still for {0} per point of severity — no less than {1}, no more than {2} — with {3} of grace beyond that.",
                Span(s.CornertimeSeverityValue), Span(s.CornertimeMinRequiredSeconds),
                Span(s.CornertimeMaxRequiredSeconds), Span(s.CornertimeMaxOverrunSeconds)));
            paragraphs.Add(L.T("Movement is detected at a sensitivity of {0}% and adds between {1} and {2} to the sentence, with a pause of between {3} and {4} before another may be counted.",
                s.CornertimeMovementSensitivityPercent,
                Span(s.CornertimeMovementPenaltyMinSeconds), Span(s.CornertimeMovementPenaltyMaxSeconds),
                Span(s.CornertimeMovementCooldownMinSeconds), Span(s.CornertimeMovementCooldownMaxSeconds)));
        }
        else
        {
            paragraphs.Add(L.T("Cornertime is not in force."));
        }

        if (s.WritingLinesEnabled)
        {
            paragraphs.Add(L.T("Lines: the Maid shall write {0} lines per point of severity — at least {1}, at most {2} — and {3} more for each mistake.",
                Num(s.WritingLinesSeverityValue), s.WritingLinesMinLines, s.WritingLinesMaxLines,
                s.WritingLinesPenaltyLinesPerMistake));
            if (s.WritingLinesNoIdleEnabled)
            {
                paragraphs.Add(L.N("Should the Maid leave the pen idle for {0}, {1} line shall be added.",
                    "Should the Maid leave the pen idle for {0}, {1} lines shall be added.",
                    s.WritingLinesNoIdleLinesToAdd, Span(s.WritingLinesNoIdleTimeoutSeconds),
                    s.WritingLinesNoIdleLinesToAdd));
            }

            if (s.WritingLinesInterruptionsEnabled)
            {
                paragraphs.Add(L.T("Madame may interrupt the Maid's writing at intervals of between {0} and {1}.",
                    Span(s.WritingLinesInterruptionMinIntervalSeconds),
                    Span(s.WritingLinesInterruptionMaxIntervalSeconds)));
            }
        }
        else
        {
            paragraphs.Add(L.T("Lines are not in force."));
        }

        var enabledPresets = presets.Count(p => p.IsEnabled);
        paragraphs.Add(s.ForcedOutfitEnabled && enabledPresets > 0
            ? L.N("Forced outfits may be imposed for as long as {0}, drawn from the {1} outfit kept ready for the purpose.",
                "Forced outfits may be imposed for as long as {0}, drawn from {1} outfits kept ready for the purpose.",
                enabledPresets, Span(s.ForcedOutfitMaxHours * 3600), enabledPresets)
            : L.T("Forced outfits are not in force."));

        return paragraphs;
    }

    private List<string> PermissionParagraphs(List<PermissionTypeConfig> permissions)
    {
        if (permissions.Count == 0)
        {
            return [L.T("No provision has been made for leave of any kind. The Maid will plan accordingly.")];
        }

        return
        [
            L.T("Leave to attend to certain needs is granted at Madame's discretion:"),
            ..permissions.Select(p => L.T("{0}: granted {1}% of the time, for {2}, and not again for {3}.",
                PermissionKindDisplay.NameOf(p.Kind, L), p.BaseGrantPercent,
                Span(p.DurationMinutes * 60), Span(p.CooldownMinutes * 60))),
        ];
    }

    private static string Money(decimal value) => CurrencyConverter.Format(value);

    private static string Num(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    /// <summary>A duration in the largest whole unit ("90 seconds", "5 minutes", "2 hours").</summary>
    private string Span(double seconds)
    {
        var s = (int)Math.Round(seconds);
        return s switch
        {
            >= 3600 when s % 3600 == 0 => L.N("{0} hour", "{0} hours", s / 3600, s / 3600),
            >= 60 when s % 60 == 0 => L.N("{0} minute", "{0} minutes", s / 60, s / 60),
            _ => L.N("{0} second", "{0} seconds", s, s),
        };
    }
}
