using ChastiMaid.Models;

namespace ChastiMaid.Services;

public record SaveSlotInfo(string SlotName, DateTime StartedAt, DateTime LastSessionAt, decimal CurrentDebt, bool IsCompleted);

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
                if (state is null) continue;
                slots.Add(new SaveSlotInfo(slotName, state.StartedAt, state.LastSessionAt, state.CurrentDebt, state.IsCompleted));
            }
            catch
            {
                // File corrotto o incompatibile — ignorato
            }
        }
        return slots;
    }

    public void DeleteSlot(string slotName) => dbService.DeleteSave(slotName);
}
