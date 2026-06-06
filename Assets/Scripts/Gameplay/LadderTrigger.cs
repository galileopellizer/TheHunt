using UnityEngine;

/// <summary>
/// Add this to any ladder GameObject alongside a trigger Collider.
/// Walk into it and press W to climb up, S to climb down — Minecraft style.
/// Works for both humans and monsters.
/// </summary>
[RequireComponent(typeof(Collider))]
public class LadderTrigger : MonoBehaviour
{
    private void Awake()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        var pc = other.GetComponent<PlayerController>();
        if (pc != null) pc.SetOnLadder(true);
    }

    private void OnTriggerExit(Collider other)
    {
        var pc = other.GetComponent<PlayerController>();
        if (pc != null) pc.SetOnLadder(false);
    }
}
