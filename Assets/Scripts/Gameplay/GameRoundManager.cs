using Unity.Netcode;
using UnityEngine;
using Object = UnityEngine.Object;

public enum GameWinner : byte { None, Humans, Monsters }
public enum GamePhase  : byte { Normal, Enrage, GameOver }

public class GameRoundManager : NetworkBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────
    [Header("Round")]
    [SerializeField] private float winCheckInterval = 0.5f;

    [Header("Enrage")]
    [SerializeField] private float enrageDuration = 120f;

    [Header("Per-Effigy Monster Buffs")]
    [Tooltip("Speed multiplier added per effigy burned (stacks).")]
    [SerializeField] private float speedBonusPerEffigy = 0.07f;   // +7% per effigy

    [Tooltip("Extra speed multiplier applied on top when enrage starts.")]
    [SerializeField] private float enrageSpeedBonus = 0.25f;      // +25% on enrage

    // ── Network Variables ─────────────────────────────────────────────────────
    public NetworkVariable<GameWinner> Winner = new(
        GameWinner.None,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public NetworkVariable<GamePhase> Phase = new(
        GamePhase.Normal,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public NetworkVariable<int> BurnedEffigies = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public NetworkVariable<int> TotalEffigies = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    /// <summary>
    /// Seconds remaining in the enrage phase. Only meaningful when Phase == Enrage.
    /// </summary>
    public NetworkVariable<float> EnrageTimeRemaining = new(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    /// <summary>
    /// Combined speed multiplier for the monster (1 = base speed).
    /// PlayerController multiplies monsterSpeed by this value.
    /// </summary>
    public NetworkVariable<float> MonsterSpeedMultiplier = new(
        1f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // ── Private ───────────────────────────────────────────────────────────────
    private float nextWinCheckTime;
    private int   lastBurnedCount;

    // ── NetworkBehaviour ──────────────────────────────────────────────────────
    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        Winner.Value                 = GameWinner.None;
        Phase.Value                  = GamePhase.Normal;
        BurnedEffigies.Value         = 0;
        TotalEffigies.Value          = 0;
        EnrageTimeRemaining.Value    = 0f;
        MonsterSpeedMultiplier.Value = 1f;
        lastBurnedCount              = 0;
        nextWinCheckTime             = Time.time + winCheckInterval;
    }

    private void Update()
    {
        if (!IsServer) return;
        if (Phase.Value == GamePhase.GameOver) return;

        // ── Enrage countdown ──────────────────────────────────────────────────
        if (Phase.Value == GamePhase.Enrage)
        {
            EnrageTimeRemaining.Value -= Time.deltaTime;

            if (EnrageTimeRemaining.Value <= 0f)
            {
                EnrageTimeRemaining.Value = 0f;
                // Time's up — any surviving human wins
                EndGame(GameWinner.Humans);
                return;
            }

            // Monster can still win by killing everyone during enrage
            if (!HasAnyAliveHuman(out bool unknown) && !unknown)
            {
                EndGame(GameWinner.Monsters);
                return;
            }

            return;
        }

        // ── Normal phase checks ───────────────────────────────────────────────
        if (Time.time < nextWinCheckTime) return;
        nextWinCheckTime = Time.time + winCheckInterval;

        UpdateEffigyCounts();
        ApplyProgressiveBuffs();

        // Monster wins if all humans die before enrage
        if (!HasAnyAliveHuman(out bool humanUnknown) && !humanUnknown)
        {
            EndGame(GameWinner.Monsters);
            return;
        }

        // All required effigies burned → start enrage
        if (AreAllEffigiesBurned(out bool effigyUnknown) && !effigyUnknown)
        {
            StartEnrage();
        }
    }

    // ── Phase control ─────────────────────────────────────────────────────────
    private void StartEnrage()
    {
        Phase.Value               = GamePhase.Enrage;
        EnrageTimeRemaining.Value = enrageDuration;

        // Stack the enrage bonus on top of progressive buffs already applied
        MonsterSpeedMultiplier.Value += enrageSpeedBonus;

        Debug.Log("[GameRoundManager] ENRAGE STARTED — monster speed multiplier: " + MonsterSpeedMultiplier.Value);
    }

    private void EndGame(GameWinner winner)
    {
        Phase.Value  = GamePhase.GameOver;
        Winner.Value = winner;
    }

    // ── Buff logic ────────────────────────────────────────────────────────────
    private void ApplyProgressiveBuffs()
    {
        int burned = BurnedEffigies.Value;
        if (burned == lastBurnedCount) return;

        // Recalculate from scratch so we never double-apply
        MonsterSpeedMultiplier.Value = 1f + burned * speedBonusPerEffigy;
        lastBurnedCount = burned;

        Debug.Log($"[GameRoundManager] Effigy burned ({burned}) — monster speed x{MonsterSpeedMultiplier.Value:F2}");
    }

    // ── Win checks ────────────────────────────────────────────────────────────
    private bool HasAnyAliveHuman(out bool unknown)
    {
        unknown = false;
        bool foundHumanBody = false;

        foreach (var bodyRole in Object.FindObjectsOfType<GamePlayerBodyRole>())
        {
            var bodyNo = bodyRole.GetComponent<NetworkObject>();
            if (bodyNo == null || !bodyNo.IsSpawned) continue;
            if (bodyRole.Role.Value != PlayerRole.Human) continue;

            foundHumanBody = true;

            var health = bodyRole.GetComponent<PlayerHealth>();
            if (health == null) { unknown = true; continue; }
            if (!health.IsDead.Value) return true;
        }

        if (!foundHumanBody) { unknown = true; return true; }
        return false;
    }

    private void UpdateEffigyCounts()
    {
        GetEffigyCounts(out int burned, out int total);
        BurnedEffigies.Value = burned;
        TotalEffigies.Value  = total;
    }

    private bool AreAllEffigiesBurned(out bool unknown)
    {
        GetEffigyCounts(out int burned, out int total);
        unknown = total <= 0;
        return total > 0 && burned >= total;
    }

    private void GetEffigyCounts(out int burned, out int total)
    {
        burned = 0;
        total  = 0;

        foreach (var effigy in Object.FindObjectsOfType<EffigyState>())
        {
            if (effigy == null || !effigy.isActiveAndEnabled) continue;
            if (!effigy.IsSpawned) continue;

            total++;
            if (effigy.IsBurned.Value) burned++;
        }
    }
}
