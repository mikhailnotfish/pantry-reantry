using UnityEngine;

[RequireComponent(typeof(ReentryBody))]
public class ReentryPlasma : MonoBehaviour
{
    [Header("Shape")]
    [Tooltip("Plasma radius as a multiple of the object's size.")]
    public float widthScale = 1.6f;
    [Tooltip("Plasma length as a multiple of its radius.")]
    public float lengthScale = 6f;
    [Tooltip("How far ahead of the object the shock front sits, as a multiple of the object's size.")]
    public float noseStandoff = 1.2f;

    [Header("Look")]
    public float brightness = 3f;
    [Tooltip("Shell swells a little as plasma builds up.")]
    public float swell = 0.4f;

    [Header("Optional")]
    public Material materialOverride;

    ReentryBody body;
    Transform shell;
    MeshRenderer shellRenderer;
    Material mat;
    float radius;
    static readonly int PlasmaId = Shader.PropertyToID("_Plasma");

    void Start()
    {
        body = GetComponent<ReentryBody>();

        var rends = GetComponentsInChildren<Renderer>();
        Bounds b = new Bounds(transform.position, Vector3.zero);
        foreach (var r in rends) b.Encapsulate(r.bounds);
        float objectSize = Mathf.Max(b.extents.x, Mathf.Max(b.extents.y, b.extents.z));
        radius = Mathf.Max(0.1f, objectSize);

        var go = new GameObject("Reentry Plasma Shell");
        shell = go.transform;
        var mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = BuildTeardrop(48, 32);
        shellRenderer = go.AddComponent<MeshRenderer>();
        shellRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        shellRenderer.receiveShadows = false;

        mat = materialOverride;
        if (mat == null)
        {
            Shader sh = ReentryShaders.Find("Reentry/PlasmaShell");
            if (sh == null)
            {
                Debug.LogError("[ReentryPlasma] Couldn't find shader 'Reentry/PlasmaShell'. Is ReentryPlasmaShell.shader in your Assets folder?");
                return;
            }
            mat = new Material(sh);
        }
        mat.SetFloat("_Intensity", brightness);
        shellRenderer.material = mat;
    }

    void LateUpdate()
    {
        if (shell == null || body == null || mat == null) return;

        float plasma = Mathf.Clamp01(body.Plasma * 1.5f);
        shellRenderer.enabled = plasma > 0.01f;

        float a = body.entryAngleDeg * Mathf.Deg2Rad;
        Vector3 flow = new Vector3(Mathf.Cos(a), -Mathf.Sin(a), 0f);

        float w = radius * widthScale * Mathf.Lerp(1f - swell, 1f, plasma);
        float len = w * lengthScale;

        shell.rotation = Quaternion.LookRotation(flow, Vector3.up);
        shell.localScale = new Vector3(w, w, len);

        shell.position = transform.position + flow * (radius * noseStandoff);

        mat.SetFloat(PlasmaId, plasma);
    }

    void OnDestroy()
    {
        if (shell != null) Destroy(shell.gameObject);
    }

    static Mesh BuildTeardrop(int rings, int sides)
    {
        int vCount = (rings + 1) * (sides + 1);
        var verts = new Vector3[vCount];
        var uvs = new Vector2[vCount];
        var tris = new int[rings * sides * 6];

        int vi = 0;
        for (int i = 0; i <= rings; i++)
        {
            float s = i / (float)rings;
            float x = Mathf.Pow(s, 0.7f);
            float r = Mathf.Sin(Mathf.PI * x);
            float z = -s;
            for (int j = 0; j <= sides; j++)
            {
                float ang = j / (float)sides * Mathf.PI * 2f;
                verts[vi] = new Vector3(r * Mathf.Cos(ang), r * Mathf.Sin(ang), z);
                uvs[vi] = new Vector2(s, j / (float)sides);
                vi++;
            }
        }

        int ti = 0;
        for (int i = 0; i < rings; i++)
        {
            for (int j = 0; j < sides; j++)
            {
                int a0 = i * (sides + 1) + j;
                int b0 = a0 + sides + 1;
                tris[ti++] = a0; tris[ti++] = b0; tris[ti++] = a0 + 1;
                tris[ti++] = a0 + 1; tris[ti++] = b0; tris[ti++] = b0 + 1;
            }
        }

        var mesh = new Mesh { name = "Reentry Teardrop" };
        mesh.vertices = verts;
        mesh.uv = uvs;
        mesh.triangles = tris;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}
