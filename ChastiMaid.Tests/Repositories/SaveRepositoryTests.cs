using ChastiMaid.Models;
using ChastiMaid.Repositories;
using LiteDB;

namespace ChastiMaid.Tests.Repositories;

public class SaveRepositoryTests : IDisposable
{
    private readonly LiteDatabase _db = new(":memory:");
    private readonly SaveRepository _repo;

    public SaveRepositoryTests()
    {
        _repo = new SaveRepository(_db);
        _repo.SaveGameState(new GameState { CurrentDebt = 100 });
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    [Fact]
    public void ApplyFine_IncreasesDebtBySeverityTimesSeverityValue()
    {
        var settings = new GameSettings { FineSeverityValue = 5 };
        _db.GetCollection<GameSettings>("game_settings").Upsert(settings);

        _repo.ApplyFine(10, settings);

        Assert.Equal(150, _repo.GetGameState()!.CurrentDebt);
    }

    [Fact]
    public void ApplyFine_NeverCreatesAnActivePunishment()
    {
        _repo.ApplyFine(10, new GameSettings { FineSeverityValue = 5 });

        Assert.Empty(_repo.GetActivePunishments());
    }

    [Fact]
    public void AssignPunishment_WithAllOptionalTypesDisabled_OnlyAppliesFine()
    {
        _db.GetCollection<GameSettings>("game_settings").Upsert(new GameSettings
        {
            FineSeverityValue = 5,
            CornertimeEnabled = false,
            WritingLinesEnabled = false,
            ForcedOutfitEnabled = false,
        });

        _repo.AssignPunishment((10, 10));

        Assert.Equal(150, _repo.GetGameState()!.CurrentDebt);
        Assert.Empty(_repo.GetActivePunishments());
    }

    [Fact]
    public void AssignPunishment_WithCornertimeEnabled_EventuallyProducesASizedCornertimePunishment()
    {
        _db.GetCollection<GameSettings>("game_settings").Upsert(new GameSettings
        {
            CornertimeEnabled = true,
            CornertimeSeverityValue = 30,
            CornertimeMaxOverrunSeconds = 120,
            WritingLinesEnabled = false,
            ForcedOutfitEnabled = false,
        });

        ActivePunishment? cornertime = null;
        for (var i = 0; i < 50 && cornertime is null; i++)
        {
            _repo.AssignPunishment((10, 10));
            cornertime = _repo.GetActivePunishments().FirstOrDefault(p => p.Kind == PunishmentKind.Cornertime);
        }

        Assert.NotNull(cornertime);
        Assert.Equal(10, cornertime!.Severity);
        Assert.Equal(300, cornertime.CornertimeRequiredSeconds);
        Assert.Equal(420, cornertime.CornertimeMaxSeconds);
    }

    [Fact]
    public void ResolvePunishment_RemovesItFromActivePunishments()
    {
        _repo.SaveActivePunishment(new ActivePunishment { Id = 1, Kind = PunishmentKind.Cornertime, Severity = 5 });

        _repo.ResolvePunishment(1);

        Assert.Empty(_repo.GetActivePunishments());
    }
}
