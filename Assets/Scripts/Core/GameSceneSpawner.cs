using Unity.Netcode;
using UnityEngine;

public class GameSceneSpawner : MonoBehaviour
{
    [SerializeField] private NetworkObject gamePlayerBodyPrefab;


    [SerializeField] private Transform[] humanSpawnPoints;
    [SerializeField] private Transform[] monsterSpawnPoints;

    private void Start()
    {
        if (NetworkManager.Singleton == null) return;
        if (!NetworkManager.Singleton.IsServer) return;

        DespawnExistingGameBodies();

        int humanIndex = 0;
        int monsterIndex = 0;

        foreach (var kvp in NetworkManager.Singleton.ConnectedClients)
        {
            ulong clientId = kvp.Key;

            var identity = kvp.Value.PlayerObject;
            var role = identity.GetComponent<PlayerRoleState>().Role.Value;

            NetworkObject prefabToSpawn;
            Transform spawnPoint;
            
            prefabToSpawn = gamePlayerBodyPrefab;

            if (role == PlayerRole.Monster)
            {
                spawnPoint = monsterSpawnPoints[monsterIndex++ % monsterSpawnPoints.Length];
            }
            else
            {
                spawnPoint = humanSpawnPoints[humanIndex++ % humanSpawnPoints.Length];
            }

            var obj = Instantiate(prefabToSpawn, spawnPoint.position, spawnPoint.rotation);
            obj.SpawnWithOwnership(clientId, true);

            // Copy role from lobby PlayerIdentity → spawned body
            var bodyRole = obj.GetComponent<GamePlayerBodyRole>();
            bodyRole.Role.Value = identity.GetComponent<PlayerRoleState>().Role.Value;

            // Copy name from lobby PlayerIdentity → spawned body
            var bodyName = obj.GetComponent<GamePlayerBodyNameState>();
            if (bodyName != null)
            {
                var identityName = identity.GetComponent<PlayerNameState>();
                if (identityName != null && identityName.PlayerName.Value.Length > 0)
                {
                    bodyName.PlayerName.Value = identityName.PlayerName.Value;
                }
            }
            
        }
    }

    private static void DespawnExistingGameBodies()
    {
        var bodies = FindObjectsOfType<GamePlayerBodyRole>(true);
        if (bodies == null || bodies.Length == 0) return;

        foreach (var body in bodies)
        {
            var netObj = body.GetComponent<NetworkObject>();
            if (netObj == null || !netObj.IsSpawned) continue;

            netObj.Despawn(true);
        }
    }
}
