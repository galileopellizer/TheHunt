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
        // Simple framing: place camera in front of bounds center
        var b = r.bounds;
        var center = b.center;

        float size = Mathf.Max(b.size.x, b.size.y, b.size.z);
        float dist = size * 2.2f;

        cam.transform.position = center + new Vector3(0f, size * 0.15f, -dist);
        cam.transform.LookAt(center);
    }
}