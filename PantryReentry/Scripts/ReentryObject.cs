using UnityEngine;

[DefaultExecutionOrder(-200)]
public class ReentryObject : MonoBehaviour
{
    public enum Kind { Fridge, Chair, BeanShelf, Cube, Custom, Toilet, Piano, WashingMachine, TrafficCone, Bathtub }

    [Tooltip("What to drop. Custom = keep whatever model you put under this object.")]
    public Kind kind = Kind.Fridge;

    [Tooltip("Set the weight, drag and heat numbers on ReentryBody to match the object.")]
    public bool applyPreset = true;

    Material template;
    Transform buildParent;

    void Awake()
    {

        if (ReentryMenu.HasChoice) kind = ReentryMenu.Selected;

        var body = GetComponent<ReentryBody>();
        if (body == null)
        {
            Debug.LogError("[ReentryObject] Put this on the same object as ReentryBody.");
            return;
        }

        var rootRenderer = GetComponent<MeshRenderer>();
        if (rootRenderer != null) template = rootRenderer.sharedMaterial;
        if (template == null)
        {
            Shader sh = ReentryShaders.Find("HDRP/Lit");
            if (sh != null) template = new Material(sh);
        }

        if (kind != Kind.Custom && kind != Kind.Cube)
        {

            var mf = GetComponent<MeshFilter>();
            if (mf != null) mf.sharedMesh = null;
            if (rootRenderer != null) rootRenderer.enabled = false;
        }

        switch (kind)
        {
            case Kind.Fridge:
                BuildFridge();
                if (applyPreset) Preset(body, 90f, 1.3f, 1.1f, 0.5f, 6500f, 1750f, 18f);
                break;
            case Kind.Chair:
                BuildChair();
                if (applyPreset) Preset(body, 7f, 0.55f, 1.2f, 0.25f, 1400f, 1300f, 60f);
                break;
            case Kind.BeanShelf:
                BuildBeanShelf();
                if (applyPreset) Preset(body, 150f, 1.4f, 1.3f, 0.6f, 5200f, 1600f, 12f);
                break;
            case Kind.Cube:
                if (applyPreset) Preset(body, 60f, 0.8f, 1.2f, 0.4f, 3000f, 1900f, 25f);
                break;
            case Kind.Toilet:
                BuildToilet();
                if (applyPreset) Preset(body, 40f, 0.35f, 1.1f, 0.25f, 4000f, 1450f, 30f);
                break;
            case Kind.Piano:
                BuildPiano();
                if (applyPreset) Preset(body, 250f, 1.9f, 1.2f, 0.6f, 3500f, 1200f, 10f);
                break;
            case Kind.WashingMachine:
                BuildWashingMachine();
                if (applyPreset) Preset(body, 75f, 0.55f, 1.1f, 0.4f, 5000f, 1700f, 22f);
                break;
            case Kind.TrafficCone:
                BuildTrafficCone();
                if (applyPreset) Preset(body, 4f, 0.12f, 0.9f, 0.12f, 1000f, 1000f, 90f);
                break;
            case Kind.Bathtub:
                BuildBathtub();
                if (applyPreset) Preset(body, 120f, 1.1f, 1.2f, 0.5f, 6000f, 1800f, 14f);
                break;
        }
    }

    public void BuildInto(Kind k, Transform parent)
    {
        if (template == null)
        {
            var r = GetComponent<MeshRenderer>();
            if (r != null) template = r.sharedMaterial;
            if (template == null)
            {
                Shader sh = ReentryShaders.Find("HDRP/Lit");
                if (sh != null) template = new Material(sh);
            }
        }
        if (template == null) return;

        buildParent = parent;
        switch (k)
        {
            case Kind.Fridge: BuildFridge(); break;
            case Kind.Chair: BuildChair(); break;
            case Kind.BeanShelf: BuildBeanShelf(); break;
            case Kind.Toilet: BuildToilet(); break;
            case Kind.Piano: BuildPiano(); break;
            case Kind.WashingMachine: BuildWashingMachine(); break;
            case Kind.TrafficCone: BuildTrafficCone(); break;
            case Kind.Bathtub: BuildBathtub(); break;
        }
        buildParent = null;
    }

    static void Preset(ReentryBody b, float mass, float area, float cd, float nose, float heatCap, float fail, float tumble)
    {
        b.mass = mass;
        b.dragArea = area;
        b.dragCoefficient = cd;
        b.noseRadius = nose;
        b.arealHeatCapacity = heatCap;
        b.failTemperature = fail;
        b.tumbleSpeed = tumble;
    }

    Material MakeMat(Color c, float metallic, float smoothness)
    {
        var m = new Material(template);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
        return m;
    }

    GameObject Part(PrimitiveType type, string name, Vector3 pos, Vector3 size, Material m, Vector3 euler = default(Vector3))
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        var col = go.GetComponent<Collider>();
        if (col != null) Destroy(col);
        go.transform.SetParent(buildParent != null ? buildParent : transform, false);
        go.transform.localPosition = pos;
        go.transform.localScale = size;
        go.transform.localRotation = Quaternion.Euler(euler);
        go.GetComponent<MeshRenderer>().sharedMaterial = m;
        return go;
    }

    void BuildFridge()
    {
        var white = MakeMat(new Color(0.88f, 0.89f, 0.9f), 0.2f, 0.7f);
        var grey = MakeMat(new Color(0.55f, 0.57f, 0.6f), 0.8f, 0.5f);
        var dark = MakeMat(new Color(0.08f, 0.08f, 0.09f), 0f, 0.3f);

        Part(PrimitiveType.Cube, "Fridge Body", Vector3.zero, new Vector3(0.7f, 1.8f, 0.7f), white);
        Part(PrimitiveType.Cube, "Door Seam", new Vector3(0.355f, 0.35f, 0f), new Vector3(0.02f, 0.02f, 0.68f), dark);
        Part(PrimitiveType.Cube, "Handle Top", new Vector3(0.39f, 0.62f, 0.25f), new Vector3(0.05f, 0.45f, 0.04f), grey);
        Part(PrimitiveType.Cube, "Handle Bottom", new Vector3(0.39f, -0.05f, 0.25f), new Vector3(0.05f, 0.6f, 0.04f), grey);
        Part(PrimitiveType.Cube, "Base Vent", new Vector3(0.355f, -0.86f, 0f), new Vector3(0.02f, 0.06f, 0.5f), dark);
    }

    void BuildChair()
    {
        var wood = MakeMat(new Color(0.55f, 0.34f, 0.18f), 0f, 0.35f);
        var cushion = MakeMat(new Color(0.7f, 0.15f, 0.12f), 0f, 0.2f);

        Part(PrimitiveType.Cube, "Seat", Vector3.zero, new Vector3(0.45f, 0.05f, 0.45f), wood);
        Part(PrimitiveType.Cube, "Cushion", new Vector3(0f, 0.045f, 0f), new Vector3(0.4f, 0.04f, 0.4f), cushion);
        float lx = 0.19f, ly = -0.225f;
        foreach (var sx in new[] { -1f, 1f })
            foreach (var sz in new[] { -1f, 1f })
                Part(PrimitiveType.Cylinder, "Leg", new Vector3(lx * sx, ly, lx * sz), new Vector3(0.045f, 0.225f, 0.045f), wood);
        Part(PrimitiveType.Cube, "Back Left Post", new Vector3(-0.19f, 0.30f, -0.2f), new Vector3(0.04f, 0.55f, 0.04f), wood);
        Part(PrimitiveType.Cube, "Back Right Post", new Vector3(0.19f, 0.30f, -0.2f), new Vector3(0.04f, 0.55f, 0.04f), wood);
        Part(PrimitiveType.Cube, "Back Top Rail", new Vector3(0f, 0.52f, -0.2f), new Vector3(0.42f, 0.09f, 0.035f), wood);
        Part(PrimitiveType.Cube, "Back Mid Rail", new Vector3(0f, 0.34f, -0.2f), new Vector3(0.4f, 0.05f, 0.03f), wood);
    }

    void BuildBeanShelf()
    {
        var steel = MakeMat(new Color(0.45f, 0.47f, 0.5f), 0.9f, 0.45f);
        var can = MakeMat(new Color(0.75f, 0.78f, 0.8f), 0.9f, 0.6f);
        var label = MakeMat(new Color(0.85f, 0.35f, 0.08f), 0f, 0.3f);

        float w = 1.0f, d = 0.4f, h = 1.8f;

        foreach (var sx in new[] { -1f, 1f })
            foreach (var sz in new[] { -1f, 1f })
                Part(PrimitiveType.Cube, "Post", new Vector3((w / 2f - 0.02f) * sx, 0f, (d / 2f - 0.02f) * sz), new Vector3(0.04f, h, 0.04f), steel);

        float[] ys = { -0.8f, -0.25f, 0.3f, 0.85f };
        foreach (float y in ys)
        {
            Part(PrimitiveType.Cube, "Shelf", new Vector3(0f, y, 0f), new Vector3(w, 0.03f, d), steel);
            for (int ix = 0; ix < 7; ix++)
                for (int iz = 0; iz < 2; iz++)
                {
                    float cx = -w / 2f + 0.1f + ix * 0.133f;
                    float cz = -0.09f + iz * 0.18f;
                    Part(PrimitiveType.Cylinder, "Bean Can", new Vector3(cx, y + 0.015f + 0.055f, cz), new Vector3(0.075f, 0.055f, 0.075f), can);
                    Part(PrimitiveType.Cylinder, "Bean Label", new Vector3(cx, y + 0.015f + 0.055f, cz), new Vector3(0.0775f, 0.035f, 0.0775f), label);
                }
        }
    }
    void BuildToilet()
    {
        var porcelain = MakeMat(new Color(0.93f, 0.94f, 0.95f), 0.05f, 0.88f);
        var seat = MakeMat(new Color(0.85f, 0.86f, 0.88f), 0.05f, 0.7f);
        var chrome = MakeMat(new Color(0.75f, 0.77f, 0.8f), 1f, 0.8f);

        Part(PrimitiveType.Cylinder, "Toilet Pedestal", new Vector3(0.03f, -0.24f, 0f), new Vector3(0.26f, 0.18f, 0.26f), porcelain);
        Part(PrimitiveType.Cylinder, "Toilet Bowl", new Vector3(0.05f, -0.02f, 0f), new Vector3(0.42f, 0.07f, 0.34f), porcelain);
        Part(PrimitiveType.Cylinder, "Toilet Seat", new Vector3(0.05f, 0.075f, 0f), new Vector3(0.40f, 0.015f, 0.32f), seat);
        Part(PrimitiveType.Cylinder, "Toilet Lid", new Vector3(0.05f, 0.105f, 0f), new Vector3(0.37f, 0.012f, 0.29f), seat);
        Part(PrimitiveType.Cube, "Toilet Tank", new Vector3(-0.2f, 0.2f, 0f), new Vector3(0.16f, 0.36f, 0.38f), porcelain);
        Part(PrimitiveType.Cube, "Tank Lid", new Vector3(-0.2f, 0.395f, 0f), new Vector3(0.18f, 0.03f, 0.40f), porcelain);
        Part(PrimitiveType.Cylinder, "Flush Button", new Vector3(-0.2f, 0.425f, 0f), new Vector3(0.05f, 0.01f, 0.05f), chrome);
    }

    void BuildPiano()
    {
        var black = MakeMat(new Color(0.045f, 0.04f, 0.04f), 0.3f, 0.9f);
        var ivory = MakeMat(new Color(0.95f, 0.93f, 0.85f), 0f, 0.5f);
        var brass = MakeMat(new Color(0.75f, 0.58f, 0.2f), 1f, 0.7f);

        Part(PrimitiveType.Cube, "Piano Body", Vector3.zero, new Vector3(0.65f, 1.3f, 1.5f), black);
        Part(PrimitiveType.Cube, "Piano Lid", new Vector3(0f, 0.665f, 0f), new Vector3(0.7f, 0.04f, 1.56f), black);
        Part(PrimitiveType.Cube, "Key Shelf", new Vector3(0.4f, -0.06f, 0f), new Vector3(0.2f, 0.06f, 1.4f), black);
        Part(PrimitiveType.Cube, "Music Stand", new Vector3(0.28f, 0.32f, 0f), new Vector3(0.04f, 0.4f, 1.1f), black);

        Part(PrimitiveType.Cube, "White Keys", new Vector3(0.44f, -0.015f, 0f), new Vector3(0.18f, 0.03f, 1.26f), ivory);
        float spacing = 1.26f / 21f;
        for (int i = 0; i < 20; i++)
        {
            int k = i % 7;
            if (k == 2 || k == 6) continue;
            float z = -0.63f + (i + 1) * spacing;
            Part(PrimitiveType.Cube, "Black Key", new Vector3(0.40f, 0.02f, z), new Vector3(0.1f, 0.03f, spacing * 0.55f), black);
        }

        Part(PrimitiveType.Cube, "Pedal Bar", new Vector3(0.36f, -0.6f, 0f), new Vector3(0.1f, 0.04f, 0.3f), brass);
        Part(PrimitiveType.Cube, "Piano Foot Left", new Vector3(0.28f, -0.66f, -0.65f), new Vector3(0.12f, 0.06f, 0.12f), black);
        Part(PrimitiveType.Cube, "Piano Foot Right", new Vector3(0.28f, -0.66f, 0.65f), new Vector3(0.12f, 0.06f, 0.12f), black);
    }

    void BuildWashingMachine()
    {
        var white = MakeMat(new Color(0.9f, 0.91f, 0.92f), 0.1f, 0.75f);
        var ring = MakeMat(new Color(0.7f, 0.72f, 0.76f), 0.9f, 0.6f);
        var glass = MakeMat(new Color(0.12f, 0.2f, 0.28f), 0f, 0.95f);
        var dark = MakeMat(new Color(0.08f, 0.08f, 0.1f), 0f, 0.4f);

        Part(PrimitiveType.Cube, "Washer Body", Vector3.zero, new Vector3(0.6f, 0.85f, 0.6f), white);
        Part(PrimitiveType.Cylinder, "Door Ring", new Vector3(0.305f, -0.08f, 0f), new Vector3(0.44f, 0.012f, 0.44f), ring, new Vector3(0f, 0f, 90f));
        Part(PrimitiveType.Cylinder, "Door Glass", new Vector3(0.315f, -0.08f, 0f), new Vector3(0.34f, 0.012f, 0.34f), glass, new Vector3(0f, 0f, 90f));
        Part(PrimitiveType.Cube, "Control Panel", new Vector3(0.305f, 0.34f, 0f), new Vector3(0.015f, 0.14f, 0.5f), dark);
        Part(PrimitiveType.Cylinder, "Dial", new Vector3(0.32f, 0.34f, 0.14f), new Vector3(0.09f, 0.015f, 0.09f), ring, new Vector3(0f, 0f, 90f));
        Part(PrimitiveType.Cube, "Display", new Vector3(0.315f, 0.34f, -0.1f), new Vector3(0.01f, 0.05f, 0.14f), glass);
    }

    void BuildTrafficCone()
    {
        var orange = MakeMat(new Color(1f, 0.38f, 0.04f), 0f, 0.45f);
        var white = MakeMat(new Color(0.95f, 0.95f, 0.95f), 0f, 0.4f);
        var baseMat = MakeMat(new Color(0.9f, 0.3f, 0.03f), 0f, 0.35f);

        Part(PrimitiveType.Cube, "Cone Base", new Vector3(0f, -0.335f, 0f), new Vector3(0.36f, 0.03f, 0.36f), baseMat);

        const int rings = 9;
        float h = 0.66f / rings;
        for (int i = 0; i < rings; i++)
        {
            float t = i / (float)(rings - 1);
            float r = Mathf.Lerp(0.155f, 0.03f, t);
            bool stripe = (i == 4 || i == 5);
            float y = -0.32f + h * (i + 0.5f);
            Part(PrimitiveType.Cylinder, stripe ? "Cone Stripe" : "Cone Ring", new Vector3(0f, y, 0f),
                 new Vector3(r * 2f + (stripe ? 0.004f : 0f), h * 0.5f, r * 2f + (stripe ? 0.004f : 0f)), stripe ? white : orange);
        }
    }

    void BuildBathtub()
    {
        var enamel = MakeMat(new Color(0.94f, 0.94f, 0.92f), 0.05f, 0.85f);
        var gold = MakeMat(new Color(0.8f, 0.62f, 0.2f), 1f, 0.75f);
        var water = MakeMat(new Color(0.3f, 0.6f, 0.85f), 0f, 0.97f);

        Part(PrimitiveType.Cube, "Tub Floor", new Vector3(0f, -0.2f, 0f), new Vector3(0.7f, 0.07f, 1.6f), enamel);
        Part(PrimitiveType.Cube, "Tub Side Left", new Vector3(-0.32f, 0f, 0f), new Vector3(0.07f, 0.5f, 1.6f), enamel);
        Part(PrimitiveType.Cube, "Tub Side Right", new Vector3(0.32f, 0f, 0f), new Vector3(0.07f, 0.5f, 1.6f), enamel);
        Part(PrimitiveType.Cube, "Tub End Back", new Vector3(0f, 0f, -0.77f), new Vector3(0.7f, 0.5f, 0.07f), enamel);
        Part(PrimitiveType.Cube, "Tub End Front", new Vector3(0f, 0f, 0.77f), new Vector3(0.7f, 0.5f, 0.07f), enamel);
        Part(PrimitiveType.Cube, "Bath Water", new Vector3(0f, -0.02f, 0.02f), new Vector3(0.58f, 0.03f, 1.48f), water);

        foreach (var sx in new[] { -1f, 1f })
            foreach (var sz in new[] { -1f, 1f })
                Part(PrimitiveType.Cylinder, "Tub Foot", new Vector3(0.27f * sx, -0.29f, 0.7f * sz), new Vector3(0.09f, 0.04f, 0.09f), gold);

        Part(PrimitiveType.Cylinder, "Tap Post", new Vector3(0f, 0.31f, -0.79f), new Vector3(0.04f, 0.1f, 0.04f), gold);
        Part(PrimitiveType.Cylinder, "Tap Spout", new Vector3(0f, 0.41f, -0.72f), new Vector3(0.035f, 0.08f, 0.035f), gold, new Vector3(90f, 0f, 0f));
    }
}
