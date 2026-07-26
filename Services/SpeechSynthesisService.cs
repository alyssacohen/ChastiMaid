using System.Globalization;
using Windows.Foundation;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Media.SpeechSynthesis;

namespace ChastiMaid.Services;

/// <summary>
///     Speaks text aloud using the on-device WinRT speech synthesizer built into Windows — no
///     third-party engine, no network, no extra package (same SDK already referenced for
///     <see cref="SpeechRecognitionService" />). Stateless: each call opens its own
///     <see cref="SpeechSynthesizer" /> and <see cref="MediaPlayer" />. Failures (no synthesizer, no
///     matching voice, playback error) are swallowed — callers already show the spoken text on
///     screen, so silence is an acceptable fallback, never a crash.
/// </summary>
public static class SpeechSynthesisService
{
    public static async Task SpeakAsync(string text, CancellationToken ct = default)
    {
        try
        {
            using var synth = new SpeechSynthesizer();
            synth.Voice = SelectVoice(synth);
            var stream = await WinRtAsyncExtensions.ToTask(synth.SynthesizeTextToStreamAsync(text));
            await PlayStreamAsync(stream, ct);
        }
        catch
        {
            /* best-effort — text already shown on screen */
        }
    }

    /// <summary>
    ///     Prefers a "Natural"-quality female voice matching the current UI language, falling back to
    ///     any female voice for that language, then any voice for that language, then the synthesizer
    ///     default.
    /// </summary>
    private static VoiceInformation SelectVoice(SpeechSynthesizer synth)
    {
        var lang = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        var candidates = SpeechSynthesizer.AllVoices
            .Where(v => v.Language.StartsWith(lang, StringComparison.OrdinalIgnoreCase))
            .ToList();

        return candidates.FirstOrDefault(v =>
                   v.DisplayName.Contains("Natural", StringComparison.OrdinalIgnoreCase) &&
                   v.Gender == VoiceGender.Female)
               ?? candidates.FirstOrDefault(v => v.Gender == VoiceGender.Female)
               ?? candidates.FirstOrDefault()
               ?? synth.Voice;
    }

    private static async Task PlayStreamAsync(SpeechSynthesisStream stream, CancellationToken ct)
    {
        using var player = new MediaPlayer();
        player.Source = MediaSource.CreateFromStream(stream, stream.ContentType);

        var tcs = new TaskCompletionSource();
        TypedEventHandler<MediaPlayer, object> onEnded = null!;
        TypedEventHandler<MediaPlayer, MediaPlayerFailedEventArgs> onFailed = null!;
        onEnded = (_, _) =>
        {
            player.MediaEnded -= onEnded;
            player.MediaFailed -= onFailed;
            tcs.TrySetResult();
        };
        onFailed = (_, _) =>
        {
            player.MediaEnded -= onEnded;
            player.MediaFailed -= onFailed;
            tcs.TrySetResult();
        };
        player.MediaEnded += onEnded;
        player.MediaFailed += onFailed;

        using var registration = ct.Register(() =>
        {
            player.Pause();
            tcs.TrySetResult();
        });

        player.Play();
        await tcs.Task;
    }
}
