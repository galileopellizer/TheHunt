using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Swaps materials on the effigy model based on burn state.
/// Attach alongside EffigyState. Assign the three materials and
/// the renderers you want affected.
/// </summary>
[DisallowMultipleComponent]
public class EffigyMaterialSwap : NetworkBehaviour
{
    [Header("Materials")]
    [SerializeField] private Material normalMaterial;
    [SerializeField] private Material burningMaterial;
    [SerializeField] private Material burnedMaterial;

    [Header("Renderers to swap (leave empty to use all children)")]
    [SerializeField] private Renderer[] targetRenderers;

    [Header("Eye Lights (disabled when burned)")]
    [SerializeField] private Light[] eyeLights;

    private EffigyState effigyState;

    private void Awake()
    {
        effigyState = GetComponent<EffigyState>();

        if (targetRenderers == null || targetRenderers.Length == 0)
            targetRenderers = GetComponentsInChildren<Renderer>(true);
    }

    public override void OnNetworkSpawn()
    {
        effigyState.IsBurning.OnValueChanged += (_, __) => UpdateMaterial();
        effigyState.IsBurned.OnValueChanged  += (_, __) => UpdateMaterial();
        UpdateMaterial();
    }

    private void UpdateMaterial()
    {
        Material mat;
        if (effigyState.IsBurned.Value)
            mat = burnedMaterial;
        else if (effigyState.IsBurning.Value)
            mat = burningMaterial;
        else
            mat = normalMaterial;

        if (mat == null) return;

        foreach (var r in targetRenderers)
            if (r != null) r.material = mat;

        // Turn off eye lights when burned
        bool burned = effigyState.IsBurned.Value;
        foreach (var l in eyeLights)
            if (l != null) l.enabled = !burned;
    }
}
