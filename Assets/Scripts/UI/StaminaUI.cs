using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

[DisallowMultipleComponent]
public class StaminaUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Image fillImage;
    [SerializeField] private TMP_Text valueText;

    [Header("Behavior")]
    [SerializeField] private float smoothSpeed = 10f;

    private PlayerController controller;
    private GamePlayerBodyRole localBodyRole;
    private float displayValue;
    private float nextFindTime;

    private void Update()
    {
        if (controller == null)
            TryFindController();

        if (controller == null || localBodyRole == null)
            return;

        float target = controller.MaxStamina > 0f
            ? Mathf.Clamp01(controller.CurrentStamina / controller.MaxStamina)
            : 0f;
        displayValue = Mathf.MoveTowards(displayValue, target, smoothSpeed * Time.deltaTime);

        if (fillImage != null)
            fillImage.fillAmount = displayValue;
        if (valueText != null)
            valueText.text = Mathf.CeilToInt(controller.CurrentStamina).ToString();
    }

    private void TryFindController()
    {
        if (Time.time < nextFindTime)
            return;

        controller = null;
        localBodyRole = null;
        var network = NetworkManager.Singleton;
        if (network == null)
        {
            nextFindTime = Time.time + 0.5f;
            return;
        }

        foreach (var no in Object.FindObjectsOfType<NetworkObject>())
        {
            if (!no.IsSpawned || !no.IsOwner)
                continue;

            localBodyRole = no.GetComponent<GamePlayerBodyRole>();
            if (localBodyRole == null)
                continue;
            controller = no.GetComponentInChildren<PlayerController>();
            if (controller != null)
                break;
        }

        nextFindTime = Time.time + 0.5f;
    }

}
