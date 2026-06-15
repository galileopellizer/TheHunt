using UnityEngine;

/// <summary>
/// Attach to any world-space AudioSource (footsteps, roar, etc.).
/// Occludes the sound when the source and the local listener
/// are on opposite sides of the cave (one in, one out).
///
/// Also attach a CaveZoneMember to the same GameObject so this script
/// knows whether THIS source is inside the cave.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class CaveAudioOcclusion : MonoBehaviour
{
    [Tooltip("Volume multiplier applied when occluded (source/listener on opposite cave sides).")]
    [SerializeField, Range(0f, 1f)] private float occludedVolume = 0.05f;

    [Tooltip("Low-pass cutoff frequency when occluded (Hz). Lower = more muffled.")]
    [SerializeField] private float occludedCutoff = 800f;

    [Tooltip("How fast volume/filter transitions when occlusion changes.")]
    [SerializeField] private float transitionSpeed = 5f;

    private AudioSource audioSource;
    private AudioLowPassFilter lowPass;
    private float baseVolume;
    private float targetVolume;
    private float targetCutoff;

    // Set this from a CaveZoneMember script on the same object
    [HideInInspector] public bool sourceInCave;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        baseVolume  = audioSource.volume;

        lowPass = GetComponent<AudioLowPassFilter>();
        if (lowPass == null)
            lowPass = gameObject.AddComponent<AudioLowPassFilter>();

        lowPass.cutoffFrequency = 22000f; // fully open by default
    }

    private void Update()
    {
        bool listenerInCave = CaveZone.LocalPlayerInCave;
        bool occluded = listenerInCave != sourceInCave;

        targetVolume = occluded ? baseVolume * occludedVolume : baseVolume;
        targetCutoff = occluded ? occludedCutoff : 22000f;

        audioSource.volume       = Mathf.Lerp(audioSource.volume, targetVolume, Time.deltaTime * transitionSpeed);
        lowPass.cutoffFrequency  = Mathf.Lerp(lowPass.cutoffFrequency, targetCutoff, Time.deltaTime * transitionSpeed);
    }
}
