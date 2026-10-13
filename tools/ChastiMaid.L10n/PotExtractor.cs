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

        using (var reader = new StreamReader(codePot, Encoding.UTF8))
        {
            var parsed = new POParser(new POParserSettings()).Parse(reader);
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
        Write(Path.Combine(root, "locale", "ChastiMaid.pot"), entries);
        Console.WriteLine($"locale/ChastiMaid.pot: {entries.Count} messages.");
        return [];
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

    private static void Write(string potPath, SortedDictionary<POKey, Entry> entries)
    {
        // No creation date in the header: the template must only change when the strings do.
        var catalog = new POCatalog
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

        foreach (var (key, entry) in entries)
        {
            List<POComment> comments =
            [
                .. entry.Comments.Select(c => new POExtractedComment { Text = c }),
                new POReferenceComment
                {
                    // Relative to locale/, where the .pot and the .po files live.
                    References = [.. entry.References.Select(r => new POSourceReference("../" + r.File, r.Line))],
                },
            ];
            if (entry.CSharpFormat)
            {
                comments.Add(new POFlagsComment { Flags = new HashSet<string> { "csharp-format" } });
            }

            catalog.Add(key.PluralId is null
                ? new POSingularEntry(key) { Comments = comments }
                : new POPluralEntry(key) { "", "" }.WithComments(comments));
        }

        using var writer = new StreamWriter(potPath, false, new UTF8Encoding(false)) { NewLine = "\n" };
        new POGenerator(new POGeneratorSettings { IgnoreEncoding = true }).Generate(writer, catalog);
    }

    private static POPluralEntry WithComments(this POPluralEntry entry, IList<POComment> comments)
    {
        entry.Comments = comments;
        return entry;
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

    private sealed class Entry
    {
        public SortedSet<(string File, int Line)> References { get; } = [];
        public SortedSet<string> Comments { get; } = new(StringComparer.Ordinal);
        public bool CSharpFormat { get; set; }
    }
}
