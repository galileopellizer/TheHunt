using Unity.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LobbyPlayerSlotUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private RawImage portrait;
    [SerializeField] private TMP_Text playerName;

    [Header("Preview")]
    [SerializeField] private CharacterPreviewRig previewRigPrefab;

    [SerializeField] private int renderSize = 256;
    [SerializeField] private string previewLayerName = "Preview";

    private CharacterPreviewRig rigInstance;
    private RenderTexture rt;
    private PlayerNameState boundNameState;
    private GameObject[] boundCharacterPrefabs;

    // Each slot gets a unique world-space pocket so cameras don't see each other's models
    private static int _nextSlot = 0;
    private int _mySlot = -1;
    private static readonly Vector3 SlotSpacing = new Vector3(500f, 0f, 0f);
    private static readonly Vector3 SlotOrigin  = new Vector3(0f, -9000f, 0f);

    public void Setup(string name, GameObject[] prefabs, PlayerNameState nameState = null)
    {
        boundCharacterPrefabs = prefabs;
        playerName.text = name;
        BindNameState(nameState);

        // Pick initial model — may already be correct or will update via OnValueChanged
        int charIdx = nameState != null ? nameState.CharacterIndex.Value : 0;
        var prefab = (prefabs != null && charIdx < prefabs.Length) ? prefabs[charIdx]
            : (prefabs != null && prefabs.Length > 0 ? prefabs[0] : null);
        if (prefab != null) SetCharacterModel(prefab);
    }

    // Legacy overload so existing callers still compile
    public void Setup(string name, GameObject modelPrefab, PlayerNameState nameState = null)
    {
        Setup(name, modelPrefab != null ? new[] { modelPrefab } : null, nameState);
    }

    private void BindNameState(PlayerNameState nameState)
    {
        if (boundNameState != null)
        {
            boundNameState.PlayerName.OnValueChanged -= HandleNameChanged;
            boundNameState.CharacterIndex.OnValueChanged -= HandleCharIndexChanged;
        }

        boundNameState = nameState;

        if (boundNameState != null)
        {
            if (boundNameState.PlayerName.Value.Length > 0)
                playerName.text = boundNameState.PlayerName.Value.ToString();

            boundNameState.PlayerName.OnValueChanged += HandleNameChanged;
            boundNameState.CharacterIndex.OnValueChanged += HandleCharIndexChanged;
        }
    }

    private void HandleCharIndexChanged(int previous, int current)
    {
        if (boundCharacterPrefabs == null || boundCharacterPrefabs.Length == 0) return;
        int idx = Mathf.Clamp(current, 0, boundCharacterPrefabs.Length - 1);
        var prefab = boundCharacterPrefabs[idx];
        if (prefab != null) SetCharacterModel(prefab);
    }

    private void HandleNameChanged(FixedString32Bytes previous, FixedString32Bytes current)
    {
        if (current.Length > 0)
        {
            playerName.text = current.ToString();
        }
    }

    private void SetCharacterModel(GameObject modelPrefab)
    {
        CleanupPreview();

        rt = new RenderTexture(renderSize, renderSize, 16, RenderTextureFormat.ARGB32);
        rt.Create();
        portrait.texture = rt;

        if (_mySlot < 0) _mySlot = _nextSlot++;
        rigInstance = Instantiate(previewRigPrefab);
        rigInstance.transform.position = SlotOrigin + SlotSpacing * _mySlot;
        rigInstance.SetTargetTexture(rt);

        // Spawn model
        var model = Instantiate(modelPrefab, rigInstance.ModelAnchor);
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.identity;

        // Force model to Preview layer so only preview cam sees it
        int previewLayer = LayerMask.NameToLayer(previewLayerName);
        SetLayerRecursively(model, previewLayer);

        // Frame based on first renderer found
        var renderer = model.GetComponentInChildren<Renderer>();
        if (renderer != null)
            rigInstance.FrameModel(renderer);
    }

    private void SetLayerRecursively(GameObject go, int layer)
    {
        if (layer < 0) return;
        go.layer = layer;
        foreach (Transform child in go.transform)
            SetLayerRecursively(child.gameObject, layer);
    }

    private void CleanupPreview()
    {
        if (rigInstance) Destroy(rigInstance.gameObject);
        rigInstance = null;

        if (rt != null)
        {
            rt.Release();
            Destroy(rt);
            rt = null;
        }
    }

    private void OnDestroy()
    {
        if (boundNameState != null)
        {
            boundNameState.PlayerName.OnValueChanged -= HandleNameChanged;
            boundNameState.CharacterIndex.OnValueChanged -= HandleCharIndexChanged;
        }

        CleanupPreview();
        _nextSlot = 0; // reset so slots stay compact across lobby rebuilds
    }
}
