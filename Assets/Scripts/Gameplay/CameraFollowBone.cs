using UnityEngine;

/// <summary>
/// Keeps this transform's position locked to a target bone each frame,
/// while preserving its own rotation (controlled by player input).
/// Attach to the Head/camera parent transform.
/// </summary>
public class CameraFollowBone : MonoBehaviour
{
    private Transform targetBone;

    private void LateUpdate()
    {
        if (targetBone != null)
            transform.position = targetBone.position;
    }

    public void SetBone(Transform bone)
    {
        targetBone = bone;
    }
}
