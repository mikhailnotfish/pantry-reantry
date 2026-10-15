using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(ReentryBody))]
public class ReentryPlasmaWrap : MonoBehaviour
{
    [Header("Shell around the object")]
    [Tooltip("Thickness of the shock layer, as a multiple of the object's size.")]
    public float thickness = 0.6f;
    public float brightness = 3f;
    [Tooltip("Upper limit on triangles per part after smoothing. Higher is smoother but slower.")]
    public int maxTriangles = 30000;

    [Header("Wake")]
    [Tooltip("Length of the wake, as a multiple of the object's size.")]
    public float wakeLength = 6f;
    [Tooltip("Scales the wake outline. 1 = same size as the object's silhouette plus the shock layer.")]
    public float wakeSpread = 1f;
    [Tooltip("How many outlines make up the wake. More = smoother, slower.")]
    public int wakeRings = 40;
    [Tooltip("Points around each outline. More = sharper corners on boxy objects.")]
    public int wakeSides = 48;
    [Tooltip("Where the wake starts, as a multiple of the object's size ahead of (+) or behind (-) its centre. 0 = the middle, where the shell is widest.")]
    public float wakeStartOffset = 0f;
    [Tooltip("Seconds between recorded outlines. The wake lives for (rings x interval) seconds.")]
    public float historyInterval = 0.03f;
    [Tooltip("How early the wake appears. 1 = grows in step with the plasma, lower = appears sooner and stronger (0.5 is a good start).")]
    public float wakeResponse = 0.5f;

    [Header("Optional")]
    public Material materialOverride;

    class Shell
    {
        public Transform source;
        public MeshRenderer renderer;
        public Vector3[] tris;
        public Vector2[] flat;
    }

    class Snapshot
    {
        public float time;
        public float[] radii;
    }

    ReentryBody body;
    readonly List<Shell> shells = new List<Shell>();
    MaterialPropertyBlock block;
    Material mat;
    float size;
    float startScale = 1f;

    Mesh wakeMesh;
    MeshRenderer wakeRenderer;
    Material wakeMat;
    Vector3[] wakeVerts;
    Vector2[] wakeUvs;
    float[] cosT, sinT, live;
    readonly List<Snapshot> history = new List<Snapshot>();
    float lastSnapshot;

    static readonly int PlasmaId = Shader.PropertyToID("_Plasma");
    static readonly int FlowId = Shader.PropertyToID("_FlowDir");
    static readonly int OffsetId = Shader.PropertyToID("_CenterOffset");
    static readonly int InflateId = Shader.PropertyToID("_Inflate");

    void Start()
    {
        body = GetComponent<ReentryBody>();
        block = new MaterialPropertyBlock();
        startScale = Mathf.Max(0.0001f, transform.localScale.x);

        mat = materialOverride;
        if (mat == null)
        {
            Shader sh = ReentryShaders.Find("Reentry/PlasmaWrap");
            if (sh == null)
            {
                Debug.LogError("[ReentryPlasmaWrap] Couldn't find shader 'Reentry/PlasmaWrap'. Is ReentryPlasmaWrap.shader in your Assets folder?");
                return;
            }
            mat = new Material(sh);
        }
        mat.SetFloat("_Intensity", brightness);

        var filters = GetComponentsInChildren<MeshFilter>();
        Bounds b = new Bounds(transform.position, Vector3.zero);
        foreach (var f in filters)
        {
            var r = f.GetComponent<Renderer>();
            if (r != null) b.Encapsulate(r.bounds);
        }
        size = Mathf.Max(0.1f, Mathf.Max(b.extents.x, Mathf.Max(b.extents.y, b.extents.z)));

        var meshCache = new Dictionary<Mesh, Mesh>();
        foreach (var f in filters)
        {
            if (f.sharedMesh == null) continue;
            if (!meshCache.TryGetValue(f.sharedMesh, out Mesh smooth))
            {
                smooth = BuildShellMesh(f.sharedMesh, maxTriangles);
                meshCache[f.sharedMesh] = smooth;
            }

            var go = new GameObject("Plasma Wrap");
            go.transform.SetParent(f.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = smooth;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            var tri = SampleTriangles(f.sharedMesh, 800);
            shells.Add(new Shell
            {
                source = f.transform,
                renderer = mr,
                tris = tri,
                flat = new Vector2[tri.Length]
            });
        }

        SetupWake();
    }

    void SetupWake()
    {
        Shader wakeShader = ReentryShaders.Find("Reentry/PlasmaWake");
        if (wakeShader == null)
        {
            Debug.LogError("[ReentryPlasmaWrap] Couldn't find shader 'Reentry/PlasmaWake'. Is ReentryPlasmaWake.shader in your Assets folder?");
            return;
        }
        wakeMat = new Material(wakeShader);
        wakeMat.SetFloat("_Intensity", brightness);

        wakeRings = Mathf.Max(4, wakeRings);
        wakeSides = Mathf.Max(8, wakeSides);

        cosT = new float[wakeSides];
        sinT = new float[wakeSides];
        live = new float[wakeSides];
        for (int k = 0; k < wakeSides; k++)
        {
            float ang = k / (float)wakeSides * Mathf.PI * 2f;
            cosT[k] = Mathf.Cos(ang);
            sinT[k] = Mathf.Sin(ang);
        }

        int vertsPerRing = wakeSides + 1;
        wakeVerts = new Vector3[wakeRings * vertsPerRing];
        wakeUvs = new Vector2[wakeVerts.Length];
        var tris = new int[(wakeRings - 1) * wakeSides * 6];
        int ti = 0;
        for (int i = 0; i < wakeRings - 1; i++)
        {
            for (int j = 0; j < wakeSides; j++)
            {
                int a0 = i * vertsPerRing + j;
                int b0 = a0 + vertsPerRing;
                tris[ti++] = a0; tris[ti++] = b0; tris[ti++] = a0 + 1;
                tris[ti++] = a0 + 1; tris[ti++] = b0; tris[ti++] = b0 + 1;
            }
        }

        wakeMesh = new Mesh { name = "Reentry Wake" };
        wakeMesh.MarkDynamic();
        wakeMesh.vertices = wakeVerts;
        wakeMesh.uv = wakeUvs;
        wakeMesh.triangles = tris;

        var wgo = new GameObject("Plasma Wake");
        wgo.AddComponent<MeshFilter>().sharedMesh = wakeMesh;
        wakeRenderer = wgo.AddComponent<MeshRenderer>();
        wakeRenderer.sharedMaterial = wakeMat;
        wakeRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        wakeRenderer.receiveShadows = false;
    }

    void LateUpdate()
    {
        if (mat == null || body == null) return;

        float presence = Mathf.Clamp01(transform.localScale.x / startScale);

        float basePlasma = Mathf.Clamp01(body.Plasma * 1.5f);
        float plasma = basePlasma * presence;
        float wakePlasma = Mathf.Pow(basePlasma, wakeResponse) * presence;

        float a = body.entryAngleDeg * Mathf.Deg2Rad;
        Vector3 flow = new Vector3(Mathf.Cos(a), -Mathf.Sin(a), 0f);

        float inflate = size * thickness * Mathf.Lerp(0.5f, 1f, plasma);

        foreach (var s in shells)
        {
            if (s.renderer == null) continue;
            s.renderer.enabled = plasma > 0.01f;

            Vector3 offset = transform.position - s.source.position;

            block.SetFloat(PlasmaId, plasma);
            block.SetVector(FlowId, flow);
            block.SetVector(OffsetId, offset);
            block.SetFloat(InflateId, inflate);
            s.renderer.SetPropertyBlock(block);
        }

        if (wakeMesh != null) UpdateWake(wakePlasma, flow, inflate);
    }

    void UpdateWake(float plasma, Vector3 flow, float shellInflate)
    {
        wakeRenderer.enabled = plasma > 0.01f;
        if (!wakeRenderer.enabled) { history.Clear(); return; }

        Vector3 center = transform.position;

        Vector3 u = Vector3.Cross(flow, Vector3.up);
        if (u.sqrMagnitude < 1e-4f) u = Vector3.Cross(flow, Vector3.right);
        u.Normalize();
        Vector3 w = Vector3.Cross(flow, u).normalized;

        ComputeProfile(center, u, w, live);

        float now = Time.time;
        if (now - lastSnapshot >= historyInterval || history.Count == 0)
        {
            lastSnapshot = now;
            var snap = new Snapshot { time = now, radii = (float[])live.Clone() };
            history.Insert(0, snap);
            while (history.Count > wakeRings - 1) history.RemoveAt(history.Count - 1);
        }

        float lifetime = Mathf.Max(0.05f, wakeRings * historyInterval);
        float length = size * wakeLength * plasma;

        float grow = shellInflate * 0.35f;
        Vector3 start = center + flow * (size * wakeStartOffset);
        int vertsPerRing = wakeSides + 1;

        for (int j = 0; j < wakeRings; j++)
        {
            float age;
            float[] prof;
            float ringFade;
            int avail = history.Count;
            if (j == 0) { age = 0f; prof = live; ringFade = 1f; }
            else if (j - 1 < avail)
            {
                var snap = history[j - 1];
                age = Mathf.Clamp(now - snap.time, 0f, lifetime);
                prof = snap.radii;
                ringFade = Mathf.Clamp01((avail - (j - 1)) / 6f);
            }
            else
            {

                var last = history[avail - 1];
                age = Mathf.Clamp(now - last.time, 0f, lifetime);
                prof = last.radii;
                ringFade = 0f;
            }

            float s = age / lifetime;
            Vector3 ringCenter = start - flow * (s * length);
            float taper = Mathf.Lerp(1f, 0.55f, s);

            for (int k = 0; k <= wakeSides; k++)
            {
                int kk = k % wakeSides;
                float r = (prof[kk] + grow) * wakeSpread * taper;
                Vector3 dir = u * cosT[kk] + w * sinT[kk];
                int idx = j * vertsPerRing + k;
                wakeVerts[idx] = ringCenter + dir * r;
                wakeUvs[idx] = new Vector2(s, ringFade);
            }
        }

        wakeMesh.vertices = wakeVerts;
        wakeMesh.uv = wakeUvs;
        wakeMesh.RecalculateNormals();
        wakeMesh.bounds = new Bounds(center, Vector3.one * (length * 2f + size * 8f));

        wakeMat.SetFloat(PlasmaId, plasma);
        wakeMat.SetVector(FlowId, flow);
    }

    void ComputeProfile(Vector3 center, Vector3 u, Vector3 w, float[] prof)
    {
        float minR = size * 0.15f;
        for (int k = 0; k < prof.Length; k++) prof[k] = minR;

        foreach (var s in shells)
        {
            if (s.source == null || s.tris == null) continue;
            Matrix4x4 m = s.source.localToWorldMatrix;

            Vector3[] tv = s.tris;
            Vector2[] flat = s.flat;
            for (int i = 0; i < tv.Length; i++)
            {
                Vector3 p = m.MultiplyPoint3x4(tv[i]) - center;
                flat[i] = new Vector2(Vector3.Dot(p, u), Vector3.Dot(p, w));
            }

            for (int t = 0; t + 2 < flat.Length; t += 3)
            {
                Vector2 a = flat[t], b = flat[t + 1], c = flat[t + 2];
                float area = (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
                if (Mathf.Abs(area) < 1e-9f) continue;
                float sgn = area > 0f ? 1f : -1f;

                for (int k = 0; k < prof.Length; k++)
                {
                    float dx = cosT[k], dy = sinT[k];
                    float tmin = 0f, tmax = float.MaxValue;
                    bool hit = true;

                    for (int e = 0; e < 3 && hit; e++)
                    {
                        Vector2 p0 = e == 0 ? a : (e == 1 ? b : c);
                        Vector2 p1 = e == 0 ? b : (e == 1 ? c : a);
                        float ex = p1.x - p0.x, ey = p1.y - p0.y;
                        float A = sgn * (ex * dy - ey * dx);
                        float B = -sgn * (ex * p0.y - ey * p0.x);
                        if (Mathf.Abs(A) < 1e-9f)
                        {
                            if (B < 0f) hit = false;
                        }
                        else
                        {
                            float tt = -B / A;
                            if (A > 0f) { if (tt > tmin) tmin = tt; }
                            else        { if (tt < tmax) tmax = tt; }
                        }
                    }

                    if (hit && tmin <= tmax && tmax < float.MaxValue && tmax > prof[k]) prof[k] = tmax;
                }
            }
        }
    }

    static Vector3[] SampleTriangles(Mesh mesh, int maxTriangles)
    {
        if (!mesh.isReadable)
        {

            Bounds bb = mesh.bounds;
            var c = new Vector3[8];
            for (int i = 0; i < 8; i++)
            {
                c[i] = bb.center + new Vector3(
                    (i & 1) == 0 ? -bb.extents.x : bb.extents.x,
                    (i & 2) == 0 ? -bb.extents.y : bb.extents.y,
                    (i & 4) == 0 ? -bb.extents.z : bb.extents.z);
            }
            int[][] quads =
            {
                new[] {0, 2, 6, 4}, new[] {1, 3, 7, 5},
                new[] {0, 1, 5, 4}, new[] {2, 3, 7, 6},
                new[] {0, 1, 3, 2}, new[] {4, 5, 7, 6}
            };
            var box = new List<Vector3>();
            foreach (var q in quads)
            {
                box.Add(c[q[0]]); box.Add(c[q[1]]); box.Add(c[q[2]]);
                box.Add(c[q[0]]); box.Add(c[q[2]]); box.Add(c[q[3]]);
            }
            return box.ToArray();
        }

        Vector3[] verts = mesh.vertices;
        int[] idx = mesh.triangles;
        int triCount = idx.Length / 3;
        int stride = Mathf.Max(1, Mathf.CeilToInt(triCount / (float)maxTriangles));
        var list = new List<Vector3>();
        for (int t = 0; t < triCount; t += stride)
        {
            list.Add(verts[idx[t * 3]]);
            list.Add(verts[idx[t * 3 + 1]]);
            list.Add(verts[idx[t * 3 + 2]]);
        }
        return list.ToArray();
    }

    void OnDestroy()
    {
        foreach (var s in shells)
            if (s.renderer != null) Destroy(s.renderer.gameObject);
        if (wakeRenderer != null) Destroy(wakeRenderer.gameObject);
    }

    static Mesh BuildShellMesh(Mesh src, int maxTriangles)
    {
        if (!src.isReadable)
        {
            Debug.LogWarning("[ReentryPlasmaWrap] Mesh '" + src.name + "' isn't readable, so it can't be smoothed. " +
                             "Tick 'Read/Write' in its import settings for a better look.");
            return src;
        }

        Vector3[] srcVerts = src.vertices;
        Vector3[] srcNormals = src.normals;
        int[] srcTris = src.triangles;
        bool hasNormals = srcNormals != null && srcNormals.Length == srcVerts.Length;
        Vector3 center = src.bounds.center;

        const float q = 1000f;
        var lookup = new Dictionary<Vector3Int, int>();
        var pos = new List<Vector3>();
        var nrm = new List<Vector3>();
        var remap = new int[srcVerts.Length];
        for (int i = 0; i < srcVerts.Length; i++)
        {
            var key = new Vector3Int(
                Mathf.RoundToInt(srcVerts[i].x * q),
                Mathf.RoundToInt(srcVerts[i].y * q),
                Mathf.RoundToInt(srcVerts[i].z * q));
            if (!lookup.TryGetValue(key, out int idx))
            {
                idx = pos.Count;
                lookup[key] = idx;
                pos.Add(srcVerts[i]);
                nrm.Add(Vector3.zero);
            }
            remap[i] = idx;
            nrm[idx] += hasNormals ? srcNormals[i] : (srcVerts[i] - center).normalized;
        }
        for (int i = 0; i < nrm.Count; i++)
        {
            Vector3 n = nrm[i];
            nrm[i] = n.sqrMagnitude > 1e-8f ? n.normalized : Vector3.up;
        }

        var tris = new List<int>(srcTris.Length);
        for (int i = 0; i < srcTris.Length; i++) tris.Add(remap[srcTris[i]]);

        for (int level = 0; level < 3 && (tris.Count / 3) * 4 <= maxTriangles; level++)
        {
            var mids = new Dictionary<long, int>();
            var next = new List<int>(tris.Count * 4);
            for (int t = 0; t < tris.Count; t += 3)
            {
                int a = tris[t], b = tris[t + 1], c = tris[t + 2];
                int ab = Mid(mids, pos, nrm, a, b);
                int bc = Mid(mids, pos, nrm, b, c);
                int ca = Mid(mids, pos, nrm, c, a);
                next.Add(a);  next.Add(ab); next.Add(ca);
                next.Add(b);  next.Add(bc); next.Add(ab);
                next.Add(c);  next.Add(ca); next.Add(bc);
                next.Add(ab); next.Add(bc); next.Add(ca);
            }
            tris = next;
        }

        var mesh = new Mesh { name = src.name + " (plasma)" };
        if (pos.Count > 65535) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(pos);
        mesh.SetNormals(nrm);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();

        Bounds bb2 = mesh.bounds;
        bb2.Expand(bb2.size.magnitude * 3f);
        mesh.bounds = bb2;
        return mesh;
    }

    static int Mid(Dictionary<long, int> mids, List<Vector3> pos, List<Vector3> nrm, int a, int b)
    {
        long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
        if (mids.TryGetValue(key, out int m)) return m;
        m = pos.Count;
        pos.Add((pos[a] + pos[b]) * 0.5f);
        Vector3 n = nrm[a] + nrm[b];
        nrm.Add(n.sqrMagnitude > 1e-8f ? n.normalized : nrm[a]);
        mids[key] = m;
        return m;
    }
}
