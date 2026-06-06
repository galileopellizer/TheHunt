using System.Threading.Tasks;
using System;
using System.Reflection;
using Unity.Netcode;
using Unity.Services.Multiplayer;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LeaveSessionAndDeleteHost : MonoBehaviour
{
    [SerializeField] private string mainSceneName = "MainScene";
    private bool leaving;

    public void Leave()
    {
        if (leaving) return;
        leaving = true;
        _ = LeaveAsync();
    }

    private async Task LeaveAsync()
    {
        try
        {
            await TryLeaveWidgetsSessionAsync();

            var service = MultiplayerService.Instance;
            if (service != null && service.Sessions != null)
            {
                foreach (var kvp in service.Sessions)
                {
                    var session = kvp.Value;
                    if (session == null) continue;

                    try
                    {
                        if (session.IsHost)
                        {
                            await session.AsHost().DeleteAsync();
                        }
                        else if (session.IsMember)
                        {
                            await session.LeaveAsync();
                        }
                    }
                    catch
                    {
                        // Ignore cleanup errors; we still leave the scene.
                    }
                }
            }
        }
        finally
        {
            ForceWidgetsSessionLeft();

            if (NetworkManager.Singleton != null)
                NetworkManager.Singleton.Shutdown();

            SceneManager.LoadScene(mainSceneName, LoadSceneMode.Single);
        }
    }

    private static async Task TryLeaveWidgetsSessionAsync()
    {
        try
        {
            var type = Type.GetType("Unity.Multiplayer.Widgets.SessionManager, Unity.Multiplayer.Widgets");
            if (type == null) return;

            var instanceProp = type.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
            var instance = instanceProp?.GetValue(null);
            if (instance == null) return;

            var leaveMethod = type.GetMethod("LeaveSession", BindingFlags.NonPublic | BindingFlags.Instance);
            if (leaveMethod == null) return;

            var task = leaveMethod.Invoke(instance, null) as Task;
            if (task != null) await task;
        }
        catch
        {
            // Ignore widget cleanup issues.
        }
    }

    private static void ForceWidgetsSessionLeft()
    {
        try
        {
            var dispatcherType = Type.GetType("Unity.Multiplayer.Widgets.WidgetEventDispatcher, Unity.Multiplayer.Widgets");
            var instanceProp = dispatcherType?.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
            var dispatcher = instanceProp?.GetValue(null);
            var onLeft = dispatcherType?.GetMethod("OnSessionLeft", BindingFlags.NonPublic | BindingFlags.Instance);
            onLeft?.Invoke(dispatcher, null);

            var managerType = Type.GetType("Unity.Multiplayer.Widgets.SessionManager, Unity.Multiplayer.Widgets");
            var managerInstance = managerType?.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
            var activeField = managerType?.GetField("m_ActiveSession", BindingFlags.NonPublic | BindingFlags.Instance);
            activeField?.SetValue(managerInstance, null);
        }
        catch
        {
            // Ignore widget cleanup issues.
        }
    }
}
