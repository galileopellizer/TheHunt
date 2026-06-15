using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

public class ForceEnableWidgetsButtons : MonoBehaviour
{
    [SerializeField] private float retryDuration = 3f;  // keep retrying for this many seconds
    [SerializeField] private float retryInterval  = 0.2f;
    private float retryTimer;
    private float nextRetry;

    private void OnEnable()
    {
        retryTimer = retryDuration;
        nextRetry  = 0f;
    }

    private void Update()
    {
        if (retryTimer <= 0f) { enabled = false; return; }

        retryTimer -= Time.deltaTime;

        // Stop immediately if a session is being joined/created
        if (IsSessionActive()) { enabled = false; return; }

        if (Time.time >= nextRetry)
        {
            nextRetry = Time.time + retryInterval;
            ForceEnable();
        }
    }

    private static bool IsSessionActive()
    {
        try
        {
            var managerType = Type.GetType("Unity.Multiplayer.Widgets.SessionManager, Unity.Multiplayer.Widgets");
            var managerInstance = managerType?.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
            var activeSession = managerType?.GetField("m_ActiveSession", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(managerInstance);
            return activeSession != null;
        }
        catch { return false; }
    }

    private static void ForceEnable()
    {
        try
        {
            // Clear Widgets session state
            var dispatcherType = Type.GetType("Unity.Multiplayer.Widgets.WidgetEventDispatcher, Unity.Multiplayer.Widgets");
            var dispatcher = dispatcherType?.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
            dispatcherType?.GetMethod("OnSessionLeft", BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(dispatcher, null);

            var managerType = Type.GetType("Unity.Multiplayer.Widgets.SessionManager, Unity.Multiplayer.Widgets");
            var managerInstance = managerType?.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
            managerType?.GetField("m_ActiveSession", BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(managerInstance, null);
        }
        catch
        {
            // Ignore widget cleanup issues.
        }

        // Force all EnterSessionBase buttons interactable
        foreach (var mb in FindObjectsOfType<MonoBehaviour>(true))
        {
            var t = mb.GetType();
            var baseType = t;
            while (baseType != null && baseType.FullName != "Unity.Multiplayer.Widgets.EnterSessionBase")
                baseType = baseType.BaseType;
            if (baseType == null) continue;

            try
            {
                var onLeft = baseType.GetMethod("OnSessionLeft", BindingFlags.Public | BindingFlags.Instance);
                onLeft?.Invoke(mb, null);

                var buttonField = baseType.GetField("m_EnterSessionButton", BindingFlags.NonPublic | BindingFlags.Instance);
                if (buttonField != null)
                {
                    var btn = buttonField.GetValue(mb) as Button;
                    if (btn) btn.interactable = true;
                }

                var sessionProp = baseType.GetProperty("Session", BindingFlags.Public | BindingFlags.Instance);
                sessionProp?.SetValue(mb, null);
            }
            catch
            {
                // Ignore per-widget errors
            }
        }
    }
}
