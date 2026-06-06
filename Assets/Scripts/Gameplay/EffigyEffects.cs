using Unity.Netcode;
using UnityEngine;

[DisallowMultipleComponent]
public class EffigyEffects : NetworkBehaviour
{
    [Header("Refs")]
    [SerializeField] private EffigyState effigyState;

    [Header("Visual Roots (Optional)")]
    [SerializeField] private GameObject idleRoot;
    [SerializeField] private GameObject burningRoot;
    [SerializeField] private GameObject burnedRoot;

    [Header("Particles (Optional)")]
    [SerializeField] private ParticleSystem burningLoopFx;
    [SerializeField] private ParticleSystem burnedBurstFx;

    [Header("Audio (Optional)")]
    [SerializeField] private AudioSource loopSource;
    [SerializeField] private AudioSource oneShotSource;
    [SerializeField] private AudioClip burnStartClip;
    [SerializeField] private AudioClip burnLoopClip;
    [SerializeField] private AudioClip burnCompleteClip;

    [Header("Audio Mix")]
    [SerializeField, Range(0f, 1f)] private float loopVolume = 0.2f;
    [SerializeField, Range(0f, 1f)] private float oneShotVolume = 0.3f;
    [SerializeField, Range(0f, 1f)] private float spatialBlend = 1f;
    [SerializeField] private float minDistance = 1f;
    [SerializeField] private float maxDistance = 14f;

    private bool subscribed;
    private bool warnedMissingFx;
    private bool warnedMissingAudio;

    private void Awake()
    {
        if (effigyState == null)
            effigyState = GetComponent<EffigyState>();

        ResolveParticleReferences();
        ResolveAudioReferences();
    }

    public override void OnNetworkSpawn()
    {
        Subscribe();
        ApplyVisualState(effigyState != null && effigyState.IsBurning.Value, effigyState != null && effigyState.IsBurned.Value);
        ApplyLoopState(effigyState != null && effigyState.IsBurning.Value, effigyState != null && effigyState.IsBurned.Value);
    }

    public override void OnNetworkDespawn()
    {
        Unsubscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void Subscribe()
    {
        if (subscribed) return;
        if (effigyState == null) return;

        effigyState.IsBurning.OnValueChanged += HandleBurningChanged;
        effigyState.IsBurned.OnValueChanged += HandleBurnedChanged;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed) return;
        if (effigyState != null)
        {
            effigyState.IsBurning.OnValueChanged -= HandleBurningChanged;
            effigyState.IsBurned.OnValueChanged -= HandleBurnedChanged;
        }

        subscribed = false;
    }

    private void HandleBurningChanged(bool oldValue, bool newValue)
    {
        bool burned = effigyState != null && effigyState.IsBurned.Value;
        ApplyVisualState(newValue, burned);
        ApplyLoopState(newValue, burned);

        if (newValue && !oldValue && !burned)
        {
            PlayOneShot(burnStartClip);
        }
    }

    private void HandleBurnedChanged(bool oldValue, bool newValue)
    {
        bool burning = effigyState != null && effigyState.IsBurning.Value;
        ApplyVisualState(burning, newValue);
        ApplyLoopState(burning, newValue);

        if (!oldValue && newValue)
        {
            if (burnedBurstFx != null)
                burnedBurstFx.Play();
            PlayOneShot(burnCompleteClip);
        }
    }

    private void ApplyVisualState(bool burning, bool burned)
    {
        // If dedicated roots are not assigned, keep idle visible as a safe fallback.
        bool hasBurningRoot = burningRoot != null;
        bool hasBurnedRoot = burnedRoot != null;

        bool showIdle = !burned && (!burning || !hasBurningRoot);
        bool showBurning = hasBurningRoot && burning && !burned;
        bool showBurned = hasBurnedRoot && burned;

        if (idleRoot != null) idleRoot.SetActive(showIdle);
        if (burningRoot != null) burningRoot.SetActive(showBurning);
        if (burnedRoot != null) burnedRoot.SetActive(showBurned);
    }

    private void ApplyLoopState(bool burning, bool burned)
    {
        bool shouldLoop = burning && !burned;

        if (burningLoopFx != null)
        {
            if (shouldLoop)
            {
                if (!burningLoopFx.isPlaying)
                    burningLoopFx.Play();
            }
            else if (burningLoopFx.isPlaying)
            {
                burningLoopFx.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }
        else if (shouldLoop && !warnedMissingFx)
        {
            warnedMissingFx = true;
            Debug.LogWarning("EffigyEffects: No burningLoopFx assigned or resolved on " + name, this);
        }

        if (loopSource == null) return;

        if (shouldLoop && burnLoopClip != null)
        {
            if (loopSource.clip != burnLoopClip)
                loopSource.clip = burnLoopClip;

            loopSource.loop = true;
            if (!loopSource.isPlaying)
                loopSource.Play();
        }
        else if (loopSource.isPlaying)
        {
            loopSource.Stop();
        }
    }

    private void PlayOneShot(AudioClip clip)
    {
        if (clip == null) return;
        if (oneShotSource == null)
        {
            if (!warnedMissingAudio)
            {
                warnedMissingAudio = true;
                Debug.LogWarning("EffigyEffects: No oneShotSource available for clip playback on " + name, this);
            }
            return;
        }
        oneShotSource.PlayOneShot(clip);
    }

    private void ResolveParticleReferences()
    {
        if (burningLoopFx == null)
            return;

        // If the assigned particle is a prefab asset reference, instantiate a runtime child.
        if (!burningLoopFx.gameObject.scene.IsValid())
        {
            var spawned = Instantiate(burningLoopFx, transform);
            spawned.transform.localPosition = Vector3.zero;
            spawned.transform.localRotation = Quaternion.identity;
            spawned.transform.localScale = Vector3.one;
            burningLoopFx = spawned;
        }
    }

    private void ResolveAudioReferences()
    {
        if (oneShotSource == null)
            oneShotSource = GetComponent<AudioSource>();

        if (oneShotSource == null && (burnStartClip != null || burnCompleteClip != null))
            oneShotSource = gameObject.AddComponent<AudioSource>();

        if (loopSource == null && burnLoopClip != null)
            loopSource = gameObject.AddComponent<AudioSource>();

        if (loopSource != null)
        {
            Apply3DDefaults(loopSource, loopVolume);
            loopSource.playOnAwake = false;
            loopSource.loop = true;
            if (loopSource.clip == null && burnLoopClip != null)
                loopSource.clip = burnLoopClip;
        }

        if (oneShotSource != null)
        {
            Apply3DDefaults(oneShotSource, oneShotVolume);
            oneShotSource.playOnAwake = false;
        }
    }

    private void Apply3DDefaults(AudioSource source, float volume)
    {
        if (source == null) return;

        source.spatialBlend = Mathf.Clamp01(spatialBlend);
        source.volume = Mathf.Clamp01(volume);
        source.minDistance = Mathf.Max(0f, minDistance);
        source.maxDistance = Mathf.Max(source.minDistance + 0.01f, maxDistance);
        source.dopplerLevel = 0f;
        source.rolloffMode = AudioRolloffMode.Logarithmic;
    }
}
