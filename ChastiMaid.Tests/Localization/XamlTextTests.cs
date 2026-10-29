using System.Xml.Linq;

namespace ChastiMaid.Tests.Localization;

/// <summary>
///     No user-visible text is written as a literal in the views: every text attribute uses a binding, a markup
///     extension ({l:Tr '...'}) or is listed in <see cref="Allowed" /> with its reason.
/// </summary>
public class XamlTextTests
{
    private static readonly HashSet<string> TextAttributes =
    [
        "Text", "Content", "Header", "Watermark", "PlaceholderText", "ToolTip.Tip", "Title", "Label",
        "Description", "OnContent", "OffContent", "Inlines",
    ];

    /// <summary>Literal texts that are not language-dependent.</summary>
    private static readonly Dictionary<string, string> Allowed = new()
    {
        ["ChastiMaid"] = "the app's name",
    };

    [Fact]
    public void Views_HaveNoLiteralUserVisibleText()
    {
        var root = TranslationFileTests.RepoRoot;
        var errors = new List<string>();
        foreach (var path in Directory.EnumerateFiles(Path.Combine(root, "Views"), "*.axaml", SearchOption.AllDirectories)
                     .Append(Path.Combine(root, "App.axaml")))
        {
            var file = Path.GetRelativePath(root, path);
            var document = XDocument.Load(path, LoadOptions.SetLineInfo);
            foreach (var element in document.Descendants())
            {
                // A style setter assigns a text property through Value.
                var setterValue = element.Name.LocalName == "Setter" &&
                                  TextAttributes.Contains(element.Attribute("Property")?.Value ?? "")
                    ? element.Attribute("Value")
                    : null;
                var literals = element.Attributes()
                    .Where(a => TextAttributes.Contains(a.Name.LocalName))
                    .Append(setterValue)
                    .OfType<XAttribute>()
                    .Where(a => IsLiteral(a.Value))
                    .Select(a => (Text: Unescape(a.Value), Where: a.Name.LocalName))
                    .Concat(element.Nodes().OfType<XText>().Select(t => (Text: t.Value.Trim(), Where: "content")));
                errors.AddRange(literals
                    .Where(l => l.Text.Any(char.IsLetter) && !Allowed.ContainsKey(l.Text))
                    .Select(l => $"{file}:{((System.Xml.IXmlLineInfo)element).LineNumber} " +
                                 $"<{element.Name.LocalName}> {l.Where}=\"{l.Text}\""));
            }
        }

        Assert.True(errors.Count == 0,
            "Literal texts in views (use {l:Tr '...'} or a binding, or add a reasoned exception):\n" +
            string.Join("\n", errors));
    }

    // "{}" escapes a literal that starts with a brace; any other "{" opens a binding or markup extension.
    private static bool IsLiteral(string value) => !value.StartsWith('{') || value.StartsWith("{}");

    private static string Unescape(string value) => value.StartsWith("{}") ? value[2..] : value;
}
