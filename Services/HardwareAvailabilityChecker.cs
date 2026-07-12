using ChastiMaid.Models;

namespace ChastiMaid.Services;

/// <summary>Aggregates the optional-hardware probes already exposed by the individual services.</summary>
public static class HardwareAvailabilityChecker
{
    public static HardwareAvailability Check(int cameraIndex)
    {
        return new HardwareAvailability(
            WebcamPreviewService.DetectWebcam(cameraIndex),
            SpeechRecognitionService.IsSpeechRecognizerInstalled(),
            SpeechRecognitionService.DetectMicrophone());
    }
}
