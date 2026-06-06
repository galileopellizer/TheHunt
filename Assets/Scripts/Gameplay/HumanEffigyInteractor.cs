using Unity.Netcode;
using UnityEngine;
using Object = UnityEngine.Object;

[DisallowMultipleComponent]
public class HumanEffigyInteractor : NetworkBehaviour
{
    [Header("Interact")]
    [SerializeField] private KeyCode interactKey = KeyCode.E;
    [SerializeField] private float interactDistance = 2.5f;
    [SerializeField] private float interactCooldown = 0.15f;

    private float nextInteractTime;
    private GamePlayerBodyRole bodyRole;
    private PlayerHealth health;

    private void Awake()
    {
        bodyRole = GetComponent<GamePlayerBodyRole>();
        health = GetComponent<PlayerHealth>();
    }

    private void Update()
    {
        if (!IsOwner) return;
        if (Time.time < nextInteractTime) return;
        if (!Input.GetKeyDown(interactKey)) return;
        if (bodyRole == null || bodyRole.Role.Value != PlayerRole.Human) return;
        if (health != null && health.IsDead.Value) return;

        EffigyState target = FindClosestEffigy();
        if (target == null) return;

        nextInteractTime = Time.time + interactCooldown;
        target.TryStartBurnServerRpc();
    }

    private EffigyState FindClosestEffigy()
    {
        var effigies = Object.FindObjectsOfType<EffigyState>();
        if (effigies == null || effigies.Length == 0)
            return null;

        EffigyState best = null;
        float bestSqrDist = float.PositiveInfinity;
        float maxSqr = interactDistance * interactDistance;

        foreach (var effigy in effigies)
        {
            if (effigy == null || !effigy.isActiveAndEnabled) continue;
            if (!effigy.IsSpawned) continue;
            if (effigy.IsBurned.Value || effigy.IsBurning.Value) continue;

            float sqrDist = (effigy.transform.position - transform.position).sqrMagnitude;
            if (sqrDist > maxSqr) continue;

            if (sqrDist < bestSqrDist)
            {
                bestSqrDist = sqrDist;
                best = effigy;
            }
        }

        return best;
    }
}
