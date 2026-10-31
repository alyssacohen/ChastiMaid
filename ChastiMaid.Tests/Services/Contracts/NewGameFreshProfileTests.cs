using ChastiMaid.Localization;
using ChastiMaid.Repositories;
using ChastiMaid.Services;
using ChastiMaid.Services.Contracts;
using LiteDB;

namespace ChastiMaid.Tests.Services.Contracts;

/// <summary>
///     Reproduces "New Game" on a profile that was only ever seeded by <see cref="DataSeeder.SeedIfEmpty"/>
///     and never touched through Settings — the reported failure scenario (blank contract screen, crash on
///     Accept) on a genuinely fresh install.
/// </summary>
public class NewGameFreshProfileTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ChastiMaidTests_" + Guid.NewGuid());
    private readonly LiteDatabase _profileDb = new(":memory:");

    public void Dispose()
    {
        _profileDb.Dispose();
        try
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
        catch (IOException)
        {
        }
    }

    [Fact]
    public void FreshlySeededProfile_NewGameFlow_DoesNotThrow()
    {
        var profile = new ProfileRepository(_profileDb);
        DataSeeder.SeedIfEmpty(profile, "en", new Localizer());

        var reread = profile.GetGameSettings();
        Assert.NotNull(reread.ParseAppellatives()); // null Appellatives after the LiteDB round-trip must not throw

        var contract = new GameContractService(profile).Build();
        var clauses = new ContractClauseBuilder(new Localizer()).Build(contract);
        Assert.NotEmpty(clauses);

        var settings = profile.GetGameSettings();
        var debt = settings.RollDebt();

        // Own data dir rather than CHASTIMAID_ROOT: that env var is process-wide, so it would leak into
        // tests running in parallel, and a fresh dir per run means no leftover save to collide with.
        using var dbService = new DatabaseService(_root);
        using var saveDb = dbService.OpenSave("test_slot");
        var save = SaveRepository.CreateFromProfile(profile, saveDb, "test_slot", debt);

        Assert.NotNull(save.GetGameState());
    }
}
