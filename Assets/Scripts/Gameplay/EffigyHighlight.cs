using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Attach to the BurnStationWithEffigy prefab root.
/// Glows when the local human player is within interact range.
/// Uses URP emission on the assigned renderers.
/// </summary>
public class EffigyHighlight : MonoBehaviour
{
    [Header("Highlight")]
    [SerializeField] private float highlightDistance = 4f;
    [SerializeField] private Color emissionColor     = new Color(1f, 0.6f, 0.1f) * 2f;
    [SerializeField] private Renderer[] targetRenderers;

    private EffigyState effigyState;
    private Material[] instanceMaterials;
    private bool isHighlighted;
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    private void Awake()
    {
        effigyState = GetComponent<EffigyState>();

        // If no renderers assigned, grab all in children
        if (targetRenderers == null || targetRenderers.Length == 0)
            targetRenderers = GetComponentsInChildren<Renderer>();

        // Create material instances so we don't modify shared assets
        var mats = new System.Collections.Generic.List<Material>();
        foreach (var r in targetRenderers)
        {
            var m = new Material(r.material);
            r.material = m;
            mats.Add(m);
        }
        instanceMaterials = mats.ToArray();
    }

    private void OnDestroy()
    {
        foreach (var m in instanceMaterials)
            if (m != null) Destroy(m);
    }

    private void Update()
    {
        // Only show highlight if not already burned/burning
        if (effigyState != null && (effigyState.IsBurned.Value || effigyState.IsBurning.Value))
        {
            SetHighlight(false);
            return;
        }

        float dist = GetDistanceToLocalHuman();
        SetHighlight(dist <= highlightDistance);
    }

    private void SetHighlight(bool on)
    {
        if (on == isHighlighted) return;
        isHighlighted = on;

        foreach (var m in instanceMaterials)
        {
            if (m == null) continue;
            if (on)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor(EmissionColorId, emissionColor);
            }
            else
            {
                m.DisableKeyword("_EMISSION");
                m.SetColor(EmissionColorId, Color.black);
            }
        }
    }

    private float GetDistanceToLocalHuman()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null) return float.MaxValue;

        foreach (var role in FindObjectsOfType<GamePlayerBodyRole>())
        {
            var no = role.GetComponent<NetworkObject>();
            if (no == null || !no.IsOwner) continue;
            if (role.Role.Value != PlayerRole.Human) continue;
            return Vector3.Distance(transform.position, role.transform.position);
        }

        return float.MaxValue;
    }
}
