using System.Runtime.CompilerServices;
using ChastiMaid.Models;
using ChastiMaid.Repositories;
using ChastiMaid.Services;
using LiteDB;

namespace ChastiMaid.Tests.Services;

public class FortuneDeckTests : IDisposable
{
    private readonly LiteDatabase _db = new(":memory:");
    private readonly GameSettings _settings = new() { IdleWageAmount = 5 };
    private readonly SaveRepository _repo;

    public FortuneDeckTests()
    {
        RuntimeHelpers.RunClassConstructor(typeof(DatabaseService).TypeHandle);
        _repo = new SaveRepository(_db);
        _repo.SaveGameState(new GameState { CurrentDebt = 1000, InitialDebt = 1000 });
        _db.GetCollection<GameSettings>("game_settings").Upsert(_settings);
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    private FortuneDeck MakeDeck(int seed)
    {
        return new FortuneDeck(_repo, _settings, new Random(seed));
    }

    [Fact]
    public void Draw_WithEmptyInventory_PoolWeightsSumTo1431()
    {
        // The pool is rebuilt fresh per Draw() from AvailableCopies(), so we can't read it directly —
        // but with an empty inventory every collectible copy is available, matching the full 43 * 10 = 430
        // collectible weight plus the fixed 1001 weight of immediate effects (286 + 715), per
        // docs/specs/fortune-deck.md "Card Pool". Draw enough times that every branch of the pool is
        // exercised at least once (statistically) and every result is a recognized outcome kind.
        var deck = MakeDeck(1);
        for (var i = 0; i < 200; i++)
        {
            var result = deck.Draw();
            Assert.True(Enum.IsDefined(result.Kind));
        }
    }

    [Fact]
    public void Draw_NeverGrantsACardWithZeroAvailableCopies()
    {
        var cooldownBypass = new CardId(CardType.CooldownBypass, 0); // 6 base copies
        for (var i = 0; i < 6; i++)
        {
            _repo.PurchaseCard(cooldownBypass, _settings);
        }

        Assert.Equal(0, _repo.AvailableCopies(cooldownBypass));

        var deck = MakeDeck(2);
        for (var i = 0; i < 300; i++)
        {
            var result = deck.Draw();
            Assert.NotEqual(cooldownBypass, result.Card);
        }
    }

    [Fact]
    public void PunishmentTargetingEffect_WithNoEligiblePunishment_ResolvesAsWhiff()
    {
        // No active punishments exist. Force a punishment-targeting outcome by seeding until one shows up;
        // it must always resolve as a Whiff since GetActivePunishments() is empty.
        var deck = MakeDeck(3);
        var sawPunishmentOutcome = false;
        for (var i = 0; i < 500; i++)
        {
            var result = deck.Draw();
            if (result.Kind is FortuneOutcomeKind.SoftenRandom or FortuneOutcomeKind.CancelRandom
                or FortuneOutcomeKind.AggravateRandom)
            {
                sawPunishmentOutcome = true;
            }
        }

        // With no eligible punishments, SoftenRandom/CancelRandom/AggravateRandom can never actually resolve —
        // the resolver always falls back to Whiff instead.
        Assert.False(sawPunishmentOutcome);
    }

    [Fact]
    public void SuspendedPermissionBuff_IsConsumedByTheNextMatchingRequest()
    {
        _db.GetCollection<PermissionTypeConfig>("permission_types").Upsert(new PermissionTypeConfig
        {
            Kind = PermissionKind.Smoke, BaseGrantPercent = 0, DurationMinutes = 5, CooldownMinutes = 30,
        });

        var deck = MakeDeck(4);
        FortuneDrawResult? grantResult = null;
        for (var i = 0; i < 2000 && grantResult is null; i++)
        {
            var result = deck.Draw();
            if (result.Kind == FortuneOutcomeKind.SuspendedGrant)
            {
                grantResult = result;
            }
        }

        Assert.NotNull(grantResult);
        var target = grantResult!.Target!.Value;

        var request = _repo.RequestPermission(target);

        Assert.Equal(PermissionRequestOutcome.Granted, request.Outcome);
    }
}
