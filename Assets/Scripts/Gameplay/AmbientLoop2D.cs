using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class AmbientLoop2D : MonoBehaviour
{
    [Header("Audio")]
    [SerializeField] private AudioSource source;
    [SerializeField] private AudioClip clip;
    [Range(0f, 1f)]
    [SerializeField] private float volume = 0.15f;
    [SerializeField] private bool playOnStart = true;

    private void Awake()
    {
        if (source == null)
            source = GetComponent<AudioSource>();

        source.spatialBlend = 0f; // 2D
        source.loop = true;
        source.playOnAwake = false;
        source.volume = volume;
        if (clip != null)
            source.clip = clip;
    }

    private void Start()
    {
        if (playOnStart && source.clip != null)
            source.Play();
    }
}
