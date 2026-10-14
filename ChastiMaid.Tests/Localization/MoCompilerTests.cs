using System.Globalization;
using ChastiMaid.L10n;
using GetText;

namespace ChastiMaid.Tests.Localization;

public class MoCompilerTests
{
    private const string Po = """
        msgid ""
        msgstr ""
        "Content-Type: text/plain; charset=UTF-8\n"
        "Language: it\n"
        "Plural-Forms: nplurals=2; plural=(n != 1);\n"

        msgid "Cancel"
        msgstr "Annulla"

        msgctxt "button"
        msgid "Start"
        msgstr "Inizia"

        msgid "{0} minute"
        msgid_plural "{0} minutes"
        msgstr[0] "{0} minuto"
        msgstr[1] "{0} minuti"

        #, fuzzy
        msgid "Back"
        msgstr "Indietro"

        msgid "Close"
        msgstr ""

        msgid ""
        "Line one\n"
        "Line two"
        msgstr ""
        "Riga uno\n"
        "Riga due"

        """;

    [Fact]
    public void Compile_ProducesCatalogReadableByGetText()
    {
        var dir = Directory.CreateTempSubdirectory("chastimaid-l10n").FullName;
        try
        {
            var po = Path.Combine(dir, "it.po");
            var mo = Path.Combine(dir, "it.mo");
            File.WriteAllText(po, Po);

            Assert.Empty(MoCompiler.Compile(po, mo));

            using var stream = File.OpenRead(mo);
            var catalog = new Catalog(stream, new CultureInfo("it"));
            Assert.Equal("Annulla", catalog.GetString("Cancel"));
            Assert.Equal("Inizia", catalog.GetParticularString("button", "Start"));
            Assert.Equal("Start", catalog.GetString("Start"));
            Assert.Equal("1 minuto", catalog.GetPluralString("{0} minute", "{0} minutes", 1, 1));
            Assert.Equal("5 minuti", catalog.GetPluralString("{0} minute", "{0} minutes", 5, 5));
            // Fuzzy and untranslated entries fall back to the English source.
            Assert.Equal("Back", catalog.GetString("Back"));
            Assert.Equal("Close", catalog.GetString("Close"));
            // Line breaks stay LF on every platform, or multi-line messages would never match.
            Assert.Equal("Riga uno\nRiga due", catalog.GetString("Line one\nLine two"));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void Compile_ReportsErrorsForMalformedFile()
    {
        var dir = Directory.CreateTempSubdirectory("chastimaid-l10n").FullName;
        try
        {
            var po = Path.Combine(dir, "it.po");
            File.WriteAllText(po, "msgid \"Cancel\"\nmsgstr \"Annulla\n");

            Assert.NotEmpty(MoCompiler.Compile(po, Path.Combine(dir, "it.mo")));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void Localizer_FillsEveryPlaceholder_InPluralAndContextMessages()
    {
        var root = Directory.CreateTempSubdirectory("chastimaid-l10n").FullName;
        var previousRoot = Environment.GetEnvironmentVariable("CHASTIMAID_ROOT");
        Environment.SetEnvironmentVariable("CHASTIMAID_ROOT", root);
        try
        {
            var po = Path.Combine(root, "it.po");
            File.WriteAllText(po, Po + """
                msgid "{0} garment: {1}."
                msgid_plural "{0} garments: {1}."
                msgstr[0] "{0} capo: {1}."
                msgstr[1] "{0} capi: {1}."

                """);
            var mo = ChastiMaid.Localization.Localizer.CatalogPath("it");
            Directory.CreateDirectory(Path.GetDirectoryName(mo)!);
            Assert.Empty(MoCompiler.Compile(po, mo));

            var localizer = new ChastiMaid.Localization.Localizer();
            localizer.SetLanguage("it");

            Assert.Equal("3 capi: abiti.", localizer.N("{0} garment: {1}.", "{0} garments: {1}.", 3, 3, "abiti"));
            Assert.Equal("Inizia", localizer.P("button", "Start"));
            Assert.Equal("Start", localizer.T("Start"));
            Assert.Equal("1.50", localizer.T("{0:F2}", 1.5m)); // invariant: always a decimal point
        }
        finally
        {
            Environment.SetEnvironmentVariable("CHASTIMAID_ROOT", previousRoot);
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void SetLanguage_FallsBackToEnglishWhenCatalogIsCorrupt()
    {
        var root = Directory.CreateTempSubdirectory("chastimaid-l10n").FullName;
        var previousRoot = Environment.GetEnvironmentVariable("CHASTIMAID_ROOT");
        Environment.SetEnvironmentVariable("CHASTIMAID_ROOT", root);
        try
        {
            var path = ChastiMaid.Localization.Localizer.CatalogPath("it");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, "not a catalog");

            var localizer = new ChastiMaid.Localization.Localizer();
            localizer.SetLanguage("it");

            Assert.Equal("Cancel", localizer.T("Cancel"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("CHASTIMAID_ROOT", previousRoot);
            Directory.Delete(root, true);
        }
    }
}
