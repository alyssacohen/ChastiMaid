using ChastiMaid.L10n;

// Build-time localization tool. Usage:
//   compile <localeDir> <outDir>   every <localeDir>/{lang}.po -> <outDir>/{lang}/LC_MESSAGES/ChastiMaid.mo
if (args is not ["compile", var localeDir, var outDir])
{
    Console.Error.WriteLine("usage: ChastiMaid.L10n compile <localeDir> <outDir>");
    return 2;
}

var failed = false;
foreach (var po in Directory.EnumerateFiles(localeDir, "*.po"))
{
    var mo = Path.Combine(outDir, Path.GetFileNameWithoutExtension(po), "LC_MESSAGES", "ChastiMaid.mo");
    Directory.CreateDirectory(Path.GetDirectoryName(mo)!);
    var errors = MoCompiler.Compile(po, mo);
    foreach (var error in errors)
    {
        // MSBuild canonical error format, so a broken .po fails the build with a clickable message.
        Console.Error.WriteLine($"{po}: error L10N001: {error}");
    }

    failed |= errors.Count > 0;
}

return failed ? 1 : 0;
