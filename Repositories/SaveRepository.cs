using ChastiMaid.Models;
using ChastiMaid.Services;
using LiteDB;

namespace ChastiMaid.Repositories;

public class SaveRepository(LiteDatabase db)
{
    private ILiteCollection<ClothingItem> Wardrobe => db.GetCollection<ClothingItem>("wardrobe");
    private ILiteCollection<OutfitRule> OutfitRules => db.GetCollection<OutfitRule>("outfit_rules");
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

    private ILiteCollection<ActivePlugAndPlayAccessory> ActivePlugAndPlayAccessoryCollection =>
        db.GetCollection<ActivePlugAndPlayAccessory>("active_plug_and_play_accessory");

    private ILiteCollection<ForcedOutfitPreset> ForcedOutfitPresets =>
        db.GetCollection<ForcedOutfitPreset>("forced_outfit_presets");

    // --- GameState ---
    public GameState? GetGameState()
    {
        return GameStates.FindById(1);
    }

    public void SaveGameState(GameState state)
    {
        GameStates.Upsert(state);
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

    // --- Wardrobe weight adjustments ---
    public void AcceptItems(IEnumerable<ClothingItem> items)
    {
        foreach (var item in items)
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
        foreach (var item in items)
        {
            item.Weight += WardrobeBalance.RejectWeightIncrement;
            item.RejectionCount++;
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

    // --- Active punishments ---
    public List<ActivePunishment> GetActivePunishments()
    {
        return ActivePunishments.Find(p => !p.IsResolved).ToList();
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
        var state = GetGameState();
        if (state is null)
        {
            return;
        }

        var fineAmount = Math.Round(severity * settings.FineSeverityValue, 0);
        state.CurrentDebt += fineAmount;
        state.InitialDebt += fineAmount;
        SaveGameState(state);
    }

    /// <summary>
    ///     Rolls a severity within the given infraction range, picks uniformly among Fine (always available) and
    ///     whichever other punishment kinds are enabled in <see cref="GameSettings" />, then applies it: Fine takes
    ///     effect immediately, the others become a pending <see cref="ActivePunishment" /> sized by severity.
    /// </summary>
    public void AssignPunishment((int Min, int Max) severityRange, string reason)
    {
        var severity = Random.Shared.Next(severityRange.Min, severityRange.Max + 1);
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

        var kind = candidates[Random.Shared.Next(candidates.Count)];
        switch (kind)
        {
            case null:
                ApplyFine(severity, settings);
                break;

            case PunishmentKind.Cornertime:
                var required = Math.Max(
                    (int)Math.Round(severity * settings.CornertimeSeverityValue),
                    settings.CornertimeMinRequiredSeconds);
                SaveActivePunishment(new ActivePunishment
                {
                    Kind = PunishmentKind.Cornertime,
                    Severity = severity,
                    AssignedAt = DateTime.UtcNow,
                    Reason = reason,
                    CornertimeRequiredSeconds = required,
                    CornertimeMaxSeconds = required + settings.CornertimeMaxOverrunSeconds,
                });
                break;

            case PunishmentKind.WritingLines:
                var phraseCount = Random.Shared.Next(1, EmployerPhraseBank.AllKeys.Count + 1);
                SaveActivePunishment(new ActivePunishment
                {
                    Kind = PunishmentKind.WritingLines,
                    Severity = severity,
                    AssignedAt = DateTime.UtcNow,
                    Reason = reason,
                    WritingLinesSentenceKeys = EmployerPhraseBank.GetRandomSet(phraseCount),
                    WritingLinesTotalLines = Math.Clamp(
                        (int)Math.Round(severity * settings.WritingLinesSeverityValue),
                        settings.WritingLinesMinLines,
                        settings.WritingLinesMaxLines),
                    WritingLinesAttemptActive = false,
                });
                break;

            case PunishmentKind.ForcedOutfit:
                var preset = enabledForcedOutfitPresets[Random.Shared.Next(enabledForcedOutfitPresets.Count)];
                SaveActivePunishment(new ActivePunishment
                {
                    Kind = PunishmentKind.ForcedOutfit,
                    Severity = severity,
                    AssignedAt = DateTime.UtcNow,
                    Reason = reason,
                    ForcedOutfitRemainingSessions =
                        Math.Max(1, (int)Math.Round(severity * preset.SessionsPerSeverityPoint)),
                    ForcedClothingItemIds = preset.ClothingItemIds,
                    ForceFullOutfit = preset.IsFullOutfit,
                    ForcedOutfitPresetName = preset.Name,
                });
                break;
        }
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

    /// Creates a new save slot by snapshotting the current profile data.
    /// Profile changes after this point will not affect the save.
    public static SaveRepository CreateFromProfile(
        ProfileRepository profile,
        DatabaseService dbService,
        string slotName,
        decimal initialDebt)
    {
        var saveDb = dbService.OpenSave(slotName);
        var save = new SaveRepository(saveDb);

        // Snapshot wardrobe
        var wardrobeCol = saveDb.GetCollection<ClothingItem>("wardrobe");
        wardrobeCol.InsertBulk(profile.GetWardrobe());

        // Snapshot outfit rules
        var rulesCol = saveDb.GetCollection<OutfitRule>("outfit_rules");
        rulesCol.InsertBulk(profile.GetOutfitRules());

        // Snapshot chores
        var choresCol = saveDb.GetCollection<Chore>("chores");
        choresCol.InsertBulk(profile.GetChores());

        // Snapshot plug and play item configs
        var plugAndPlayCol = saveDb.GetCollection<PlugAndPlayItemConfig>("plug_and_play_items");
        plugAndPlayCol.InsertBulk(profile.GetPlugAndPlayItems());

        // Snapshot forced outfit presets
        var forcedOutfitPresetsCol = saveDb.GetCollection<ForcedOutfitPreset>("forced_outfit_presets");
        forcedOutfitPresetsCol.InsertBulk(profile.GetForcedOutfitPresets());

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
        });

        return save;
    }
}
