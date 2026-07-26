using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage.Streams;

namespace ChastiMaid.Services;

/// <summary>
///     Plays a short two-tone chime to mark the end of a punishment — audible even to a user facing
///     away from the screen. The tone is generated in memory as raw PCM/WAV (no bundled audio asset,
///     no licensing question) and played with the same <see cref="MediaPlayer" /> already used by
///     <see cref="SpeechSynthesisService" />.
/// </summary>
public static class NotificationSoundService
{
    private const int SampleRate = 44100;

    // Keeps the player (and the stream it reads from) rooted until playback finishes — PlayChime()
    // returns immediately, so nothing else would otherwise keep them alive for the GC.
    private static MediaPlayer? _activePlayer;
    private static InMemoryRandomAccessStream? _activeStream;

    public static void PlayChime()
    {
        _ = PlayChimeAsync();
    }

    private static async Task PlayChimeAsync()
    {
        try
        {
            var stream = new InMemoryRandomAccessStream();
            var writer = new DataWriter(stream);
            writer.WriteBytes(BuildChimeWav());
            await WinRtAsyncExtensions.ToTask(writer.StoreAsync());
            writer.DetachStream();
            writer.Dispose();
            stream.Seek(0);

            var player = new MediaPlayer { Source = MediaSource.CreateFromStream(stream, "audio/wav") };
            _activePlayer = player;
            _activeStream = stream;

            player.MediaEnded += (_, _) => Cleanup(player, stream);
            player.MediaFailed += (_, _) => Cleanup(player, stream);
            player.Play();
        }
        catch
        {
            /* best-effort notification only */
        }
    }

    private static void Cleanup(MediaPlayer player, InMemoryRandomAccessStream stream)
    {
        player.Dispose();
        stream.Dispose();
        if (ReferenceEquals(_activePlayer, player))
        {
            _activePlayer = null;
            _activeStream = null;
        }
    }

    private static byte[] BuildChimeWav()
    {
        var tone1 = GenerateTone(880.0, 0.15); // A5
        var tone2 = GenerateTone(1318.5, 0.20); // E6 — bright ascending "ding-dong"
        return WrapPcmAsWav(tone1.Concat(tone2).ToArray());
    }

    private static short[] GenerateTone(double frequencyHz, double durationSeconds)
    {
        var sampleCount = (int)(SampleRate * durationSeconds);
        var fadeSamples = (int)(SampleRate * 0.02); // 20ms fade in/out to avoid clicks
        var samples = new short[sampleCount];
        for (var i = 0; i < sampleCount; i++)
        {
            var t = i / (double)SampleRate;
            var envelope = Math.Min(1.0, Math.Min(i, sampleCount - i) / (double)fadeSamples);
            samples[i] = (short)(Math.Sin(2 * Math.PI * frequencyHz * t) * envelope * short.MaxValue * 0.5);
        }

        return samples;
    }

    private static byte[] WrapPcmAsWav(short[] samples)
    {
        const int bitsPerSample = 16;
        const int channels = 1;
        var dataSize = samples.Length * sizeof(short);
        var byteRate = SampleRate * channels * bitsPerSample / 8;
        var blockAlign = channels * bitsPerSample / 8;

        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);

        w.Write("RIFF"u8.ToArray());
        w.Write(36 + dataSize);
        w.Write("WAVE"u8.ToArray());
        w.Write("fmt "u8.ToArray());
        w.Write(16);
        w.Write((short)1); // PCM
        w.Write((short)channels);
        w.Write(SampleRate);
        w.Write(byteRate);
        w.Write((short)blockAlign);
        w.Write((short)bitsPerSample);
        w.Write("data"u8.ToArray());
        w.Write(dataSize);
        foreach (var sample in samples)
        {
            w.Write(sample);
        }

        return ms.ToArray();
    }
}
