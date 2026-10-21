using System.Globalization;
using Avalonia.Controls;
using Avalonia.Data;
using ChastiMaid.Converters;

namespace ChastiMaid.Tests.Converters;

/// <summary>A real TextBox bound the way the chore fields are: the text follows the current culture both ways.</summary>
public class NumericTextConverterTests
{
    private sealed class Holder
    {
        public decimal Value { get; set; }
    }

    [Fact]
    public void UnderItalian_TheCommaIsTheDecimalSeparatorBothWays()
    {
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("it-IT");
        try
        {
            var holder = new Holder { Value = 2.5m };
            var box = new TextBox { DataContext = holder };
            box.Bind(TextBox.TextProperty, new Binding(nameof(Holder.Value))
            {
                Mode = BindingMode.TwoWay,
                Converter = NumericTextConverter.Instance,
                ConverterParameter = "0",
            });
            Assert.Equal("2,5", box.Text);

            box.Text = "1,5";

            Assert.Equal(1.5m, holder.Value);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }
}
