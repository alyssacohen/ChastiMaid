using System.Text;
using System.Xml.Linq;
using Karambolo.PO;

namespace ChastiMaid.L10n;

/// <summary>
///     Temporary migration helper (removed together with the .resx files): fills the untranslated entries of
///     <c>locale/it.po</c> whose msgid is the English text of a resx key, with that key's Italian text.
///     Entries whose Italian text was marked "NEEDS REVIEW" become fuzzy.
/// </summary>
public static class ResxImporter
{
    public static void Import(string root)
    {
        var english = Load(Path.Combine(root, "Localization", "Resources.resx"));
        var italian = Load(Path.Combine(root, "Localization", "Resources.it.resx"));
        var byEnglish = english.ToLookup(e => e.Value.Text, e => italian[e.Key]);

        var poPath = Path.Combine(root, "locale", "it.po");
        var catalog = PotExtractor.Parse(poPath).Catalog;
        var filled = 0;
        foreach (var entry in catalog.OfType<POSingularEntry>().Where(e => string.IsNullOrEmpty(e.Translation)))
        {
            // The same English text with two different Italian texts needs a human choice.
            var candidates = byEnglish[entry.Key.Id].DistinctBy(c => c.Text).ToList();
            if (candidates is not [var match])
            {
                continue;
            }

            entry.Translation = match.Text;
            if (match.NeedsReview)
            {
                var flags = entry.Comments.OfType<POFlagsComment>().FirstOrDefault();
                if (flags is null)
                {
                    entry.Comments.Add(flags = new POFlagsComment { Flags = new HashSet<string>() });
                }

                flags.Flags.Add("fuzzy");
            }

            filled++;
        }

        using var writer = new StreamWriter(poPath, false, new UTF8Encoding(false)) { NewLine = "\n" };
        new POGenerator(new POGeneratorSettings { IgnoreEncoding = true }).Generate(writer, catalog);
        Console.WriteLine($"locale/it.po: {filled} translations imported from the resx files.");
    }

    private static Dictionary<string, (string Text, bool NeedsReview)> Load(string resxPath) =>
        XDocument.Load(resxPath).Root!.Elements("data").ToDictionary(
            d => d.Attribute("name")!.Value,
            d => (d.Element("value")!.Value.ReplaceLineEndings("\n"),
                d.Element("comment")?.Value.Contains("NEEDS REVIEW") ?? false));
}
