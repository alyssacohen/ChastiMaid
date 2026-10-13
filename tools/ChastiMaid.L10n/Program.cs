using ChastiMaid.L10n;

// Localization tool. Usage (from the repository root):
//   extract [root]                 regenerate locale/ChastiMaid.pot from the C# and .axaml sources,
//                                  then update every locale/*.po to match it
//   compile <localeDir> <outDir>   every <localeDir>/{lang}.po -> <outDir>/{lang}/LC_MESSAGES/ChastiMaid.mo
//                                  (run by the build, see ChastiMaid.csproj)
switch (args)
{
    case ["extract", .. var rest] when rest.Length <= 1:
    {
        var errors = PotExtractor.Extract(rest is [var root] ? root : Directory.GetCurrentDirectory());
        foreach (var error in errors)
        {
            Console.Error.WriteLine($"error L10N002: {error}");
        }

        return errors.Count > 0 ? 1 : 0;
    }
    case ["import-resx"]:
        // Temporary, for the resx -> gettext migration only.
        ResxImporter.Import(Directory.GetCurrentDirectory());
        return 0;
    case ["compile", var localeDir, var outDir]:
    {
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
    }
    default:
        Console.Error.WriteLine("usage: ChastiMaid.L10n extract [root] | compile <localeDir> <outDir>");
        return 2;
}
