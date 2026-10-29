using ChastiMaid.Services;

namespace ChastiMaid.Tests.Services;

public class SaveSlotManagerTests
{
    private static readonly DateTime Now = new(2026, 10, 29, 15, 30, 12);

    [Fact]
    public void NewSlotName_FreeMinute_KeepsThePlainFormat()
    {
        Assert.Equal("save_20261029_1530", SaveSlotManager.NewSlotName(Now, ["save_20261029_1529"]));
    }

    [Fact]
    public void NewSlotName_TakenMinute_AddsTheFirstFreeSuffix()
    {
        string[] existing = ["save_20261029_1530", "save_20261029_1530_2"];

        Assert.Equal("save_20261029_1530_3", SaveSlotManager.NewSlotName(Now, existing));
    }

    [Fact]
    public void NewSlotName_IgnoresCase_LikeWindowsFileNames()
    {
        Assert.Equal("save_20261029_1530_2", SaveSlotManager.NewSlotName(Now, ["SAVE_20261029_1530"]));
    }
}
