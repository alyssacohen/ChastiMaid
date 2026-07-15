using System.Runtime.CompilerServices;
using ChastiMaid.Services;
using LiteDB;

namespace ChastiMaid.Tests.Services;

public class DatabaseServiceDateTimeTests
{
    private class Probe
    {
        [BsonId] public int Id { get; set; } = 1;
        public DateTime StoredUtc { get; set; }
    }

    [Fact]
    public void Utc_DateTime_Round_Trips_Through_LiteDb_Without_Timezone_Drift()
    {
        // Forces DatabaseService's static constructor, which registers the BsonMapper fix this
        // test guards against regressing (typeof(...) alone does not trigger it).
        RuntimeHelpers.RunClassConstructor(typeof(DatabaseService).TypeHandle);

        using var stream = new MemoryStream();
        var original = DateTime.UtcNow.AddMinutes(2);
        using (var db = new LiteDatabase(stream))
        {
            db.GetCollection<Probe>("probe").Insert(new Probe { StoredUtc = original });
        }

        stream.Position = 0;
        using var readDb = new LiteDatabase(stream);
        var readBack = readDb.GetCollection<Probe>("probe").FindById(1)!;

        Assert.Equal(DateTimeKind.Utc, readBack.StoredUtc.Kind);
        Assert.Equal(original, readBack.StoredUtc, TimeSpan.FromMilliseconds(1));
    }
}
