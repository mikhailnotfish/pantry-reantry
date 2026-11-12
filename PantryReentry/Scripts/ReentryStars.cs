using UnityEngine;

[DefaultExecutionOrder(200)]
public class ReentryStars : MonoBehaviour
{
    [Header("Hook up (found automatically if left empty)")]
    public ReentryBody body;
    public ReentrySky sky;
    public Camera cam;

    [Header("Stars")]
    public int starCount = 6000;
    [Tooltip("Dim stars packed along a band across the sky, which blur together into a Milky Way.")]
    public int milkyWayStars = 14000;
    public float brightness = 3f;
    public int seed = 12345;

    [Header("Fading")]
    [Tooltip("Below this altitude (metres) the stars are fully hidden by the daytime sky.")]
    public float fadeOutAltitude = 30000f;
    [Tooltip("Above this altitude (metres) the stars are at full brightness.")]
    public float fullAltitude = 70000f;
    [Tooltip("Stars whose line of sight skims the planet's edge dim over this many metres of height.")]
    public float limbDimHeight = 100000f;

    Transform dome;
    Material mat;
    float planetRadius = 6371000f;

    void Start()
    {
        if (body == null) body = FindAnyObjectByType<ReentryBody>();
        if (sky == null) sky = FindAnyObjectByType<ReentrySky>();
        if (cam == null) cam = Camera.main != null ? Camera.main : FindAnyObjectByType<Camera>();
        if (sky != null) planetRadius = sky.planetRadius;

        Shader sh = ReentryShaders.Find("Reentry/Stars");
        if (sh == null)
        {
            Debug.LogError("[ReentryStars] Couldn't find shader 'Reentry/Stars'. Is ReentryStars.shader in your Assets folder?");
            enabled = false;
            return;
        }
        if (cam == null)
        {
            Debug.LogError("[ReentryStars] No camera found.");
            enabled = false;
            return;
        }

        float radius = Mathf.Max(50f, cam.farClipPlane * 0.45f);
        mat = new Material(sh);

        var go = new GameObject("Star Dome");
        dome = go.transform;
        go.AddComponent<MeshFilter>().sharedMesh = BuildStars(radius);
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
    }

    void LateUpdate()
    {
        if (dome == null || cam == null) return;

        dome.position = cam.transform.position;

        float altitude = body != null ? Mathf.Max(0f, body.AltitudeMeters) : 100000f;
        Vector3 anchor = body != null ? body.transform.position : Vector3.zero;
        Vector3 planetCentre = anchor + Vector3.down * (planetRadius + altitude);

        float visibility = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(fadeOutAltitude, fullAltitude, altitude));

        mat.SetVector("_PlanetCenter", planetCentre - cam.transform.position);
        mat.SetFloat("_PlanetRadius", planetRadius);
        mat.SetFloat("_AtmoHeight", limbDimHeight);
        mat.SetFloat("_Visibility", visibility);
        mat.SetFloat("_Intensity", brightness);
    }

    void OnDestroy()
    {
        if (dome != null) Destroy(dome.gameObject);
    }

    Mesh BuildStars(float R)
    {
        var rng = new System.Random(seed);
        int total = starCount + milkyWayStars;

        var verts = new Vector3[total * 4];
        var norms = new Vector3[total * 4];
        var cols = new Color[total * 4];
        var uvs = new Vector2[total * 4];
        var tris = new int[total * 6];

        Vector3 bandNormal = new Vector3(0.35f, 0.8f, 0.45f).normalized;
        Vector3 e1 = Vector3.Cross(bandNormal, Vector3.up).normalized;
        Vector3 e2 = Vector3.Cross(bandNormal, e1);

        for (int i = 0; i < total; i++)
        {
            bool band = i >= starCount;
            Vector3 d;
            float b, size;

            if (!band)
            {
                d = RandomOnSphere(rng);
                float m = (float)rng.NextDouble();
                b = 0.1f + 0.9f * Mathf.Pow(m, 5f);
                size = Mathf.Lerp(0.0012f, 0.004f, b);
            }
            else
            {

                float phi;
                do { phi = (float)rng.NextDouble() * Mathf.PI * 2f; }
                while (rng.NextDouble() > 0.35f + 0.65f * (0.5f + 0.5f * Mathf.Cos(phi)));

                float lat = Gaussian(rng) * 0.13f;
                d = (Mathf.Cos(lat) * (Mathf.Cos(phi) * e1 + Mathf.Sin(phi) * e2) + Mathf.Sin(lat) * bandNormal).normalized;
                float m = (float)rng.NextDouble();
                b = 0.03f + 0.22f * m * m;
                size = Mathf.Lerp(0.0012f, 0.002f, m);
            }

            Color tint = StarColor(rng);
            tint.a = Mathf.Clamp01(b);

            Vector3 t1 = Vector3.Cross(d, Mathf.Abs(d.y) > 0.99f ? Vector3.right : Vector3.up).normalized;
            Vector3 t2 = Vector3.Cross(d, t1);
            float h = R * size;
            Vector3 c = d * R;

            int v = i * 4;
            verts[v + 0] = c - t1 * h - t2 * h;
            verts[v + 1] = c + t1 * h - t2 * h;
            verts[v + 2] = c + t1 * h + t2 * h;
            verts[v + 3] = c - t1 * h + t2 * h;
            uvs[v + 0] = new Vector2(0, 0);
            uvs[v + 1] = new Vector2(1, 0);
            uvs[v + 2] = new Vector2(1, 1);
            uvs[v + 3] = new Vector2(0, 1);
            for (int k = 0; k < 4; k++) { norms[v + k] = d; cols[v + k] = tint; }

            int t = i * 6;
            tris[t + 0] = v; tris[t + 1] = v + 2; tris[t + 2] = v + 1;
            tris[t + 3] = v; tris[t + 4] = v + 3; tris[t + 5] = v + 2;
        }

        var mesh = new Mesh { name = "Stars" };
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.vertices = verts;
        mesh.normals = norms;
        mesh.colors = cols;
        mesh.uv = uvs;
        mesh.triangles = tris;
        mesh.bounds = new Bounds(Vector3.zero, Vector3.one * R * 2.5f);
        return mesh;
    }

    static Vector3 RandomOnSphere(System.Random rng)
    {
        float z = (float)(rng.NextDouble() * 2.0 - 1.0);
        float a = (float)(rng.NextDouble() * Mathf.PI * 2.0);
        float r = Mathf.Sqrt(Mathf.Max(0f, 1f - z * z));
        return new Vector3(r * Mathf.Cos(a), z, r * Mathf.Sin(a));
    }

    static float Gaussian(System.Random rng)
    {
        double u1 = 1.0 - rng.NextDouble();
        double u2 = rng.NextDouble();
        return (float)(System.Math.Sqrt(-2.0 * System.Math.Log(u1)) * System.Math.Cos(2.0 * System.Math.PI * u2));
    }

    static Color StarColor(System.Random rng)
    {
        double r = rng.NextDouble();
        if (r < 0.55) return new Color(0.92f, 0.95f, 1.00f, 1f);
        if (r < 0.75) return new Color(0.65f, 0.80f, 1.00f, 1f);
        if (r < 0.92) return new Color(1.00f, 0.82f, 0.58f, 1f);
        return new Color(1.00f, 0.56f, 0.38f, 1f);
    }
}
