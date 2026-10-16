using System.Text;
using System.Text.RegularExpressions;
using Karambolo.PO;
using Xunit.Abstractions;

namespace ChastiMaid.Tests.Localization;

/// <summary>
///     Checks the gettext files in <c>locale/</c>: every .po parses with a valid header, keeps the placeholders of
///     its msgids and covers the whole template. Untranslated and fuzzy entries are reported, not failed: they
///     simply fall back to English until a translator confirms them in Poedit.
/// </summary>
public partial class TranslationFileTests(ITestOutputHelper output)
{
    public static TheoryData<string> Languages =>
        [.. Directory.EnumerateFiles(LocaleDir, "*.po").Select(p => Path.GetFileNameWithoutExtension(p)!).Order()];

    [Fact]
    public void LocaleFolder_HasAtLeastOneTranslation()
    {
        Assert.NotEmpty(Languages);
    }

    [Theory]
    [MemberData(nameof(Languages))]
    public void Po_ParsesWithAValidHeader(string language)
    {
        var catalog = Load($"{language}.po");

        Assert.Equal(language, catalog.Language);
        Assert.True(catalog.PluralFormCount > 0, "Plural-Forms: nplurals is missing");
        Assert.False(string.IsNullOrWhiteSpace(catalog.PluralFormSelector), "Plural-Forms: plural= is missing");
    }

    [Theory]
    [MemberData(nameof(Languages))]
    public void Po_KeepsThePlaceholdersOfEveryMsgid(string language)
    {
        var errors = new List<string>();
        foreach (var entry in Load($"{language}.po"))
        {
            var expected = Placeholders(entry.Key.Id).Union(Placeholders(entry.Key.PluralId)).ToHashSet();
            foreach (var (form, index) in entry.Select((f, i) => (f, i)).Where(t => !string.IsNullOrEmpty(t.f)))
            {
                // A plural form may spell a count out ("una sola tenuta" for {1}), so it may leave a placeholder out.
                var found = Placeholders(form).ToHashSet();
                if (entry.Key.PluralId is null ? !found.SetEquals(expected) : !found.IsSubsetOf(expected))
                {
                    errors.Add($"{Describe(entry.Key)} [form {index}]: expected {{{string.Join(", ", expected)}}}, " +
                               $"found {{{string.Join(", ", Placeholders(form))}}} in \"{form}\"");
                }
            }
        }

        Assert.True(errors.Count == 0, string.Join("\n", errors));
    }

    [Theory]
    [MemberData(nameof(Languages))]
    public void Po_TranslatesEveryPluralFormDeclaredInTheHeader(string language)
    {
        var catalog = Load($"{language}.po");
        var errors = catalog.OfType<POPluralEntry>()
            .Where(e => e.Any(f => !string.IsNullOrEmpty(f)) && e.Count != catalog.PluralFormCount)
            .Select(e => $"{Describe(e.Key)}: {e.Count} forms, the header declares {catalog.PluralFormCount}")
            .ToList();

        Assert.True(errors.Count == 0, string.Join("\n", errors));
    }

    [Theory]
    [MemberData(nameof(Languages))]
    public void Po_ContainsEveryMsgidOfTheTemplate(string language)
    {
        var po = Load($"{language}.po");
        var missing = Load("ChastiMaid.pot").OfType<IPOEntry>().Where(e => !po.Contains(e.Key)).Select(e => Describe(e.Key)).ToList();

        Assert.True(missing.Count == 0,
            $"Missing from {language}.po (run the extract command):\n" + string.Join("\n", missing));

        // Warnings only: these fall back to English at runtime.
        var untranslated = po.OfType<IPOEntry>().Where(e => !e.Any() || e.Any(string.IsNullOrEmpty)).ToList();
        var fuzzy = po.OfType<IPOEntry>().Where(e => e.Comments?.OfType<POFlagsComment>().Any(c => c.Flags.Contains("fuzzy")) == true)
            .ToList();
        output.WriteLine($"WARNING {language}.po: {untranslated.Count} untranslated, {fuzzy.Count} fuzzy.");
        foreach (var entry in untranslated)
        {
            output.WriteLine($"  untranslated: {Describe(entry.Key)}");
        }

        foreach (var entry in fuzzy)
        {
            output.WriteLine($"  fuzzy: {Describe(entry.Key)}");
        }
    }

    // {0}, {1:F2}, {name}: positional placeholders and custom named ones alike; "{{" is an escaped brace.
    [GeneratedRegex(@"(?<!\{)\{(?!\{)[^{}]+\}")]
    private static partial Regex PlaceholderRegex();

    private static HashSet<string> Placeholders(string? text) =>
        text is null ? [] : [.. PlaceholderRegex().Matches(text).Select(m => m.Value)];

    private static string Describe(POKey key) => key.ContextId is null ? $"\"{key.Id}\"" : $"[{key.ContextId}] \"{key.Id}\"";

    internal static string RepoRoot { get; } = FindRepoRoot();

    private static string LocaleDir => Path.Combine(RepoRoot, "locale");

    private static POCatalog Load(string fileName)
    {
        using var reader = new StreamReader(Path.Combine(LocaleDir, fileName), Encoding.UTF8);
        var options = new POStringDecodingOptions
            { KeepKeyStringsPlatformIndependent = true, KeepTranslationStringsPlatformIndependent = true };
        var result = new POParser(new POParserSettings { StringDecodingOptions = options }).Parse(reader);
        Assert.True(result.Success, $"{fileName} does not parse:\n" + string.Join("\n", result.Diagnostics));
        return result.Catalog;
    }

    private static string FindRepoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "ChastiMaid.slnx")))
            {
                return dir.FullName;
            }
        }

        throw new DirectoryNotFoundException("Repository root (ChastiMaid.slnx) not found.");
    }
}
