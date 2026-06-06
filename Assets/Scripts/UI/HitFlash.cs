using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Attach to a full-screen UI Image (red, alpha 0 at rest).
/// Called by PlayerHealth when the local player takes damage.
/// </summary>
[RequireComponent(typeof(Image))]
public class HitFlash : MonoBehaviour
{
    public static HitFlash Instance { get; private set; }

    [SerializeField] private float peakAlpha = 0.45f;
    [SerializeField] private float fadeInTime = 0.05f;
    [SerializeField] private float fadeOutTime = 0.4f;

    private Image image;
    private Coroutine flashRoutine;

    private void Awake()
    {
        Instance = this;
        image = GetComponent<Image>();
        SetAlpha(0f);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void Flash()
    {
        if (flashRoutine != null) StopCoroutine(flashRoutine);
        flashRoutine = StartCoroutine(DoFlash());
    }

    private IEnumerator DoFlash()
    {
        // Fade in
        float t = 0f;
        while (t < fadeInTime)
        {
            t += Time.deltaTime;
            SetAlpha(Mathf.Lerp(0f, peakAlpha, t / fadeInTime));
            yield return null;
        }
        SetAlpha(peakAlpha);

        // Fade out
        t = 0f;
        while (t < fadeOutTime)
        {
            t += Time.deltaTime;
            SetAlpha(Mathf.Lerp(peakAlpha, 0f, t / fadeOutTime));
            yield return null;
        }
        SetAlpha(0f);
    }

    private void SetAlpha(float a)
    {
        var c = image.color;
        c.a = a;
        image.color = c;
    }
}
