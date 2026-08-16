using System.Globalization;
using NAudio.Wave;
using SherpaOnnx;

namespace ChastiMaid.Services;

/// <summary>
///     Speaks text aloud using sherpa-onnx's offline Piper VITS models — Paola (it) and Amy (en), both female
///     voices, one loaded lazily per language on first use and kept for the lifetime of the service (see
///     <see cref="Dispose" />). Playback goes through NAudio. Concurrent calls are serialized: two overlapping
///     narration lines (e.g. from <c>CornertimePunishmentViewModel</c>'s narration timer) would otherwise hit
///     the same native <see cref="OfflineTts" /> instance at once.
///     <para>
///         Failures (model missing/corrupt, no output device, playback error) are swallowed — callers already
///         show the spoken text on screen, so silence is an acceptable fallback, never a crash.
///     </para>
/// </summary>
public sealed class SherpaOnnxTtsService : ITtsService, IDisposable
{
    private static readonly string ContentRoot =
        Path.Combine(AppContext.BaseDirectory, "Content", "Speech", "TTS");

    private readonly SemaphoreSlim _speakLock = new(1, 1);
    private readonly Lock _loadLock = new();
    private readonly Dictionary<string, OfflineTts?> _ttsByLanguage = new();
    private bool _disposed;

    public async Task SpeakAsync(string text, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        await _speakLock.WaitAsync(ct);
        try
        {
            var tts = GetOrLoadTts(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
            if (tts is null)
            {
                return;
            }

            OfflineTtsGeneratedAudio? audio = null;
            try
            {
                audio = await Task.Run(() =>
                {
                    var genConfig = new OfflineTtsGenerationConfig { Sid = 0, Speed = 1.0f, SilenceScale = 0.2f };
                    return tts.GenerateWithConfig(text, genConfig, null);
                }, ct);

                await PlayAsync(audio.Samples, audio.SampleRate, ct);
            }
            catch
            {
                /* best-effort — text already shown on screen */
            }
            finally
            {
                audio?.Dispose();
            }
        }
        finally
        {
            _speakLock.Release();
        }
    }

    private OfflineTts? GetOrLoadTts(string language)
    {
        var lang = language.Equals("it", StringComparison.OrdinalIgnoreCase) ? "it" : "en";

        lock (_loadLock)
        {
            if (_ttsByLanguage.TryGetValue(lang, out var cached))
            {
                return cached;
            }

            OfflineTts? tts = null;
            try
            {
                var dir = Path.Combine(ContentRoot, lang);
                var modelFile = lang == "it" ? "it_IT-paola-medium.onnx" : "en_US-amy-medium.onnx";
                var modelPath = Path.Combine(dir, modelFile);
                var tokensPath = Path.Combine(dir, "tokens.txt");
                var dataDir = Path.Combine(dir, "espeak-ng-data");

                if (File.Exists(modelPath) && File.Exists(tokensPath) && Directory.Exists(dataDir))
                {
                    var config = new OfflineTtsConfig();
                    config.Model.Vits.Model = modelPath;
                    config.Model.Vits.Tokens = tokensPath;
                    config.Model.Vits.DataDir = dataDir;
                    config.Model.Vits.NoiseScale = 0.667f;
                    config.Model.Vits.NoiseScaleW = 0.8f;
                    config.Model.Vits.LengthScale = 1.0f;
                    config.Model.NumThreads = 1;
                    config.Model.Provider = "cpu";
                    config.MaxNumSentences = 1;

                    tts = new OfflineTts(config);
                }
            }
            catch
            {
                tts = null;
            }

            _ttsByLanguage[lang] = tts;
            return tts;
        }
    }

    private static async Task PlayAsync(float[] samples, int sampleRate, CancellationToken ct)
    {
        using var waveOut = new WaveOut();
        waveOut.Init(new FloatArraySampleProvider(samples, sampleRate));

        var tcs = new TaskCompletionSource();
        EventHandler<StoppedEventArgs> onStopped = null!;
        onStopped = (_, _) =>
        {
            waveOut.PlaybackStopped -= onStopped;
            tcs.TrySetResult();
        };
        waveOut.PlaybackStopped += onStopped;

        using var registration = ct.Register(() =>
        {
            waveOut.Stop();
            tcs.TrySetResult();
        });

        waveOut.Play();
        await tcs.Task;
    }

    private sealed class FloatArraySampleProvider(float[] samples, int sampleRate) : ISampleProvider
    {
        private int _position;

        public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 1);

        public int Read(Span<float> buffer)
        {
            var remaining = samples.Length - _position;
            var toCopy = Math.Min(remaining, buffer.Length);
            samples.AsSpan(_position, toCopy).CopyTo(buffer);
            _position += toCopy;
            return toCopy;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        foreach (var tts in _ttsByLanguage.Values)
        {
            tts?.Dispose();
        }

        _speakLock.Dispose();
    }
}
