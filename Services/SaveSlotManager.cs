using ChastiMaid.Models;

namespace ChastiMaid.Services;

public record SaveSlotInfo(
    string SlotName,
    DateTime StartedAt,
    DateTime LastSessionAt,
    decimal CurrentDebt,
    bool IsCompleted,
    bool IsSafeLocked);

public class SaveSlotManager(DatabaseService dbService)
{
    public List<SaveSlotInfo> ListSlots()
    {
        var slots = new List<SaveSlotInfo>();
        foreach (var slotName in dbService.ListSaveSlots())
        {
            try
            {
                using var saveDb = dbService.OpenSave(slotName);
                var state = saveDb.GetCollection<GameState>("game_state").FindById(1);
                if (state is null)
                {
                    continue;
                }

                slots.Add(new SaveSlotInfo(slotName, state.StartedAt, state.LastSessionAt, state.CurrentDebt,
                    state.IsCompleted, state.IsSafeLocked));
            }
            catch
            {
                // Corrupt or incompatible save file — skip
            }
        }

        return slots;
    }

    public void DeleteSlot(string slotName)
    {
        dbService.DeleteSave(slotName);

        // Also delete the media folder (outfit video, safe combo, etc.)
        var mediaDir = Path.Combine(AppPaths.Root, "data", "saves", slotName);
        if (Directory.Exists(mediaDir))
        {
            Directory.Delete(mediaDir, true);
        }
    }
}
