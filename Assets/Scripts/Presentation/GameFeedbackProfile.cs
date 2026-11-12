using System;
using UnityEngine;

/// <summary>Authored clips and mix levels in Resources/Presentation/GameFeedbackProfile.</summary>
[CreateAssetMenu(menuName = "Hooligan Manager/Presentation/Feedback Profile")]
public sealed class GameFeedbackProfile : ScriptableObject
{
    public AudioClip menuMusic;
    public AudioClip explorationMusic;
    public AudioClip matchdayMusic;
    [Range(0, 1)] public float musicGain = .2f;
    [Range(.1f, 5)] public float crossfadeSeconds = 1.5f;
    public Cue[] cues = Array.Empty<Cue>();
    [Serializable] public sealed class Cue
    {
        public GameFeedbackEvent kind;
        public AudioClip clip;
        [Range(0, 1)] public float gain = .55f;
        [Range(.02f, 2)] public float cooldown = .12f;
        public bool spatial;
    }
}
