using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Karambolo.PO;

namespace ChastiMaid.L10n;

/// <summary>
///     Regenerates <c>locale/ChastiMaid.pot</c>: GetText.NET.Extractor reads the C# calls
///     (<c>T</c>, <c>N</c>, <c>P</c>, <c>Localizer.Mark</c>), then this class adds what the extractor cannot
///     see — the <c>{l:Tr '...'}</c> labels in the .axaml files and the <c>translators:</c> comments.
/// </summary>
public static partial class PotExtractor
{
    // Folders whose sources are not part of the app (the extractor scans the whole tree).
    private static readonly string[] ExcludedDirs =
        [".claude", ".git", "bin", "obj", "dist", "tools", "ChastiMaid.Tests", "ChastiMaid.Launcher"];

    // {l:Tr 'text'} or {l:Tr &quot;text&quot;} (for texts with an apostrophe), optionally followed by Context=.
    [GeneratedRegex("""\{l:Tr\s+(?:'(?<text>[^']*)'|&quot;(?<text>.*?)&quot;)(?:\s*,\s*Context=(?:'(?<ctx>[^']*)'|&quot;(?<ctx>.*?)&quot;))?\s*\}""")]
    private static partial Regex TrRegex();

    [GeneratedRegex(@"\{l:Tr\b")]
    private static partial Regex AnyTrRegex();

    // A "// translators: ..." (C#) or "<!-- translators: ... -->" (XAML) line right above the string.
    // A note spanning several lines repeats the "translators:" prefix on each line.
    [GeneratedRegex(@"^\s*(?://|<!--)\s*translators:\s*(?<text>.*?)\s*(?:-->)?\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex TranslatorCommentRegex();

    // An interpolated string would be extracted with {0} placeholders that never match the runtime text.
    [GeneratedRegex(@"\b(?:T|N|P|Mark)\(\s*\$")]
    private static partial Regex InterpolatedCallRegex();

    /// <summary>Returns the errors; empty when the template was written.</summary>
    public static IReadOnlyList<string> Extract(string root)
    {
        root = Path.GetFullPath(root);
        var errors = new List<string>();
        var entries = new SortedDictionary<POKey, Entry>(Comparer<POKey>.Create(Compare));

        var codePot = Path.Combine(root, "obj", "l10n", "code.pot");
        Directory.CreateDirectory(Path.GetDirectoryName(codePot)!);
        if (!RunDotnet(root, "tool restore") || !RunDotnet(root,
                $"GetText.Extractor -s . -t \"{codePot}\" -u -as T Mark -ap N -ad P"))
        {
            return ["GetText.Extractor failed (see the output above)."];
        }

        {
            var parsed = Parse(codePot);
            if (!parsed.Success)
            {
                return [.. parsed.Diagnostics.Select(d => d.ToString())];
            }

            foreach (var entry in parsed.Catalog)
            {
                var references = entry.Comments.OfType<POReferenceComment>().SelectMany(c => c.References)
                    // The extractor writes paths relative to the template file itself, not to its folder.
                    .Select(r => (File: Relative(root, Path.Combine(codePot, r.FilePath)), r.Line))
                    .Where(r => IsAppSource(r.File));
                foreach (var (file, line) in references)
                {
                    Get(entries, entry.Key).References.Add((file, line));
                }

                if (entries.TryGetValue(entry.Key, out var kept) &&
                    entry.Comments.OfType<POFlagsComment>().Any(c => c.Flags.Contains("csharp-format")))
                {
                    kept.CSharpFormat = true;
                }
            }
        }

        foreach (var path in AppSources(root, "*.axaml"))
        {
            var file = Relative(root, path);
            var lines = File.ReadAllLines(path);
            for (var i = 0; i < lines.Length; i++)
            {
                var matches = TrRegex().Matches(lines[i]);
                if (matches.Count != AnyTrRegex().Count(lines[i]))
                {
                    errors.Add($"{file}:{i + 1}: unreadable {{l:Tr}} — write the text as a quoted literal on one line.");
                }

                foreach (Match match in matches)
                {
                    var context = match.Groups["ctx"].Success ? WebUtility.HtmlDecode(match.Groups["ctx"].Value) : null;
                    var key = new POKey(WebUtility.HtmlDecode(match.Groups["text"].Value), null, context);
                    Get(entries, key).References.Add((file, i + 1));
                }
            }
        }

        foreach (var path in AppSources(root, "*.cs"))
        {
            var lines = File.ReadAllLines(path);
            for (var i = 0; i < lines.Length; i++)
            {
                if (InterpolatedCallRegex().IsMatch(lines[i]))
                {
                    errors.Add($"{Relative(root, path)}:{i + 1}: interpolated string passed to a translation " +
                               "method — use a literal with {0} placeholders and pass the values as arguments.");
                }
            }
        }

        if (errors.Count > 0)
        {
            return errors;
        }

        AddTranslatorComments(root, entries.Values);
        var localeDir = Path.Combine(root, "locale");
        Save(Path.Combine(localeDir, "ChastiMaid.pot"), TemplateHeader(), entries, null);
        Console.WriteLine($"locale/ChastiMaid.pot: {entries.Count} messages.");

        // Bring every translation in line with the template: new messages are added untranslated, references
        // and comments are refreshed, messages that left the source are dropped (and reported).
        foreach (var po in Directory.EnumerateFiles(localeDir, "*.po").Order(StringComparer.Ordinal))
        {
            var name = Path.GetFileName(po);
            var parsed = Parse(po);
            if (!parsed.Success)
            {
                errors.AddRange(parsed.Diagnostics.Select(d => $"locale/{name}: {d}"));
                continue;
            }

            var old = parsed.Catalog;
            var dropped = old.OfType<IPOEntry>()
                .Where(e => !entries.ContainsKey(e.Key) && e.Any(t => !string.IsNullOrEmpty(t))).ToList();
            var carried = CarryOver(entries.Keys.Where(k => !old.Contains(k)), dropped);
            foreach (var entry in dropped)
            {
                var to = carried.FirstOrDefault(c => c.Value == entry).Key;
                Console.WriteLine(to.Id is null
                    ? $"locale/{name}: dropped \"{entry.Key.Id}\" = \"{entry.First()}\""
                    : $"locale/{name}: \"{entry.Key.Id}\" -> \"{to.Id}\", translation kept as fuzzy");
            }

            Save(po, new POCatalog
            {
                Encoding = old.Encoding, Language = old.Language, PluralFormCount = old.PluralFormCount,
                PluralFormSelector = old.PluralFormSelector, HeaderComments = old.HeaderComments, Headers = old.Headers,
            }, entries, old, carried);
            var translated = entries.Keys.Count(k => old.TryGetValue(k, out var e) && e.Any() && !e.Any(string.IsNullOrEmpty));
            Console.WriteLine($"locale/{name}: {translated} translated, {entries.Count - translated} to translate.");
        }

        return errors;
    }

    internal static POParseResult Parse(string poPath)
    {
        using var reader = new StreamReader(poPath, Encoding.UTF8);
        // Platform independent: by default the parser turns a line break into Environment.NewLine, and on
        // Windows a msgid with CR LF would never match the LF of the source code.
        var options = new POStringDecodingOptions
            { KeepKeyStringsPlatformIndependent = true, KeepTranslationStringsPlatformIndependent = true };
        return new POParser(new POParserSettings { StringDecodingOptions = options }).Parse(reader);
    }

    private static void AddTranslatorComments(string root, IEnumerable<Entry> entries)
    {
        var sources = new Dictionary<string, string[]>();
        foreach (var entry in entries)
        {
            foreach (var (file, line) in entry.References)
            {
                if (!sources.TryGetValue(file, out var lines))
                {
                    sources[file] = lines = File.ReadAllLines(Path.Combine(root, file));
                }

                // Walk up over the contiguous comment lines right above the string.
                var comment = new List<string>();
                for (var i = line - 2; i >= 0 && TranslatorCommentRegex().Match(lines[i]) is { Success: true } m; i--)
                {
                    comment.Insert(0, m.Groups["text"].Value);
                }

                if (comment.Count > 0)
                {
                    entry.Comments.Add(string.Join(" ", comment));
                }
            }
        }
    }

    // No creation date in the header: the template must only change when the strings do.
    private static POCatalog TemplateHeader() => new()
    {
        Encoding = "UTF-8",
        PluralFormCount = 2,
        PluralFormSelector = "(n != 1)",
        HeaderComments =
        [
            new POTranslatorComment { Text = "ChastiMaid translation template." },
            new POTranslatorComment { Text = "Generated by: dotnet run --project tools/ChastiMaid.L10n -- extract" },
            new POTranslatorComment { Text = "Do not edit by hand." },
        ],
        Headers = new Dictionary<string, string>
        {
            ["Project-Id-Version"] = "ChastiMaid",
            ["MIME-Version"] = "1.0",
            ["Content-Transfer-Encoding"] = "8bit",
        },
    };

    /// <summary>
    ///     Writes <paramref name="entries" /> into <paramref name="catalog" /> (a header-only catalog), taking
    ///     translations, the fuzzy flag and the translator's own notes from <paramref name="translations" />.
    /// </summary>
    private static void Save(string path, POCatalog catalog, SortedDictionary<POKey, Entry> entries,
        POCatalog? translations, IReadOnlyDictionary<POKey, IPOEntry>? carried = null)
    {
        foreach (var (key, entry) in entries)
        {
            IPOEntry? old = null;
            translations?.TryGetValue(key, out old);
            IPOEntry? previous = null;
            if (old is null && carried?.TryGetValue(key, out previous) == true)
            {
                old = previous;
            }

            var flags = new HashSet<string>();
            if (entry.CSharpFormat)
            {
                flags.Add("csharp-format");
            }

            if (previous is not null || (old?.Comments?.OfType<POFlagsComment>().Any(c => c.Flags.Contains("fuzzy")) ?? false))
            {
                flags.Add("fuzzy");
            }

            List<POComment> comments =
            [
                // The old source text, which Poedit shows next to the new one so the change is easy to spot.
                .. previous is null ? [] : new POComment[] { new POPreviousValueComment { IdKind = POIdKind.Id, Value = previous.Key.Id } },
                .. old?.Comments?.OfType<POTranslatorComment>() ?? [],
                .. entry.Comments.Select(c => new POExtractedComment { Text = c }),
                new POReferenceComment
                {
                    // Relative to locale/, where the .pot and the .po files live.
                    References = [.. entry.References.Select(r => new POSourceReference("../" + r.File, r.Line))],
                },
            ];
            if (flags.Count > 0)
            {
                comments.Add(new POFlagsComment { Flags = flags });
            }

            if (key.PluralId is null)
            {
                catalog.Add(new POSingularEntry(key)
                    { Comments = comments, Translation = (old as POSingularEntry)?.Translation });
            }
            else
            {
                var plural = new POPluralEntry(key) { Comments = comments };
                foreach (var form in old as POPluralEntry ?? Enumerable.Repeat("", catalog.PluralFormCount))
                {
                    plural.Add(form);
                }

                catalog.Add(plural);
            }
        }

        using var writer = new StreamWriter(path, false, new UTF8Encoding(false)) { NewLine = "\n" };
        new POGenerator(new POGeneratorSettings { IgnoreEncoding = true }).Generate(writer, catalog);
    }

    private static int Compare(POKey a, POKey b)
    {
        var byId = string.CompareOrdinal(a.Id, b.Id);
        return byId != 0 ? byId : string.CompareOrdinal(a.ContextId, b.ContextId);
    }

    private static Entry Get(SortedDictionary<POKey, Entry> entries, POKey key)
    {
        if (!entries.TryGetValue(key, out var entry))
        {
            return entries[key] = new Entry();
        }

        // gettext identifies a message by context + msgid: when the same text is used both as T("x") and as
        // N("x", "xs", n), the plural form must win or the template loses msgid_plural.
        if (key.PluralId is not null && entries.Keys.First(k => Compare(k, key) == 0).PluralId is null)
        {
            entries.Remove(key);
            entries[key] = entry;
        }

        return entry;
    }

    private static IEnumerable<string> AppSources(string root, string pattern) =>
        Directory.EnumerateFiles(root, pattern, SearchOption.AllDirectories)
            .Where(p => IsAppSource(Relative(root, p)))
            .Order(StringComparer.Ordinal);

    private static bool IsAppSource(string relativePath) =>
        !relativePath.StartsWith("..") && !ExcludedDirs.Contains(relativePath.Split('/')[0]);

    private static string Relative(string root, string path) =>
        Path.GetRelativePath(root, Path.GetFullPath(path)).Replace('\\', '/');

    private static bool RunDotnet(string root, string arguments)
    {
        using var process = Process.Start(new ProcessStartInfo("dotnet", arguments) { WorkingDirectory = root })!;
        process.WaitForExit();
        return process.ExitCode == 0;
    }

    /// <summary>
    ///     Pairs each new message with the dropped translation whose source text is closest (same context and
    ///     plural shape, at least 70% alike), so editing a source string keeps its translation as fuzzy.
    /// </summary>
    public static Dictionary<POKey, IPOEntry> CarryOver(IEnumerable<POKey> added, IEnumerable<IPOEntry> dropped)
    {
        var pool = dropped.ToList();
        var carried = new Dictionary<POKey, IPOEntry>();
        foreach (var key in added)
        {
            var best = pool
                .Where(e => e.Key.ContextId == key.ContextId && (e.Key.PluralId is null) == (key.PluralId is null))
                .Select(e => (Entry: e, Score: Similarity(e.Key.Id, key.Id)))
                .Where(m => m.Score >= 0.7)
                .OrderByDescending(m => m.Score).Select(m => m.Entry).FirstOrDefault();
            if (best is not null)
            {
                carried[key] = best;
                pool.Remove(best);
            }
        }

        return carried;
    }

    // 1 - Levenshtein distance / length of the longer text.
    private static double Similarity(string a, string b)
    {
        var previous = Enumerable.Range(0, b.Length + 1).ToArray();
        var current = new int[b.Length + 1];
        for (var i = 1; i <= a.Length; i++)
        {
            current[0] = i;
            for (var j = 1; j <= b.Length; j++)
            {
                current[j] = Math.Min(Math.Min(current[j - 1], previous[j]) + 1,
                    previous[j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1));
            }

            (previous, current) = (current, previous);
        }

        return 1.0 - previous[b.Length] / (double)Math.Max(1, Math.Max(a.Length, b.Length));
    }

    private sealed class Entry
    {
        public SortedSet<(string File, int Line)> References { get; } = [];
        public SortedSet<string> Comments { get; } = new(StringComparer.Ordinal);
        public bool CSharpFormat { get; set; }
    }
}
