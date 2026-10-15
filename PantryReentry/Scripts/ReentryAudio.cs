using System;
using System.Threading.Tasks;
using UnityEngine;

public class ReentryAudio : MonoBehaviour
{
    [Header("Volumes")]
    [Range(0f, 1f)] public float masterVolume = 1f;
    [Range(0f, 1f)] public float musicVolume = 0.55f;
    [Range(0f, 1f)] public float plasmaVolume = 0.8f;
    [Range(0f, 1f)] public float crackleVolume = 0.5f;
    [Range(0f, 1f)] public float explosionVolume = 1f;
    [Tooltip("Volume of the snap when you click a card or button.")]
    [Range(0f, 1f)] public float clickVolume = 0.8f;
    [Tooltip("Volume of the soft tick when you move the mouse onto a card or button.")]
    [Range(0f, 1f)] public float hoverVolume = 0.35f;
    public bool hoverSounds = true;

    [Header("Your own sounds (optional)")]
    [Tooltip("A song for the menu. Leave empty to use the built-in one.")]
    public AudioClip menuMusic;
    [Tooltip("A looping roar for the plasma. Leave empty to use the built-in one.")]
    public AudioClip plasmaLoop;
    [Tooltip("The explosion when the object burns up. Leave empty to use the built-in one.")]
    public AudioClip explosion;
    [Tooltip("The snap when you click something. Leave empty to use the built-in one.")]
    public AudioClip clickSound;
    [Tooltip("The tick when you hover something. Leave empty to use the built-in one.")]
    public AudioClip hoverSound;

    [Header("Feel")]
    [Tooltip("Seconds for the music to fade in.")]
    public float musicFadeIn = 3f;
    [Tooltip("How quiet the roar is when the plasma is just starting, and how loud at full burn (0 to 1).")]
    public float roarCurve = 0.8f;

    const int SampleRate = 32000;

    ReentryBody body;
    float startScale = 1f;
    bool burnedLast;

    static ReentryAudio instance;
    AudioSource music, roar, rumble, crackle, boom, ui;
    AudioClip clickClip, hoverClip;
    AudioLowPassFilter roarFilter, crackleFilter;
    Task<float[]> musicTask;
    float musicLevel;

    float Sfx { get { return masterVolume * ReentrySettings.Master * ReentrySettings.Sfx; } }
    float Mus { get { return masterVolume * ReentrySettings.Master * ReentrySettings.Music; } }

    public static void Click()
    {
        if (instance != null && instance.ui != null && instance.clickClip != null)
            instance.ui.PlayOneShot(instance.clickClip, instance.clickVolume * instance.Sfx);
    }

    public static void Hover()
    {
        if (instance != null && instance.hoverSounds && instance.ui != null && instance.hoverClip != null)
            instance.ui.PlayOneShot(instance.hoverClip, instance.hoverVolume * instance.Sfx);
    }

    void OnEnable() { instance = this; }
    void OnDisable() { if (instance == this) instance = null; }

    void Start()
    {
        instance = this;
        body = FindAnyObjectByType<ReentryBody>();
        if (body != null) startScale = Mathf.Max(0.0001f, body.transform.localScale.x);

        if (FindAnyObjectByType<AudioListener>() == null)
        {
            Camera cam = Camera.main != null ? Camera.main : FindAnyObjectByType<Camera>();
            if (cam != null) cam.gameObject.AddComponent<AudioListener>();
        }

        if (!ReentryMenu.Launched)
        {
            music = MakeSource("Menu Music", true);
            if (menuMusic != null)
            {
                music.clip = menuMusic;
                music.volume = 0f;
                music.Play();
            }
            else
            {

                musicTask = Task.Run(() => SynthMusic(SampleRate));
            }
        }

        var rng = new System.Random(4242);

        roar = MakeSource("Plasma Roar", true);
        roar.clip = plasmaLoop != null ? plasmaLoop : MakeLoop(BuildRoar(rng, 5f), "Plasma Roar");
        roarFilter = roar.gameObject.AddComponent<AudioLowPassFilter>();
        roarFilter.cutoffFrequency = 600f;
        roar.volume = 0f;
        roar.Play();

        rumble = MakeSource("Plasma Rumble", true);
        rumble.clip = MakeLoop(BuildRumble(rng, 5f), "Plasma Rumble");
        rumble.volume = 0f;
        rumble.Play();

        crackle = MakeSource("Heat Crackle", true);
        crackle.clip = MakeLoop(BuildCrackle(rng, 4f), "Heat Crackle");
        crackleFilter = crackle.gameObject.AddComponent<AudioLowPassFilter>();
        crackleFilter.cutoffFrequency = 5000f;
        crackle.volume = 0f;
        crackle.Play();

        boom = MakeSource("Explosion", false);
        if (explosion == null) explosion = BuildExplosion();

        ui = MakeSource("UI Sounds", false);
        clickClip = clickSound != null ? clickSound : BuildClick();
        hoverClip = hoverSound != null ? hoverSound : BuildHover();
    }

    AudioSource MakeSource(string name, bool loop)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var s = go.AddComponent<AudioSource>();
        s.playOnAwake = false;
        s.loop = loop;
        s.spatialBlend = 0f;
        return s;
    }

    void Update()
    {
        UpdateMusic();
        UpdateBurnSounds();
    }

    void UpdateMusic()
    {
        if (music == null) return;

        if (musicTask != null && musicTask.IsCompleted)
        {
            if (musicTask.IsFaulted)
            {
                Debug.LogWarning("[ReentryAudio] Couldn't compose the menu music: " + musicTask.Exception);
            }
            else
            {
                float[] data = musicTask.Result;
                var clip = AudioClip.Create("Menu Music", data.Length, 1, SampleRate, false);
                clip.SetData(data, 0);
                music.clip = clip;
                music.volume = 0f;
                music.Play();
            }
            musicTask = null;
        }

        float target = ReentryMenu.Launching ? 0f : musicVolume * Mus;
        float rate = ReentryMenu.Launching ? 1.5f : Mathf.Max(0.05f, musicVolume) / Mathf.Max(0.2f, musicFadeIn);
        musicLevel = Mathf.MoveTowards(musicLevel, target, rate * Time.unscaledDeltaTime);
        music.volume = musicLevel;
    }

    void UpdateBurnSounds()
    {
        if (body == null) return;

        float presence = Mathf.Clamp01(body.transform.localScale.x / startScale);
        float p = Mathf.Clamp01(body.Plasma);
        float heat = Mathf.Clamp01(body.Heat);
        float level = Mathf.Pow(p, roarCurve) * presence * Sfx;

        roar.volume = level * plasmaVolume;
        roar.pitch = 0.85f + 0.35f * p;
        roarFilter.cutoffFrequency = Mathf.Lerp(450f, 9000f, Mathf.Pow(p, 0.8f));

        rumble.volume = Mathf.Pow(p, 0.6f) * presence * Sfx * plasmaVolume;
        rumble.pitch = 0.8f + 0.4f * p;

        crackle.volume = Mathf.Clamp01(Mathf.Max(0f, p * 0.9f + heat * 0.4f - 0.15f)) * presence * Sfx * crackleVolume;

        if (body.BurnedUp && !burnedLast)
            boom.PlayOneShot(explosion, explosionVolume * Sfx);
        burnedLast = body.BurnedUp;
    }

    static AudioClip MakeLoop(float[] raw, string name)
    {

        int fade = Mathf.Min(raw.Length / 4, SampleRate / 3);
        int n = raw.Length - fade;
        var o = new float[n];
        for (int i = 0; i < n; i++) o[i] = raw[i];
        for (int i = 0; i < fade; i++)
        {
            float t = i / (float)fade;
            o[i] = raw[i] * t + raw[n + i] * (1f - t);
        }
        var clip = AudioClip.Create(name, n, 1, SampleRate, false);
        clip.SetData(o, 0);
        return clip;
    }

    static float Noise(System.Random r) { return (float)(r.NextDouble() * 2.0 - 1.0); }

    static void Normalize(float[] b, float peak)
    {
        float m = 1e-6f;
        for (int i = 0; i < b.Length; i++) m = Mathf.Max(m, Mathf.Abs(b[i]));
        float g = peak / m;
        for (int i = 0; i < b.Length; i++) b[i] *= g;
    }

    static float[] BuildRoar(System.Random r, float seconds)
    {
        int n = (int)(seconds * SampleRate) + SampleRate / 3;
        var b = new float[n];
        float y1 = 0f, y2 = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)SampleRate;
            float x = Noise(r);
            y1 += 0.30f * (x - y1);
            y2 += 0.30f * (y1 - y2);
            float flutter = 1f + 0.18f * Mathf.Sin(t * 2f * Mathf.PI * 6.3f) + 0.12f * Mathf.Sin(t * 2f * Mathf.PI * 13.1f + 1f);
            b[i] = y2 * flutter;
        }
        Normalize(b, 0.9f);
        return b;
    }

    static float[] BuildRumble(System.Random r, float seconds)
    {
        int n = (int)(seconds * SampleRate) + SampleRate / 3;
        var b = new float[n];
        float a = 0f, c = 0f, d = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)SampleRate;
            float x = Noise(r);
            a += 0.012f * (x - a);
            c += 0.02f * (a - c);
            d += 0.03f * (c - d);
            b[i] = d * (1f + 0.25f * Mathf.Sin(t * 2f * Mathf.PI * 3.1f));
        }
        Normalize(b, 0.9f);
        return b;
    }

    static float[] BuildCrackle(System.Random r, float seconds)
    {
        int n = (int)(seconds * SampleRate) + SampleRate / 3;
        var b = new float[n];
        float env = 0f, y = 0f;
        for (int i = 0; i < n; i++)
        {
            if (r.NextDouble() < 28.0 / SampleRate)
                env = 0.3f + (float)r.NextDouble() * 0.7f;
            float x = Noise(r) * env;
            env *= 0.992f;
            y += 0.6f * (x - y);
            b[i] = y;
        }
        Normalize(b, 0.9f);
        return b;
    }

    static AudioClip BuildExplosion()
    {
        var r = new System.Random(7);
        float seconds = 5f;
        int n = (int)(seconds * SampleRate);
        var b = new float[n];
        float phase = 0f, nz = 0f, ra = 0f, rb = 0f;

        for (int i = 0; i < n; i++)
        {
            float t = i / (float)SampleRate;

            float f = 32f + 90f * Mathf.Exp(-t * 7f);
            phase += 2f * Mathf.PI * f / SampleRate;
            float thump = Mathf.Sin(phase) * Mathf.Exp(-t * 1.9f) * 1.0f;

            float crack = t < 0.05f ? Noise(r) * (1f - t / 0.05f) : 0f;

            float a = Mathf.Lerp(0.85f, 0.03f, 1f - Mathf.Exp(-t * 2.8f));
            nz += a * (Noise(r) - nz);
            float blast = nz * Mathf.Exp(-t * 1.3f) * 1.2f;

            ra += 0.02f * (Noise(r) - ra);
            rb += 0.04f * (ra - rb);
            float tail = rb * Mathf.Exp(-t * 0.7f) * 9f;

            float s = thump + crack * 0.9f + blast + tail;
            b[i] = (float)Math.Tanh(s * 1.4f);
        }

        int tailFade = SampleRate / 5;
        for (int i = 0; i < tailFade; i++) b[n - 1 - i] *= i / (float)tailFade;

        Normalize(b, 0.95f);
        var clip = AudioClip.Create("Explosion", n, 1, SampleRate, false);
        clip.SetData(b, 0);
        return clip;
    }

    static AudioClip BuildClick()
    {
        var r = new System.Random(99);
        int n = (int)(0.14f * SampleRate);
        var b = new float[n];
        float lp = 0f, phase = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)SampleRate;
            float x = Noise(r);
            lp += 0.25f * (x - lp);
            float hp = x - lp;
            float burst = hp * Mathf.Exp(-t * 110f) * 0.9f;

            float f = 520f + 1500f * Mathf.Exp(-t * 28f);
            phase += 2f * Mathf.PI * f / SampleRate;
            float blip = Mathf.Sin(phase) * Mathf.Exp(-t * 48f) * 0.8f;

            float knock = Mathf.Sin(2f * Mathf.PI * 170f * t) * Mathf.Exp(-t * 70f) * 0.5f;

            b[i] = (float)Math.Tanh((burst + blip + knock) * 1.5f);
        }
        Normalize(b, 0.9f);
        var clip = AudioClip.Create("UI Click", n, 1, SampleRate, false);
        clip.SetData(b, 0);
        return clip;
    }

    static AudioClip BuildHover()
    {
        int n = (int)(0.06f * SampleRate);
        var b = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)SampleRate;
            b[i] = Mathf.Sin(2f * Mathf.PI * 2300f * t) * Mathf.Exp(-t * 130f)
                 + 0.4f * Mathf.Sin(2f * Mathf.PI * 3450f * t) * Mathf.Exp(-t * 170f);
        }
        Normalize(b, 0.6f);
        var clip = AudioClip.Create("UI Hover", n, 1, SampleRate, false);
        clip.SetData(b, 0);
        return clip;
    }

    static float Midi(int note) { return 440f * Mathf.Pow(2f, (note - 69) / 12f); }

    static float[] SynthMusic(int sr)
    {
        const float bpm = 72f;
        float beat = 60f / bpm;
        float bar = beat * 4f;
        const int bars = 8;
        int L = (int)(bars * bar * sr);

        int[][] chords =
        {
            new[] { 57, 60, 64, 69 },
            new[] { 53, 57, 60, 65 },
            new[] { 60, 64, 67, 72 },
            new[] { 55, 59, 62, 67 },
            new[] { 57, 60, 64, 69 },
            new[] { 53, 57, 60, 65 },
            new[] { 55, 59, 62, 67 },
            new[] { 52, 59, 64, 71 },
        };
        int[] roots = { 45, 41, 48, 43, 45, 41, 43, 40 };

        var pad = new float[L];
        var bass = new float[L];
        var lead = new float[L];

        for (int c = 0; c < bars; c++)
        {
            float start = c * bar;
            float len = bar + 1.6f;
            int s0 = (int)(start * sr);
            int count = (int)(len * sr);
            for (int k = 0; k < chords[c].Length; k++)
            {
                float f = Midi(chords[c][k]);
                for (int v = 0; v < 2; v++)
                {
                    double ff = f * (v == 0 ? 0.9985 : 1.0015);
                    for (int i = 0; i < count; i++)
                    {
                        float tt = i / (float)sr;
                        float env = Mathf.SmoothStep(0f, 1f, tt / 1.1f);
                        if (tt > bar) env *= 1f - Mathf.SmoothStep(0f, 1f, (tt - bar) / 1.6f);
                        double ph = ff * (s0 + i) / sr;
                        ph -= Math.Floor(ph);
                        float saw = (float)(ph * 2.0 - 1.0);
                        pad[(s0 + i) % L] += saw * env * 0.035f;
                    }
                }
            }
        }

        {
            float y = 0f;
            for (int pass = 0; pass < 2; pass++)
                for (int i = 0; i < L; i++)
                {
                    float t = i / (float)sr;
                    float fc = 1000f + 450f * Mathf.Sin(t * 2f * Mathf.PI / (bar * 4f));
                    float a = 1f - Mathf.Exp(-2f * Mathf.PI * fc / sr);
                    y += a * (pad[i] - y);
                    pad[i] = y;
                }
        }

        for (int c = 0; c < bars; c++)
        {
            int s0 = (int)(c * bar * sr);
            int count = (int)((bar + 0.6f) * sr);
            float f = Midi(roots[c]);
            for (int i = 0; i < count; i++)
            {
                float tt = i / (float)sr;
                float env = Mathf.SmoothStep(0f, 1f, tt / 0.25f);
                if (tt > bar) env *= 1f - Mathf.SmoothStep(0f, 1f, (tt - bar) / 0.6f);
                float s = Mathf.Sin(2f * Mathf.PI * f * tt) + 0.25f * Mathf.Sin(4f * Mathf.PI * f * tt);
                bass[(s0 + i) % L] += s * env * 0.16f;
            }
        }

        int[] pattern = { 0, 2, 1, 3, 2, 1, 3, 2 };
        for (int c = 0; c < bars; c++)
        {
            bool busy = c >= 2;
            float step = busy ? beat * 0.5f : beat;
            int steps = busy ? 8 : 4;
            for (int s = 0; s < steps; s++)
            {
                int note = chords[c][pattern[s] % 4] + 12;
                Pluck(lead, sr, L, c * bar + s * step, Midi(note), 0.055f, 4.2f);
            }
        }

        (int bar, int beat, int note)[] melody =
        {
            (0, 0, 76), (1, 0, 77), (1, 2, 76), (2, 0, 79), (2, 2, 76),
            (3, 0, 74), (3, 2, 79), (4, 0, 76), (4, 2, 72), (5, 0, 77),
            (5, 2, 81), (6, 0, 79), (6, 2, 83), (7, 0, 83), (7, 2, 79),
        };
        foreach (var m in melody)
            Bell(lead, sr, L, m.bar * bar + m.beat * beat, Midi(m.note), 0.075f);

        var echoed = new float[L];
        Array.Copy(lead, echoed, L);
        int d = (int)(beat * 0.75f * sr);
        for (int pass = 0; pass < 3; pass++)
            for (int i = 0; i < L; i++)
            {
                int j = i - d; if (j < 0) j += L;
                echoed[i] = lead[i] + echoed[j] * 0.45f;
            }

        var dry = new float[L];
        for (int i = 0; i < L; i++) dry[i] = pad[i] + echoed[i] * 0.8f;
        var wet = new float[L];
        float[] combTimes = { 0.0297f, 0.0371f, 0.0411f, 0.0437f };
        foreach (float ct in combTimes)
        {
            int cd = (int)(ct * sr);
            var buf = new float[L];
            Array.Copy(dry, buf, L);
            for (int pass = 0; pass < 2; pass++)
                for (int i = 0; i < L; i++)
                {
                    int j = i - cd; if (j < 0) j += L;
                    buf[i] = dry[i] + buf[j] * 0.72f;
                }
            for (int i = 0; i < L; i++) wet[i] += buf[i] * 0.07f;
        }

        var o = new float[L];
        for (int i = 0; i < L; i++)
            o[i] = pad[i] + bass[i] + echoed[i] + wet[i];

        Normalize(o, 0.85f);
        return o;
    }

    static void Pluck(float[] buf, int sr, int L, float t0, float f, float amp, float decay)
    {
        int s0 = (int)(t0 * sr);
        int count = (int)(1.6f * sr);
        for (int i = 0; i < count; i++)
        {
            float tt = i / (float)sr;
            float env = Mathf.Min(1f, tt / 0.006f) * Mathf.Exp(-tt * decay);
            float w = Mathf.Sin(2f * Mathf.PI * f * tt)
                    + 0.4f * Mathf.Sin(4f * Mathf.PI * f * tt) * Mathf.Exp(-tt * 3f)
                    + 0.2f * Mathf.Sin(6f * Mathf.PI * f * tt) * Mathf.Exp(-tt * 5f);
            buf[(s0 + i) % L] += w * env * amp;
        }
    }

    static void Bell(float[] buf, int sr, int L, float t0, float f, float amp)
    {
        int s0 = (int)(t0 * sr);
        int count = (int)(3.5f * sr);
        for (int i = 0; i < count; i++)
        {
            float tt = i / (float)sr;
            float env = Mathf.Min(1f, tt / 0.02f) * Mathf.Exp(-tt * 1.1f);
            float w = Mathf.Sin(2f * Mathf.PI * f * tt)
                    + 0.35f * Mathf.Sin(2f * Mathf.PI * f * 2.01f * tt) * Mathf.Exp(-tt * 2.5f)
                    + 0.15f * Mathf.Sin(2f * Mathf.PI * f * 3.97f * tt) * Mathf.Exp(-tt * 4f);
            buf[(s0 + i) % L] += w * env * amp;
        }
    }
}
