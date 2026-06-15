using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Lobbies;
using UnityEngine;

/// <summary>
/// On startup, signs in and leaves any stale lobby memberships left
/// from previous crashed/failed sessions.
/// Attach to a GameObject in MainScene with early script execution order.
/// </summary>
public class LobbyCleanup : MonoBehaviour
{
    private async void Start()
    {
        try
        {
            if (UnityServices.State != ServicesInitializationState.Initialized)
                await UnityServices.InitializeAsync();

            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();

            var lobbies = await LobbyService.Instance.GetJoinedLobbiesAsync();
            foreach (var lobbyId in lobbies)
            {
                try { await LobbyService.Instance.RemovePlayerAsync(lobbyId, AuthenticationService.Instance.PlayerId); }
                catch { /* already removed or expired */ }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[LobbyCleanup] {e.Message}");
        }
    }
}
