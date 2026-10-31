using ChastiMaid.Models;
using ChastiMaid.Services;
using LiteDB;

namespace ChastiMaid.Repositories;

public class SaveRepository(LiteDatabase db)
{
    private ILiteCollection<ClothingItem> Wardrobe => db.GetCollection<ClothingItem>("wardrobe");
    private ILiteCollection<OutfitRule> OutfitRules => db.GetCollection<OutfitRule>("outfit_rules_v2");
    private ILiteCollection<Chore> Chores => db.GetCollection<Chore>("chores");

    private ILiteCollection<ActivePunishment> ActivePunishments =>
        db.GetCollection<ActivePunishment>("active_punishments");

    private ILiteCollection<Session> Sessions => db.GetCollection<Session>("sessions");
    private ILiteCollection<GameState> GameStates => db.GetCollection<GameState>("game_state");
    private ILiteCollection<GameSettings> Settings => db.GetCollection<GameSettings>("game_settings");
    private ILiteCollection<PoseCheckRecord> PoseChecks => db.GetCollection<PoseCheckRecord>("pose_checks");
    private ILiteCollection<CurtsyCheckRecord> CurtsyChecks => db.GetCollection<CurtsyCheckRecord>("curtsy_checks");
    private ILiteCollection<KneelCheckRecord> KneelChecks => db.GetCollection<KneelCheckRecord>("kneel_checks");

    private ILiteCollection<PlugAndPlayItemConfig> PlugAndPlayItems =>
        db.GetCollection<PlugAndPlayItemConfig>("plug_and_play_items");

    private ILiteCollection<DiaperPlayItemConfig> DiaperPlayItems =>
        db.GetCollection<DiaperPlayItemConfig>("diaper_play_items");

    private ILiteCollection<ChastityDeviceItemConfig> ChastityDeviceItems =>
        db.GetCollection<ChastityDeviceItemConfig>("chastity_device_items");

    private ILiteCollection<ActivePlugAndPlayAccessory> ActivePlugAndPlayAccessoryCollection =>
        db.GetCollection<ActivePlugAndPlayAccessory>("active_plug_and_play_accessory");

    private ILiteCollection<ForcedOutfitPreset> ForcedOutfitPresets =>
        db.GetCollection<ForcedOutfitPreset>("forced_outfit_presets");

    private ILiteCollection<PermissionTypeConfig> PermissionTypes =>
        db.GetCollection<PermissionTypeConfig>("permission_types");

    private ILiteCollection<PermissionCooldown> PermissionCooldowns =>
        db.GetCollection<PermissionCooldown>("permission_cooldowns");

    private ILiteCollection<ActivePermissionBreak> ActivePermissionBreakCollection =>
        db.GetCollection<ActivePermissionBreak>("active_permission_break");

    private ILiteCollection<InventoryCard> InventoryCards => db.GetCollection<InventoryCard>("inventory_cards");

    private ILiteCollection<SuspendedModifier> SuspendedModifiers =>
        db.GetCollection<SuspendedModifier>("suspended_modifiers");

    // --- GameState ---
    public GameState? GetGameState()
    {
        return GameStates.FindById(1);
    }

    public void SaveGameState(GameState state)
    {
        GameStates.Upsert(state);
    }

    /// <summary>
    ///     Marks the game as completed, so it can only reopen on the completion screen. Returns true only the
    ///     first time, letting the caller count the completion once.
    /// </summary>
    public bool MarkGameCompleted()
    {
        var state = GetGameState();
        if (state is null || state.IsCompleted)
        {
            return false;
        }

        state.IsCompleted = true;
        SaveGameState(state);
        return true;
    }

    // --- GameSettings ---
    public GameSettings GetGameSettings()
    {
        return Settings.FindById(1) ?? new GameSettings();
    }

    // --- Snapshot collections (read-only after new game) ---
    public List<ClothingItem> GetWardrobe()
    {
        return Wardrobe.FindAll().ToList();
    }

    public List<OutfitRule> GetOutfitRules()
    {
        return OutfitRules.FindAll().ToList();
    }

    public List<Chore> GetChores()
    {
        return Chores.FindAll().ToList();
    }

    public List<ForcedOutfitPreset> GetForcedOutfitPresets()
    {
        return ForcedOutfitPresets.FindAll().ToList();
    }

    public List<PermissionTypeConfig> GetPermissionTypes()
    {
        return PermissionTypes.FindAll().ToList();
    }

    // --- Wardrobe weight adjustments ---
    // Weight-0 items stay out of the economy, or accepting/rejecting them would make them random picks.
    public void AcceptItems(IEnumerable<ClothingItem> items)
    {
        foreach (var item in items.Where(i => i.Weight > 0))
        {
            item.Weight = Math.Max(
                WardrobeBalance.AcceptWeightFloor,
                item.Weight - WardrobeBalance.AcceptWeightDecrement);
            if (item.RejectionCount > 0)
            {
                item.RejectionCount--;
            }

            Wardrobe.Update(item);
        }
    }

    public void RejectItems(IEnumerable<ClothingItem> items)
    {
        foreach (var item in items.Where(i => i.Weight > 0))
        {
            item.Weight += WardrobeBalance.RejectWeightIncrement;
            item.RejectionCount++;
            Wardrobe.Update(item);
        }

        var state = GetGameState();
        if (state is null)
        {
            return;
        }

        state.WardrobeRejectionsSinceRescale++;
        if (state.WardrobeRejectionsSinceRescale >= WardrobeBalance.RescaleEveryNRejections)
        {
            RescaleWardrobeWeights(state);
            state.WardrobeRejectionsSinceRescale = 0;
        }

        SaveGameState(state);
    }

    /// <summary>
    ///     Restores the wardrobe's total Weight to what it was at new-game snapshot time, preserving each item's
    ///     relative proportion — prevents the accept/reject economy from collapsing everything to
    ///     <see cref="WardrobeBalance.AcceptWeightFloor" /> or growing unbounded over a long game, without needing a
    ///     hard per-item cap that would erase the spread the user set up in Settings.
    /// </summary>
    private void RescaleWardrobeWeights(GameState state)
    {
        var wardrobe = Wardrobe.FindAll().ToList();
        var currentTotal = wardrobe.Sum(i => i.Weight);
        if (currentTotal <= 0)
        {
            return;
        }

        var factor = state.InitialWardrobeWeightTotal / currentTotal;
        foreach (var item in wardrobe)
        {
            item.Weight *= factor;
            Wardrobe.Update(item);
        }
    }

    // --- In-game chores (can add during session, don't affect profile) ---
    public void AddChore(Chore chore)
    {
        Chores.Insert(chore);
    }

    public void UpdateChoreCompletion(int choreId, DateTime completedAt)
    {
        var chore = Chores.FindById(choreId);
        if (chore is null)
        {
            return;
        }

        chore.LastCompletedAt = completedAt;
        Chores.Update(chore);
    }

    public void DeleteChore(int choreId)
    {
        Chores.Delete(choreId);
    }

    // --- Active punishments ---
    public List<ActivePunishment> GetActivePunishments()
    {
        return ActivePunishments.Find(p => !p.IsResolved).ToList();
    }

    /// <summary>The forced-outfit punishment currently in effect, i.e. the oldest one still pending — later
    /// ones queue behind it and only take effect once this one resolves.</summary>
    public ActivePunishment? GetActiveForcedOutfitPunishment()
    {
        return GetActivePunishments()
            .Where(p => p.Kind == PunishmentKind.ForcedOutfit)
            .OrderBy(p => p.AssignedAt)
            .FirstOrDefault();
    }

    private const double ForcedOutfitGraceSeconds = 1800; // first 30 minutes of each continuous stretch are free
    private const double ForcedOutfitMinRequiredHours = 0.5;

    /// <summary>
    ///     Advances the in-effect Forced Outfit punishment's real-time progress by the time elapsed since the last
    ///     tick. Call periodically (and at session lifecycle points) while the app is open — see AutoSaveService.
    /// </summary>
    public void TickForcedOutfitProgress()
    {
        var punishment = GetActiveForcedOutfitPunishment();
        if (punishment is null)
        {
            return;
        }

        var now = DateTime.UtcNow;
        if (punishment.ForcedOutfitLastTickUtc is not { } lastTick)
        {
            punishment.ForcedOutfitLastTickUtc = now;
            SaveActivePunishment(punishment);
            return;
        }

        // ponytail: clamp against sleep/hibernate gaps being counted as played time; revisit if this causes false negatives
        var maxDelta = TimeSpan.FromMinutes(2 * GetGameSettings().AutoSaveIntervalMinutes).TotalSeconds;
        var delta = Math.Clamp((now - lastTick).TotalSeconds, 0, maxDelta);

        var runStart = punishment.ForcedOutfitRunElapsedSeconds;
        var runEnd = runStart + delta;
        var creditable = Math.Max(0, runEnd - Math.Max(runStart, ForcedOutfitGraceSeconds));

        punishment.ForcedOutfitElapsedSeconds += creditable;
        punishment.ForcedOutfitRunElapsedSeconds = runEnd;
        punishment.ForcedOutfitLastTickUtc = now;

        if (punishment.ForcedOutfitElapsedSeconds >= (punishment.ForcedOutfitRequiredSeconds ?? 0))
        {
            punishment.IsResolved = true;
        }

        SaveActivePunishment(punishment);
    }

    public void SaveActivePunishment(ActivePunishment punishment)
    {
        ActivePunishments.Upsert(punishment);
    }

    public void ResolvePunishment(int id)
    {
        var punishment = ActivePunishments.FindById(id);
        if (punishment is null)
        {
            return;
        }

        punishment.IsResolved = true;
        ActivePunishments.Update(punishment);
    }

    /// <summary>
    ///     Applies a fine (mirroring the "Abandon" outcome) to any Writing Lines punishment whose attempt was left
    ///     active when the app last closed, without resolving it — it stays pending, retryable later. Call once when
    ///     a save slot is opened.
    /// </summary>
    public void ResolveOrphanedWritingLinesAttempts()
    {
        foreach (var punishment in GetActivePunishments()
                     .Where(p => p.Kind == PunishmentKind.WritingLines && p.WritingLinesAttemptActive))
        {
            ApplyFine(punishment.Severity);
            punishment.WritingLinesAttemptActive = false;
            SaveActivePunishment(punishment);
        }
    }

    // --- Pose checks ---
    public void SavePoseCheckRecord(PoseCheckRecord record)
    {
        PoseChecks.Upsert(record);
    }

    public List<PoseCheckRecord> GetPendingPoseChecks()
    {
        return PoseChecks.Find(r => !r.Reviewed).ToList();
    }

    public List<PoseCheckRecord> GetPendingSelfCheckablePoseChecks()
    {
        return PoseChecks.Find(r => !r.Reviewed && r.HasOutfitSnapshot).ToList();
    }

    // --- Curtsy checks ---
    public void SaveCurtsyCheckRecord(CurtsyCheckRecord record)
    {
        CurtsyChecks.Upsert(record);
    }

    public List<CurtsyCheckRecord> GetPendingCurtsyChecks()
    {
        return CurtsyChecks.Find(r => !r.Reviewed).ToList();
    }

    // --- Kneel checks ---
    public void SaveKneelCheckRecord(KneelCheckRecord record)
    {
        KneelChecks.Upsert(record);
    }

    public List<KneelCheckRecord> GetPendingKneelChecks()
    {
        return KneelChecks.Find(r => !r.Reviewed).ToList();
    }

    // --- Plug and Play ---
    public List<PlugAndPlayItemConfig> GetPlugAndPlayItems()
    {
        return PlugAndPlayItems.FindAll().ToList();
    }

    // --- Diaper Play ---
    public List<DiaperPlayItemConfig> GetDiaperPlayItems()
    {
        return DiaperPlayItems.FindAll().ToList();
    }

    // --- Chastity Device ---
    public List<ChastityDeviceItemConfig> GetChastityDeviceItems()
    {
        return ChastityDeviceItems.FindAll().ToList();
    }

    public ActivePlugAndPlayAccessory? GetActivePlugAndPlayAccessory()
    {
        return ActivePlugAndPlayAccessoryCollection.FindById(1);
    }

    public void SaveActivePlugAndPlayAccessory(ActivePlugAndPlayAccessory accessory)
    {
        ActivePlugAndPlayAccessoryCollection.Upsert(accessory);
    }

    public void ClearActivePlugAndPlayAccessory()
    {
        ActivePlugAndPlayAccessoryCollection.Delete(1);
    }

    /// <summary>
    ///     Applies the Fine punishment's debt increase immediately. Never creates a pending record. Raises
    ///     InitialDebt alongside CurrentDebt so the fine grows the amount owed rather than eating into
    ///     credit already settled (which would otherwise show as negative progress).
    /// </summary>
    public void ApplyFine(int severity, GameSettings? settings = null)
    {
        settings ??= GetGameSettings();
        ChargeExpense(Math.Round(severity * settings.FineSeverityValue, 0));
    }

    /// <summary>
    ///     Raises CurrentDebt and InitialDebt by the same amount, so the expense grows the amount owed rather than
    ///     eating into credit already settled (which would otherwise show as negative progress). Shared by ApplyFine,
    ///     Shop purchases and Fortune Deck draws.
    /// </summary>
    public void ChargeExpense(decimal amount)
    {
        var state = GetGameState();
        if (state is null)
        {
            return;
        }

        state.CurrentDebt += amount;
        state.InitialDebt += amount;
        SaveGameState(state);
    }

    /// <summary>
    ///     Reduces CurrentDebt only (never InitialDebt). May go negative, representing credit the player can spend
    ///     against future Shop/Fortune Deck purchases. Same shape as idle wage and chore payouts. Used for Fortune
    ///     Deck cash wins, which credit the player rather than growing the amount owed.
    /// </summary>
    public void CreditEarnings(decimal amount)
    {
        var state = GetGameState();
        if (state is null)
        {
            return;
        }

        state.CurrentDebt -= amount;
        SaveGameState(state);
    }

    /// <summary>
    ///     Settles the daily interest at session start: at most once per local calendar day, and only while the safe
    ///     is open. On the first start of a new day it applies the daily rate once, however many days were skipped:
    ///     a positive debt grows, a credit erodes toward (never past) zero. The day is recorded even when nothing is
    ///     charged (safe locked, zero balance), so days with the safe locked never accumulate. A save without a
    ///     recorded day (created before this rule) and a clock moved backwards are not charged; both just record
    ///     today. The new balance and the day are saved in one write, so a crash cannot charge the same day twice.
    /// </summary>
    public void ApplyDailyInterest(TimeProvider clock)
    {
        var state = GetGameState();
        if (state is null)
        {
            return;
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var today = GameDay.LocalDate(now, clock.LocalTimeZone);
        DateOnly? lastDay = state.LastInterestDayAt is { } last ? GameDay.LocalDate(last, clock.LocalTimeZone) : null;
        if (lastDay == today)
        {
            return;
        }

        if (lastDay < today && !state.IsSafeLocked)
        {
            var rate = (decimal)state.DailyInterestRate;
            state.CurrentDebt = state.CurrentDebt switch
            {
                > 0 => state.CurrentDebt + Math.Round(state.CurrentDebt * rate, 0),
                < 0 => Math.Min(0, state.CurrentDebt + Math.Round(-state.CurrentDebt * rate, 0)),
                _ => state.CurrentDebt,
            };
        }

        state.LastInterestDayAt = now;
        SaveGameState(state);
    }

    // --- Cards (Shop + Fortune Deck shared inventory) ---
    public List<InventoryCard> GetInventory()
    {
        return InventoryCards.FindAll().ToList();
    }

    /// <summary>Copies of this card type currently in the shared pool, i.e. not already in the player's inventory.</summary>
    public int AvailableCopies(CardId id)
    {
        var owned = InventoryCards.FindAll().Count(c => c.CardId == id);
        return Math.Max(0, CardCatalog.BaseCopies(id) - owned);
    }

    /// <summary>Buys one copy of the card, charging its price, if the shared pool still has one available.</summary>
    public bool PurchaseCard(CardId id, GameSettings? settings = null)
    {
        if (AvailableCopies(id) <= 0)
        {
            return false;
        }

        settings ??= GetGameSettings();
        InventoryCards.Insert(new InventoryCard { Type = id.Type, Variant = id.Variant });
        ChargeExpense(CardCatalog.PriceOf(id, settings));
        return true;
    }

    /// <summary>Adds a copy to the inventory without charging anything (used by Fortune Deck collectible wins).</summary>
    public void GrantCard(CardId id)
    {
        InventoryCards.Insert(new InventoryCard { Type = id.Type, Variant = id.Variant });
    }

    /// <summary>Consumes one owned copy of the card, returning it to the shared pool. False if none was owned.</summary>
    public bool ConsumeCard(CardId id)
    {
        var owned = InventoryCards.FindAll().FirstOrDefault(c => c.CardId == id);
        if (owned is null)
        {
            return false;
        }

        InventoryCards.Delete(owned.Id);
        return true;
    }

    /// <summary>
    ///     Rolls a severity within the given infraction range, picks uniformly among Fine (always available) and
    ///     whichever other punishment kinds are enabled in <see cref="GameSettings" />, then applies it: Fine takes
    ///     effect immediately, the others become a pending <see cref="ActivePunishment" /> sized by severity.
    ///     Severity a capped kind cannot absorb is not lost: the residual is drawn again among all kinds (possibly the
    ///     same one) until it is spent. A residual too small to reach the drawn kind's minimum is charged as a fine.
    ///     The rolled severity is first multiplied by the variable-severity coefficient (see
    ///     <see cref="RollSeverityCoefficient" />), drawn once per assignment unless <paramref name="coefficient" />
    ///     supplies it.
    /// </summary>
    public void AssignPunishment((int Min, int Max) severityRange, string reason, double? coefficient = null)
    {
        var rolled = Random.Shared.Next(severityRange.Min, severityRange.Max + 1);
        var remaining = (int)Math.Round(
            rolled * (coefficient ?? RollSeverityCoefficient()), MidpointRounding.AwayFromZero);
        if (remaining <= 0)
        {
            return; // nothing to punish (e.g. 1 s late rounds to 0); sizing would still raise it to a kind's minimum
        }

        var settings = GetGameSettings();

        var candidates = new List<PunishmentKind?> { null }; // null = Fine
        if (settings.CornertimeEnabled)
        {
            candidates.Add(PunishmentKind.Cornertime);
        }

        if (settings.WritingLinesEnabled)
        {
            candidates.Add(PunishmentKind.WritingLines);
        }

        var enabledForcedOutfitPresets = GetForcedOutfitPresets().Where(p => p.IsEnabled).ToList();
        if (settings.ForcedOutfitEnabled && enabledForcedOutfitPresets.Count > 0)
        {
            candidates.Add(PunishmentKind.ForcedOutfit);
        }

        // Terminates: Fine is always a candidate and absorbs everything, and a capped piece consumes at least 1 point.
        var isResidual = false;
        do
        {
            var kind = candidates[Random.Shared.Next(candidates.Count)];
            var preset = kind is PunishmentKind.ForcedOutfit
                ? enabledForcedOutfitPresets[Random.Shared.Next(enabledForcedOutfitPresets.Count)]
                : null;
            var piece = kind switch
            {
                PunishmentKind.Cornertime => SizePiece(remaining, isResidual, settings.CornertimeSeverityValue,
                    settings.CornertimeMinRequiredSeconds, settings.CornertimeMaxRequiredSeconds),
                PunishmentKind.WritingLines => SizePiece(remaining, isResidual, settings.WritingLinesSeverityValue,
                    settings.WritingLinesMinLines, settings.WritingLinesMaxLines),
                PunishmentKind.ForcedOutfit => SizePiece(remaining, isResidual, preset!.HoursPerSeverityPoint * 3600,
                    ForcedOutfitMinRequiredHours * 3600, settings.ForcedOutfitMaxHours * 3600),
                _ => null,
            };

            if (kind is null || piece is null)
            {
                ApplyFine(remaining, settings);
                return;
            }

            var punishment = new ActivePunishment
            {
                Kind = kind.Value,
                Severity = piece.Value.Consumed,
                AssignedAt = DateTime.UtcNow,
                Reason = reason,
            };
            switch (kind)
            {
                case PunishmentKind.WritingLines:
                    var phraseCount = Random.Shared.Next(1, EmployerPhraseBank.All.Count + 1);
                    punishment.WritingLinesSentences = EmployerPhraseBank.GetRandomSet(phraseCount);
                    punishment.WritingLinesNoIdleActive =
                        settings.WritingLinesNoIdleEnabled && Random.Shared.NextDouble() < 0.5;
                    punishment.WritingLinesInterruptionsActive =
                        settings.WritingLinesInterruptionsEnabled && Random.Shared.NextDouble() < 0.5;
                    break;

                case PunishmentKind.ForcedOutfit:
                    punishment.ForcedClothingItemIds = preset!.ClothingItemIds;
                    punishment.ForceFullOutfit = preset.IsFullOutfit;
                    punishment.ForcedOutfitPresetName = preset.Name;
                    break;
            }

            SetQuantity(
                punishment,
                kind is PunishmentKind.ForcedOutfit ? piece.Value.Quantity : Math.Round(piece.Value.Quantity),
                settings);
            SaveActivePunishment(punishment);

            remaining -= piece.Value.Consumed;
            isResidual = true;
        } while (remaining > 0);
    }

    /// <summary>
    ///     Draws the coefficient (1 to <see cref="SeverityBalance.MaxCoefficient" />) that the employer's current
    ///     displeasure applies to a new infraction: overdue chores, refused garments, pending punishments and the
    ///     share of the debt still owed. Exactly 1 when she has nothing to reproach.
    /// </summary>
    public double RollSeverityCoefficient()
    {
        var state = GetGameState();
        if (state is null)
        {
            return 1;
        }

        var mood = SeverityBalance.Mood(
            SeverityBalance.AverageChoreLateness(GetChores(), state.StartedAt, DateTime.UtcNow),
            state.OutfitRefusals,
            GetActivePunishments().Count,
            state.CurrentDebt,
            state.InitialDebt);
        return SeverityBalance.Coefficient(mood, Random.Shared);
    }

    /// <summary>Counts garments the player explicitly refused (outfit regenerated or modified).</summary>
    public void RecordOutfitRefusal(int garments)
    {
        var state = GetGameState();
        if (state is null)
        {
            return;
        }

        // Capped so that a long streak of refusals is still forgiven in a handful of well-behaved days.
        state.OutfitRefusals = Math.Min(SeverityBalance.RefusalSaturation, state.OutfitRefusals + garments);
        state.OutfitRefusalDate = DateTime.UtcNow;
        SaveGameState(state);
    }

    /// <summary>
    ///     Halves the refusal count: the reward for accepting a proposed outfit untouched. Withheld on a day that
    ///     already recorded a refusal, however the player got back to the proposal (relaunch, back navigation).
    /// </summary>
    public void ForgiveOutfitRefusals()
    {
        var state = GetGameState();
        if (state is null || (state.OutfitRefusalDate is { } refusedAt && GameDay.IsSameDay(refusedAt, DateTime.UtcNow)))
        {
            return;
        }

        state.OutfitRefusals /= 2;
        SaveGameState(state);
    }

    /// <summary>
    ///     Sizes one capped punishment for <paramref name="severity" /> points at <paramref name="rate" /> units per
    ///     point: the quantity (clamped to the kind's limits) and how many points it absorbs — all of them, unless
    ///     the cap is hit, in which case only what fits (at least 1, so the caller's loop always progresses). Null
    ///     when a residual is too small to reach the kind's minimum, which the caller charges as a fine instead.
    /// </summary>
    private static (int Consumed, double Quantity)? SizePiece(
        int severity, bool isResidual, double rate, double min, double max)
    {
        var quantity = severity * rate;
        if (isResidual && quantity < min)
        {
            return null;
        }

        var consumed = quantity > max ? Math.Max(1, (int)(max / rate)) : severity;
        return (consumed, Math.Clamp(quantity, min, max));
    }

    /// <summary>Writes the quantity that represents the punishment's size: seconds for Cornertime and Forced Outfit,
    /// lines for Writing Lines.</summary>
    private static void SetQuantity(ActivePunishment punishment, double quantity, GameSettings settings)
    {
        switch (punishment.Kind)
        {
            case PunishmentKind.Cornertime:
                punishment.CornertimeRequiredSeconds = (int)quantity;
                punishment.CornertimeMaxSeconds = (int)quantity + settings.CornertimeMaxOverrunSeconds;
                break;

            case PunishmentKind.WritingLines:
                punishment.WritingLinesTotalLines = (int)quantity;
                break;

            case PunishmentKind.ForcedOutfit:
                punishment.ForcedOutfitRequiredSeconds = quantity;
                break;
        }
    }

    /// <summary>Halves the target punishment's severity, rounding up. Multiple calls stack multiplicatively.</summary>
    public bool SoftenPunishment(int punishmentId)
    {
        return ScalePunishment(punishmentId, 0.5);
    }

    /// <summary>
    ///     Fortune Deck's negative mirror of <see cref="SoftenPunishment" />: multiplies severity by 1.5×,
    ///     rounding up. What exceeds the kind's configured maximum spills into a new punishment of the same kind.
    /// </summary>
    public bool AggravatePunishment(int punishmentId)
    {
        return ScalePunishment(punishmentId, 1.5);
    }

    public bool CancelPunishment(int punishmentId)
    {
        var punishment = ActivePunishments.FindById(punishmentId);
        if (punishment is null || punishment.IsResolved)
        {
            return false;
        }

        punishment.IsResolved = true;
        SaveActivePunishment(punishment);
        return true;
    }

    /// <summary>
    ///     Scales the quantity that represents the punishment's severity (writing lines, cornertime seconds, or
    ///     forced-outfit required seconds — always recomputed from the original total, never from what's left) by
    ///     <paramref name="factor" />, rounding up and re-clamping to the kind's configured limits, and scales
    ///     <see cref="ActivePunishment.Severity" /> (the failure fine) along with it. Whatever exceeds the kind's
    ///     maximum spills into a new pending punishment of the same kind and configuration — or into a fine, when it
    ///     is too small to reach the kind's minimum — taking its proportional share of the severity. A forced-outfit
    ///     punishment whose recalculated total falls at or below the time already credited is marked resolved.
    ///     Never touches the outfit already in effect for the current session (see docs/specs/shop.md
    ///     "Forced-Outfit Timing Rules") — StartSessionViewModel only reads this state at the next session's start.
    /// </summary>
    private bool ScalePunishment(int punishmentId, double factor)
    {
        var punishment = ActivePunishments.FindById(punishmentId);
        if (punishment is null || punishment.IsResolved)
        {
            return false;
        }

        var settings = GetGameSettings();
        double scaled, min, max;
        switch (punishment.Kind)
        {
            case PunishmentKind.Cornertime when punishment.CornertimeRequiredSeconds is { } seconds:
                scaled = Math.Ceiling(seconds * factor);
                min = settings.CornertimeMinRequiredSeconds;
                max = settings.CornertimeMaxRequiredSeconds;
                break;

            case PunishmentKind.WritingLines when punishment.WritingLinesTotalLines is { } lines:
                scaled = Math.Ceiling(lines * factor);
                min = settings.WritingLinesMinLines;
                max = settings.WritingLinesMaxLines;
                break;

            case PunishmentKind.ForcedOutfit when punishment.ForcedOutfitRequiredSeconds is { } requiredSeconds:
                scaled = requiredSeconds * factor;
                min = ForcedOutfitMinRequiredHours * 3600;
                max = settings.ForcedOutfitMaxHours * 3600;
                break;

            default:
                return false;
        }

        var severity = (int)Math.Ceiling(punishment.Severity * factor);

        // ponytail: one spill piece is enough because "target quantity ≤ cap" is an invariant (assignment, cards and
        // in-execution penalties all respect the cap), so the excess never exceeds the cap itself. Loop here if a
        // larger factor or an uncapped penalty ever breaks that.
        var excess = scaled - max;
        if (excess > 0)
        {
            // Split by quantity share, not by the kind's global rate: the target's own rate may differ (raised to
            // the minimum, grown by penalty lines, per-preset hours).
            var spillSeverity = Math.Max(1, (int)Math.Round(severity * excess / scaled));
            severity = Math.Max(1, severity - spillSeverity);

            if (excess < min)
            {
                ApplyFine(spillSeverity, settings);
            }
            else
            {
                var spill = new ActivePunishment
                {
                    Kind = punishment.Kind,
                    Severity = spillSeverity,
                    AssignedAt = DateTime.UtcNow,
                    Reason = punishment.Reason,
                    WritingLinesSentences = punishment.WritingLinesSentences,
                    WritingLinesNoIdleActive = punishment.WritingLinesNoIdleActive,
                    WritingLinesInterruptionsActive = punishment.WritingLinesInterruptionsActive,
                    ForcedClothingItemIds = punishment.ForcedClothingItemIds,
                    ForceFullOutfit = punishment.ForceFullOutfit,
                    ForcedOutfitPresetName = punishment.ForcedOutfitPresetName,
                };
                SetQuantity(spill, excess, settings);
                SaveActivePunishment(spill);
            }
        }

        punishment.Severity = severity;
        SetQuantity(punishment, Math.Clamp(scaled, min, max), settings);
        if (punishment.Kind == PunishmentKind.ForcedOutfit
            && punishment.ForcedOutfitElapsedSeconds >= punishment.ForcedOutfitRequiredSeconds)
        {
            punishment.IsResolved = true;
        }

        SaveActivePunishment(punishment);
        return true;
    }

    // --- Permissions (break requests) ---
    public ActivePermissionBreak? GetActivePermissionBreak()
    {
        return ActivePermissionBreakCollection.FindById(1);
    }

    /// <summary>
    ///     Resolves a permission request: automatically denied (with a mocking message, cooldown still applied) when
    ///     Diaper Play is active for a toilet-related kind, then denied-for-insistence while a cooldown is running
    ///     (unless bypassed), otherwise a probability roll against the kind's configured grant chance, adjusted by
    ///     <paramref name="mods" /> and any suspended Fortune Deck modifiers targeting this kind (which are consumed
    ///     here regardless of outcome). Granting starts an <see cref="ActivePermissionBreak" />; the caller is
    ///     responsible for pausing/resuming <c>SessionLoopService</c> around it.
    /// </summary>
    public PermissionRequestResult RequestPermission(PermissionKind kind, PermissionModifiers mods = default)
    {
        mods = mods.CombinedWith(ConsumeSuspendedModifiers(kind));

        var config = GetPermissionTypes().FirstOrDefault(c => c.Kind == kind) ?? new PermissionTypeConfig { Kind = kind };

        var isToiletKind = kind is PermissionKind.Pee or PermissionKind.Poop;
        if (isToiletKind && GetGameState() is { DiaperPlayActiveThisSession: true })
        {
            SetPermissionCooldown(kind, config.CooldownMinutes);
            return new PermissionRequestResult(PermissionRequestOutcome.DeniedDiaper, PermissionPhraseBank.GetRandomDeniedDiaper());
        }

        var cooldown = PermissionCooldowns.FindById((int)kind);
        if (!mods.BypassCooldown && cooldown is not null && cooldown.CooldownUntil > DateTime.UtcNow)
        {
            SetPermissionCooldown(kind, config.CooldownMinutes);
            return new PermissionRequestResult(
                PermissionRequestOutcome.DeniedInsistence,
                PermissionPhraseBank.GetRandomDeniedInsistence());
        }

        var grantPercent = mods.GuaranteedGrant
            ? 100
            : mods.ProbabilityBonusPercent == 0
                ? config.BaseGrantPercent
                : Math.Clamp(config.BaseGrantPercent + mods.ProbabilityBonusPercent, 0, 95);

        if (Random.Shared.Next(100) < grantPercent)
        {
            var now = DateTime.UtcNow;
            var duration = Math.Max(0, config.DurationMinutes + mods.DurationBonusMinutes);
            ActivePermissionBreakCollection.Upsert(new ActivePermissionBreak
            {
                Kind = kind,
                StartedAt = now,
                MaxEndAt = now.AddMinutes(duration),
            });
            return new PermissionRequestResult(PermissionRequestOutcome.Granted, PermissionPhraseBank.GetRandomGranted());
        }

        SetPermissionCooldown(kind, config.CooldownMinutes);
        return new PermissionRequestResult(PermissionRequestOutcome.Denied, PermissionPhraseBank.GetRandomDenied());
    }

    // --- Suspended modifiers (Fortune Deck permission-targeting buffs/debuffs) ---
    public void AddSuspendedModifier(PermissionKind kind, SuspendedEffect effect, int value)
    {
        SuspendedModifiers.Insert(new SuspendedModifier { Kind = kind, Effect = effect, Value = value });
    }

    /// <summary>Removes and folds together every suspended modifier targeting the given kind.</summary>
    private PermissionModifiers ConsumeSuspendedModifiers(PermissionKind kind)
    {
        var matching = SuspendedModifiers.FindAll().Where(m => m.Kind == kind).ToList();
        if (matching.Count == 0)
        {
            return PermissionModifiers.None;
        }

        var combined = PermissionModifiers.None;
        foreach (var modifier in matching)
        {
            combined = combined.CombinedWith(modifier.Effect switch
            {
                SuspendedEffect.Probability => new PermissionModifiers(false, modifier.Value, 0, false),
                SuspendedEffect.Duration => new PermissionModifiers(false, 0, modifier.Value, false),
                SuspendedEffect.Grant => new PermissionModifiers(false, 0, 0, true),
                _ => PermissionModifiers.None,
            });
            SuspendedModifiers.Delete(modifier.Id);
        }

        return combined;
    }

    /// <summary>
    ///     Ends the active break. If it ran past its granted duration, assigns a punishment sized like popup
    ///     lateness (<see cref="InfractionSeverity.PopupLateSeverityPerSecond" /> per second overrun).
    /// </summary>
    public void EndPermissionBreak()
    {
        var active = GetActivePermissionBreak();
        if (active is null)
        {
            return;
        }

        var overrunSeconds = (DateTime.UtcNow - active.MaxEndAt).TotalSeconds;
        if (overrunSeconds > 0)
        {
            var severity = (int)Math.Round(overrunSeconds * InfractionSeverity.PopupLateSeverityPerSecond);
            if (severity > 0)
            {
                AssignPunishment((severity, severity), "PunishmentReasonPermissionOverrun");
            }
        }

        ActivePermissionBreakCollection.Delete(1);
    }

    /// <summary>Rolls a cooldown duration within ±25% of the configured base and stores it for the kind.</summary>
    private void SetPermissionCooldown(PermissionKind kind, int baseCooldownMinutes)
    {
        var factor = 0.75 + Random.Shared.NextDouble() * 0.5;
        PermissionCooldowns.Upsert(new PermissionCooldown
        {
            Id = (int)kind,
            Kind = kind,
            CooldownUntil = DateTime.UtcNow.AddMinutes(baseCooldownMinutes * factor),
        });
    }

    // --- Sessions ---
    public void AddSession(Session session)
    {
        Sessions.Insert(session);
    }

    public void UpdateSession(Session session)
    {
        Sessions.Upsert(session);
    }

    public List<Session> GetSessions()
    {
        return Sessions.FindAll().ToList();
    }

    /// Fills a new, empty save database by snapshotting the current profile data. The caller owns
    /// <paramref name="saveDb" /> (in the app, <c>MainWindowViewModel.CreateSaveSlot</c>, which tracks and closes it).
    /// Profile changes after this point will not affect the save.
    public static SaveRepository CreateFromProfile(
        ProfileRepository profile,
        LiteDatabase saveDb,
        string slotName,
        decimal initialDebt)
    {
        var save = new SaveRepository(saveDb);

        // Snapshot wardrobe
        var wardrobeSnapshot = profile.GetWardrobe();
        var wardrobeCol = saveDb.GetCollection<ClothingItem>("wardrobe");
        wardrobeCol.InsertBulk(wardrobeSnapshot);

        // Snapshot outfit rules
        var rulesCol = saveDb.GetCollection<OutfitRule>("outfit_rules_v2");
        rulesCol.InsertBulk(profile.GetOutfitRules());

        // Snapshot chores
        var choresCol = saveDb.GetCollection<Chore>("chores");
        choresCol.InsertBulk(profile.GetChores());

        // Snapshot plug and play item configs
        var plugAndPlayCol = saveDb.GetCollection<PlugAndPlayItemConfig>("plug_and_play_items");
        plugAndPlayCol.InsertBulk(profile.GetPlugAndPlayItems());

        // Snapshot diaper play item configs
        var diaperPlayCol = saveDb.GetCollection<DiaperPlayItemConfig>("diaper_play_items");
        diaperPlayCol.InsertBulk(profile.GetDiaperPlayItems());

        // Snapshot chastity device item configs
        var chastityDeviceCol = saveDb.GetCollection<ChastityDeviceItemConfig>("chastity_device_items");
        chastityDeviceCol.InsertBulk(profile.GetChastityDeviceItems());

        // Snapshot forced outfit presets
        var forcedOutfitPresetsCol = saveDb.GetCollection<ForcedOutfitPreset>("forced_outfit_presets");
        forcedOutfitPresetsCol.InsertBulk(profile.GetForcedOutfitPresets());

        // Snapshot permission type configs
        var permissionTypesCol = saveDb.GetCollection<PermissionTypeConfig>("permission_types");
        permissionTypesCol.InsertBulk(profile.GetPermissionTypes());

        // Snapshot game settings
        var settings = profile.GetGameSettings();
        var settingsCol = saveDb.GetCollection<GameSettings>("game_settings");
        settingsCol.Upsert(settings);

        // Create initial game state
        save.SaveGameState(new GameState
        {
            SlotName = slotName,
            InitialDebt = initialDebt,
            CurrentDebt = initialDebt,
            DailyInterestRate = settings.DailyInterestRate,
            StartedAt = DateTime.UtcNow,
            LastSessionAt = DateTime.UtcNow,
            InitialWardrobeWeightTotal = wardrobeSnapshot.Sum(i => i.Weight),
        });

        return save;
    }
}
