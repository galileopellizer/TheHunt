using System.Collections;
using UnityEngine;

/// <summary>
/// Attach to the player camera. Call Shake() when the monster lands a hit.
/// </summary>
public class CameraShake : MonoBehaviour
{
    public static CameraShake Instance { get; private set; }

    [SerializeField] private float duration = 0.25f;
    [SerializeField] private float magnitude = 0.12f;

    private Vector3 originLocalPos;
    private Coroutine shakeRoutine;

    private void Awake()
    {
        Instance = this;
        originLocalPos = transform.localPosition;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void Shake()
    {
        if (shakeRoutine != null) StopCoroutine(shakeRoutine);
        shakeRoutine = StartCoroutine(DoShake());
    }

    private IEnumerator DoShake()
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            float strength = Mathf.Lerp(magnitude, 0f, elapsed / duration);
            transform.localPosition = originLocalPos + Random.insideUnitSphere * strength;
            elapsed += Time.deltaTime;
            yield return null;
        }
        transform.localPosition = originLocalPos;
    }
}
