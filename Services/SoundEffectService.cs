using Avalonia.Platform;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage.Streams;

namespace ChastiMaid.Services;

/// <summary>
///     Plays short cue sounds bundled under Assets/Sounds, using the same <see cref="MediaPlayer" />
///     (WinRT) playback approach the app already relies on for audio. Playbacks are tracked in a set
///     rather than a single field so overlapping cues (e.g. the fortune-deck shuffle firing once per
///     round) don't cut each other off.
/// </summary>
public static class SoundEffectService
{
    private static readonly HashSet<MediaPlayer> ActivePlayers = [];

    public static void PlayDeskBell()
    {
        _ = PlayAsync("desk-bell.wav");
    }

    public static void PlayHandBell()
    {
        _ = PlayAsync("hand-bell.wav");
    }

    public static void PlayShufflingCards()
    {
        _ = PlayAsync("shuffling-playing-cards.wav");
    }

    public static void PlayFanOut()
    {
        _ = PlayAsync("fan-out.wav");
    }

    public static void PlayBuzz()
    {
        _ = PlayAsync("buzz.wav");
    }

    public static void PlayClock()
    {
        _ = PlayAsync("clock.wav");
    }

    private static async Task PlayAsync(string fileName)
    {
        try
        {
            var uri = new Uri($"avares://ChastiMaid/Assets/Sounds/{fileName}");
            await using var assetStream = AssetLoader.Open(uri);

            var stream = new InMemoryRandomAccessStream();
            var writer = new DataWriter(stream);
            using (var ms = new MemoryStream())
            {
                await assetStream.CopyToAsync(ms);
                writer.WriteBytes(ms.ToArray());
            }

            await WinRtAsyncExtensions.ToTask(writer.StoreAsync());
            writer.DetachStream();
            writer.Dispose();
            stream.Seek(0);

            var player = new MediaPlayer { Source = MediaSource.CreateFromStream(stream, "audio/wav") };
            ActivePlayers.Add(player);

            player.MediaEnded += (_, _) => Cleanup(player, stream);
            player.MediaFailed += (_, _) => Cleanup(player, stream);
            player.Play();
        }
        catch
        {
            /* best-effort cue only */
        }
    }

    private static void Cleanup(MediaPlayer player, InMemoryRandomAccessStream stream)
    {
        player.Dispose();
        stream.Dispose();
        ActivePlayers.Remove(player);
    }
}
