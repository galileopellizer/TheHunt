using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class GateInteractable : NetworkBehaviour
{
    [Header("Rotation")]
    [SerializeField] private Transform pivotTransform;   // the part that rotates (assign fence root or a pivot child)
    [SerializeField] private float openAngle    = 90f;   // degrees to rotate when open
    [SerializeField] private float animDuration = 0.4f;

    [Header("Interaction")]
    [SerializeField] private float interactRange = 2.5f;
    [SerializeField] private KeyCode interactKey = KeyCode.E;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip openClip;
    [SerializeField] private AudioClip closeClip;

    private NetworkVariable<bool> isOpen = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private Quaternion closedRotation;
    private Quaternion openRotation;
    private Coroutine animCoroutine;

    public override void OnNetworkSpawn()
    {
        if (pivotTransform == null) pivotTransform = transform;

        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake  = false;
            audioSource.spatialBlend = 1f;
            audioSource.minDistance  = 1f;
            audioSource.maxDistance  = 10f;
        }

        closedRotation = pivotTransform.localRotation;
        openRotation   = closedRotation * Quaternion.Euler(0f, openAngle, 0f);

        isOpen.OnValueChanged += OnGateStateChanged;

        // Snap to current state on late join
        pivotTransform.localRotation = isOpen.Value ? openRotation : closedRotation;
    }

    public override void OnNetworkDespawn()
    {
        isOpen.OnValueChanged -= OnGateStateChanged;
    }

    private void Update()
    {
        if (!Input.GetKeyDown(interactKey)) return;

        var playerPos = GetLocalPlayerPosition();
        if (playerPos == null) return;

        float dist = Vector3.Distance(playerPos.Value, transform.position);

        if (dist > interactRange) return;

        if (IsSpawned)
            ToggleServerRpc();
        else
            OnGateStateChanged(isOpen.Value, !isOpen.Value);
    }

    private static Vector3? GetLocalPlayerPosition()
    {
        if (NetworkManager.Singleton != null)
        {
            foreach (var no in FindObjectsOfType<NetworkObject>())
            {
                if (!no.IsOwner) continue;
                if (no.GetComponent<GamePlayerBodyRole>() == null) continue;
                return no.transform.position;
            }
        }
        return Camera.main != null ? Camera.main.transform.position : (Vector3?)null;
    }

    [ServerRpc(RequireOwnership = false)]
    private void ToggleServerRpc()
    {
        isOpen.Value = !isOpen.Value;
    }

    private void OnGateStateChanged(bool previous, bool current)
    {
        if (animCoroutine != null) StopCoroutine(animCoroutine);
        animCoroutine = StartCoroutine(AnimateGate(current));
        PlaySound(current);
    }

    private IEnumerator AnimateGate(bool opening)
    {
        Quaternion from = pivotTransform.localRotation;
        Quaternion to   = opening ? openRotation : closedRotation;

        float t = 0f;
        while (t < animDuration)
        {
            t += Time.deltaTime;
            pivotTransform.localRotation = Quaternion.Lerp(from, to, t / animDuration);
            yield return null;
        }
        pivotTransform.localRotation = to;
    }

    private void PlaySound(bool opening)
    {
        if (audioSource == null) return;
        var clip = opening ? openClip : closeClip;
        if (clip != null) audioSource.PlayOneShot(clip);
    }
}
