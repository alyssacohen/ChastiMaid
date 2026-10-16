using ChastiMaid.L10n;
using Karambolo.PO;

namespace ChastiMaid.Tests.Localization;

public class CarryOverTests
{
    private static POSingularEntry Translated(string id, string? context = null) =>
        new(new POKey(id, null, context)) { Translation = "x" };

    [Fact]
    public void EditedSourceText_KeepsItsTranslation()
    {
        var dropped = Translated("{0}! Come here at once.");
        var edited = new POKey("{0}! Come here at once!");

        var carried = PotExtractor.CarryOver([edited], [dropped]);

        Assert.Same(dropped, carried[edited]);
    }

    [Fact]
    public void UnrelatedTextOrOtherContext_IsNotMatched()
    {
        var carried = PotExtractor.CarryOver(
            [new POKey("Open the wardrobe"), new POKey("{0}! Come here at once!", null, "button")],
            [Translated("{0}! Come here at once.")]);

        Assert.Empty(carried);
    }
}
