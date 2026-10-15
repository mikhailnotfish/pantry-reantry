using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class ReentryDiagnostics : MonoBehaviour
{
    static readonly string[] shaderNames =
    {
        "Reentry/PlasmaShell", "Reentry/PlasmaWrap", "Reentry/PlasmaWake", "Reentry/AdditiveParticle",
        "Reentry/Streak", "Reentry/Stars", "Hidden/Reentry/PlanetDetail", "Hidden/Reentry/CubeProbe", "HDRP/Lit"
    };

    List<string> shaderLines = new List<string>();
    List<string> messages = new List<string>();
    bool show = true;
    float hideAt;
    GUIStyle style;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Create()
    {
        if (FindAnyObjectByType<ReentryDiagnostics>() != null) return;
        var go = new GameObject("Reentry Diagnostics");
        DontDestroyOnLoad(go);
        go.AddComponent<ReentryDiagnostics>();
    }

    void Awake()
    {
        hideAt = Time.realtimeSinceStartup + 15f;
        Application.logMessageReceived += OnLog;
        foreach (string n in shaderNames)
        {
            Shader s = ReentryShaders.Find(n);
            if (s == null) shaderLines.Add("MISSING   " + n);
            else shaderLines.Add((s.isSupported ? "ok        " : "NOT SUPPORTED  ") + n);
        }
    }

    void OnDestroy() { Application.logMessageReceived -= OnLog; }

    void OnLog(string text, string stack, LogType type)
    {
        if (type == LogType.Log) return;
        if (messages.Count > 14) messages.RemoveAt(0);
        if (text.Length > 170) text = text.Substring(0, 170) + "...";
        messages.Add(type + ": " + text);
    }

    void Update()
    {
        bool pressed = false;
#if ENABLE_INPUT_SYSTEM
        pressed = Keyboard.current != null && Keyboard.current.f3Key.wasPressedThisFrame;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        pressed = pressed || Input.GetKeyDown(KeyCode.F3);
#endif
        if (pressed) { show = !show; hideAt = float.MaxValue; }
        if (show && Time.realtimeSinceStartup > hideAt) show = false;
    }

    void OnGUI()
    {
        if (!show) return;
        if (style == null) style = new GUIStyle(GUI.skin.label) { richText = false };
        style.fontSize = Mathf.Max(12, Screen.height / 60);
        style.normal.textColor = Color.white;

        string t = "DIAGNOSTICS (F3 to hide)\n" + Application.version + "  " + SystemInfo.graphicsDeviceType + "\n\nSHADERS\n";
        t += string.Join("\n", shaderLines.ToArray());
        t += "\n\nWARNINGS / ERRORS\n";
        t += messages.Count == 0 ? "(none)" : string.Join("\n", messages.ToArray());

        var r = new Rect(10, 10, Screen.width - 20, Screen.height - 20);
        GUI.color = new Color(0, 0, 0, 0.75f);
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(r, t, style);
    }
}
