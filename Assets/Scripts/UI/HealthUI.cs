using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

[DisallowMultipleComponent]
public class HealthUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject root;
    [SerializeField] private Image fillImage;
    [SerializeField] private TMP_Text valueText;

    [Header("Behavior")]
    [SerializeField] private bool hideForMonster = true;
    [SerializeField] private bool showDeadLabel = true;
    [SerializeField] private float smoothSpeed = 10f;
    [SerializeField] private float findInterval = 0.5f;

    private PlayerHealth localHealth;
    private GamePlayerBodyRole localRole;
    private float displayValue = 1f;
    private float nextFindTime;

    private void Update()
    {
        if (localHealth == null || localRole == null)
            TryFindLocalBody();

        if (localHealth == null || localRole == null)
            return;

        bool isHuman = localRole.Role.Value == PlayerRole.Human;
        bool visible = !hideForMonster || isHuman;
        if (root != null && root.activeSelf != visible)
            root.SetActive(visible);

        if (!visible)
            return;

        float max = localHealth.MaxHealth;
        float current = Mathf.Clamp(localHealth.CurrentHealth.Value, 0f, max);
        float target = max > 0f ? current / max : 0f;
        displayValue = Mathf.MoveTowards(displayValue, target, smoothSpeed * Time.deltaTime);

        if (fillImage != null)
            fillImage.fillAmount = displayValue;

        if (valueText != null)
        {
            if (localHealth.IsDead.Value && showDeadLabel)
                valueText.text = "DEAD";
            else
                valueText.text = Mathf.CeilToInt(current).ToString();
        }
    }

    private void TryFindLocalBody()
    {
        if (Time.time < nextFindTime)
            return;

        localHealth = null;
        localRole = null;

        var network = NetworkManager.Singleton;
        if (network != null)
        {
            foreach (var no in Object.FindObjectsOfType<NetworkObject>())
            {
                if (!no.IsSpawned || !no.IsOwner)
                    continue;

                localRole = no.GetComponent<GamePlayerBodyRole>();
                if (localRole == null)
                    continue;

                localHealth = no.GetComponent<PlayerHealth>();
                if (localHealth != null)
                    break;
            }
        }

        nextFindTime = Time.time + Mathf.Max(0.1f, findInterval);
    }
}
