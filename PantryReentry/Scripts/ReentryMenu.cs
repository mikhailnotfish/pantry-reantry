using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering.HighDefinition;

public class ReentryMenu : MonoBehaviour
{

    public static bool Launched;
    public static bool HasChoice;
    public static ReentryObject.Kind Selected = ReentryObject.Kind.Fridge;
    public static bool Launching;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOnPlay()
    {
        Launched = false;
        HasChoice = false;
        Launching = false;
        Selected = ReentryObject.Kind.Fridge;
    }

    public static void Reload()
    {
        int i = SceneManager.GetActiveScene().buildIndex;
        if (i < 0)
        {
            Debug.LogError("[Reentry] Add this scene in File > Build Profiles > Add Open Scenes, then try again.");
            return;
        }
        SceneManager.LoadScene(i);
    }

    [Header("Text")]
    public string titleTop = "PANTRY";
    public string titleBottom = "REENTRY";
    public string tagline = "Everything burns eventually.";
    public string version = "v0.1";

    [Header("Card previews")]
    [Tooltip("Show a small spinning 3D copy of each object in the bottom-left of its card.")]
    public bool showPreviews = true;
    [Tooltip("Size of the preview square relative to the card. 1 is the normal size.")]
    public float previewScale = 1f;
    [Tooltip("How many previews are redrawn each frame. Lower = smoother game on slow graphics cards, but the previews update less often.")]
    public int previewsPerFrame = 4;
    [Tooltip("Degrees per second the previews turn.")]
    public float previewSpin = 40f;
    [Tooltip("How fast the preview turns while you hover its card.")]
    public float previewSpinHover = 160f;
    [Tooltip("The previews live on this Unity layer, far away from the scene. Change it only if you already use layer 31.")]
    public int previewLayer = 31;

    [Header("Feel")]
    [Tooltip("How fast the camera slowly circles the object behind the menu (degrees per second).")]
    public float idleSpin = 4f;
    [Tooltip("Seconds for the title to slide into the selection screen.")]
    public float transitionTime = 1.1f;
    [Tooltip("Seconds for the screen to fade to black when you launch.")]
    public float launchFadeTime = 0.7f;
    [Tooltip("Where the object sits on the title screen: 0 = middle, 0.3 = far right, -0.3 = far left (fraction of screen width).")]
    public float titleShiftX = 0.28f;
    [Tooltip("Where the object sits on the selection screen. Positive Y lifts it up so the cards don't cover it.")]
    public float selectShiftX = 0f;
    public float selectShiftY = 0.2f;
    [Tooltip("How many cards go across before starting a new row.")]
    public int cardsPerRow = 4;
    [Tooltip("Moves just the cards (Fridge, Chair, Shelf) left/right, on top of the offset above. Negative = left.")]
    public float cardsOffsetX = 0f;
    [Tooltip("Moves just the three cards up/down. Positive = down, negative = up.")]
    public float cardsOffsetY = 0f;
    [Tooltip("Pushes the selection screen's header and cards left or right (fraction of screen width). Negative = left, positive = right.")]
    public float selectOffsetX = -0.06f;
    [Tooltip("How far from the left edge the title and buttons start (fraction of screen width).")]
    public float titleLeftMargin = 0.12f;
    [Tooltip("Pushes the title, buttons and cards down the screen (fraction of screen height). 0.1 = a tenth of the screen.")]
    public float uiOffsetY = 0.1f;
    public Color accent = new Color(1f, 0.55f, 0.12f);

    struct Entry
    {
        public ReentryObject.Kind kind;
        public string name, mass, toughness, blurb;
    }

    Entry[] entries =
    {
        new Entry { kind = ReentryObject.Kind.Fridge,         name = "FRIDGE",          mass = "90 kg",  toughness = "MEDIUM",   blurb = "Insulated confidence in a white box. Cold inside, soon to be hot outside." },
        new Entry { kind = ReentryObject.Kind.Chair,          name = "CHAIR",           mass = "7 kg",   toughness = "LOW",      blurb = "Lovely dining furniture. Absolutely not designed for this." },
        new Entry { kind = ReentryObject.Kind.BeanShelf,      name = "SHELF OF BEANS",  mass = "150 kg", toughness = "HIGH",     blurb = "Steel shelving and 112 cans. The heaviest payload and the tastiest debris." },
        new Entry { kind = ReentryObject.Kind.Toilet,         name = "TOILET",          mass = "40 kg",  toughness = "MEDIUM",   blurb = "Hard porcelain, terrible aerodynamics. Please keep the lid down." },
        new Entry { kind = ReentryObject.Kind.Piano,          name = "UPRIGHT PIANO",   mass = "250 kg", toughness = "LOW",      blurb = "A quarter-tonne of music. Expect a very dramatic final note." },
        new Entry { kind = ReentryObject.Kind.WashingMachine, name = "WASHING MACHINE", mass = "75 kg",  toughness = "HIGH",     blurb = "Spin cycle: infinite. Now with an extra hot wash." },
        new Entry { kind = ReentryObject.Kind.TrafficCone,    name = "TRAFFIC CONE",    mass = "4 kg",   toughness = "VERY LOW", blurb = "Light, orange and deeply unprepared for the atmosphere." },
        new Entry { kind = ReentryObject.Kind.Bathtub,        name = "BATHTUB",         mass = "120 kg", toughness = "HIGH",     blurb = "Cast iron with a little water left in. A hot bath is coming." },
    };

    ReentryBody body;
    ReentryOrbitCamera orbit;

    RenderTexture[] previewTex;
    Camera[] previewCams;
    Transform[] previewPivots;
    float[] previewAngle;
    int hoverIndex = -1;
    int previewGroup;
    int lastHoverIndex = -1;
    int lastBarHover;
    bool showing;

    float progress, targetProgress;
    float launchT = -1f;
    bool settingsOpen;
    float settingsT;
    int dragId = -1;
    ReentryObject.Kind pending;

    GUIStyle titleStyle, tagStyle, itemStyle, smallStyle, cardNameStyle, statStyle, blurbStyle, headStyle;
    Texture2D white, gradient;

    void Start()
    {
        Launching = false;
        showing = !Launched;
        body = FindAnyObjectByType<ReentryBody>();
        orbit = FindAnyObjectByType<ReentryOrbitCamera>();

        if (showing)
        {
            if (body != null)
            {
                body.timeScale = 0f;
                body.showDebugHud = false;
            }
            if (orbit != null) { orbit.idleSpin = idleSpin; orbit.ShiftX = titleShiftX; }
            if (showPreviews) BuildPreviews();
        }
    }

    void BuildPreviews()
    {
        var obj = body != null ? body.GetComponent<ReentryObject>() : null;
        if (obj == null)
        {
            Debug.LogWarning("[ReentryMenu] Card previews need the Reentry Object script on your cube.");
            return;
        }

        Camera main = Camera.main != null ? Camera.main : FindAnyObjectByType<Camera>();
        if (main != null) main.cullingMask &= ~(1 << previewLayer);

        var sky = FindAnyObjectByType<ReentrySky>();
        Vector3 sunDir = (sky != null && sky.sun != null) ? sky.sun.transform.forward : new Vector3(0.4f, -0.45f, 0.8f).normalized;
        Vector3 viewDir = (Quaternion.AngleAxis(30f, Vector3.up) * sunDir).normalized;

        int n = entries.Length;
        previewTex = new RenderTexture[n];
        previewCams = new Camera[n];
        previewPivots = new Transform[n];
        previewAngle = new float[n];

        for (int i = 0; i < n; i++)
        {
            var pivotGo = new GameObject("Menu Preview " + entries[i].name);
            pivotGo.transform.position = new Vector3(2000f + i * 300f, 2000f, 2000f);
            obj.BuildInto(entries[i].kind, pivotGo.transform);
            foreach (var t in pivotGo.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = previewLayer;

            Bounds b = new Bounds(pivotGo.transform.position, Vector3.zero);
            bool has = false;
            foreach (var r in pivotGo.GetComponentsInChildren<Renderer>())
            {
                if (!has) { b = r.bounds; has = true; } else b.Encapsulate(r.bounds);
            }
            float radius = Mathf.Max(0.3f, b.extents.magnitude);
            Vector3 center = b.center;

            const float fov = 28f;
            float dist = radius / Mathf.Sin(fov * 0.5f * Mathf.Deg2Rad) * 1.05f;

            var rt = new RenderTexture(256, 256, 24, RenderTextureFormat.ARGB32);
            rt.name = "Menu Preview";

            var camGo = new GameObject("Menu Preview Camera " + entries[i].name);
            camGo.transform.position = center - viewDir * dist;
            camGo.transform.rotation = Quaternion.LookRotation(viewDir, Vector3.up);
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = fov;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = dist + radius * 2f;
            cam.cullingMask = 1 << previewLayer;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.02f, 0.02f, 0.03f, 1f);
            cam.targetTexture = rt;

            var hd = camGo.GetComponent<HDAdditionalCameraData>();
            if (hd == null) hd = camGo.AddComponent<HDAdditionalCameraData>();
            hd.clearColorMode = HDAdditionalCameraData.ClearColorMode.Color;
            hd.backgroundColorHDR = cam.backgroundColor;
            hd.antialiasing = HDAdditionalCameraData.AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            hd.volumeLayerMask = ~0;

            cam.enabled = false;

            previewTex[i] = rt;
            previewCams[i] = cam;
            previewPivots[i] = pivotGo.transform;
            previewAngle[i] = i * 70f;
        }
    }

    void OnDestroy()
    {
        if (previewTex != null)
            foreach (var rt in previewTex) if (rt != null) { rt.Release(); Destroy(rt); }
        if (previewCams != null)
            foreach (var c in previewCams) if (c != null) Destroy(c.gameObject);
        if (previewPivots != null)
            foreach (var p in previewPivots) if (p != null) Destroy(p.gameObject);
    }

    void Update()
    {
        if (!showing) return;

        float dt = Time.unscaledDeltaTime;

        if (previewCams != null)
        {
            bool on = progress > 0.4f && launchT < 0f;
            int per = Mathf.Max(1, previewsPerFrame);
            int groups = Mathf.Max(1, Mathf.CeilToInt(previewCams.Length / (float)per));
            previewGroup = (previewGroup + 1) % groups;
            for (int i = 0; i < previewCams.Length; i++)
            {
                if (previewCams[i] == null) continue;
                previewCams[i].enabled = on && (i % groups) == previewGroup;
                previewAngle[i] += (i == hoverIndex ? previewSpinHover : previewSpin) * dt;
                previewPivots[i].rotation = Quaternion.Euler(0f, previewAngle[i], 0f);
            }
        }

        progress = Mathf.MoveTowards(progress, targetProgress, dt / Mathf.Max(0.05f, transitionTime));
        settingsT = Mathf.MoveTowards(settingsT, settingsOpen ? 1f : 0f, dt / 0.35f);

        if (launchT >= 0f)
        {
            launchT += dt;
            if (launchT >= launchFadeTime)
            {
                launchT = -1f;
                Selected = pending;
                HasChoice = true;
                Launched = true;
                Reload();
            }
        }
    }

    void GoToSelect()
    {
        targetProgress = 1f;
        if (orbit != null)
        {
            orbit.TargetDistance *= 0.8f; orbit.idleSpin = idleSpin * 0.5f;
            orbit.ShiftX = selectShiftX; orbit.ShiftY = selectShiftY;
        }
    }

    void GoToTitle()
    {
        targetProgress = 0f;
        if (orbit != null)
        {
            orbit.TargetDistance *= 1.25f; orbit.idleSpin = idleSpin;
            orbit.ShiftX = titleShiftX; orbit.ShiftY = 0f;
        }
    }

    void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void MakeStyles()
    {
        if (titleStyle != null) return;

        white = new Texture2D(1, 1);
        white.SetPixel(0, 0, Color.white);
        white.Apply();

        gradient = new Texture2D(256, 1, TextureFormat.RGBA32, false);
        gradient.wrapMode = TextureWrapMode.Clamp;
        for (int x = 0; x < 256; x++)
        {
            float t = x / 255f;
            gradient.SetPixel(x, 0, new Color(0f, 0f, 0f, 0.88f * Mathf.Pow(1f - t, 1.6f)));
        }
        gradient.Apply();

        titleStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
        tagStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleLeft, fontStyle = FontStyle.Italic };
        itemStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
        smallStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleLeft };
        cardNameStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
        statStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleLeft };
        blurbStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperLeft, wordWrap = true };
        headStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
    }

    GUIStyle valueStyleCache;
    GUIStyle itemStyleSmall(float u)
    {
        if (valueStyleCache == null) valueStyleCache = new GUIStyle(statStyle) { fontStyle = FontStyle.Bold };
        valueStyleCache.fontSize = Mathf.RoundToInt(32 * u);
        return valueStyleCache;
    }

    static float Smooth(float a, float b, float t) { return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a, b, t)); }

    void Fill(Rect r, Color c, float alpha)
    {
        c.a *= alpha;
        var old = GUI.color;
        GUI.color = c;
        GUI.DrawTexture(r, white);
        GUI.color = old;
    }

    void Text(Rect r, string s, GUIStyle st, Color c, float alpha)
    {
        c.a *= alpha;
        var old = GUI.color;
        st.normal.textColor = c;
        GUI.color = Color.white;
        GUI.Label(r, s, st);
        GUI.color = old;
    }

    bool Bar(Rect r, string label, float alpha, bool active, float u)
    {
        bool hover = active && r.Contains(Event.current.mousePosition);
        if (Event.current.type == EventType.Repaint)
        {
            Fill(r, hover ? new Color(0.22f, 0.22f, 0.26f, 0.92f) : new Color(0.05f, 0.05f, 0.07f, 0.78f), alpha);
            Fill(new Rect(r.x, r.y, (hover ? 12f : 5f) * u, r.height), hover ? accent : new Color(0.55f, 0.57f, 0.62f), alpha);
        }
        Text(new Rect(r.x + 34f * u, r.y, r.width - 40f * u, r.height), label, itemStyle,
             hover ? Color.white : new Color(0.88f, 0.9f, 0.95f), alpha);

        if (Event.current.type == EventType.Repaint)
        {
            int id = label.GetHashCode();
            if (hover && lastBarHover != id) { ReentryAudio.Hover(); lastBarHover = id; }
            else if (!hover && lastBarHover == id) lastBarHover = 0;
        }

        bool clicked = active && GUI.Button(r, GUIContent.none, GUIStyle.none);
        if (clicked) ReentryAudio.Click();
        return clicked;
    }

    void OnGUI()
    {
        if (!showing) return;
        MakeStyles();

        float u = Screen.height / 1080f;
        float W = Screen.width, H = Screen.height;

        titleStyle.fontSize = Mathf.RoundToInt(130 * u);
        tagStyle.fontSize = Mathf.RoundToInt(34 * u);
        itemStyle.fontSize = Mathf.RoundToInt(36 * u);
        smallStyle.fontSize = Mathf.RoundToInt(22 * u);
        cardNameStyle.fontSize = Mathf.RoundToInt(46 * u);
        statStyle.fontSize = Mathf.RoundToInt(26 * u);
        blurbStyle.fontSize = Mathf.RoundToInt(26 * u);
        headStyle.fontSize = Mathf.RoundToInt(32 * u);

        float titleA = (1f - Smooth(0f, 0.5f, progress)) * (1f - settingsT);
        float selA = Smooth(0.45f, 1f, progress);
        bool titleLive = titleA > 0.95f && launchT < 0f;
        bool selLive = selA > 0.95f && launchT < 0f;

        if (Event.current.type == EventType.Repaint)
        {
            var old = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, Mathf.Max(Mathf.Max(titleA, selA * 0.8f), settingsT * 0.9f));
            GUI.DrawTexture(new Rect(0, 0, W * 0.62f, H), gradient);
            GUI.color = old;
        }

        Matrix4x4 oldMatrix = GUI.matrix;
        GUI.matrix = Matrix4x4.Translate(new Vector3(0f, H * uiOffsetY, 0f));
        if (titleA > 0.001f) DrawTitle(u, W, H, titleA, titleLive);
        if (settingsT > 0.001f) DrawSettings(u, W, H, settingsT, settingsT > 0.95f && launchT < 0f);
        if (selA > 0.001f)
        {

            GUI.matrix = Matrix4x4.Translate(new Vector3(W * selectOffsetX, H * uiOffsetY, 0f));
            DrawSelect(u, W, H, selA, selLive);
        }
        GUI.matrix = oldMatrix;

        if (launchT >= 0f && Event.current.type == EventType.Repaint)
            Fill(new Rect(0, 0, W, H), Color.black, Mathf.SmoothStep(0f, 1f, launchT / Mathf.Max(0.05f, launchFadeTime)));
    }

    void DrawTitle(float u, float W, float H, float a, bool live)
    {

        float slide = (1f - a) * -160f * u;
        float x = W * titleLeftMargin + slide;
        float y = H * 0.14f;

        Fill(new Rect(x + 6f * u, y + 12f * u, 10f * u, 260f * u), accent, a);
        Text(new Rect(x + 40f * u, y, W * 0.6f, 150f * u), titleTop, titleStyle, Color.white, a);
        Text(new Rect(x + 40f * u, y + 128f * u, W * 0.6f, 150f * u), titleBottom, titleStyle, accent, a);
        Text(new Rect(x + 44f * u, y + 285f * u, W * 0.5f, 50f * u), tagline, tagStyle, new Color(0.85f, 0.88f, 0.95f), a);

        float bw = 520f * u, bh = 84f * u;
        float by = H * 0.55f;
        if (Bar(new Rect(x + 40f * u, by, bw, bh), "PLAY", a, live, u)) GoToSelect();
        if (Bar(new Rect(x + 40f * u, by + (bh + 16f * u), bw, bh), "SETTINGS", a, live, u)) settingsOpen = true;
        if (Bar(new Rect(x + 40f * u, by + 2f * (bh + 16f * u), bw, bh), "QUIT", a, live, u)) Quit();

        Matrix4x4 keep = GUI.matrix;
        GUI.matrix = Matrix4x4.identity;
        Text(new Rect(x + 40f * u, H - 70f * u, 400f * u, 36f * u), version, smallStyle, new Color(0.6f, 0.62f, 0.7f), a);
        GUI.matrix = keep;
    }

    GUIStyle settingsHeadStyle;

    void DrawSettings(float u, float W, float H, float a, bool live)
    {
        if (settingsHeadStyle == null) settingsHeadStyle = new GUIStyle(headStyle) { fontStyle = FontStyle.Bold };
        settingsHeadStyle.fontSize = Mathf.RoundToInt(64 * u);

        float x = W * titleLeftMargin + 40f * u + (1f - a) * 60f * u;
        float y = H * 0.08f;
        float rw = 820f * u, rh = 64f * u, gap = 10f * u;

        Fill(new Rect(x - 34f * u, y + 8f * u, 10f * u, 70f * u), accent, a);
        Text(new Rect(x, y, rw, 90f * u), "SETTINGS", settingsHeadStyle, Color.white, a);
        y += 100f * u;

        bool changed = false;
        float v;

        v = SliderRow(new Rect(x, y, rw, rh), "MASTER VOLUME", ReentrySettings.Master, 0f, 1f, Mathf.RoundToInt(ReentrySettings.Master * 100f) + "%", 1, a, live, u);
        if (v != ReentrySettings.Master) { ReentrySettings.Master = v; changed = true; } y += rh + gap;
        v = SliderRow(new Rect(x, y, rw, rh), "MUSIC", ReentrySettings.Music, 0f, 1f, Mathf.RoundToInt(ReentrySettings.Music * 100f) + "%", 2, a, live, u);
        if (v != ReentrySettings.Music) { ReentrySettings.Music = v; changed = true; } y += rh + gap;
        v = SliderRow(new Rect(x, y, rw, rh), "EFFECTS", ReentrySettings.Sfx, 0f, 1f, Mathf.RoundToInt(ReentrySettings.Sfx * 100f) + "%", 3, a, live, u);
        if (v != ReentrySettings.Sfx) { ReentrySettings.Sfx = v; changed = true; } y += rh + gap;
        v = SliderRow(new Rect(x, y, rw, rh), "CAMERA SHAKE", ReentrySettings.Shake, 0f, 1.5f, Mathf.RoundToInt(ReentrySettings.Shake * 100f) + "%", 4, a, live, u);
        if (v != ReentrySettings.Shake) { ReentrySettings.Shake = v; changed = true; } y += rh + gap;

        if (ToggleRow(new Rect(x, y, rw, rh), "TERRAIN DETAIL", ReentrySettings.TerrainDetail ? "ON" : "OFF", ReentrySettings.TerrainDetail, a, live, u))
        { ReentrySettings.TerrainDetail = !ReentrySettings.TerrainDetail; changed = true; } y += rh + gap;
        if (ToggleRow(new Rect(x, y, rw, rh), "FULLSCREEN", ReentrySettings.Fullscreen ? "ON" : "OFF", ReentrySettings.Fullscreen, a, live, u))
        { ReentrySettings.Fullscreen = !ReentrySettings.Fullscreen; changed = true; } y += rh + gap;
        if (ToggleRow(new Rect(x, y, rw, rh), "VSYNC", ReentrySettings.VSync ? "ON" : "OFF", ReentrySettings.VSync, a, live, u))
        { ReentrySettings.VSync = !ReentrySettings.VSync; changed = true; } y += rh + gap;

        string fpsText = ReentrySettings.FpsCap <= 0 ? "NO LIMIT" : ReentrySettings.FpsCap + " FPS";
        if (ToggleRow(new Rect(x, y, rw, rh), "FRAME RATE LIMIT", fpsText, ReentrySettings.FpsCap > 0, a, live, u))
        {
            var opts = ReentrySettings.FpsOptions;
            int idx = System.Array.IndexOf(opts, ReentrySettings.FpsCap);
            ReentrySettings.FpsCap = opts[(idx + 1) % opts.Length];
            changed = true;
        }
        y += rh + gap * 3f;

        if (Bar(new Rect(x, y, 420f * u, 76f * u), "BACK", a, live, u))
        {
            settingsOpen = false;
            ReentrySettings.Save();
        }

        if (changed) ReentrySettings.ApplyDisplay();
    }

    void RowBackground(Rect r, bool hover, float a, float u)
    {
        if (Event.current.type != EventType.Repaint) return;
        Fill(r, hover ? new Color(0.20f, 0.20f, 0.24f, 0.92f) : new Color(0.05f, 0.05f, 0.07f, 0.78f), a);
        Fill(new Rect(r.x, r.y, (hover ? 10f : 5f) * u, r.height), hover ? accent : new Color(0.55f, 0.57f, 0.62f), a);
    }

    float SliderRow(Rect r, string label, float v, float min, float max, string valueText, int id, float a, bool live, float u)
    {
        Event e = Event.current;
        Rect track = new Rect(r.x + r.width * 0.42f, r.y + r.height * 0.5f - 5f * u, r.width * 0.40f, 10f * u);
        Rect hit = new Rect(track.x - 14f * u, r.y, track.width + 28f * u, r.height);
        bool hover = live && (r.Contains(e.mousePosition) || dragId == id);

        if (live)
        {
            if (e.type == EventType.MouseDown && hit.Contains(e.mousePosition)) { dragId = id; ReentryAudio.Click(); }
            if (dragId == id && (e.type == EventType.MouseDown || e.type == EventType.MouseDrag))
            {
                v = Mathf.Lerp(min, max, Mathf.InverseLerp(track.x, track.xMax, e.mousePosition.x));
                e.Use();
            }
            if (e.type == EventType.MouseUp && dragId == id) { dragId = -1; ReentrySettings.Save(); e.Use(); }
        }

        RowBackground(r, hover, a, u);
        Text(new Rect(r.x + 28f * u, r.y, r.width * 0.4f, r.height), label, smallStyleBold(u), new Color(0.88f, 0.9f, 0.95f), a);
        if (e.type == EventType.Repaint)
        {
            float f = Mathf.InverseLerp(min, max, v);
            Fill(track, new Color(0.25f, 0.26f, 0.3f), a);
            Fill(new Rect(track.x, track.y, track.width * f, track.height), accent, a);
            Fill(new Rect(track.x + track.width * f - 8f * u, r.y + r.height * 0.5f - 17f * u, 16f * u, 34f * u), Color.white, a);
        }
        Text(new Rect(r.xMax - 150f * u, r.y, 130f * u, r.height), valueText, rightStyle(u), Color.white, a);
        return v;
    }

    bool ToggleRow(Rect r, string label, string valueText, bool on, float a, bool live, float u)
    {
        bool hover = live && r.Contains(Event.current.mousePosition);
        RowBackground(r, hover, a, u);
        Text(new Rect(r.x + 28f * u, r.y, r.width * 0.5f, r.height), label, smallStyleBold(u), new Color(0.88f, 0.9f, 0.95f), a);
        Text(new Rect(r.xMax - 330f * u, r.y, 310f * u, r.height), valueText, rightStyle(u), on ? accent : new Color(0.6f, 0.62f, 0.7f), a);
        bool clicked = live && GUI.Button(r, GUIContent.none, GUIStyle.none);
        if (clicked) ReentryAudio.Click();
        return clicked;
    }

    GUIStyle sbStyle, rStyle;
    GUIStyle smallStyleBold(float u)
    {
        if (sbStyle == null) sbStyle = new GUIStyle(smallStyle) { fontStyle = FontStyle.Bold };
        sbStyle.fontSize = Mathf.RoundToInt(28 * u);
        return sbStyle;
    }
    GUIStyle rightStyle(float u)
    {
        if (rStyle == null) rStyle = new GUIStyle(smallStyle) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleRight };
        rStyle.fontSize = Mathf.RoundToInt(28 * u);
        return rStyle;
    }

    GUIStyle ValueStyle(float px)
    {
        if (valueStyleCache == null) valueStyleCache = new GUIStyle(statStyle) { fontStyle = FontStyle.Bold };
        valueStyleCache.fontSize = Mathf.RoundToInt(px);
        return valueStyleCache;
    }

    void DrawSelect(float u, float W, float H, float a, bool live)
    {

        float hx = W * 0.07f;
        float rise = (1f - a) * 40f * u;
        Fill(new Rect(hx, H * 0.09f + rise, 8f * u, 92f * u), accent, a);
        Text(new Rect(hx + 28f * u, H * 0.09f + rise, 800f * u, 44f * u), "SELECT YOUR PAYLOAD", headStyle, accent, a);
        Text(new Rect(hx + 28f * u, H * 0.09f + 46f * u + rise, 900f * u, 46f * u), "Pick something to throw at the planet.", tagStyle, new Color(0.88f, 0.9f, 0.96f), a);

        bool back = Bar(new Rect(W * 0.93f - 300f * u, H * 0.09f + rise, 300f * u, 76f * u), "BACK", a, live, u);

        int n = entries.Length;
        int cols = Mathf.Clamp(cardsPerRow, 1, n);
        int rows = Mathf.CeilToInt(n / (float)cols);
        const float DW = 400f, DH = 330f;
        float gapDesign = 24f;

        float top = H * (0.23f + cardsOffsetY);
        float availW = W * 0.86f;
        float availH = H - top - H * uiOffsetY - 40f * u;
        float s = Mathf.Min((availW - gapDesign * u * (cols - 1)) / (cols * DW * u),
                            (availH - gapDesign * u * (rows - 1)) / (rows * DH * u));
        s = Mathf.Clamp(s, 0.3f, 1.3f);
        float k = u * s;
        float cardW = DW * k, cardH = DH * k, gap = gapDesign * k;

        bool hasPrev = previewTex != null;

        cardNameStyle.fontSize = Mathf.RoundToInt(34f * k);
        statStyle.fontSize = Mathf.RoundToInt(18f * k);
        blurbStyle.fontSize = Mathf.RoundToInt(19f * k);
        hoverIndex = -1;

        Color grey = new Color(0.6f, 0.62f, 0.7f);

        for (int i = 0; i < n; i++)
        {
            int row = i / cols;
            int col = i % cols;
            int inRow = Mathf.Min(cols, n - row * cols);
            float rowW = inRow * cardW + (inRow - 1) * gap;
            float x0 = (W - rowW) * 0.5f + W * cardsOffsetX;

            float ca = Smooth(0.0f + i * 0.07f, 0.7f + i * 0.07f, a);
            Rect r = new Rect(x0 + col * (cardW + gap), top + row * (cardH + gap) + (1f - ca) * 60f * u, cardW, cardH);
            bool hover = live && r.Contains(Event.current.mousePosition);
            if (hover) { r.y -= 8f * k; hoverIndex = i; }

            if (Event.current.type == EventType.Repaint)
            {
                Fill(r, hover ? new Color(0.16f, 0.16f, 0.2f, 0.94f) : new Color(0.05f, 0.05f, 0.07f, 0.84f), ca);
                float t = Mathf.Max(2f, (hover ? 5f : 2f) * k);
                Color e = hover ? accent : new Color(0.4f, 0.42f, 0.48f);
                Fill(new Rect(r.x, r.y, r.width, t), e, ca);
                if (hover)
                {
                    Fill(new Rect(r.x, r.yMax - t, r.width, t), e, ca);
                    Fill(new Rect(r.x, r.y, t, r.height), e, ca);
                    Fill(new Rect(r.xMax - t, r.y, t, r.height), e, ca);
                }
            }

            float px = r.x + 20f * k;
            float pw = r.width - 40f * k;

            Text(new Rect(px, r.y + 12f * k, pw, 48f * k), entries[i].name, cardNameStyle, hover ? accent : Color.white, ca);
            Fill(new Rect(px, r.y + 64f * k, pw, Mathf.Max(1f, 2f * k)), new Color(0.4f, 0.42f, 0.48f), ca);

            float ps = 130f * k * Mathf.Max(0.3f, previewScale);
            Rect pr = new Rect(px, r.y + 78f * k, ps, ps);
            if (Event.current.type == EventType.Repaint)
            {
                float b = Mathf.Max(1.5f, 2.5f * k);
                Fill(new Rect(pr.x - b, pr.y - b, pr.width + b * 2f, pr.height + b * 2f), hover ? accent : new Color(0.3f, 0.32f, 0.38f), ca);
                if (hasPrev)
                {
                    var old = GUI.color;
                    GUI.color = new Color(1f, 1f, 1f, ca);
                    GUI.DrawTexture(pr, previewTex[i], ScaleMode.StretchToFill, false);
                    GUI.color = old;
                }
                else Fill(pr, new Color(0.02f, 0.02f, 0.03f), ca);
            }

            float sx = pr.xMax + 18f * k;
            float sw = r.xMax - sx - 14f * k;
            Text(new Rect(sx, pr.y + 6f * k, sw, 24f * k), "MASS", statStyle, grey, ca);
            Text(new Rect(sx, pr.y + 26f * k, sw, 34f * k), entries[i].mass, ValueStyle(24f * k), Color.white, ca);
            Text(new Rect(sx, pr.y + 66f * k, sw, 24f * k), "TOUGHNESS", statStyle, grey, ca);
            Text(new Rect(sx, pr.y + 86f * k, sw, 34f * k), entries[i].toughness, ValueStyle(24f * k), Color.white, ca);

            Text(new Rect(px, pr.yMax + 14f * k, pw, r.yMax - pr.yMax - 20f * k), entries[i].blurb, blurbStyle, new Color(0.8f, 0.83f, 0.9f), ca);

            if (live && GUI.Button(r, GUIContent.none, GUIStyle.none))
            {
                pending = entries[i].kind;
                launchT = 0f;
                Launching = true;
                ReentryAudio.Click();
            }
        }

        if (Event.current.type == EventType.Repaint)
        {
            if (hoverIndex != lastHoverIndex && hoverIndex >= 0) ReentryAudio.Hover();
            lastHoverIndex = hoverIndex;
        }

        if (back) GoToTitle();
    }
}
