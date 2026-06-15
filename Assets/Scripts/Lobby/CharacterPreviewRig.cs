using UnityEngine;

public class CharacterPreviewRig : MonoBehaviour
{
    [SerializeField] private Camera cam;
    [SerializeField] private Transform modelAnchor;

    public Transform ModelAnchor => modelAnchor;

    public void SetTargetTexture(RenderTexture texture)
    {
        cam.targetTexture = texture;
    }

    public void FrameModel(Renderer r)
    {
        // Frame the full character — use all renderers for accurate bounds
        var allRenderers = r.transform.root.GetComponentsInChildren<Renderer>();
        var b = allRenderers[0].bounds;
        foreach (var rend in allRenderers)
            b.Encapsulate(rend.bounds);

        var center = b.center;
        float size = Mathf.Max(b.size.x, b.size.y, b.size.z);
        float dist = size * 1.1f;

        cam.transform.position = center + new Vector3(0f, 0f, -dist);
        cam.transform.LookAt(center);
    }
}