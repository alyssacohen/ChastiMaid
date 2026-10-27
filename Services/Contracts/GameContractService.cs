using System.Globalization;
using System.Numerics;
using System.Reflection;
using System.Text.Json;
using ChastiMaid.Models;
using ChastiMaid.Repositories;
using ChastiMaid.Services.Content;

namespace ChastiMaid.Services.Contracts;

/// <summary>
///     The single place that builds, serializes, validates and applies a <see cref="GameContract" />.
///     Applying replaces the profile's settings and every collection a save snapshots; machine-specific
///     settings are never touched.
/// </summary>
public class GameContractService(ProfileRepository profile)
{
    private readonly record struct Range(string Property, double Min, double Max);

    // Bounds mirror the Settings UI (there is no validation in the model). Fields the UI leaves unbounded
    // above get a generous ceiling that only rejects absurd values.
    // ponytail: duplicates the UI limits; upgrade path is making the Settings views read this table.
    private static readonly Range[] Ranges =
    [
        new(nameof(GameSettings.MinDebt), 0, 1_000_000),
        new(nameof(GameSettings.MaxDebt), 0, 1_000_000),
        new(nameof(GameSettings.DailyInterestRate), 0.005, 0.10),
        new(nameof(GameSettings.IdleWageAmount), 0, 100_000),
        new(nameof(GameSettings.IdleWageIntervalMinutes), 1, 120),
        new(nameof(GameSettings.PopupMinIntervalSeconds), 10, 86_400),
        new(nameof(GameSettings.PopupMaxIntervalSeconds), 10, 86_400),
        new(nameof(GameSettings.PopupTimeoutSeconds), 5, 60),
        new(nameof(GameSettings.HydrationMinIntervalMinutes), 1, 1_440),
        new(nameof(GameSettings.CurtsyVideoDurationSeconds), 2, 30),
        new(nameof(GameSettings.QuoteListenTimeoutSeconds), 5, 120),
        new(nameof(GameSettings.QuoteMatchMinSimilarityPercent), 1, 100),
        new(nameof(GameSettings.BusyCaptureMinIntervalSeconds), 1, 3_600),
        new(nameof(GameSettings.BusyCaptureMaxIntervalSeconds), 1, 3_600),
        new(nameof(GameSettings.BusyCaptureVideoMinSeconds), 1, 60),
        new(nameof(GameSettings.BusyCaptureVideoMaxSeconds), 1, 60),
        new(nameof(GameSettings.KneelMinDurationSeconds), 1, 86_400),
        new(nameof(GameSettings.KneelMaxDurationSeconds), 1, 86_400),
        new(nameof(GameSettings.KneelPhotoMinIntervalSeconds), 1, 3_600),
        new(nameof(GameSettings.KneelPhotoMaxIntervalSeconds), 1, 3_600),
        new(nameof(GameSettings.FineSeverityValue), 0.5, 100_000),
        new(nameof(GameSettings.CornertimeSeverityValue), 1, 100_000),
        new(nameof(GameSettings.CornertimeMinRequiredSeconds), 10, 86_400),
        new(nameof(GameSettings.CornertimeMaxOverrunSeconds), 10, 86_400),
        new(nameof(GameSettings.CornertimeMovementCooldownMinSeconds), 1, 3_600),
        new(nameof(GameSettings.CornertimeMovementCooldownMaxSeconds), 1, 3_600),
        new(nameof(GameSettings.CornertimeMovementSensitivityPercent), 1, 100),
        new(nameof(GameSettings.CornertimeMovementPenaltyMinSeconds), 1, 3_600),
        new(nameof(GameSettings.CornertimeMovementPenaltyMaxSeconds), 1, 3_600),
        new(nameof(GameSettings.CornertimeMaxRequiredSeconds), 10, 86_400),
        new(nameof(GameSettings.WritingLinesSeverityValue), 1, 1_000),
        new(nameof(GameSettings.WritingLinesPenaltyLinesPerMistake), 1, 1_000),
        new(nameof(GameSettings.WritingLinesMinLines), 1, 10_000),
        new(nameof(GameSettings.WritingLinesMaxLines), 1, 10_000),
        new(nameof(GameSettings.WritingLinesNoIdleTimeoutSeconds), 1, 3_600),
        new(nameof(GameSettings.WritingLinesNoIdleLinesToAdd), 1, 1_000),
        new(nameof(GameSettings.WritingLinesInterruptionMinIntervalSeconds), 1, 3_600),
        new(nameof(GameSettings.WritingLinesInterruptionMaxIntervalSeconds), 1, 3_600),
        new(nameof(GameSettings.ForcedOutfitMaxHours), 0.5, 720),
    ];

    // (min, max) property pairs that must satisfy min <= max.
    private static readonly (string Min, string Max)[] Pairs =
    [
        (nameof(GameSettings.MinDebt), nameof(GameSettings.MaxDebt)),
        (nameof(GameSettings.PopupMinIntervalSeconds), nameof(GameSettings.PopupMaxIntervalSeconds)),
        (nameof(GameSettings.BusyCaptureMinIntervalSeconds), nameof(GameSettings.BusyCaptureMaxIntervalSeconds)),
        (nameof(GameSettings.BusyCaptureVideoMinSeconds), nameof(GameSettings.BusyCaptureVideoMaxSeconds)),
        (nameof(GameSettings.KneelMinDurationSeconds), nameof(GameSettings.KneelMaxDurationSeconds)),
        (nameof(GameSettings.KneelPhotoMinIntervalSeconds), nameof(GameSettings.KneelPhotoMaxIntervalSeconds)),
        (nameof(GameSettings.CornertimeMinRequiredSeconds), nameof(GameSettings.CornertimeMaxRequiredSeconds)),
        (nameof(GameSettings.CornertimeMovementCooldownMinSeconds),
            nameof(GameSettings.CornertimeMovementCooldownMaxSeconds)),
        (nameof(GameSettings.CornertimeMovementPenaltyMinSeconds),
            nameof(GameSettings.CornertimeMovementPenaltyMaxSeconds)),
        (nameof(GameSettings.WritingLinesMinLines), nameof(GameSettings.WritingLinesMaxLines)),
        (nameof(GameSettings.WritingLinesInterruptionMinIntervalSeconds),
            nameof(GameSettings.WritingLinesInterruptionMaxIntervalSeconds)),
    ];

    private const int MaxLayer = 4; // WardrobeBalance.LayerProbability keys are 0..4

    private readonly OutfitRuleContentService _ruleService = new();

    /// <summary>Names of the numeric game-rule settings this service bounds; a test keeps it in sync with <see cref="GameSettings" />.</summary>
    public static IReadOnlySet<string> RangedProperties { get; } = Ranges.Select(r => r.Property).ToHashSet();

    private static PropertyInfo Prop(string name) =>
        typeof(GameSettings).GetProperty(name) ?? throw new InvalidOperationException($"Unknown setting {name}");

    /// <summary>Builds a contract from the current profile: settings plus every collection a save snapshots.</summary>
    public GameContract Build()
    {
        var wardrobe = profile.GetWardrobe();
        var rules = profile.GetOutfitRules();
        return new GameContract
        {
            Settings = profile.GetGameSettings(),
            Wardrobe = wardrobe,
            OutfitRules = rules,
            Tasks = profile.GetChores(),
            ForcedOutfitPresets = profile.GetForcedOutfitPresets(),
            PlugAndPlayItems = profile.GetPlugAndPlayItems(),
            DiaperPlayItems = profile.GetDiaperPlayItems(),
            ChastityDeviceItems = profile.GetChastityDeviceItems(),
            PermissionTypes = profile.GetPermissionTypes(),
            WardrobeCatalog = WardrobeCatalog.Merge(profile.GetWardrobeCatalog(), wardrobe, rules),
        };
    }

    /// <summary>Serializes a contract to human-readable JSON.</summary>
    public string Serialize(GameContract contract) => JsonSerializer.Serialize(contract, ContentJsonOptions.Options);

    /// <summary>
    ///     Parses and validates a contract file. Unreadable JSON or an unsupported schema version is fatal;
    ///     everything else is clamped, discarded or reported. Never touches the profile.
    /// </summary>
    public ContractValidationResult Validate(string json)
    {
        GameContract? contract;
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object
                || !doc.RootElement.TryGetProperty("schemaVersion", out var version)
                || version.ValueKind != JsonValueKind.Number
                || !version.TryGetInt32(out var schemaVersion))
            {
                return Fatal(ContractIssueKind.InvalidFile, "schemaVersion");
            }

            if (schemaVersion != GameContract.CurrentSchemaVersion)
            {
                return Fatal(ContractIssueKind.UnsupportedSchemaVersion, schemaVersion.ToString(CultureInfo.InvariantCulture));
            }

            contract = doc.RootElement.Deserialize<GameContract>(ContentJsonOptions.Options);
        }
        catch (JsonException)
        {
            return Fatal(ContractIssueKind.InvalidFile, "json");
        }

        return contract is null ? Fatal(ContractIssueKind.InvalidFile, "json") : Validate(contract);
    }

    /// <summary>Validates a contract, normalizing it in place (clamping, discarding). Never touches the profile.</summary>
    public ContractValidationResult Validate(GameContract contract)
    {
        var issues = new List<ContractIssue>();

        if (contract.SchemaVersion != GameContract.CurrentSchemaVersion)
        {
            return Fatal(ContractIssueKind.UnsupportedSchemaVersion,
                contract.SchemaVersion.ToString(CultureInfo.InvariantCulture));
        }

        (string Name, object? Value)[] sections =
        [
            (nameof(GameContract.Settings), contract.Settings),
            (nameof(GameContract.Wardrobe), contract.Wardrobe),
            (nameof(GameContract.OutfitRules), contract.OutfitRules),
            (nameof(GameContract.Tasks), contract.Tasks),
            (nameof(GameContract.ForcedOutfitPresets), contract.ForcedOutfitPresets),
            (nameof(GameContract.PlugAndPlayItems), contract.PlugAndPlayItems),
            (nameof(GameContract.DiaperPlayItems), contract.DiaperPlayItems),
            (nameof(GameContract.ChastityDeviceItems), contract.ChastityDeviceItems),
            (nameof(GameContract.PermissionTypes), contract.PermissionTypes),
        ];
        issues.AddRange(sections.Where(s => s.Value is null)
            .Select(s => new ContractIssue(ContractIssueKind.MissingSection, s.Name)));

        if (issues.Count > 0)
        {
            return new ContractValidationResult(null, issues);
        }

        NormalizeSettings(contract.Settings!, issues);
        var wardrobe = NormalizeWardrobe(contract.Wardrobe!, issues);
        var rules = NormalizeRules(contract.OutfitRules!, wardrobe, issues);
        var tasks = NormalizeTasks(contract.Tasks!, issues);
        var wardrobeIds = wardrobe.Select(i => i.Id).ToHashSet();
        var presets = NormalizePresets(contract.ForcedOutfitPresets!, wardrobeIds, issues);
        var plugAndPlay = NormalizePlugAndPlay(contract.PlugAndPlayItems!, wardrobeIds, issues);
        var diaperPlay = KeepLinkedItems(contract.DiaperPlayItems!, c => c.ClothingItemId, c => c.Id = 0,
            nameof(GameContract.DiaperPlayItems), wardrobeIds, issues);
        var chastity = KeepLinkedItems(contract.ChastityDeviceItems!, c => c.ClothingItemId, c => c.Id = 0,
            nameof(GameContract.ChastityDeviceItems), wardrobeIds, issues);
        var permissions = NormalizePermissions(contract.PermissionTypes!, issues);

        if (wardrobe.Count == 0)
        {
            issues.Add(new ContractIssue(ContractIssueKind.EmptyWardrobe, nameof(GameContract.Wardrobe)));
            return new ContractValidationResult(null, issues);
        }

        contract.Wardrobe = wardrobe;
        contract.OutfitRules = rules;
        contract.Tasks = tasks;
        contract.ForcedOutfitPresets = presets;
        contract.PlugAndPlayItems = plugAndPlay;
        contract.DiaperPlayItems = diaperPlay;
        contract.ChastityDeviceItems = chastity;
        contract.PermissionTypes = permissions;
        contract.WardrobeCatalog = WardrobeCatalog.Merge(contract.WardrobeCatalog, wardrobe, rules);
        return new ContractValidationResult(contract, issues);
    }

    /// <summary>
    ///     Applies a validated contract to the profile: settings (keeping the profile's device-level values),
    ///     then wardrobe, outfit rules and tasks. Throws if the result carries a fatal issue.
    /// </summary>
    public void Apply(ContractValidationResult result)
    {
        if (result.Contract is not { Settings: { } settings } contract)
        {
            throw new InvalidOperationException("Cannot apply a contract that failed validation.");
        }

        // Device-level properties are not in the file; carry the current ones over so they survive the upsert.
        var current = profile.GetGameSettings();
        settings.PlayerName = current.PlayerName;
        settings.Language = current.Language;
        settings.AutoSaveIntervalMinutes = current.AutoSaveIntervalMinutes;
        settings.SelectedCameraIndex = current.SelectedCameraIndex;

        // ponytail: no cross-collection transaction; validation ran first, so a failure here is a DB fault.
        profile.SaveGameSettings(settings);
        profile.ReplaceWardrobe(contract.Wardrobe!);
        profile.ReplaceOutfitRules(contract.OutfitRules!);
        profile.ReplaceChores(contract.Tasks!);
        profile.ReplaceForcedOutfitPresets(contract.ForcedOutfitPresets!);
        profile.ReplacePlugAndPlayItems(contract.PlugAndPlayItems!);
        profile.ReplaceDiaperPlayItems(contract.DiaperPlayItems!);
        profile.ReplaceChastityDeviceItems(contract.ChastityDeviceItems!);
        profile.ReplacePermissionTypes(contract.PermissionTypes!);
        profile.SaveWardrobeCatalog(contract.WardrobeCatalog
                                    ?? WardrobeCatalog.Merge(null, contract.Wardrobe!, contract.OutfitRules!));
    }

    private static ContractValidationResult Fatal(ContractIssueKind kind, string subject) =>
        new(null, [new ContractIssue(kind, subject)]);

    private static void NormalizeSettings(GameSettings settings, List<ContractIssue> issues)
    {
        foreach (var (property, min, max) in Ranges)
        {
            var prop = Prop(property);
            var raw = Convert.ToDouble(prop.GetValue(settings), CultureInfo.InvariantCulture);
            var clamped = Math.Clamp(raw, min, max);
            if (clamped == raw)
            {
                continue;
            }

            prop.SetValue(settings, Convert.ChangeType(clamped, prop.PropertyType, CultureInfo.InvariantCulture));
            issues.Add(new ContractIssue(ContractIssueKind.ValueClamped, property));
        }

        foreach (var (minName, maxName) in Pairs)
        {
            var minProp = Prop(minName);
            var maxProp = Prop(maxName);
            var minValue = minProp.GetValue(settings)!;
            var maxValue = maxProp.GetValue(settings)!;
            if (Convert.ToDouble(minValue, CultureInfo.InvariantCulture)
                <= Convert.ToDouble(maxValue, CultureInfo.InvariantCulture))
            {
                continue;
            }

            minProp.SetValue(settings, maxValue);
            maxProp.SetValue(settings, minValue);
            issues.Add(new ContractIssue(ContractIssueKind.RangeSwapped, minName));
        }
    }

    private static List<ClothingItem> NormalizeWardrobe(List<ClothingItem> items, List<ContractIssue> issues)
    {
        var kept = new List<ClothingItem>();
        var ids = new HashSet<int>();
        foreach (var item in items)
        {
            item.Layers = [..(item.Layers ?? []).Where(l => l is >= 0 and <= MaxLayer).Distinct()];
            item.Slots = [..(item.Slots ?? []).Where(s => !string.IsNullOrWhiteSpace(s))];
            item.Tags = [..(item.Tags ?? []).Where(t => !string.IsNullOrWhiteSpace(t))];

            // Id 0 means "let LiteDB assign one"; only real ids can collide.
            var duplicate = item.Id > 0 && !ids.Add(item.Id);
            if (string.IsNullOrWhiteSpace(item.Name) || item.Layers.Count == 0 || duplicate)
            {
                issues.Add(new ContractIssue(ContractIssueKind.WardrobeItemDiscarded, item.Name ?? ""));
                continue;
            }

            item.Multiplier = ClampFinite(item.Multiplier, 0, 100, item.Name, issues);
            item.Weight = ClampFinite(item.Weight, 0, 1_000, item.Name, issues);
            kept.Add(item);
        }

        return kept;
    }

    private List<OutfitRule> NormalizeRules(List<OutfitRule> rules, List<ClothingItem> wardrobe,
        List<ContractIssue> issues)
    {
        foreach (var rule in rules)
        {
            rule.SourceTag ??= "";
            rule.RelatedTag ??= "";
        }

        var tags = wardrobe.SelectMany(i => i.Tags).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var (valid, discarded) = _ruleService.Validate(rules, tags);
        issues.AddRange(discarded.Select(d =>
            new ContractIssue(ContractIssueKind.OutfitRuleDiscarded, string.Join(", ", d.MissingTags))));

        var ids = new HashSet<int>();
        return valid.Where(r =>
        {
            if (r.Id <= 0 || ids.Add(r.Id))
            {
                return true;
            }

            issues.Add(new ContractIssue(ContractIssueKind.OutfitRuleDiscarded, $"{r.SourceTag}/{r.RelatedTag}"));
            return false;
        }).ToList();
    }

    private static List<Chore> NormalizeTasks(List<Chore> tasks, List<ContractIssue> issues)
    {
        var kept = new List<Chore>();
        var ids = new HashSet<int>();
        foreach (var task in tasks)
        {
            if (string.IsNullOrWhiteSpace(task.Name) || (task.Id > 0 && !ids.Add(task.Id)))
            {
                issues.Add(new ContractIssue(ContractIssueKind.TaskDiscarded, task.Name ?? ""));
                continue;
            }

            task.Description ??= "";
            task.Value = ClampReport(task.Value, 0, 100_000, task.Name, issues);
            task.FrequencyDays = ClampReport(task.FrequencyDays, 1, 365, task.Name, issues);
            task.MinCompletionMinutes = ClampReport(task.MinCompletionMinutes, 0, 1_440, task.Name, issues);
            kept.Add(task);
        }

        return kept;
    }

    private static ContractIssue Discarded(string section, string detail) =>
        new(ContractIssueKind.ConfigEntryDiscarded, $"{section}: {detail}");

    /// <summary>
    ///     Keeps entries whose wardrobe item exists, one per item. Config entry ids mean nothing to anyone
    ///     (only the item id is referenced), so they are reset and reassigned by the database.
    /// </summary>
    private static List<T> KeepLinkedItems<T>(List<T> entries, Func<T, int> itemId, Action<T> resetId,
        string section, HashSet<int> wardrobeIds, List<ContractIssue> issues)
    {
        var seen = new HashSet<int>();
        var kept = new List<T>();
        foreach (var entry in entries)
        {
            var id = itemId(entry);
            if (!wardrobeIds.Contains(id) || !seen.Add(id))
            {
                issues.Add(Discarded(section, $"item {id}"));
                continue;
            }

            resetId(entry);
            kept.Add(entry);
        }

        return kept;
    }

    private static List<PlugAndPlayItemConfig> NormalizePlugAndPlay(List<PlugAndPlayItemConfig> entries,
        HashSet<int> wardrobeIds, List<ContractIssue> issues)
    {
        var kept = KeepLinkedItems(entries, c => c.ClothingItemId, c => c.Id = 0,
            nameof(GameContract.PlugAndPlayItems), wardrobeIds, issues);
        foreach (var c in kept)
        {
            var subject = $"PlugAndPlayItems: item {c.ClothingItemId}";
            c.MinDurationMinutes = ClampReport(c.MinDurationMinutes, 1, 1_440, subject, issues);
            c.MaxDurationMinutes = ClampReport(c.MaxDurationMinutes, 1, 1_440, subject, issues);
            if (c.MinDurationMinutes > c.MaxDurationMinutes)
            {
                (c.MinDurationMinutes, c.MaxDurationMinutes) = (c.MaxDurationMinutes, c.MinDurationMinutes);
                issues.Add(new ContractIssue(ContractIssueKind.RangeSwapped, subject));
            }
        }

        return kept;
    }

    private static List<ForcedOutfitPreset> NormalizePresets(List<ForcedOutfitPreset> presets,
        HashSet<int> wardrobeIds, List<ContractIssue> issues)
    {
        var kept = new List<ForcedOutfitPreset>();
        var ids = new HashSet<int>();
        foreach (var preset in presets)
        {
            var itemIds = (preset.ClothingItemIds ?? []).Distinct().ToList();
            preset.ClothingItemIds = [..itemIds.Where(wardrobeIds.Contains)];
            issues.AddRange(itemIds.Where(id => !wardrobeIds.Contains(id))
                .Select(id => Discarded(nameof(GameContract.ForcedOutfitPresets), $"{preset.Name}: item {id}")));

            if (string.IsNullOrWhiteSpace(preset.Name) || preset.ClothingItemIds.Count == 0
                || (preset.Id > 0 && !ids.Add(preset.Id)))
            {
                issues.Add(Discarded(nameof(GameContract.ForcedOutfitPresets), preset.Name ?? ""));
                continue;
            }

            preset.HoursPerSeverityPoint = ClampFinite(preset.HoursPerSeverityPoint, 0.1, 1_000,
                $"ForcedOutfitPresets: {preset.Name}", issues);
            kept.Add(preset);
        }

        return kept;
    }

    private static List<PermissionTypeConfig> NormalizePermissions(List<PermissionTypeConfig> configs,
        List<ContractIssue> issues)
    {
        var kept = new List<PermissionTypeConfig>();
        var kinds = new HashSet<PermissionKind>();
        foreach (var config in configs)
        {
            if (!Enum.IsDefined(config.Kind) || !kinds.Add(config.Kind))
            {
                issues.Add(Discarded(nameof(GameContract.PermissionTypes), config.Kind.ToString()));
                continue;
            }

            var subject = $"PermissionTypes: {config.Kind}";
            config.Id = 0;
            config.BaseGrantPercent = ClampReport(config.BaseGrantPercent, 0, 100, subject, issues);
            config.DurationMinutes = ClampReport(config.DurationMinutes, 1, 1_440, subject, issues);
            config.CooldownMinutes = ClampReport(config.CooldownMinutes, 1, 10_080, subject, issues);
            kept.Add(config);
        }

        return kept;
    }

    private static T ClampReport<T>(T value, T min, T max, string subject, List<ContractIssue> issues)
        where T : INumber<T>
    {
        var clamped = T.Clamp(value, min, max);
        if (clamped != value)
        {
            issues.Add(new ContractIssue(ContractIssueKind.ValueClamped, subject));
        }

        return clamped;
    }

    private static double ClampFinite(double value, double min, double max, string subject, List<ContractIssue> issues)
    {
        var clamped = double.IsFinite(value) ? Math.Clamp(value, min, max) : min;
        if (clamped != value)
        {
            issues.Add(new ContractIssue(ContractIssueKind.ValueClamped, subject));
        }

        return clamped;
    }
}
