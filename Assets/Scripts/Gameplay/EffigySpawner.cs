using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Place this in the GameScene. Assign the effigy prefab and all possible
/// spawn point transforms. On game start the server picks effigyCount random
/// points (no repeats) and spawns an effigy at each.
/// </summary>
public class EffigySpawner : MonoBehaviour
{
    [SerializeField] private NetworkObject effigyPrefab;
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField] private int effigyCount = 3;

    private void Start()
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;
        if (effigyPrefab == null) { Debug.LogWarning("[EffigySpawner] No prefab assigned."); return; }
        if (spawnPoints == null || spawnPoints.Length == 0) { Debug.LogWarning("[EffigySpawner] No spawn points."); return; }

        int count = Mathf.Min(effigyCount, spawnPoints.Length);

        // Shuffle indices
        int[] indices = new int[spawnPoints.Length];
        for (int i = 0; i < indices.Length; i++) indices[i] = i;
        for (int i = indices.Length - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (indices[i], indices[j]) = (indices[j], indices[i]);
        }

        for (int i = 0; i < count; i++)
        {
            Transform pt = spawnPoints[indices[i]];
            var obj = Instantiate(effigyPrefab, pt.position, pt.rotation);
            obj.Spawn(true);
        }
    }
}
