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

    public void Setup(string name, GameObject modelPrefab, PlayerNameState nameState = null)
    {
        playerName.text = name;
        BindNameState(nameState);
        SetCharacterModel(modelPrefab);
    }

    private void BindNameState(PlayerNameState nameState)
    {
        if (boundNameState != null)
        {
            boundNameState.PlayerName.OnValueChanged -= HandleNameChanged;
        }

        boundNameState = nameState;

        if (boundNameState != null)
        {
            if (boundNameState.PlayerName.Value.Length > 0)
            {
                playerName.text = boundNameState.PlayerName.Value.ToString();
            }

            boundNameState.PlayerName.OnValueChanged += HandleNameChanged;
        }
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

        rigInstance = Instantiate(previewRigPrefab);
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
        }

        CleanupPreview();
    }
}
