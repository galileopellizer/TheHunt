using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class RoleRevealUI : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private TMP_Text roleText;
    [SerializeField] private TMP_Text subtitleText;

    [Header("Timing")]
    [SerializeField] private float fadeInDuration  = 0.6f;
    [SerializeField] private float holdDuration    = 2.0f;
    [SerializeField] private float fadeOutDuration = 0.8f;
    [SerializeField] private float pollInterval    = 0.1f; // how often to check if role is ready
    [SerializeField] private float maxWait         = 5f;   // give up after this long

    [Header("Colors")]
    [SerializeField] private Color humanColor   = new Color(0.4f, 0.8f, 1f);
    [SerializeField] private Color monsterColor = new Color(1f, 0.25f, 0.2f);

    private void Start()
    {
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        gameObject.SetActive(true);
        StartCoroutine(WaitForRoleThenReveal());
    }

    private IEnumerator WaitForRoleThenReveal()
    {
        float waited = 0f;
        GamePlayerBodyRole role = null;

        Debug.Log("[RoleReveal] Starting poll...");

        while (waited < maxWait)
        {
            role = FindLocalRole();
            Debug.Log($"[RoleReveal] Polled — role={(role == null ? "null" : role.Role.Value.ToString())} waited={waited:F1}");
            if (role != null) break;
            yield return new WaitForSeconds(pollInterval);
            waited += pollInterval;
        }

        if (role == null) { Debug.LogWarning("[RoleReveal] Timed out, no role found."); gameObject.SetActive(false); yield break; }

        Debug.Log($"[RoleReveal] Role found: {role.Role.Value}");

        bool isMonster = role.Role.Value == PlayerRole.Monster;

        roleText.text     = isMonster ? "YOU ARE THE MONSTER" : "YOU ARE A HUMAN";
        roleText.color    = isMonster ? monsterColor : humanColor;
        if (subtitleText != null)
            subtitleText.text = isMonster ? "Hunt them down." : "Light the effigies. Survive.";

        yield return StartCoroutine(Fade(0f, 1f, fadeInDuration));
        yield return new WaitForSeconds(holdDuration);
        yield return StartCoroutine(Fade(1f, 0f, fadeOutDuration));

        gameObject.SetActive(false);
    }

    private IEnumerator Fade(float from, float to, float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(from, to, t / duration);
            yield return null;
        }
        canvasGroup.alpha = to;
    }

    private static GamePlayerBodyRole FindLocalRole()
    {
        var nm = Unity.Netcode.NetworkManager.Singleton;
        if (nm == null) return null;

        foreach (var no in FindObjectsOfType<Unity.Netcode.NetworkObject>())
        {
            if (!no.IsOwner) continue;
            var r = no.GetComponent<GamePlayerBodyRole>();
            if (r != null) return r;
        }
        return null;
    }
}
