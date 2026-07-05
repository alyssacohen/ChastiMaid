using System.Globalization;
using System.Resources;

namespace ChastiMaid.Localization;

/// <summary>
///     Thin wrapper over the generated .resources satellite assemblies for Resources.resx / Resources.it.resx.
///     Hand-written (not VS-designer-generated) because the ResX single-file generator only runs inside
///     Visual Studio, not under `dotnet build`.
/// </summary>
public static class Resources
{
    public static readonly ResourceManager ResourceManager =
        new("ChastiMaid.Localization.Resources", typeof(Resources).Assembly);

    public static string Get(string key)
    {
        return ResourceManager.GetString(key, CultureInfo.CurrentUICulture) ?? key;
    }
}
