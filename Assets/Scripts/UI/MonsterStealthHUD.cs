using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Monster stealth HUD — bottom center of screen.
///
/// Setup:
///   - hudRoot      : the whole panel (hidden for humans)
///   - fillBar      : passive bar  — Image Type=Filled, Fill Method=Horizontal
///   - activeBar    : active timer bar — same setup, drains while active stealth ticks
///   - statusLabel  : "STAND STILL" / "INVISIBLE" / "ACTIVE STEALTH"
///   - chargesLabel : "CHARGES  2 / 2"
/// </summary>
public class MonsterStealthHUD : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private GameObject hudRoot;
    [SerializeField] private Image      fillBar;      // passive progress + active timer — same bar
    [SerializeField] private TMP_Text   statusLabel;
    [SerializeField] private TMP_Text   chargesLabel;

    [Header("Colors")]
    [SerializeField] private Color barNormalColor = new Color(0.4f, 0.8f, 1f);
    [SerializeField] private Color barInvisColor  = new Color(0.6f, 0.3f, 1f);
    [SerializeField] private Color barActiveColor = new Color(1f,   0.3f, 0.8f);

    [Header("Smoothing")]
    [SerializeField] private float fillSpeed  = 2f;
    [SerializeField] private float drainSpeed = 6f;

    private MonsterStealth stealth;
    private float nextRefresh;
    private float smoothedFill;

    private void Awake()
    {
        if (hudRoot != null) hudRoot.SetActive(false);
    }

    private void Update()
    {
        if (stealth == null && Time.time > nextRefresh)
        {
            nextRefresh = Time.time + 0.5f;
            stealth = MonsterStealth.LocalMonsterStealth;
        }

        bool isMonster = stealth != null;
        if (hudRoot != null) hudRoot.SetActive(isMonster);
        if (!isMonster) return;

        bool  passiveInvis = stealth.IsPassiveInvisible.Value;
        bool  activeInvis  = stealth.IsActiveInvisible.Value;
        int   charges      = stealth.ActiveChargesLeft.Value;
        int   maxCharges   = stealth.MaxActiveCharges;
        float targetFill   = stealth.StillProgress;

        // ── Single bar — passive fill OR active timer drain ───────────────────
        float displayFill;
        Color displayColor;

        if (activeInvis)
        {
            displayFill  = stealth.ActiveStealthProgress; // counts down 1→0
            displayColor = barActiveColor;
        }
        else
        {
            float speed  = targetFill > smoothedFill ? fillSpeed : drainSpeed;
            smoothedFill = Mathf.MoveTowards(smoothedFill, targetFill, speed * Time.deltaTime);
            if (passiveInvis) smoothedFill = 1f;
            displayFill  = smoothedFill;
            displayColor = passiveInvis ? barInvisColor : barNormalColor;
        }

        if (fillBar != null)
        {
            fillBar.fillAmount = displayFill;
            fillBar.color      = displayColor;
        }

        // ── Status label ──────────────────────────────────────────────────────
        if (statusLabel != null)
        {
            statusLabel.text = activeInvis  ? "ACTIVE STEALTH"
                             : passiveInvis ? "INVISIBLE"
                             : targetFill >= 1f ? "READY"
                             : "STAND STILL";
        }

        // ── Charges ───────────────────────────────────────────────────────────
        if (chargesLabel != null)
            chargesLabel.text = $"CHARGES  {charges} / {maxCharges}";
    }
}
