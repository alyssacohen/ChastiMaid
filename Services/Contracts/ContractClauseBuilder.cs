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
public static class ContractClauseBuilder
{
    /// <summary>Builds the clauses in reading order, numbered from 1. The variable-severity clause is always last.</summary>
    public static IReadOnlyList<ContractClause> Build(GameContract contract)
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

        List<(string TitleKey, List<string> Paragraphs)> clauses =
        [
            ("ContractDebtTitle",
            [
                F("ContractDebtRange", Money(s.MinDebt), Money(s.MaxDebt)),
                F("ContractInterest", Num(s.DailyInterestRate * 100)),
            ]),
            ("ContractWageTitle",
            [
                F("ContractWage", Money(s.IdleWageAmount), Span(s.IdleWageIntervalMinutes * 60)),
            ]),
            ("ContractWardrobeTitle", WardrobeParagraphs(wardrobe, rules, s, plugAndPlay, diaperPlay, chastity)),
            ("ContractTasksTitle", TaskParagraphs(tasks)),
            ("ContractCheckinsTitle", CheckinParagraphs(s)),
            ("ContractPunishmentsTitle", PunishmentParagraphs(s, presets)),
            ("ContractPermissionsTitle", PermissionParagraphs(permissions)),
            ("ContractOtherTitle",
            [
                Resources.Get(s.CornertimeVoiceEnabled ? "ContractCornertimeVoiceOn" : "ContractCornertimeVoiceOff"),
                s.ParseAppellatives().Count is var n and > 0
                    ? F("ContractAppellatives", n)
                    : Resources.Get("ContractAppellativesNone"),
            ]),

            // TODO(variable-severity): informational only. The future "behavior" parameter (task frequencies
            // respected, pending punishments, refused garments) will scale punishment severity; no calculation
            // exists yet and none may be implied here. See docs/design/new-game-contract.md.
            ("ContractSeverityTitle", [Resources.Get("ContractSeverity")]),
        ];

        return clauses.Select((c, i) => new ContractClause(i + 1, Resources.Get(c.TitleKey), c.Paragraphs)).ToList();
    }

    private static List<string> WardrobeParagraphs(List<ClothingItem> wardrobe, List<OutfitRule> rules, GameSettings s,
        List<PlugAndPlayItemConfig> plugAndPlay, List<DiaperPlayItemConfig> diaperPlay,
        List<ChastityDeviceItemConfig> chastity)
    {
        var perLayer = WardrobeLayer.All
            .Select(l => (l.DisplayName, Count: wardrobe.Count(i => i.Layers.Contains(l.Order))))
            .Where(l => l.Count > 0)
            .Select(l => $"{l.DisplayName} {l.Count}");

        var paragraphs = new List<string>
        {
            F("ContractWardrobeCount", wardrobe.Count, string.Join(", ", perLayer)),
            rules.Count == 0
                ? Resources.Get("ContractRulesNone")
                : F("ContractRules", rules.Count, rules.Count(r => r.Relation == OutfitRelation.Requires),
                    rules.Count(r => r.Relation == OutfitRelation.Incompatible)),
            s.PlugAndPlayEnabled && plugAndPlay.Count > 0
                ? F("ContractPlugAndPlayOn", plugAndPlay.Count,
                    Span(plugAndPlay.Min(c => c.MinDurationMinutes) * 60),
                    Span(plugAndPlay.Max(c => c.MaxDurationMinutes) * 60))
                : Resources.Get("ContractPlugAndPlayOff"),
            s.DiaperPlayEnabled && diaperPlay.Count > 0
                ? F("ContractDiaperPlayOn", diaperPlay.Count)
                : Resources.Get("ContractDiaperPlayOff"),
        };

        if (chastity.Count > 0)
        {
            paragraphs.Add(F("ContractChastity", chastity.Count));
        }

        return paragraphs;
    }

    private static List<string> TaskParagraphs(List<Chore> tasks) =>
    [
        tasks.Count == 0
            ? Resources.Get("ContractTasksNone")
            : F("ContractTasks", tasks.Count, Money(tasks.Sum(t => t.Value))),
    ];

    private static List<string> CheckinParagraphs(GameSettings s) =>
    [
        F("ContractSummons", Span(s.PopupMinIntervalSeconds), Span(s.PopupMaxIntervalSeconds),
            Span(s.PopupTimeoutSeconds)),
        s.HydrationEnabled
            ? F("ContractHydrationOn", Span(s.HydrationMinIntervalMinutes * 60))
            : Resources.Get("ContractHydrationOff"),
        s.CurtsyEnabled
            ? F("ContractCurtsyOn", Span(s.CurtsyVideoDurationSeconds))
            : Resources.Get("ContractCurtsyOff"),
        s.QuoteReadingEnabled
            ? F("ContractQuoteOn", Span(s.QuoteListenTimeoutSeconds), s.QuoteMatchMinSimilarityPercent)
            : Resources.Get("ContractQuoteOff"),
        s.KneelEnabled
            ? F("ContractKneelOn", Span(s.KneelMinDurationSeconds), Span(s.KneelMaxDurationSeconds),
                Span(s.KneelPhotoMinIntervalSeconds), Span(s.KneelPhotoMaxIntervalSeconds))
            : Resources.Get("ContractKneelOff"),
        F("ContractBusyCapture", Span(s.BusyCaptureMinIntervalSeconds), Span(s.BusyCaptureMaxIntervalSeconds)),
    ];

    private static List<string> PunishmentParagraphs(GameSettings s, List<ForcedOutfitPreset> presets)
    {
        var paragraphs = new List<string> { F("ContractFine", Money(s.FineSeverityValue)) };

        if (s.CornertimeEnabled)
        {
            paragraphs.Add(F("ContractCornertimeOn", Span(s.CornertimeSeverityValue),
                Span(s.CornertimeMinRequiredSeconds), Span(s.CornertimeMaxRequiredSeconds),
                Span(s.CornertimeMaxOverrunSeconds)));
            paragraphs.Add(F("ContractCornertimeMovement", s.CornertimeMovementSensitivityPercent,
                Span(s.CornertimeMovementPenaltyMinSeconds), Span(s.CornertimeMovementPenaltyMaxSeconds),
                Span(s.CornertimeMovementCooldownMinSeconds), Span(s.CornertimeMovementCooldownMaxSeconds)));
        }
        else
        {
            paragraphs.Add(Resources.Get("ContractCornertimeOff"));
        }

        if (s.WritingLinesEnabled)
        {
            paragraphs.Add(F("ContractWritingLinesOn", Num(s.WritingLinesSeverityValue), s.WritingLinesMinLines,
                s.WritingLinesMaxLines, s.WritingLinesPenaltyLinesPerMistake));
            if (s.WritingLinesNoIdleEnabled)
            {
                paragraphs.Add(F("ContractWritingNoIdle", Span(s.WritingLinesNoIdleTimeoutSeconds),
                    s.WritingLinesNoIdleLinesToAdd));
            }

            if (s.WritingLinesInterruptionsEnabled)
            {
                paragraphs.Add(F("ContractWritingInterruptions",
                    Span(s.WritingLinesInterruptionMinIntervalSeconds),
                    Span(s.WritingLinesInterruptionMaxIntervalSeconds)));
            }
        }
        else
        {
            paragraphs.Add(Resources.Get("ContractWritingLinesOff"));
        }

        var enabledPresets = presets.Count(p => p.IsEnabled);
        paragraphs.Add(s.ForcedOutfitEnabled && enabledPresets > 0
            ? F("ContractForcedOutfitOn", Span(s.ForcedOutfitMaxHours * 3600), enabledPresets)
            : Resources.Get("ContractForcedOutfitOff"));

        return paragraphs;
    }

    private static List<string> PermissionParagraphs(List<PermissionTypeConfig> permissions)
    {
        if (permissions.Count == 0)
        {
            return [Resources.Get("ContractPermissionsNone")];
        }

        return
        [
            Resources.Get("ContractPermissionsIntro"),
            ..permissions.Select(p => F("ContractPermissionLine", Resources.Get($"PermissionKind{p.Kind}"),
                p.BaseGrantPercent, Span(p.DurationMinutes * 60), Span(p.CooldownMinutes * 60))),
        ];
    }

    private static string F(string key, params object[] args) => string.Format(Resources.Get(key), args);

    private static string Money(decimal value) => CurrencyConverter.Format(value);

    private static string Num(double value) => value.ToString("0.##", CultureInfo.CurrentCulture);

    /// <summary>A duration in the largest whole unit ("90 seconds", "5 minutes", "2 hours").</summary>
    private static string Span(double seconds)
    {
        var s = (int)Math.Round(seconds);
        return s switch
        {
            >= 3600 when s % 3600 == 0 => Count("ContractHour", s / 3600),
            >= 60 when s % 60 == 0 => Count("ContractMinute", s / 60),
            _ => Count("ContractSecond", s),
        };
    }

    private static string Count(string key, int n) => F(n == 1 ? key + "One" : key + "Many", n);
}
