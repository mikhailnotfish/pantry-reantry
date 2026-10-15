using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

public static class ReentryPlanetGen
{
    public class Result
    {
        public int size, emissionSize;
        public Color32[][] color;
        public Color32[][] emission;
    }

    class Params
    {
        public int seed;
        public float sea;
        public float cloudCoverage;
    }

    static uint HashU(int x, int y, int z, int seed)
    {
        unchecked
        {
            uint h = (uint)x * 374761393u + (uint)y * 668265263u + (uint)z * 2147483647u + (uint)seed * 1274126177u;
            h = (h ^ (h >> 13)) * 1274126177u;
            return h ^ (h >> 16);
        }
    }

    static float Fade(float t) { return t * t * t * (t * (t * 6f - 15f) + 10f); }

    static float Grad(uint h, float x, float y, float z)
    {
        switch (h & 15u)
        {
            case 0: return x + y;
            case 1: return -x + y;
            case 2: return x - y;
            case 3: return -x - y;
            case 4: return x + z;
            case 5: return -x + z;
            case 6: return x - z;
            case 7: return -x - z;
            case 8: return y + z;
            case 9: return -y + z;
            case 10: return y - z;
            case 11: return -y - z;
            case 12: return x + y;
            case 13: return -x + y;
            case 14: return -y + z;
            default: return -y - z;
        }
    }

    static float Noise3(float x, float y, float z, int seed)
    {
        int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y), zi = Mathf.FloorToInt(z);
        float fx = x - xi, fy = y - yi, fz = z - zi;
        float u = Fade(fx), v = Fade(fy), w = Fade(fz);

        float n000 = Grad(HashU(xi, yi, zi, seed), fx, fy, fz);
        float n100 = Grad(HashU(xi + 1, yi, zi, seed), fx - 1f, fy, fz);
        float n010 = Grad(HashU(xi, yi + 1, zi, seed), fx, fy - 1f, fz);
        float n110 = Grad(HashU(xi + 1, yi + 1, zi, seed), fx - 1f, fy - 1f, fz);
        float n001 = Grad(HashU(xi, yi, zi + 1, seed), fx, fy, fz - 1f);
        float n101 = Grad(HashU(xi + 1, yi, zi + 1, seed), fx - 1f, fy, fz - 1f);
        float n011 = Grad(HashU(xi, yi + 1, zi + 1, seed), fx, fy - 1f, fz - 1f);
        float n111 = Grad(HashU(xi + 1, yi + 1, zi + 1, seed), fx - 1f, fy - 1f, fz - 1f);

        float x00 = n000 + (n100 - n000) * u, x10 = n010 + (n110 - n010) * u;
        float x01 = n001 + (n101 - n001) * u, x11 = n011 + (n111 - n011) * u;
        float y0 = x00 + (x10 - x00) * v, y1 = x01 + (x11 - x01) * v;
        float n = y0 + (y1 - y0) * w;
        return Mathf.Clamp01(0.5f + 0.62f * n);
    }

    static float Fbm(float x, float y, float z, int octaves, int seed)
    {
        float sum = 0f, amp = 0.5f, norm = 0f;
        for (int i = 0; i < octaves; i++)
        {
            sum += amp * Noise3(x, y, z, seed + i * 131);
            norm += amp;
            float nx = (0.00f * x + 0.80f * y + 0.60f * z) * 2.02f;
            float ny = (-0.80f * x + 0.36f * y - 0.48f * z) * 2.02f;
            float nz = (-0.60f * x - 0.48f * y + 0.64f * z) * 2.02f;
            x = nx; y = ny; z = nz;
            amp *= 0.5f;
        }
        return sum / norm;
    }

    static float Remap(float v) { return Mathf.Clamp01((v - 0.5f) * 1.9f + 0.5f); }
    static float Smooth(float a, float b, float v) { return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a, b, v)); }

    static float Elevation(Vector3 p, int seed, out Vector3 warp)
    {

        float wx = Fbm(p.x * 2.2f + 3.1f, p.y * 2.2f + 7.7f, p.z * 2.2f + 1.3f, 4, seed + 1) - 0.5f;
        float wy = Fbm(p.x * 2.2f + 9.2f, p.y * 2.2f + 4.4f, p.z * 2.2f + 6.6f, 4, seed + 2) - 0.5f;
        float wz = Fbm(p.x * 2.2f + 5.5f, p.y * 2.2f + 2.9f, p.z * 2.2f + 8.1f, 4, seed + 3) - 0.5f;
        warp = new Vector3(wx, wy, wz);

        float qx = p.x + wx * 0.9f, qy = p.y + wy * 0.9f, qz = p.z + wz * 0.9f;
        return Fbm(qx * 1.25f + 0.7f, qy * 1.25f + 2.1f, qz * 1.25f + 4.3f, 7, seed);
    }

    static Params MakeParams(int seed, float landAmount, float cloudCoverage)
    {
        var pp = new Params { seed = seed, cloudCoverage = cloudCoverage };

        const int count = 6000;
        var samples = new float[count];
        float golden = Mathf.PI * (3f - Mathf.Sqrt(5f));
        for (int i = 0; i < count; i++)
        {
            float y = 1f - 2f * (i + 0.5f) / count;
            float r = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
            float th = golden * i;
            Vector3 d = new Vector3(Mathf.Cos(th) * r, y, Mathf.Sin(th) * r), w;
            samples[i] = Elevation(d, seed, out w);
        }
        Array.Sort(samples);
        int idx = Mathf.Clamp(Mathf.RoundToInt((1f - Mathf.Clamp01(landAmount)) * (count - 1)), 0, count - 1);
        pp.sea = samples[idx];
        return pp;
    }

    static readonly Color DeepOcean = new Color(0.010f, 0.045f, 0.17f);
    static readonly Color Shallow = new Color(0.045f, 0.28f, 0.40f);
    static readonly Color Sand = new Color(0.62f, 0.55f, 0.36f);
    static readonly Color Desert = new Color(0.60f, 0.45f, 0.26f);
    static readonly Color Grass = new Color(0.20f, 0.33f, 0.10f);
    static readonly Color Forest = new Color(0.045f, 0.16f, 0.06f);
    static readonly Color Jungle = new Color(0.02f, 0.11f, 0.04f);
    static readonly Color Tundra = new Color(0.36f, 0.38f, 0.32f);
    static readonly Color Rock = new Color(0.30f, 0.27f, 0.24f);
    static readonly Color Snow = new Color(0.90f, 0.93f, 0.96f);
    static readonly Color CloudCol = new Color(0.93f, 0.94f, 0.96f);

    struct Surf { public Color color; public float city; }

    static Surf Sample(Vector3 p, Params pp, bool clouds)
    {
        int seed = pp.seed;
        Vector3 warp;
        float e = Elevation(p, seed, out warp);

        float lat = Mathf.Asin(Mathf.Clamp(p.y, -1f, 1f));
        float absLat = Mathf.Abs(lat) / (Mathf.PI * 0.5f);

        Color col;
        float city = 0f;
        float detail = Remap(Fbm(p.x * 30f, p.y * 30f, p.z * 30f, 5, seed + 900));

        if (e < pp.sea)
        {

            float depth = Mathf.Clamp01((pp.sea - e) / 0.16f);
            col = Color.Lerp(Shallow, DeepOcean, Mathf.Pow(Smooth(0.0f, 1f, depth), 0.6f));
            col *= 0.90f + 0.20f * detail;
        }
        else
        {
            float hl = (e - pp.sea) / Mathf.Max(0.001f, 1f - pp.sea);
            float moist = Remap(Fbm(p.x * 2.8f + 11f, p.y * 2.8f + 5f, p.z * 2.8f + 3f, 5, seed + 40));
            float tn = Fbm(p.x * 4f + 21f, p.y * 4f + 8f, p.z * 4f + 2f, 3, seed + 50) - 0.5f;

            float t = 1f - Mathf.Pow(absLat, 1.5f);
            t = Mathf.Clamp01(t - hl * 0.9f + tn * 0.25f);

            col = Color.Lerp(Desert, Grass, Smooth(0.30f, 0.50f, moist));
            col = Color.Lerp(col, Forest, Smooth(0.50f, 0.72f, moist));
            col = Color.Lerp(col, Jungle, Smooth(0.78f, 0.95f, t) * Smooth(0.60f, 0.85f, moist));
            col = Color.Lerp(Tundra, col, Smooth(0.28f, 0.45f, t));
            col = Color.Lerp(Snow, col, Smooth(0.10f, 0.24f, t));

            col = Color.Lerp(col, Rock, Smooth(0.35f, 0.65f, hl));
            col = Color.Lerp(col, Snow, Smooth(0.60f, 0.85f, hl) * 0.9f);
            col = Color.Lerp(Sand, col, Smooth(0.0f, 0.035f, hl));
            col *= 0.62f + 0.76f * detail;

            if (hl < 0.40f && t > 0.30f)
            {
                float density = Smooth(0.52f, 0.74f, Remap(Fbm(p.x * 4.5f + 31f, p.y * 4.5f + 17f, p.z * 4.5f + 9f, 5, seed + 70)));
                float coast = 1f + 0.8f * (1f - Smooth(0f, 0.12f, hl));
                float sparkle = Smooth(0.58f, 0.85f, Fbm(p.x * 55f, p.y * 55f, p.z * 55f, 4, seed + 95));
                city = Mathf.Clamp01(density * sparkle * coast * 1.4f);
            }
        }

        float nn = Fbm(p.x * 6f + 3f, p.y * 6f + 1f, p.z * 6f + 5f, 3, seed + 60) - 0.5f;
        float ice = Smooth(0.78f + nn * 0.14f, 0.90f + nn * 0.14f, absLat);
        col = Color.Lerp(col, new Color(0.90f, 0.94f, 0.98f), ice);
        city *= 1f - ice;

        if (clouds && pp.cloudCoverage > 0.001f)
        {
            float cl = Fbm((p.x + warp.x * 0.7f) * 3.3f + 71f, (p.y + warp.y * 0.7f) * 3.3f + 19f, (p.z + warp.z * 0.7f) * 3.3f + 43f, 7, seed + 300);
            float fine = Fbm(p.x * 14f + 5f, p.y * 14f + 9f, p.z * 14f + 2f, 4, seed + 310);
            float band = 0.5f + 0.5f * Mathf.Cos(lat * 6f);
            float v = Remap(cl) * 0.72f + band * 0.18f + (fine - 0.5f) * 0.25f + 0.05f;
            float lo = Mathf.Lerp(0.66f, 0.24f, Mathf.Clamp01(pp.cloudCoverage));
            float cover = Smooth(lo, lo + 0.26f, v);
            col = Color.Lerp(col, CloudCol, cover * 0.96f);
            city *= 1f - cover;
        }

        col.a = 1f;
        return new Surf { color = col, city = city };
    }

    public static Vector3 FaceDir(int face, int px, int py, int size, int variant)
    {
        float u = (px + 0.5f) / size * 2f - 1f;
        float v = (py + 0.5f) / size * 2f - 1f;
        if ((variant & 1) != 0) u = -u;
        if ((variant & 2) != 0) v = -v;
        Vector3 d;
        switch (face)
        {
            case 0: d = new Vector3(1f, -v, -u); break;
            case 1: d = new Vector3(-1f, -v, u); break;
            case 2: d = new Vector3(u, 1f, v); break;
            case 3: d = new Vector3(u, -1f, -v); break;
            case 4: d = new Vector3(u, -v, 1f); break;
            default: d = new Vector3(-u, -v, -1f); break;
        }
        return d.normalized;
    }

    static int DominantFace(Vector3 d)
    {
        float ax = Mathf.Abs(d.x), ay = Mathf.Abs(d.y), az = Mathf.Abs(d.z);
        if (ax >= ay && ax >= az) return d.x > 0f ? 0 : 1;
        if (ay >= az) return d.y > 0f ? 2 : 3;
        return d.z > 0f ? 4 : 5;
    }

    public static int[] Calibrate()
    {
        var variants = new int[6];
        Shader sh = ReentryShaders.Find("Hidden/Reentry/CubeProbe");
        if (sh == null)
        {
            Debug.LogWarning("[ReentryPlanet] Couldn't find ReentryCubeProbe.shader. Add it to your Assets folder. The planet might show seams.");
            return variants;
        }

        const int S = 16, W = 96, H = 48;
        var mat = new Material(sh);
        var rt = new RenderTexture(W, H, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        var tex = new Texture2D(W, H, TextureFormat.RGBA32, false, true);
        var err = new double[2, 6, 4];

        for (int w = 0; w < 4; w++)
        {
            var cube = new Cubemap(S, GraphicsFormat.R8G8B8A8_UNorm, TextureCreationFlags.None);
            for (int f = 0; f < 6; f++)
            {
                var data = new Color32[S * S];
                for (int y = 0; y < S; y++)
                    for (int x = 0; x < S; x++)
                    {
                        Vector3 d = FaceDir(f, x, y, S, w);
                        data[y * S + x] = new Color32((byte)(d.x * 127.5f + 127.5f), (byte)(d.y * 127.5f + 127.5f), (byte)(d.z * 127.5f + 127.5f), 255);
                    }
                cube.SetPixelData(data, 0, (CubemapFace)f);
            }
            cube.Apply(false, false);

            mat.SetTexture("_Cube", cube);
            Graphics.Blit(Texture2D.whiteTexture, rt, mat);
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            var px = tex.GetPixels32();

            for (int o = 0; o < 2; o++)
                for (int y = 0; y < H; y++)
                    for (int x = 0; x < W; x++)
                    {
                        float u = (x + 0.5f) / W, v = (y + 0.5f) / H;
                        if (o == 1) v = 1f - v;
                        float lon = (u * 2f - 1f) * Mathf.PI, lat = (v - 0.5f) * Mathf.PI;
                        Vector3 d = new Vector3(Mathf.Cos(lat) * Mathf.Cos(lon), Mathf.Sin(lat), Mathf.Cos(lat) * Mathf.Sin(lon));
                        int face = DominantFace(d);
                        Color32 c = px[y * W + x];
                        double e = Mathf.Abs(c.r - (d.x * 127.5f + 127.5f)) + Mathf.Abs(c.g - (d.y * 127.5f + 127.5f)) + Mathf.Abs(c.b - (d.z * 127.5f + 127.5f));
                        err[o, face, w] += e;
                    }

            UnityEngine.Object.Destroy(cube);
        }

        double bestTotal = double.MaxValue;
        for (int o = 0; o < 2; o++)
        {
            double total = 0;
            var pick = new int[6];
            for (int f = 0; f < 6; f++)
            {
                int bw = 0;
                for (int w = 1; w < 4; w++) if (err[o, f, w] < err[o, f, bw]) bw = w;
                pick[f] = bw;
                total += err[o, f, bw];
            }
            if (total < bestTotal) { bestTotal = total; variants = pick; }
        }

        UnityEngine.Object.Destroy(rt);
        UnityEngine.Object.Destroy(tex);
        UnityEngine.Object.Destroy(mat);

        double perPixel = bestTotal / (W * H);
        if (perPixel > 40.0)
            Debug.LogWarning("[ReentryPlanet] Cubemap face check looked unsure (" + perPixel.ToString("F1") + "). The planet may show faint seams.");
        return variants;
    }

    public static Task<Result> GenerateAsync(int size, int seed, float landAmount, float cloudCoverage, bool cityLights, int[] variants, int threads)
    {
        return Task.Run(() => Generate(size, seed, landAmount, cloudCoverage, cityLights, variants, threads));
    }

    static Result Generate(int size, int seed, float landAmount, float cloudCoverage, bool cityLights, int[] variants, int threads)
    {
        var opts = new ParallelOptions { MaxDegreeOfParallelism = Mathf.Max(1, threads) };
        var pp = MakeParams(seed, landAmount, cloudCoverage);
        var res = new Result { size = size, color = new Color32[6][] };
        for (int f = 0; f < 6; f++) res.color[f] = new Color32[size * size];

        Parallel.For(0, 6 * size, opts, idx =>
        {
            int f = idx / size, y = idx % size;
            var arr = res.color[f];
            for (int x = 0; x < size; x++)
            {
                Vector3 d = FaceDir(f, x, y, size, variants[f]);
                arr[y * size + x] = (Color32)Sample(d, pp, true).color;
            }
        });

        if (cityLights)
        {
            int es = Mathf.Max(256, size / 2);
            res.emissionSize = es;
            res.emission = new Color32[6][];
            for (int f = 0; f < 6; f++) res.emission[f] = new Color32[es * es];

            Parallel.For(0, 6 * es, opts, idx =>
            {
                int f = idx / es, y = idx % es;
                var arr = res.emission[f];
                for (int x = 0; x < es; x++)
                {
                    Vector3 d = FaceDir(f, x, y, es, variants[f]);
                    float c = Sample(d, pp, true).city;
                    arr[y * es + x] = new Color32((byte)(255f * c), (byte)(190f * c), (byte)(105f * c), 255);
                }
            });
        }
        return res;
    }

    public static Cubemap MakeCubemap(Color32[][] faces, int size, string name)
    {
        var cube = new Cubemap(size, GraphicsFormat.R8G8B8A8_SRGB, TextureCreationFlags.MipChain);
        cube.name = name;
        for (int f = 0; f < 6; f++) cube.SetPixelData(faces[f], 0, (CubemapFace)f);
        cube.Apply(true, true);
        cube.wrapMode = TextureWrapMode.Clamp;
        cube.filterMode = FilterMode.Trilinear;
        return cube;
    }

    public static Vector3 LatLon(float latDeg, float lonDeg)
    {
        float la = latDeg * Mathf.Deg2Rad, lo = lonDeg * Mathf.Deg2Rad;
        return new Vector3(Mathf.Cos(la) * Mathf.Cos(lo), Mathf.Sin(la), Mathf.Cos(la) * Mathf.Sin(lo));
    }
}
