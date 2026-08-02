using System.Runtime.CompilerServices;
using ChastiMaid.Models;
using ChastiMaid.Repositories;
using ChastiMaid.Services;
using LiteDB;

namespace ChastiMaid.Tests.Repositories;

public class SaveRepositoryTests : IDisposable
{
    private readonly LiteDatabase _db = new(":memory:");
    private readonly SaveRepository _repo;

    public SaveRepositoryTests()
    {
        // Registers DatabaseService's UTC DateTime round-trip fix (see DatabaseServiceDateTimeTests) —
        // needed here because this test class talks to LiteDB directly without going through DatabaseService.
        RuntimeHelpers.RunClassConstructor(typeof(DatabaseService).TypeHandle);

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

        _repo.AssignPunishment((10, 10), "TestReason");

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
            _repo.AssignPunishment((10, 10), "TestReason");
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

    [Fact]
    public void AssignPunishment_WithWritingLinesEnabled_ClampsTotalLinesToMinimum()
    {
        _db.GetCollection<GameSettings>("game_settings").Upsert(new GameSettings
        {
            CornertimeEnabled = false,
            WritingLinesEnabled = true,
            WritingLinesSeverityValue = 0.1,
            WritingLinesMinLines = 20,
            WritingLinesMaxLines = 150,
            ForcedOutfitEnabled = false,
        });

        ActivePunishment? writingLines = null;
        for (var i = 0; i < 50 && writingLines is null; i++)
        {
            _repo.AssignPunishment((1, 1), "TestReason");
            writingLines = _repo.GetActivePunishments().FirstOrDefault(p => p.Kind == PunishmentKind.WritingLines);
        }

        Assert.NotNull(writingLines);
        Assert.Equal(20, writingLines!.WritingLinesTotalLines);
    }

    [Fact]
    public void AssignPunishment_WithWritingLinesEnabled_ClampsTotalLinesToMaximum()
    {
        _db.GetCollection<GameSettings>("game_settings").Upsert(new GameSettings
        {
            CornertimeEnabled = false,
            WritingLinesEnabled = true,
            WritingLinesSeverityValue = 1000,
            WritingLinesMinLines = 20,
            WritingLinesMaxLines = 150,
            ForcedOutfitEnabled = false,
        });

        ActivePunishment? writingLines = null;
        for (var i = 0; i < 50 && writingLines is null; i++)
        {
            _repo.AssignPunishment((10, 10), "TestReason");
            writingLines = _repo.GetActivePunishments().FirstOrDefault(p => p.Kind == PunishmentKind.WritingLines);
        }

        Assert.NotNull(writingLines);
        Assert.Equal(150, writingLines!.WritingLinesTotalLines);
    }

    [Fact]
    public void AssignPunishment_WithWritingLinesEnabled_AssignsANonEmptySentenceKeySet()
    {
        _db.GetCollection<GameSettings>("game_settings").Upsert(new GameSettings
        {
            CornertimeEnabled = false,
            WritingLinesEnabled = true,
            ForcedOutfitEnabled = false,
        });

        ActivePunishment? writingLines = null;
        for (var i = 0; i < 50 && writingLines is null; i++)
        {
            _repo.AssignPunishment((10, 10), "TestReason");
            writingLines = _repo.GetActivePunishments().FirstOrDefault(p => p.Kind == PunishmentKind.WritingLines);
        }

        Assert.NotNull(writingLines);
        Assert.NotNull(writingLines!.WritingLinesSentenceKeys);
        Assert.InRange(writingLines.WritingLinesSentenceKeys!.Count, 1, 12);
        Assert.False(writingLines.WritingLinesAttemptActive);
    }

    [Fact]
    public void ResolveOrphanedWritingLinesAttempts_AppliesFineAndClearsFlagWithoutResolving()
    {
        _repo.SaveActivePunishment(new ActivePunishment
        {
            Id = 1,
            Kind = PunishmentKind.WritingLines,
            Severity = 10,
            WritingLinesTotalLines = 30,
            WritingLinesAttemptActive = true,
        });
        _db.GetCollection<GameSettings>("game_settings").Upsert(new GameSettings { FineSeverityValue = 5 });

        _repo.ResolveOrphanedWritingLinesAttempts();

        Assert.Equal(150, _repo.GetGameState()!.CurrentDebt);
        var punishment = _repo.GetActivePunishments().Single(p => p.Id == 1);
        Assert.False(punishment.WritingLinesAttemptActive);
        Assert.False(punishment.IsResolved);
    }

    [Fact]
    public void ResolveOrphanedWritingLinesAttempts_LeavesInactiveAttemptsUntouched()
    {
        _repo.SaveActivePunishment(new ActivePunishment
        {
            Id = 1,
            Kind = PunishmentKind.WritingLines,
            Severity = 10,
            WritingLinesTotalLines = 30,
            WritingLinesAttemptActive = false,
        });
        _db.GetCollection<GameSettings>("game_settings").Upsert(new GameSettings { FineSeverityValue = 5 });

        _repo.ResolveOrphanedWritingLinesAttempts();

        Assert.Equal(100, _repo.GetGameState()!.CurrentDebt);
    }

    [Fact]
    public void SaveActivePunishment_RoundTripsWritingLinesFields()
    {
        _repo.SaveActivePunishment(new ActivePunishment
        {
            Id = 1,
            Kind = PunishmentKind.WritingLines,
            Severity = 10,
            WritingLinesTotalLines = 42,
            WritingLinesSentenceKeys = ["WritingLineObedience", "WritingLineGratitude"],
            WritingLinesAttemptActive = true,
        });

        var reloaded = _repo.GetActivePunishments().Single(p => p.Id == 1);

        Assert.Equal(42, reloaded.WritingLinesTotalLines);
        Assert.Equal(["WritingLineObedience", "WritingLineGratitude"], reloaded.WritingLinesSentenceKeys);
        Assert.True(reloaded.WritingLinesAttemptActive);
    }

    private void SeedPermissionType(PermissionKind kind, int baseGrantPercent, int durationMinutes, int cooldownMinutes)
    {
        _db.GetCollection<PermissionTypeConfig>("permission_types").Upsert(new PermissionTypeConfig
        {
            Kind = kind, BaseGrantPercent = baseGrantPercent,
            DurationMinutes = durationMinutes, CooldownMinutes = cooldownMinutes,
        });
    }

    [Fact]
    public void RequestPermission_AlwaysGranted_StartsAnActiveBreak()
    {
        SeedPermissionType(PermissionKind.TeaCoffee, 100, 10, 30);

        var result = _repo.RequestPermission(PermissionKind.TeaCoffee);

        Assert.Equal(PermissionRequestOutcome.Granted, result.Outcome);
        var active = _repo.GetActivePermissionBreak();
        Assert.NotNull(active);
        Assert.Equal(PermissionKind.TeaCoffee, active!.Kind);
    }

    [Fact]
    public void RequestPermission_AlwaysDenied_StartsACooldownWithoutAnActiveBreak()
    {
        SeedPermissionType(PermissionKind.Smoke, 0, 5, 30);

        var result = _repo.RequestPermission(PermissionKind.Smoke);

        Assert.Equal(PermissionRequestOutcome.Denied, result.Outcome);
        Assert.Null(_repo.GetActivePermissionBreak());
    }

    [Fact]
    public void RequestPermission_WhileCooldownActive_DeniesForInsistence()
    {
        SeedPermissionType(PermissionKind.Smoke, 0, 5, 30);
        _repo.RequestPermission(PermissionKind.Smoke);

        var result = _repo.RequestPermission(PermissionKind.Smoke);

        Assert.Equal(PermissionRequestOutcome.DeniedInsistence, result.Outcome);
    }

    [Fact]
    public void RequestPermission_ForToiletKindWithDiaperPlayActive_AlwaysDeniedRegardlessOfGrantChance()
    {
        SeedPermissionType(PermissionKind.Pee, 100, 5, 10);
        var state = _repo.GetGameState()!;
        state.DiaperPlayActiveThisSession = true;
        _repo.SaveGameState(state);

        var result = _repo.RequestPermission(PermissionKind.Pee);

        Assert.Equal(PermissionRequestOutcome.DeniedDiaper, result.Outcome);
        Assert.Null(_repo.GetActivePermissionBreak());
    }

    [Fact]
    public void EndPermissionBreak_WithinGrantedDuration_AssignsNoPunishment()
    {
        SeedPermissionType(PermissionKind.SitDown, 100, 10, 20);
        _repo.RequestPermission(PermissionKind.SitDown);

        _repo.EndPermissionBreak();

        Assert.Null(_repo.GetActivePermissionBreak());
        Assert.Empty(_repo.GetActivePunishments());
        Assert.Equal(100, _repo.GetGameState()!.CurrentDebt);
    }

    [Fact]
    public void EndPermissionBreak_PastMaxEndAt_AssignsAPunishment()
    {
        _db.GetCollection<GameSettings>("game_settings").Upsert(new GameSettings
        {
            CornertimeEnabled = false, WritingLinesEnabled = false, ForcedOutfitEnabled = false,
            FineSeverityValue = 5,
        });
        SeedPermissionType(PermissionKind.SitDown, 100, 5, 20);
        _repo.RequestPermission(PermissionKind.SitDown);
        var active = _repo.GetActivePermissionBreak()!;
        active.MaxEndAt = DateTime.UtcNow.AddSeconds(-20); // pretend the break already overran by 20s
        _db.GetCollection<ActivePermissionBreak>("active_permission_break").Upsert(active);

        _repo.EndPermissionBreak();

        Assert.Null(_repo.GetActivePermissionBreak());
        Assert.Equal(150, _repo.GetGameState()!.CurrentDebt); // Fine-only: 10 severity × 5/point
    }
}
