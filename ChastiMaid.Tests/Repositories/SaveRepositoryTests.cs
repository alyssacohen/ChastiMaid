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
    public void PurchaseCard_RaisesBothDebts()
    {
        var settings = new GameSettings { IdleWageAmount = 5 };
        _db.GetCollection<GameSettings>("game_settings").Upsert(settings);
        _repo.SaveGameState(new GameState { CurrentDebt = 100, InitialDebt = 100 });
        var cardId = new CardId(CardType.CooldownBypass, 0); // price multiplier 10 -> 50

        var purchased = _repo.PurchaseCard(cardId, settings);

        Assert.True(purchased);
        var state = _repo.GetGameState()!;
        Assert.Equal(150, state.CurrentDebt);
        Assert.Equal(150, state.InitialDebt);
        Assert.Single(_repo.GetInventory());
    }

    [Fact]
    public void PurchaseCard_WhenNoCopiesLeft_ReturnsFalse()
    {
        var settings = new GameSettings();
        _db.GetCollection<GameSettings>("game_settings").Upsert(settings);
        var cardId = new CardId(CardType.GrantPermission, 0); // 2 base copies

        for (var i = 0; i < 2; i++)
        {
            Assert.True(_repo.PurchaseCard(cardId, settings));
        }

        Assert.False(_repo.PurchaseCard(cardId, settings));
        Assert.Equal(2, _repo.GetInventory().Count);
    }

    [Fact]
    public void ConsumeCard_ReturnsCopyToThePool()
    {
        var settings = new GameSettings();
        _db.GetCollection<GameSettings>("game_settings").Upsert(settings);
        var cardId = new CardId(CardType.SoftenPunishment, 0);
        _repo.PurchaseCard(cardId, settings);

        var consumed = _repo.ConsumeCard(cardId);

        Assert.True(consumed);
        Assert.Empty(_repo.GetInventory());
        Assert.Equal(3, _repo.AvailableCopies(cardId));
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
        Assert.InRange(writingLines.WritingLinesSentenceKeys!.Count, 1, EmployerPhraseBank.AllKeys.Count);
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

    [Fact]
    public void SoftenPunishment_HalvesLinesRoundingUp()
    {
        _db.GetCollection<GameSettings>("game_settings")
            .Upsert(new GameSettings { WritingLinesMinLines = 1, WritingLinesMaxLines = 999 });
        _repo.SaveActivePunishment(new ActivePunishment
        {
            Id = 1, Kind = PunishmentKind.WritingLines, Severity = 10, WritingLinesTotalLines = 35,
        });

        var softened = _repo.SoftenPunishment(1);

        Assert.True(softened);
        var punishment = _repo.GetActivePunishments().Single(p => p.Id == 1);
        Assert.Equal(18, punishment.WritingLinesTotalLines);
        Assert.Equal(5, punishment.Severity);
    }

    [Fact]
    public void SoftenPunishment_TwiceIsQuarter()
    {
        _db.GetCollection<GameSettings>("game_settings")
            .Upsert(new GameSettings { WritingLinesMinLines = 1, WritingLinesMaxLines = 999 });
        _repo.SaveActivePunishment(new ActivePunishment
        {
            Id = 1, Kind = PunishmentKind.WritingLines, Severity = 10, WritingLinesTotalLines = 100,
        });

        _repo.SoftenPunishment(1);
        _repo.SoftenPunishment(1);

        // 100 -> 50 -> 25 (both exact, no rounding needed to demonstrate the quarter)
        Assert.Equal(25, _repo.GetActivePunishments().Single(p => p.Id == 1).WritingLinesTotalLines);
    }

    [Fact]
    public void AggravatePunishment_NeverExceedsConfiguredCap()
    {
        _db.GetCollection<GameSettings>("game_settings").Upsert(new GameSettings
        {
            CornertimeMinRequiredSeconds = 10, CornertimeMaxRequiredSeconds = 120, CornertimeMaxOverrunSeconds = 30,
        });
        _repo.SaveActivePunishment(new ActivePunishment
        {
            Id = 1, Kind = PunishmentKind.Cornertime, Severity = 10, CornertimeRequiredSeconds = 100,
        });

        for (var i = 0; i < 5; i++)
        {
            _repo.AggravatePunishment(1);
        }

        var punishment = _repo.GetActivePunishments().Single(p => p.Id == 1);
        Assert.Equal(120, punishment.CornertimeRequiredSeconds);
        Assert.Equal(150, punishment.CornertimeMaxSeconds);
        Assert.Equal(12, punishment.Severity); // stays at the cap's share instead of ratcheting up

        // 100 -> 150 spills 30s, then 120 -> 180 spills 60s four times: nothing is truncated away.
        var spills = _repo.GetActivePunishments().Where(p => p.Id != 1).ToList();
        Assert.Equal(5, spills.Count);
        Assert.All(spills, p => Assert.Equal(PunishmentKind.Cornertime, p.Kind));
        Assert.All(spills, p => Assert.Equal(p.CornertimeRequiredSeconds + 30, p.CornertimeMaxSeconds));
        Assert.Equal(270, spills.Sum(p => p.CornertimeRequiredSeconds));
        Assert.Equal(3 + 4 * 6, spills.Sum(p => p.Severity));
    }

    [Fact]
    public void AggravatePunishment_ExcessBelowMinimum_BecomesAFine()
    {
        _db.GetCollection<GameSettings>("game_settings").Upsert(new GameSettings
        {
            FineSeverityValue = 5, CornertimeMinRequiredSeconds = 60, CornertimeMaxRequiredSeconds = 120,
        });
        _repo.SaveActivePunishment(new ActivePunishment
        {
            Id = 1, Kind = PunishmentKind.Cornertime, Severity = 10, CornertimeRequiredSeconds = 100,
        });

        _repo.AggravatePunishment(1); // 150s: 30s over the cap, below the 60s minimum -> 3 of the 15 points as a fine

        Assert.Equal(120, _repo.GetActivePunishments().Single().CornertimeRequiredSeconds);
        Assert.Equal(115, _repo.GetGameState()!.CurrentDebt);
    }

    [Fact]
    public void AssignPunishment_SeverityOverTheCap_IsSpentOnFurtherPunishmentsInsteadOfTruncated()
    {
        _db.GetCollection<GameSettings>("game_settings").Upsert(new GameSettings
        {
            FineSeverityValue = 1,
            CornertimeEnabled = true,
            CornertimeSeverityValue = 30,
            CornertimeMinRequiredSeconds = 60,
            CornertimeMaxRequiredSeconds = 600, // 20 points per cornertime
            WritingLinesEnabled = false,
            ForcedOutfitEnabled = false,
        });

        // 41 points: at most two capped cornertimes, and a 1-point residual (30s, below the minimum) must be fined.
        const int rounds = 30;
        for (var i = 0; i < rounds; i++)
        {
            _repo.AssignPunishment((41, 41), "TestReason");
        }

        var punishments = _repo.GetActivePunishments();
        var fined = (int)(_repo.GetGameState()!.CurrentDebt - 100);
        Assert.Equal(41 * rounds, fined + punishments.Sum(p => p.Severity));
        Assert.All(punishments, p => Assert.InRange(p.CornertimeRequiredSeconds!.Value, 60, 600));
        Assert.All(punishments, p => Assert.Equal(p.Severity * 30, p.CornertimeRequiredSeconds));
    }

    [Fact]
    public void SoftenPunishment_ForcedOutfit_RescalesRequiredSecondsAndResolvesIfAlreadyElapsed()
    {
        _db.GetCollection<GameSettings>("game_settings").Upsert(new GameSettings { ForcedOutfitMaxHours = 30 });
        _repo.SaveActivePunishment(new ActivePunishment
        {
            Id = 1, Kind = PunishmentKind.ForcedOutfit, Severity = 10,
            ForcedOutfitRequiredSeconds = 4 * 3600, ForcedOutfitElapsedSeconds = 1 * 3600, // 1h already credited
        });

        var softened = _repo.SoftenPunishment(1); // ceil(4h * 0.5) = 2h required, 1h already elapsed -> not resolved

        Assert.True(softened);
        var punishment = _repo.GetActivePunishments().Single(p => p.Id == 1);
        Assert.Equal(2 * 3600, punishment.ForcedOutfitRequiredSeconds);
        Assert.Equal(1 * 3600, punishment.ForcedOutfitElapsedSeconds);
        Assert.False(punishment.IsResolved);

        _repo.SoftenPunishment(1); // 2h * 0.5 = 1h required, 1h already elapsed -> resolved

        // GetActivePunishments() filters out resolved rows, so read the raw collection instead.
        var resolved = _db.GetCollection<ActivePunishment>("active_punishments").FindById(1);
        Assert.True(resolved.IsResolved);
    }

    [Fact]
    public void TickForcedOutfitProgress_FirstTickOnlyEstablishesBaseline()
    {
        _repo.SaveActivePunishment(new ActivePunishment
        {
            Id = 1, Kind = PunishmentKind.ForcedOutfit, Severity = 10, ForcedOutfitRequiredSeconds = 3600,
        });

        _repo.TickForcedOutfitProgress();

        var punishment = _repo.GetActivePunishments().Single(p => p.Id == 1);
        Assert.Equal(0, punishment.ForcedOutfitElapsedSeconds);
        Assert.NotNull(punishment.ForcedOutfitLastTickUtc);
    }

    [Fact]
    public void TickForcedOutfitProgress_CreditsOnlyTimeBeyondTheGracePeriod()
    {
        // Large AutoSaveIntervalMinutes so the tick's own sleep/hibernate clamp doesn't interfere with this test.
        _db.GetCollection<GameSettings>("game_settings").Upsert(new GameSettings { AutoSaveIntervalMinutes = 120 });
        _repo.SaveActivePunishment(new ActivePunishment
        {
            Id = 1, Kind = PunishmentKind.ForcedOutfit, Severity = 10, ForcedOutfitRequiredSeconds = 3600,
            ForcedOutfitLastTickUtc = DateTime.UtcNow.AddMinutes(-25), ForcedOutfitRunElapsedSeconds = 0,
        });

        _repo.TickForcedOutfitProgress(); // stretch reaches 25 min, still under the 30 min grace

        var punishment = _repo.GetActivePunishments().Single(p => p.Id == 1);
        Assert.Equal(0, punishment.ForcedOutfitElapsedSeconds);

        punishment.ForcedOutfitLastTickUtc = DateTime.UtcNow.AddMinutes(-10);
        _repo.SaveActivePunishment(punishment);
        _repo.TickForcedOutfitProgress(); // stretch reaches 35 min: only the 5 min past the grace are credited

        var afterCrossing = _repo.GetActivePunishments().Single(p => p.Id == 1);
        Assert.InRange(afterCrossing.ForcedOutfitElapsedSeconds, 290, 310);
    }

    [Fact]
    public void TickForcedOutfitProgress_ResolvesOnceElapsedReachesRequired()
    {
        _repo.SaveActivePunishment(new ActivePunishment
        {
            Id = 1, Kind = PunishmentKind.ForcedOutfit, Severity = 10, ForcedOutfitRequiredSeconds = 1800,
            ForcedOutfitLastTickUtc = DateTime.UtcNow.AddHours(-1), ForcedOutfitRunElapsedSeconds = 1800,
            ForcedOutfitElapsedSeconds = 1800,
        });

        _repo.TickForcedOutfitProgress();

        var resolved = _db.GetCollection<ActivePunishment>("active_punishments").FindById(1);
        Assert.True(resolved.IsResolved);
    }

    [Fact]
    public void CancelPunishment_MarksResolved()
    {
        _repo.SaveActivePunishment(new ActivePunishment { Id = 1, Kind = PunishmentKind.WritingLines, Severity = 5 });

        var cancelled = _repo.CancelPunishment(1);

        Assert.True(cancelled);
        Assert.True(_db.GetCollection<ActivePunishment>("active_punishments").FindById(1).IsResolved);
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
    public void RequestPermission_WithGuaranteedGrant_AlwaysGrants()
    {
        SeedPermissionType(PermissionKind.Smoke, 0, 5, 30);

        var result = _repo.RequestPermission(PermissionKind.Smoke, new PermissionModifiers(false, 0, 0, true));

        Assert.Equal(PermissionRequestOutcome.Granted, result.Outcome);
    }

    [Fact]
    public void RequestPermission_WithBypass_IgnoresRunningCooldown()
    {
        SeedPermissionType(PermissionKind.Smoke, 0, 5, 30);
        _repo.RequestPermission(PermissionKind.Smoke); // starts a cooldown (0% grant chance)

        var result = _repo.RequestPermission(PermissionKind.Smoke, new PermissionModifiers(true, 0, 0, false));

        Assert.NotEqual(PermissionRequestOutcome.DeniedInsistence, result.Outcome);
    }

    [Fact]
    public void RequestPermission_ProbabilityBonus_IsCappedAt95()
    {
        SeedPermissionType(PermissionKind.Smoke, 90, 5, 30);

        // With a +20 bonus the effective chance would be 110%; the roll must never exceed 95%.
        // Run enough times that an uncapped roll (>=95, <110) would almost certainly show up as Granted
        // even on unlucky seeds — instead assert every outcome is decided, i.e. no exception/overflow, and
        // that denials still occur (proving the roll isn't unconditionally 100%).
        var deniedSeen = false;
        for (var i = 0; i < 200 && !deniedSeen; i++)
        {
            _db.GetCollection<PermissionCooldown>("permission_cooldowns").DeleteAll();
            var result = _repo.RequestPermission(PermissionKind.Smoke, new PermissionModifiers(false, 20, 0, false));
            if (result.Outcome == PermissionRequestOutcome.Denied)
            {
                deniedSeen = true;
            }
        }

        Assert.True(deniedSeen);
    }

    [Fact]
    public void RequestPermission_ConsumesMatchingSuspendedModifiers()
    {
        SeedPermissionType(PermissionKind.Smoke, 0, 5, 30);
        _repo.AddSuspendedModifier(PermissionKind.Smoke, SuspendedEffect.Grant, 0);

        var result = _repo.RequestPermission(PermissionKind.Smoke);

        Assert.Equal(PermissionRequestOutcome.Granted, result.Outcome);

        // The suspended modifier was consumed: a second request goes back to the base 0% grant chance.
        _db.GetCollection<PermissionCooldown>("permission_cooldowns").DeleteAll();
        var second = _repo.RequestPermission(PermissionKind.Smoke);
        Assert.Equal(PermissionRequestOutcome.Denied, second.Outcome);
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

    [Fact]
    public void CreditEarnings_CanGoNegative_RepresentingCredit()
    {
        _repo.CreditEarnings(150); // CurrentDebt starts at 100

        Assert.Equal(-50, _repo.GetGameState()!.CurrentDebt);
    }

    [Fact]
    public void ApplyDailyInterest_UnlockedSafeWithDebt_CompoundsItUpward()
    {
        _repo.SaveGameState(new GameState
        {
            CurrentDebt = 100, IsSafeLocked = false, DailyInterestRate = 0.1,
            LastSessionAt = DateTime.UtcNow.AddDays(-2),
        });

        _repo.ApplyDailyInterest(DateTime.UtcNow);

        // Day 1: 100 + 10 = 110. Day 2: 110 + 11 = 121.
        Assert.Equal(121, _repo.GetGameState()!.CurrentDebt);
    }

    [Fact]
    public void ApplyDailyInterest_UnlockedSafeWithCredit_ErodesItTowardZero()
    {
        _repo.SaveGameState(new GameState
        {
            CurrentDebt = -50, IsSafeLocked = false, DailyInterestRate = 0.1,
            LastSessionAt = DateTime.UtcNow.AddDays(-1),
        });

        _repo.ApplyDailyInterest(DateTime.UtcNow);

        Assert.Equal(-45, _repo.GetGameState()!.CurrentDebt);
    }

    [Fact]
    public void ApplyDailyInterest_UnlockedSafeWithSmallCredit_NeverFlipsPastZeroIntoDebt()
    {
        _repo.SaveGameState(new GameState
        {
            CurrentDebt = -1, IsSafeLocked = false, DailyInterestRate = 0.9,
            LastSessionAt = DateTime.UtcNow.AddDays(-1),
        });

        _repo.ApplyDailyInterest(DateTime.UtcNow);

        Assert.Equal(0, _repo.GetGameState()!.CurrentDebt);
    }

    [Theory]
    [InlineData(100)]
    [InlineData(-50)]
    public void ApplyDailyInterest_LockedSafe_LeavesBalanceUnchanged(decimal currentDebt)
    {
        _repo.SaveGameState(new GameState
        {
            CurrentDebt = currentDebt, IsSafeLocked = true, DailyInterestRate = 0.1,
            LastSessionAt = DateTime.UtcNow.AddDays(-2),
        });

        _repo.ApplyDailyInterest(DateTime.UtcNow);

        Assert.Equal(currentDebt, _repo.GetGameState()!.CurrentDebt);
    }
}
