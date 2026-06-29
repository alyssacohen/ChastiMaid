using Avalonia;
using Avalonia.Controls;

namespace ChastiMaid.Views.Controls;

public partial class DirtyTabHeader : UserControl
{
    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<DirtyTabHeader, string>(nameof(Text), "");

    public static readonly StyledProperty<bool> IsDirtyProperty =
        AvaloniaProperty.Register<DirtyTabHeader, bool>(nameof(IsDirty));

    public DirtyTabHeader()
    {
        InitializeComponent();
    }

    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public bool IsDirty
    {
        get => GetValue(IsDirtyProperty);
        set => SetValue(IsDirtyProperty, value);
    }
}
