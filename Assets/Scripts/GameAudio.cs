using UnityEngine;

/// <summary>Compatibility facade. All live playback and music mixing belong to GameFeedback.</summary>
public sealed class GameAudio : MonoBehaviour
{
    public static void Play(string name) => GameFeedback.PlayLegacy(name);
    public static void PlayPresentationTheme(string kind) => GameFeedback.Emit(
        string.Equals(kind, "defeat", System.StringComparison.OrdinalIgnoreCase)
            ? GameFeedbackEvent.TaskFailed : GameFeedbackEvent.TaskComplete);
}
