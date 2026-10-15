using UnityEngine;
using UnityEngine.SceneManagement;

public class ReentryResults : MonoBehaviour
{
    [Header("Hook up (found automatically if left empty)")]
    public ReentryBody body;

    [Tooltip("Seconds to wait after the object burns up (so you can enjoy the fireworks) before the results appear.")]
    public float delayAfterBurnUp = 4f;
    [Tooltip("Seconds to wait after landing before the results appear.")]
    public float delayAfterLanding = 1.5f;
    public bool hideDebugHudOnResults = true;

    [Header("Layout")]
    [Tooltip("Pushes the results text and buttons up or down (fraction of screen height). Positive = down, negative = up. The dark backdrop always fills the screen.")]
    public float uiOffsetY = 0.04f;
    [Tooltip("Moves just the two buttons up or down (fraction of screen height). Positive = down, negative = up.")]
    public float buttonsOffsetY = 0f;
    [Tooltip("Pushes the results left or right (fraction of screen width). Positive = right, negative = left.")]
    public float uiOffsetX = 0f;

    float peakTemp, peakSpeed, peakPlasma;
    float simTime;
    bool burned;
    float burnAlt, burnSpeed, burnSimTime;
    bool landed;
    float landSpeed;
    float timer;
    bool shown;
    float showAt;

    string objectName = "Object";

    GUIStyle titleStyle, verdictStyle, labelStyle, valueStyle, buttonStyle, boxStyle;
    Texture2D bg;

    void Start()
    {
        if (body == null) body = FindAnyObjectByType<ReentryBody>();
        if (body == null)
        {
            Debug.LogError("[ReentryResults] Couldn't find a ReentryBody.");
            enabled = false;
            return;
        }

        var obj = body.GetComponent<ReentryObject>();
        objectName = obj != null ? PrettyName(obj.kind) : body.name;
    }

    static string PrettyName(ReentryObject.Kind k)
    {
        switch (k)
        {
            case ReentryObject.Kind.BeanShelf: return "Shelf of Beans";
            case ReentryObject.Kind.Fridge: return "Fridge";
            case ReentryObject.Kind.Chair: return "Chair";
            case ReentryObject.Kind.Cube: return "Cube";
            case ReentryObject.Kind.Toilet: return "Toilet";
            case ReentryObject.Kind.Piano: return "Upright Piano";
            case ReentryObject.Kind.WashingMachine: return "Washing Machine";
            case ReentryObject.Kind.TrafficCone: return "Traffic Cone";
            case ReentryObject.Kind.Bathtub: return "Bathtub";
            default: return "Mystery Object";
        }
    }

    float reloadAt = -1f;

    void Update()
    {
        if (reloadAt >= 0f && Time.unscaledTime >= reloadAt)
        {
            reloadAt = -1f;
            ReentryMenu.Reload();
            return;
        }
        if (body == null || shown) return;

        simTime += Time.deltaTime * body.timeScale;
        peakTemp = Mathf.Max(peakTemp, body.TemperatureKelvin);
        peakSpeed = Mathf.Max(peakSpeed, body.SpeedMetersPerSecond);
        peakPlasma = Mathf.Max(peakPlasma, body.Plasma);

        if (body.BurnedUp && !burned)
        {
            burned = true;
            burnAlt = body.AltitudeMeters;
            burnSpeed = body.SpeedMetersPerSecond;
            burnSimTime = simTime;
        }

        if (!landed && body.AltitudeMeters <= 0f)
        {
            landed = true;
            landSpeed = body.SpeedMetersPerSecond;
        }

        if (burned || landed)
        {
            timer += Time.deltaTime;
            float wait = burned ? delayAfterBurnUp : delayAfterLanding;
            if (timer >= wait)
            {
                shown = true;
                showAt = Time.time;
                if (hideDebugHudOnResults) body.showDebugHud = false;
            }
        }
    }

    string Headline()
    {
        if (burned) return "BURNED UP";
        return "SURVIVED!";
    }

    string Verdict()
    {
        string n = objectName;
        if (burned)
        {
            float km = burnAlt / 1000f;
            if (km > 85f) return $"The {n} was vaporised almost instantly. It barely touched the atmosphere.";
            if (km > 70f) return $"The {n} lasted about a second in the glow before it was gone.";
            if (km > 50f) return $"The {n} put on a brilliant show, then came apart at {km:F0} km.";
            if (km > 30f) return $"The {n} fought hard, but the air won at {km:F0} km.";
            return $"Impressive. The {n} made it all the way down to {km:F0} km before giving up.";
        }
        if (landSpeed > 150f) return $"The {n} survived the fire, then hit the ground at {landSpeed:F0} m/s. Whatever is left is very flat.";
        return $"The {n} made it all the way down. Somewhere, a very confused farmer is looking at it.";
    }

    string Grade()
    {
        if (!burned) return "S";
        float km = burnAlt / 1000f;
        if (km > 85f) return "F";
        if (km > 70f) return "D";
        if (km > 50f) return "C";
        if (km > 30f) return "B";
        return "A";
    }

    void MakeStyles()
    {
        if (titleStyle != null) return;

        bg = new Texture2D(1, 1);
        bg.SetPixel(0, 0, new Color(0.04f, 0.04f, 0.06f, 0.88f));
        bg.Apply();

        boxStyle = new GUIStyle(GUI.skin.box);
        boxStyle.normal.background = bg;

        titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 46,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        verdictStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 20,
            alignment = TextAnchor.MiddleCenter,
            wordWrap = true
        };
        verdictStyle.normal.textColor = new Color(1f, 0.9f, 0.75f);
        labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, alignment = TextAnchor.MiddleLeft };
        labelStyle.normal.textColor = new Color(0.7f, 0.72f, 0.78f);
        valueStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleRight };
        valueStyle.normal.textColor = Color.white;
        buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 22, fontStyle = FontStyle.Bold };
    }

    void OnGUI()
    {
        if (!shown) return;
        MakeStyles();

        float a = Mathf.Clamp01((Time.time - showAt) / 0.8f);
        GUI.color = new Color(1f, 1f, 1f, a);

        float u = Screen.height / 1080f;
        float W = Screen.width, H = Screen.height;

        titleStyle.fontSize = Mathf.RoundToInt(96 * u);
        verdictStyle.fontSize = Mathf.RoundToInt(32 * u);
        labelStyle.fontSize = Mathf.RoundToInt(30 * u);
        valueStyle.fontSize = Mathf.RoundToInt(30 * u);
        buttonStyle.fontSize = Mathf.RoundToInt(38 * u);

        GUI.Box(new Rect(0f, 0f, W, H), GUIContent.none, boxStyle);

        Matrix4x4 oldMatrix = GUI.matrix;
        GUI.matrix = Matrix4x4.Translate(new Vector3(W * uiOffsetX, H * uiOffsetY, 0f));

        titleStyle.normal.textColor = burned ? new Color(1f, 0.45f, 0.1f) : new Color(0.4f, 1f, 0.6f);
        GUI.Label(new Rect(0f, H * 0.07f, W, 130f * u), Headline(), titleStyle);

        var nameStyle = new GUIStyle(verdictStyle) { fontSize = Mathf.RoundToInt(28 * u), fontStyle = FontStyle.Bold };
        GUI.Label(new Rect(0f, H * 0.07f + 125f * u, W, 40f * u), objectName.ToUpper(), nameStyle);

        GUI.Label(new Rect(W * 0.15f, H * 0.07f + 180f * u, W * 0.7f, 110f * u), Verdict(), verdictStyle);

        float colX = W * 0.25f, colW = W * 0.5f;
        float rowH = 56f * u;
        float rowY = H * 0.07f + 330f * u;
        Row(colX, colW, rowH, ref rowY, "Grade", Grade());
        if (burned)
        {
            Row(colX, colW, rowH, ref rowY, "Burned up at", $"{burnAlt / 1000f:F1} km");
            Row(colX, colW, rowH, ref rowY, "Speed at burn-up", $"{burnSpeed:F0} m/s");
            Row(colX, colW, rowH, ref rowY, "Time in the atmosphere", FormatTime(burnSimTime));
        }
        else
        {
            Row(colX, colW, rowH, ref rowY, "Landing speed", $"{landSpeed:F0} m/s");
            Row(colX, colW, rowH, ref rowY, "Time in the atmosphere", FormatTime(simTime));
        }
        Row(colX, colW, rowH, ref rowY, "Hottest skin", $"{peakTemp:F0} K  (limit {body.failTemperature:F0} K)");
        Row(colX, colW, rowH, ref rowY, "Top speed", $"{peakSpeed:F0} m/s");

        float bw = 400f * u, bh = 84f * u, bgap = 40f * u;

        float by = rowY + 50f * u + H * buttonsOffsetY;
        by = Mathf.Min(by, H - bh - 24f * u - H * uiOffsetY);
        float bx = (W - (2f * bw + bgap)) * 0.5f;
        if (GUI.Button(new Rect(bx, by, bw, bh), "Drop again", buttonStyle))
        {
            if (reloadAt < 0f)
            {
                ReentryAudio.Click();
                ReentryMenu.Launched = true;
                reloadAt = Time.unscaledTime + 0.18f;
            }
        }
        if (GUI.Button(new Rect(bx + bw + bgap, by, bw, bh), "Main menu", buttonStyle))
        {
            if (reloadAt < 0f)
            {
                ReentryAudio.Click();
                ReentryMenu.Launched = false;
                reloadAt = Time.unscaledTime + 0.18f;
            }
        }

        GUI.matrix = oldMatrix;
        GUI.color = Color.white;
    }

    void Row(float x, float w, float h, ref float rowY, string label, string value)
    {
        GUI.Label(new Rect(x, rowY, w * 0.5f, h), label, labelStyle);
        GUI.Label(new Rect(x + w * 0.5f, rowY, w * 0.5f, h), value, valueStyle);
        rowY += h;
    }

    static string FormatTime(float s)
    {
        int m = Mathf.FloorToInt(s / 60f);
        int sec = Mathf.FloorToInt(s % 60f);
        return m > 0 ? $"{m}m {sec:D2}s" : $"{sec}s";
    }
}
