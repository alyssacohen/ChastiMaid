using ChastiMaid.Models;
using ChastiMaid.Services.Content;

namespace ChastiMaid.Tests.Services.Content;

public class TaskContentServiceTests : IDisposable
{
    private readonly string _tempFile = Path.GetTempFileName();
    private readonly TaskContentService _service = new();

    public void Dispose()
    {
        File.Delete(_tempFile);
    }

    [Fact]
    public void ExportThenLoad_RoundTripsEqualChores()
    {
        var chores = new List<Chore>
        {
            new() { Id = 1, Name = "Dusting", Description = "desc", Value = 10, FrequencyDays = 1, MinCompletionMinutes = 5 },
        };

        _service.Export(chores, _tempFile);
        var loaded = _service.Load(_tempFile);

        var chore = Assert.Single(loaded);
        Assert.Equal("Dusting", chore.Name);
        Assert.Equal("desc", chore.Description);
        Assert.Equal(10, chore.Value);
        Assert.Null(chore.LastCompletedAt);
    }

    [Fact]
    public void Load_IgnoresLastCompletedAtEvenIfPresentInFile()
    {
        File.WriteAllText(_tempFile, """[{"id":1,"name":"x","lastCompletedAt":"2026-01-01T00:00:00"}]""");

        var loaded = _service.Load(_tempFile);

        Assert.Null(loaded[0].LastCompletedAt);
    }
}
