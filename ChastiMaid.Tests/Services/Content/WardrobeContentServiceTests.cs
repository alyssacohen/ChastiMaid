using ChastiMaid.Models;
using ChastiMaid.Services.Content;

namespace ChastiMaid.Tests.Services.Content;

public class WardrobeContentServiceTests : IDisposable
{
    private readonly string _tempFile = Path.GetTempFileName();
    private readonly WardrobeContentService _service = new();

    public void Dispose()
    {
        File.Delete(_tempFile);
    }

    [Fact]
    public void ExportThenLoad_RoundTripsEqualItems()
    {
        var items = new List<ClothingItem>
        {
            new() { Id = 1, Name = "test dress", Layers = [3], Slots = ["torso"], Multiplier = 1.2, Weight = 0.8, Tags = ["tag"] },
        };

        _service.Export(items, _tempFile);
        var loaded = _service.Load(_tempFile);

        var item = Assert.Single(loaded);
        Assert.Equal(1, item.Id);
        Assert.Equal("test dress", item.Name);
        Assert.Equal([3], item.Layers);
        Assert.Equal(["torso"], item.Slots);
        Assert.Equal(1.2, item.Multiplier);
        Assert.Equal(0.8, item.Weight);
        Assert.Equal(["tag"], item.Tags);
    }

    [Fact]
    public void Export_DoesNotSerializeRejectionCount()
    {
        _service.Export([new ClothingItem { Id = 1, Name = "x", RejectionCount = 42 }], _tempFile);

        Assert.DoesNotContain("rejectionCount", File.ReadAllText(_tempFile));
    }

    [Fact]
    public void Load_ResetsRejectionCountRegardlessOfFileContent()
    {
        File.WriteAllText(_tempFile, """[{"id":1,"name":"x","rejectionCount":99}]""");

        var loaded = _service.Load(_tempFile);

        Assert.Equal(0, loaded[0].RejectionCount);
    }
}
