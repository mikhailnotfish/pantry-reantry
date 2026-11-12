using UnityEngine;

public class ReentryPiece : MonoBehaviour
{
    ReentryBody body;
    Vector3 velocity;
    Vector3 spinAxis;
    float spinSpeed;
    float life, age;
    float dragFactor;
    float dragStrength;
    float maxFallBack;
    float emissionNits;
    Vector3 baseScale;
    bool dying;

    MeshRenderer meshRenderer;
    MaterialPropertyBlock block;
    Color[] baseColors;

    Transform streak;
    Material streakMat;
    float streakLength, streakBrightness, streakWidth;

    static Mesh streakMesh;

    static readonly int EmissiveId = Shader.PropertyToID("_EmissiveColor");
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    public void Init(ReentryBody body, Vector3 kick, float life, float dragFactor, float dragStrength,
                     float maxFallBack, float emissionNits, float streakLength, float streakBrightness, Material streakTemplate)
    {
        this.body = body;
        this.velocity = kick;
        this.life = Mathf.Max(0.3f, life);
        this.dragFactor = dragFactor;
        this.dragStrength = dragStrength;
        this.maxFallBack = maxFallBack;
        this.emissionNits = emissionNits;
        this.streakLength = streakLength;
        this.streakBrightness = streakBrightness;

        baseScale = transform.localScale;
        spinAxis = Random.onUnitSphere;
        spinSpeed = Random.Range(30f, 220f);

        meshRenderer = GetComponent<MeshRenderer>();
        block = new MaterialPropertyBlock();
        var mats = meshRenderer.sharedMaterials;
        baseColors = new Color[mats.Length];
        for (int i = 0; i < mats.Length; i++)
            baseColors[i] = (mats[i] != null && mats[i].HasProperty(BaseColorId)) ? mats[i].GetColor(BaseColorId) : Color.white;

        if (streakTemplate != null && streakLength > 0f)
        {
            Bounds b = GetComponent<MeshFilter>().sharedMesh.bounds;
            float size = Mathf.Max(b.extents.x * baseScale.x, Mathf.Max(b.extents.y * baseScale.y, b.extents.z * baseScale.z));
            streakWidth = Mathf.Clamp(size * 1.6f, 0.08f, 1.5f);

            var go = new GameObject("Fragment Streak");
            go.AddComponent<MeshFilter>().sharedMesh = GetStreakMesh();
            var mr = go.AddComponent<MeshRenderer>();
            streakMat = new Material(streakTemplate);
            mr.sharedMaterial = streakMat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            streak = go.transform;
        }
    }

    static Mesh GetStreakMesh()
    {
        if (streakMesh != null) return streakMesh;

        const int N = 10;
        var verts = new Vector3[(N + 1) * 2];
        var cols = new Color[verts.Length];
        var uvs = new Vector2[verts.Length];
        var tris = new int[N * 6];

        for (int i = 0; i <= N; i++)
        {
            float t = i / (float)N;
            float half = 0.5f * Mathf.Pow(1f - t, 0.8f);

            Color c;
            if (t < 0.4f) c = Color.Lerp(new Color(1.0f, 0.92f, 0.75f, 1f), new Color(1.0f, 0.40f, 0.06f, 0.7f), t / 0.4f);
            else c = Color.Lerp(new Color(1.0f, 0.40f, 0.06f, 0.7f), new Color(0.45f, 0.05f, 0.0f, 0f), (t - 0.4f) / 0.6f);

            int v = i * 2;
            verts[v] = new Vector3(t, -half, 0f);
            verts[v + 1] = new Vector3(t, half, 0f);
            cols[v] = cols[v + 1] = c;
            uvs[v] = new Vector2(t, 0f);
            uvs[v + 1] = new Vector2(t, 1f);
        }
        for (int i = 0; i < N; i++)
        {
            int v = i * 2, k = i * 6;
            tris[k] = v; tris[k + 1] = v + 1; tris[k + 2] = v + 2;
            tris[k + 3] = v + 1; tris[k + 4] = v + 3; tris[k + 5] = v + 2;
        }

        streakMesh = new Mesh { name = "Fragment Streak" };
        streakMesh.vertices = verts;
        streakMesh.colors = cols;
        streakMesh.uv = uvs;
        streakMesh.triangles = tris;
        streakMesh.RecalculateBounds();

        streakMesh.bounds = new Bounds(new Vector3(0.5f, 0f, 0f), new Vector3(1000f, 1000f, 1000f));
        return streakMesh;
    }

    static Color HeatColor(float t)
    {
        Color c0 = new Color(0.35f, 0.02f, 0.00f);
        Color c1 = new Color(1.00f, 0.28f, 0.02f);
        Color c2 = new Color(1.00f, 0.78f, 0.40f);
        Color c = Color.Lerp(c0, c1, Mathf.Clamp01(t * 2f));
        return Color.Lerp(c, c2, Mathf.Clamp01(t * 2f - 1f));
    }

    void Update()
    {
        if (body == null || dying) return;

        float dt = Time.deltaTime;
        age += dt;
        float k = age / life;
        if (k >= 1f)
        {
            dying = true;
            Destroy(gameObject);
            return;
        }

        float rho = body.Density;
        float speed = body.SpeedMetersPerSecond;
        float mainDrag = 0.5f * rho * speed * speed * body.dragCoefficient * body.dragArea / Mathf.Max(0.01f, body.mass);
        float a = body.entryAngleDeg * Mathf.Deg2Rad;
        Vector3 flow = new Vector3(Mathf.Cos(a), -Mathf.Sin(a), 0f);

        velocity += -flow * (mainDrag * Mathf.Max(0f, dragFactor - 1f) * dragStrength) * dt;
        float back = Vector3.Dot(velocity, -flow);
        if (back > maxFallBack) velocity -= -flow * (back - maxFallBack);

        transform.position += velocity * dt;
        transform.Rotate(spinAxis, spinSpeed * dt, Space.World);

        float shrink = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 1f, k));
        float shrinkClamped = Mathf.Max(0.02f, shrink);
        transform.localScale = baseScale * shrinkClamped;

        float heat = Mathf.Lerp(1f, 0.1f, k);
        float glow = Mathf.Pow(1f - k, 1.2f);
        Color e = HeatColor(heat) * (glow * emissionNits);
        float charred = Mathf.Clamp01(k * 1.5f + 0.4f);

        for (int i = 0; i < baseColors.Length; i++)
        {
            Color b = baseColors[i];
            Color dark = new Color(b.r * 0.2f, b.g * 0.2f, b.b * 0.2f, b.a);
            block.SetVector(EmissiveId, new Vector4(e.r, e.g, e.b, 1f));
            block.SetColor(BaseColorId, Color.Lerp(b, dark, charred));
            meshRenderer.SetPropertyBlock(block, i);
        }

        UpdateStreak(flow, glow, shrinkClamped);
    }

    void UpdateStreak(Vector3 flow, float glow, float shrink)
    {
        if (streak == null) return;

        Vector3 x = -flow;
        Camera cam = Camera.main;
        Vector3 toCam = cam != null ? (cam.transform.position - transform.position).normalized : Vector3.back;
        Vector3 z = toCam - x * Vector3.Dot(toCam, x);
        if (z.sqrMagnitude < 1e-4f) z = Vector3.Cross(x, Vector3.up);
        z.Normalize();
        Vector3 y = Vector3.Cross(z, x);

        streak.SetPositionAndRotation(transform.position, Quaternion.LookRotation(z, y));

        float plasma = Mathf.Clamp01(body.Plasma * 1.5f);
        float length = streakLength * (0.25f + 0.75f * glow) * plasma;
        streak.localScale = new Vector3(Mathf.Max(0.01f, length), streakWidth * shrink, 1f);
        streakMat.SetFloat("_Intensity", streakBrightness * glow * plasma);
    }

    void OnDestroy()
    {
        if (streak != null) Destroy(streak.gameObject);
    }
}
