using Unity.Netcode;
using UnityEngine;

public enum SurfaceType { Grass, Gravel, Wood }

[System.Serializable]
public class SurfaceFootsteps
{
    public SurfaceType surface;
    [Header("Human")]
    public AudioClip[] humanWalkClips;
    public AudioClip[] humanRunClips;
    [Header("Monster")]
    public AudioClip[] monsterWalkClips;
    public AudioClip[] monsterRunClips;
}

[System.Serializable]
public class TerrainLayerMapping
{
    [Tooltip("Index of this texture in the Terrain's layer list (0 = first painted texture)")]
    public int terrainLayerIndex;
    public SurfaceType surface;
}

/// <summary>
/// Attach to the player body prefab.
/// Plays footstep sounds based on movement speed and surface type.
/// Detects terrain textures via alphamap sampling, and collider surfaces via tag.
/// </summary>
[DisallowMultipleComponent]
public class FootstepController : NetworkBehaviour
{
    [Header("Surface Clips")]
    [SerializeField] private SurfaceFootsteps[] surfaces;

    [Header("Terrain Mapping")]
    [Tooltip("Fallback if the terrain has no TerrainSurfaceMap component.")]
    [SerializeField] private TerrainLayerMapping[] terrainMappings;

    [Header("Collider Tag Mapping")]
    [Tooltip("Tag 'Wood' maps to wood surface, etc. Falls back to Gravel if unknown.")]
    [SerializeField] private string woodTag   = "Wood";
    [SerializeField] private string grassTag  = "Grass";
    [SerializeField] private string gravelTag = "Gravel";

    [Header("Cadence")]
    [SerializeField] private float walkStepInterval = 0.5f;
    [SerializeField] private float runStepInterval  = 0.3f;
    [SerializeField] private float speedThreshold   = 0.5f;   // min speed to trigger steps

    [Header("Audio - Human")]
    [SerializeField, Range(0f, 1f)] private float humanWalkVolume  = 0.25f;
    [SerializeField, Range(0f, 1f)] private float humanRunVolume   = 0.4f;
    [SerializeField] private float humanMaxDistance = 20f;

    [Header("Audio - Monster")]
    [SerializeField, Range(0f, 1f)] private float monsterWalkVolume = 0.8f;
    [SerializeField, Range(0f, 1f)] private float monsterRunVolume  = 1f;
    [SerializeField] private float monsterMaxDistance = 40f;

    [Header("Audio - Local 2D")]
    [SerializeField, Range(0f, 1f)] private float local2DVolume = 1f;

    [Header("Audio - Shared")]
    [SerializeField] private float pitchVariance = 0f;

    private AudioSource audioSource3D;
    private AudioSource audioSource2D;
    private CharacterController cc;
    private GamePlayerBodyRole bodyRole;
    private float nextStepTime;

    public override void OnNetworkSpawn()
    {
        cc           = GetComponent<CharacterController>();
        bodyRole     = GetComponent<GamePlayerBodyRole>();
        lastPosition = transform.position;

        // 3D source — heard by everyone nearby
        audioSource3D              = gameObject.AddComponent<AudioSource>();
        audioSource3D.spatialBlend = 1f;
        audioSource3D.rolloffMode  = AudioRolloffMode.Linear;
        audioSource3D.minDistance  = 1f;
        audioSource3D.maxDistance  = 25f;
        audioSource3D.playOnAwake  = false;
        audioSource3D.loop         = false;

        if (!IsOwner) return;

        // 2D source — only local player hears this (self-feedback)
        audioSource2D              = gameObject.AddComponent<AudioSource>();
        audioSource2D.spatialBlend = 0f;
        audioSource2D.playOnAwake  = false;
        audioSource2D.loop         = false;
        audioSource2D.volume       = local2DVolume;
    }

    private bool wasMoving;
    private bool wasRunning;
    private SurfaceType lastSurface;
    private Vector3 lastPosition;
    private float nextSurfaceCheckTime;
    private float smoothedRemoteSpeed;

    private void Update()
    {
        float horizontalSpeed;

        if (IsOwner && cc != null && cc.enabled)
            horizontalSpeed = new Vector3(cc.velocity.x, 0f, cc.velocity.z).magnitude;
        else
        {
            Vector3 pos = transform.position;
            float rawSpeed = new Vector3(pos.x - lastPosition.x, 0f, pos.z - lastPosition.z).magnitude / Mathf.Max(Time.deltaTime, 0.0001f);
            lastPosition = pos;
            smoothedRemoteSpeed = Mathf.Lerp(smoothedRemoteSpeed, rawSpeed, Time.deltaTime * 5f);
            horizontalSpeed = smoothedRemoteSpeed;
        }

        // Suppress monster 3D footsteps during active stealth
        bool monsterActiveStealth = false;
        if (bodyRole != null && bodyRole.Role.Value == PlayerRole.Monster)
        {
            var stealth = GetComponent<MonsterStealth>();
            if (stealth != null && stealth.IsActiveInvisible.Value)
                monsterActiveStealth = true;
        }

        if (horizontalSpeed < speedThreshold || monsterActiveStealth)
        {
            if (wasMoving)
            {
                StopAllCoroutines();
                StartCoroutine(FadeOut(audioSource3D));
                if (IsOwner) StartCoroutine(FadeOut(audioSource2D));
                wasMoving = false;
            }
            return;
        }

        // Owner uses key state; remote uses speed threshold per role
        bool running;
        if (IsOwner)
            running = IsRunning();
        else
        {
            bool isMonster = bodyRole != null && bodyRole.Role.Value == PlayerRole.Monster;
            float runEnterThreshold = isMonster ? 6.5f : 6f;
            float runExitThreshold  = isMonster ? 5f   : 4.5f;
            if (wasRunning)
                running = horizontalSpeed > runExitThreshold;
            else
                running = horizontalSpeed > runEnterThreshold;
        }

        // Re-check surface every 0.5s
        SurfaceType currentSurface = lastSurface;
        if (Time.time > nextSurfaceCheckTime)
        {
            currentSurface = DetectSurface();
            nextSurfaceCheckTime = Time.time + 0.5f;
        }

        bool surfaceChanged = currentSurface != lastSurface;
        lastSurface = currentSurface;

        if (!wasMoving || running != wasRunning || surfaceChanged)
        {
            wasMoving  = true;
            wasRunning = running;
            StopAllCoroutines();
            PlayLoop(lastSurface, running);
        }
    }

    private bool IsRunning()
    {
        if (bodyRole == null) return false;
        if (bodyRole.Role.Value == PlayerRole.Monster)
            return Input.GetKey(KeyCode.LeftShift);
        else
            return Input.GetKey(KeyCode.LeftShift) && new Vector3(cc.velocity.x, 0f, cc.velocity.z).magnitude > 0.1f;
    }

    private SurfaceType DetectSurface()
    {
        // Raycast straight down from player feet
        Ray ray = new Ray(transform.position + Vector3.up * 0.1f, Vector3.down);
        if (!Physics.Raycast(ray, out RaycastHit hit, 2f)) return SurfaceType.Gravel;

        // Check if we hit terrain
        var terrain = hit.collider.GetComponent<Terrain>();
        if (terrain != null)
        {
            var surfaceMap = terrain.GetComponent<TerrainSurfaceMap>();
            return surfaceMap != null
                ? GetTerrainSurfaceFromMap(terrain, hit.point, surfaceMap)
                : GetTerrainSurface(terrain, hit.point);
        }

        // Check collider tag
        return GetTagSurface(hit.collider.tag);
    }

    private SurfaceType GetTerrainSurfaceFromMap(Terrain terrain, Vector3 worldPos, TerrainSurfaceMap surfaceMap)
    {
        TerrainData data = terrain.terrainData;
        Vector3 terrainPos = terrain.transform.position;

        int mapX = Mathf.RoundToInt((worldPos.x - terrainPos.x) / data.size.x * data.alphamapWidth);
        int mapZ = Mathf.RoundToInt((worldPos.z - terrainPos.z) / data.size.z * data.alphamapHeight);
        mapX = Mathf.Clamp(mapX, 0, data.alphamapWidth  - 1);
        mapZ = Mathf.Clamp(mapZ, 0, data.alphamapHeight - 1);

        float[,,] alphamap = data.GetAlphamaps(mapX, mapZ, 1, 1);

        int dominantLayer = 0;
        float maxWeight   = 0f;
        for (int i = 0; i < alphamap.GetLength(2); i++)
        {
            if (alphamap[0, 0, i] > maxWeight)
            {
                maxWeight     = alphamap[0, 0, i];
                dominantLayer = i;
            }
        }

        return surfaceMap.GetSurface(dominantLayer);
    }

    private SurfaceType GetTerrainSurface(Terrain terrain, Vector3 worldPos)
    {
        TerrainData data = terrain.terrainData;
        Vector3 terrainPos = terrain.transform.position;

        // Convert world position to alphamap coordinates
        int mapX = Mathf.RoundToInt((worldPos.x - terrainPos.x) / data.size.x * data.alphamapWidth);
        int mapZ = Mathf.RoundToInt((worldPos.z - terrainPos.z) / data.size.z * data.alphamapHeight);
        mapX = Mathf.Clamp(mapX, 0, data.alphamapWidth  - 1);
        mapZ = Mathf.Clamp(mapZ, 0, data.alphamapHeight - 1);

        float[,,] alphamap = data.GetAlphamaps(mapX, mapZ, 1, 1);

        // Find dominant texture layer
        int dominantLayer = 0;
        float maxWeight   = 0f;
        for (int i = 0; i < alphamap.GetLength(2); i++)
        {
            if (alphamap[0, 0, i] > maxWeight)
            {
                maxWeight    = alphamap[0, 0, i];
                dominantLayer = i;
            }
        }

        // Map layer index to surface type
        foreach (var mapping in terrainMappings)
        {
            if (mapping.terrainLayerIndex == dominantLayer)
                return mapping.surface;
        }

        return SurfaceType.Gravel; // fallback
    }

    private SurfaceType GetTagSurface(string tag)
    {
        if (tag == woodTag)   return SurfaceType.Wood;
        if (tag == grassTag)  return SurfaceType.Grass;
        if (tag == gravelTag) return SurfaceType.Gravel;
        return SurfaceType.Gravel;
    }

    private System.Collections.IEnumerator FadeOut(AudioSource src, float duration = 1.5f)
    {
        if (src == null || !src.isPlaying) yield break;
        float startVolume = src.volume;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            src.volume = Mathf.Lerp(startVolume, 0f, elapsed / duration);
            yield return null;
        }
        src.Stop();
        src.volume = startVolume;
    }

    private void PlayLoop(SurfaceType surface, bool running)
    {
        SurfaceFootsteps match = null;
        foreach (var s in surfaces)
        {
            if (s.surface == surface) { match = s; break; }
        }
        if (match == null) return;

        bool isMonster = bodyRole != null && bodyRole.Role.Value == PlayerRole.Monster;
        var clips = isMonster
            ? (running ? match.monsterRunClips : match.monsterWalkClips)
            : (running ? match.humanRunClips   : match.humanWalkClips);
        if (clips == null || clips.Length == 0) return;

        var clip = clips[Random.Range(0, clips.Length)];

        if (audioSource3D != null)
        {
            audioSource3D.loop       = true;
            audioSource3D.maxDistance = isMonster ? monsterMaxDistance : humanMaxDistance;
            audioSource3D.volume     = isMonster
                ? (running ? monsterRunVolume : monsterWalkVolume)
                : (running ? humanRunVolume   : humanWalkVolume);
            audioSource3D.clip = clip;
            audioSource3D.Play();
        }

        if (audioSource2D != null && IsOwner)
        {
            audioSource2D.loop = true;
            audioSource2D.clip = clip;
            audioSource2D.Play();
        }
    }
}
