using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(ReentryBody))]
public class ReentryBreakup : MonoBehaviour
{
    [Header("Breakup")]
    [Tooltip("Roughly how many fragments the whole object splits into.")]
    public int pieces = 24;
    [Tooltip("How hard the pieces are kicked outward when it breaks (metres per second).")]
    public float scatterSpeed = 3f;
    [Tooltip("How long each fragment lasts before it has burned away (min, max seconds).")]
    public Vector2 pieceLife = new Vector2(2.5f, 6f);
    [Tooltip("The intact object and its plasma fade out over this many seconds at the moment of breakup.")]
    public float breakupFadeTime = 0.5f;
    [Tooltip("Give fragments a back side, so they don't vanish when you look at them from behind.")]
    public bool doubleSided = true;

    [Header("Falling behind")]
    [Tooltip("Each fragment gets between 1x and (1 + this)x the main object's drag per kilo. Higher = they spread out more.")]
    public float dragSpread = 3f;
    [Tooltip("How strongly the air pulls fragments backwards. 0 = they hover with the object.")]
    public float dragStrength = 0.6f;
    [Tooltip("Cap on how fast a fragment can fall behind (metres per second).")]
    public float maxFallBackSpeed = 25f;

    [Header("Streaks")]
    [Tooltip("Length of each fragment's glowing streak in metres. It always points straight back along the airflow. 0 turns streaks off.")]
    public float streakLength = 6f;
    public float trailBrightness = 3f;
    public Material streakMaterial;

    class Part
    {
        public MeshFilter filter;
        public MeshRenderer renderer;
        public float area;
    }

    class Frag
    {
        public Mesh mesh;
        public Vector3 pivot;
    }

    ReentryBody body;
    Material streakMat;
    bool done;

    void Start()
    {
        body = GetComponent<ReentryBody>();
        body.burnDuration = breakupFadeTime;

        streakMat = streakMaterial;
        if (streakMat == null && streakLength > 0f)
        {
            Shader sh = ReentryShaders.Find("Reentry/Streak");
            if (sh != null) streakMat = new Material(sh);
            else Debug.LogWarning("[ReentryBreakup] Couldn't find shader 'Reentry/Streak', so fragments will have no streaks. Is ReentryStreak.shader in your Assets folder?");
        }
    }

    void Update()
    {
        if (!done && body != null && body.BurnedUp) Shatter();
    }

    [ContextMenu("Shatter now")]
    public void ShatterNow()
    {
        if (!done && body != null) Shatter();
    }

    void Shatter()
    {
        done = true;
        Vector3 center = transform.position;

        var parts = new List<Part>();
        float totalArea = 0f;
        foreach (var f in GetComponentsInChildren<MeshFilter>())
        {
            if (f.sharedMesh == null) continue;
            if (f.name == "Plasma Wrap") continue;
            var r = f.GetComponent<MeshRenderer>();
            if (r == null || !r.enabled) continue;
            var p = new Part { filter = f, renderer = r, area = MeshArea(f) };
            parts.Add(p);
            totalArea += p.area;
        }

        foreach (var p in parts)
        {

            int n = Mathf.Max(1, Mathf.RoundToInt(pieces * p.area / Mathf.Max(1e-6f, totalArea)));
            SpawnFragments(p, n, center);
            p.renderer.enabled = false;
        }
    }

    void SpawnFragments(Part p, int n, Vector3 center)
    {
        Mesh mesh = p.filter.sharedMesh;

        if (!mesh.isReadable || n <= 1 && mesh.triangles.Length < 12)
        {
            if (!mesh.isReadable)
                Debug.LogWarning("[ReentryBreakup] Mesh '" + mesh.name + "' isn't readable, so it breaks off as one piece. Tick 'Read/Write' in its import settings to let it shatter.");
            CreateFragment(p, mesh, Vector3.zero, center);
            return;
        }

        foreach (var frag in Fracture(mesh, n))
            CreateFragment(p, frag.mesh, frag.pivot, center);
    }

    void CreateFragment(Part p, Mesh mesh, Vector3 pivot, Vector3 center)
    {
        Transform tr = p.filter.transform;
        var go = new GameObject("Fragment");
        go.transform.SetPositionAndRotation(tr.TransformPoint(pivot), tr.rotation);
        go.transform.localScale = tr.lossyScale;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterials = p.renderer.sharedMaterials;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        Vector3 outward = go.transform.position - center;
        if (outward.sqrMagnitude < 1e-6f) outward = Random.onUnitSphere;
        outward.Normalize();
        Vector3 kick = (outward + Random.insideUnitSphere * 0.6f).normalized * scatterSpeed * Random.Range(0.4f, 1.4f);

        float a = body.entryAngleDeg * Mathf.Deg2Rad;
        Vector3 flow = new Vector3(Mathf.Cos(a), -Mathf.Sin(a), 0f);
        float forward = Vector3.Dot(kick, flow);
        if (forward > 0f) kick -= flow * forward * 0.9f;

        go.AddComponent<ReentryPiece>().Init(
            body,
            kick,
            Random.Range(pieceLife.x, pieceLife.y),
            1f + Random.Range(0f, dragSpread),
            dragStrength,
            maxFallBackSpeed,
            body.emissionNits,
            streakLength,
            trailBrightness,
            streakMat);
    }

    static float MeshArea(MeshFilter f)
    {
        Mesh m = f.sharedMesh;
        Vector3 s = f.transform.lossyScale;
        float scale2 = Mathf.Abs((s.x * s.y + s.y * s.z + s.x * s.z) / 3f);

        if (!m.isReadable)
        {
            Vector3 e = m.bounds.extents;
            return 8f * (e.x * e.y + e.y * e.z + e.x * e.z) * scale2;
        }

        Vector3[] v = m.vertices;
        int[] t = m.triangles;
        float area = 0f;
        for (int i = 0; i + 2 < t.Length; i += 3)
            area += Vector3.Cross(v[t[i + 1]] - v[t[i]], v[t[i + 2]] - v[t[i]]).magnitude * 0.5f;
        return area * scale2;
    }

    List<Frag> Fracture(Mesh src, int n)
    {
        Vector3[] verts = src.vertices;
        Vector3[] normals = src.normals;
        Vector2[] uvs = src.uv;
        Vector4[] tangents = src.tangents;
        bool hasUV = uvs != null && uvs.Length == verts.Length;
        bool hasT = tangents != null && tangents.Length == verts.Length;
        if (normals == null || normals.Length != verts.Length)
        {
            normals = new Vector3[verts.Length];
            Vector3 c0 = src.bounds.center;
            for (int i = 0; i < verts.Length; i++) normals[i] = (verts[i] - c0).normalized;
        }

        int subCount = src.subMeshCount;
        var triA = new List<int>();
        var triB = new List<int>();
        var triC = new List<int>();
        var triSub = new List<int>();
        for (int s = 0; s < subCount; s++)
        {
            int[] t = src.GetTriangles(s);
            for (int i = 0; i + 2 < t.Length; i += 3)
            {
                triA.Add(t[i]); triB.Add(t[i + 1]); triC.Add(t[i + 2]);
                triSub.Add(s);
            }
        }
        int T = triA.Count;
        var result = new List<Frag>();
        if (T == 0) return result;

        var cen = new Vector3[T];
        for (int t = 0; t < T; t++) cen[t] = (verts[triA[t]] + verts[triB[t]] + verts[triC[t]]) / 3f;

        n = Mathf.Clamp(n, 1, Mathf.Max(1, T / 2));
        var assign = new int[T];
        if (n > 1) Cluster(cen, n, assign);

        var members = new List<int>[n];
        for (int c = 0; c < n; c++) members[c] = new List<int>();
        for (int t = 0; t < T; t++) members[assign[t]].Add(t);

        for (int c = 0; c < n; c++)
        {
            var mem = members[c];
            if (mem.Count == 0) continue;

            Vector3 pivot = Vector3.zero;
            foreach (int t in mem) pivot += cen[t];
            pivot /= mem.Count;

            var remap = new Dictionary<int, int>();
            var pos = new List<Vector3>();
            var nor = new List<Vector3>();
            var uv = new List<Vector2>();
            var tan = new List<Vector4>();
            var subTris = new List<int>[subCount];
            for (int s = 0; s < subCount; s++) subTris[s] = new List<int>();

            var corner = new int[3];
            foreach (int t in mem)
            {
                corner[0] = triA[t]; corner[1] = triB[t]; corner[2] = triC[t];
                for (int j = 0; j < 3; j++)
                {
                    int o = corner[j];
                    if (!remap.TryGetValue(o, out int ni))
                    {
                        ni = pos.Count;
                        remap[o] = ni;
                        pos.Add(verts[o] - pivot);
                        nor.Add(normals[o]);
                        if (hasUV) uv.Add(uvs[o]);
                        if (hasT) tan.Add(tangents[o]);
                    }
                    corner[j] = ni;
                }
                var list = subTris[triSub[t]];
                list.Add(corner[0]); list.Add(corner[1]); list.Add(corner[2]);
            }

            if (doubleSided)
            {

                int nF = pos.Count;
                for (int i = 0; i < nF; i++)
                {
                    pos.Add(pos[i]);
                    nor.Add(-nor[i]);
                    if (hasUV) uv.Add(uv[i]);
                    if (hasT) { var tg = tan[i]; tan.Add(new Vector4(tg.x, tg.y, tg.z, -tg.w)); }
                }
                for (int s = 0; s < subCount; s++)
                {
                    var list = subTris[s];
                    int cnt = list.Count;
                    for (int i = 0; i + 2 < cnt; i += 3)
                    {
                        list.Add(list[i] + nF);
                        list.Add(list[i + 2] + nF);
                        list.Add(list[i + 1] + nF);
                    }
                }
            }

            var m = new Mesh { name = src.name + " fragment" };
            if (pos.Count > 65535) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            m.SetVertices(pos);
            m.SetNormals(nor);
            if (hasUV) m.SetUVs(0, uv);
            if (hasT) m.SetTangents(tan);
            m.subMeshCount = subCount;
            for (int s = 0; s < subCount; s++) m.SetTriangles(subTris[s], s);
            m.RecalculateBounds();

            result.Add(new Frag { mesh = m, pivot = pivot });
        }
        return result;
    }

    static void Cluster(Vector3[] pts, int n, int[] assign)
    {
        int T = pts.Length;
        var seeds = new Vector3[n];
        var minD = new float[T];
        for (int i = 0; i < T; i++) minD[i] = float.MaxValue;

        seeds[0] = pts[Random.Range(0, T)];
        for (int k = 1; k < n; k++)
        {
            int far = 0;
            float best = -1f;
            for (int i = 0; i < T; i++)
            {
                float d = (pts[i] - seeds[k - 1]).sqrMagnitude;
                if (d < minD[i]) minD[i] = d;
                if (minD[i] > best) { best = minD[i]; far = i; }
            }
            seeds[k] = pts[far];
        }

        var sum = new Vector3[n];
        var count = new int[n];
        for (int iter = 0; iter < 4; iter++)
        {
            for (int i = 0; i < T; i++)
            {
                int bestK = 0;
                float bestD = float.MaxValue;
                for (int k = 0; k < n; k++)
                {
                    float d = (pts[i] - seeds[k]).sqrMagnitude;
                    if (d < bestD) { bestD = d; bestK = k; }
                }
                assign[i] = bestK;
            }
            for (int k = 0; k < n; k++) { sum[k] = Vector3.zero; count[k] = 0; }
            for (int i = 0; i < T; i++) { sum[assign[i]] += pts[i]; count[assign[i]]++; }
            for (int k = 0; k < n; k++) if (count[k] > 0) seeds[k] = sum[k] / count[k];
        }
    }
}
