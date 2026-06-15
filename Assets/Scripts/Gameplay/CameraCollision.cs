using UnityEngine;

/// <summary>
/// Prevents the camera from clipping through geometry by raycasting
/// from the player's head position to the camera each frame.
/// Attach to the same GameObject as your Camera.
/// </summary>
public class CameraCollision : MonoBehaviour
{
    [Tooltip("The point to raycast FROM (player head / camera anchor).")]
    [SerializeField] private Transform cameraAnchor;

    [Tooltip("Layers that block the camera.")]
    [SerializeField] private LayerMask collisionMask = ~0; // everything by default

    [Tooltip("How far to pull the camera in front of a hit surface.")]
    [SerializeField] private float surfaceOffset = 0.1f;

    [Tooltip("How quickly the camera returns to its original position after unblocking.")]
    [SerializeField] private float returnSpeed = 10f;

    private Vector3 defaultLocalPos;

    private void Awake()
    {
        defaultLocalPos = transform.localPosition;
    }

    private void LateUpdate()
    {
        if (cameraAnchor == null) return;

        Vector3 origin = cameraAnchor.position;
        Vector3 desiredWorld = cameraAnchor.TransformPoint(defaultLocalPos);
        Vector3 dir = desiredWorld - origin;
        float dist = dir.magnitude;

        Vector3 targetLocalPos = defaultLocalPos;

        if (dist > 0.001f && Physics.Raycast(origin, dir.normalized, out RaycastHit hit, dist, collisionMask))
        {
            // Pull camera to just in front of the hit surface
            float safeDistance = Mathf.Max(0f, hit.distance - surfaceOffset);
            Vector3 safeWorld = origin + dir.normalized * safeDistance;
            targetLocalPos = cameraAnchor.InverseTransformPoint(safeWorld);
        }

        transform.localPosition = Vector3.Lerp(
            transform.localPosition,
            targetLocalPos,
            Time.deltaTime * returnSpeed
        );
    }
}
