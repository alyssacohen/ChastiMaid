namespace ChastiMaid.Models;

/// <summary>Snapshot of which optional hardware/software capabilities are available on this machine.</summary>
public record HardwareAvailability(bool HasWebcam, bool HasSpeechRecognizer, bool HasMicrophone)
{
    public bool HasAnyIssue => !HasWebcam || !HasSpeechRecognizer || !HasMicrophone;
}
