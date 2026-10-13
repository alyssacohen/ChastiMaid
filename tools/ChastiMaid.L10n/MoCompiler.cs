using System.Text;
using Karambolo.PO;

namespace ChastiMaid.L10n;

/// <summary>
///     Compiles a .po file into a GNU gettext .mo file. Like <c>msgfmt</c>, entries that are untranslated
///     or flagged <c>fuzzy</c> are left out, so they fall back to the English source text at runtime.
/// </summary>
public static class MoCompiler
{
    /// <summary>Returns the parse errors; empty when <paramref name="moPath" /> was written.</summary>
    public static IReadOnlyList<string> Compile(string poPath, string moPath)
    {
        using var reader = new StreamReader(poPath, Encoding.UTF8);
        var result = new POParser(new POParserSettings()).Parse(reader);
        if (!result.Success)
        {
            return [.. result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => d.ToString())];
        }

        var catalog = result.Catalog;
        var messages = new SortedDictionary<string, string>(StringComparer.Ordinal) { [""] = Header(catalog) };
        foreach (var entry in catalog)
        {
            var translations = entry.ToList();
            var fuzzy = entry.Comments?.OfType<POFlagsComment>().Any(c => c.Flags.Contains("fuzzy")) ?? false;
            if (fuzzy || translations.Count == 0 || translations.Any(string.IsNullOrEmpty))
            {
                continue;
            }

            var key = entry.Key;
            var id = (key.ContextId is null ? "" : key.ContextId + "\u0004") + key.Id +
                     (key.PluralId is null ? "" : "\0" + key.PluralId);
            messages[id] = string.Join("\0", translations);
        }

        Write(moPath, messages);
        return [];
    }

    private static string Header(POCatalog catalog)
    {
        var header = new StringBuilder("Content-Type: text/plain; charset=UTF-8\n");
        if (catalog.Language is not null)
        {
            header.Append($"Language: {catalog.Language}\n");
        }

        if (catalog.PluralFormSelector is not null)
        {
            header.Append($"Plural-Forms: nplurals={catalog.PluralFormCount}; plural={catalog.PluralFormSelector};\n");
        }

        return header.ToString();
    }

    // .mo layout: 28-byte header, then the table of (length, offset) pairs for the sorted originals,
    // the same table for the translations, then the NUL-terminated strings themselves. No hash table.
    private static void Write(string moPath, SortedDictionary<string, string> messages)
    {
        var originals = messages.Keys.Select(Encoding.UTF8.GetBytes).ToList();
        var translations = messages.Values.Select(Encoding.UTF8.GetBytes).ToList();
        var count = messages.Count;
        const int headerSize = 28;
        var offset = headerSize + count * 16;

        using var writer = new BinaryWriter(File.Create(moPath));
        writer.Write(0x950412deu);
        writer.Write(0);
        writer.Write(count);
        writer.Write(headerSize);
        writer.Write(headerSize + count * 8);
        writer.Write(0);
        writer.Write(offset);
        foreach (var bytes in originals.Concat(translations))
        {
            writer.Write(bytes.Length);
            writer.Write(offset);
            offset += bytes.Length + 1;
        }

        foreach (var bytes in originals.Concat(translations))
        {
            writer.Write(bytes);
            writer.Write((byte)0);
        }
    }
}
