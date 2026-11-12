using UnityEngine;

public class ReentryBody : MonoBehaviour
{
    const double EarthRadius = 6.371e6;
    const double Sigma = 5.670374e-8;

    [Header("Entry conditions")]
    public float startAltitude = 120000f;
    public float entrySpeed = 7600f;
    public float entryAngleDeg = 6f;

    [Header("Object properties (tune per object)")]
    public float mass = 60f;
    public float dragArea = 0.8f;
    public float dragCoefficient = 1.2f;
    public float noseRadius = 0.4f;
    public float arealHeatCapacity = 3000f;
    public float emissivity = 0.8f;
    public float failTemperature = 1900f;
    [Tooltip("Seconds the object takes to shrink away once it burns up. The breakup script shortens this.")]
    public float burnDuration = 4f;

    [Header("Presentation")]
    public float timeScale = 5f;
    public float tumbleSpeed = 25f;
    public float plasmaReferenceFlux = 1e6f;
    public bool showDebugHud = true;

    public float AltitudeMeters => (float)altitude;
    public float SpeedMetersPerSecond => (float)System.Math.Sqrt(vx * vx + vy * vy);
    public float TemperatureKelvin => (float)temperature;
    public float Heat { get; private set; }
    public float Plasma { get; private set; }
    public float Density { get; private set; }
    public bool BurnedUp => burnedUp;

    double altitude, vx, vy, temperature = 280.0, flux;
    bool burnedUp, landed;
    float burnTimer;
    Vector3 baseScale, tumbleAxis;
    Renderer[] renderers;
    MaterialPropertyBlock block;

    static readonly int EmissiveId = Shader.PropertyToID("_EmissiveColor");
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    Color[] baseColors;

    [Header("HDRP look")]
    [Tooltip("Peak emissive brightness in nits. HDRP uses physical units; tune against your exposure.")]
    public float emissionNits = 30000f;

    static Color HeatColor(float t)
    {
        Color c0 = new Color(0.35f, 0.02f, 0.00f);
        Color c1 = new Color(1.00f, 0.28f, 0.02f);
        Color c2 = new Color(1.00f, 0.78f, 0.40f);
        Color c3 = new Color(0.75f, 0.85f, 1.20f);
        Color c = Color.Lerp(c0, c1, Mathf.Clamp01(t * 3f));
        c = Color.Lerp(c, c2, Mathf.Clamp01(t * 3f - 1f));
        c = Color.Lerp(c, c3, Mathf.Clamp01(t * 3f - 2f));
        return c;
    }

    void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>();
        block = new MaterialPropertyBlock();
        baseColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            var m = renderers[i].sharedMaterial;
            baseColors[i] = (m != null && m.HasProperty(BaseColorId)) ? m.GetColor(BaseColorId) : Color.white;
        }
        baseScale = transform.localScale;
        tumbleAxis = Random.onUnitSphere;

        altitude = startAltitude;
        double ang = entryAngleDeg * Mathf.Deg2Rad;
        vx = entrySpeed * System.Math.Cos(ang);
        vy = -entrySpeed * System.Math.Sin(ang);
    }

    void Update()
    {
        if (!landed)
        {
            float dt = Time.deltaTime * timeScale;
            int steps = Mathf.Max(1, Mathf.CeilToInt(dt / 0.05f));
            double h = dt / steps;
            for (int i = 0; i < steps && !landed; i++) Step(h);
        }
        Apply();
    }

    void Step(double h)
    {
        double speed = System.Math.Sqrt(vx * vx + vy * vy);
        double rho = 1.225 * System.Math.Exp(-altitude / 7200.0);
        double r = EarthRadius + altitude;
        double g = 9.81 * (EarthRadius / r) * (EarthRadius / r);

        double dragAccel = 0.5 * rho * speed * speed * dragCoefficient * dragArea / mass;
        double ax = -dragAccel * vx / speed;
        double ay = -dragAccel * vy / speed - g + vx * vx / r;

        vx += ax * h;
        vy += ay * h;
        altitude += vy * h;

        flux = 1.7415e-4 * System.Math.Sqrt(rho / noseRadius) * speed * speed * speed;
        double radiated = emissivity * Sigma * System.Math.Pow(temperature, 4);
        temperature += (flux - radiated) / arealHeatCapacity * h;
        if (temperature < 3.0) temperature = 3.0;

        Density = (float)rho;

        if (!burnedUp && temperature > failTemperature)
        {
            burnedUp = true;
            Debug.Log($"[Reentry] {name} burned up at {altitude / 1000.0:F1} km, {speed:F0} m/s");
        }
        if (altitude <= 0.0)
        {
            landed = true;
            Debug.Log($"[Reentry] {name} reached the ground (burnedUp={burnedUp})");
        }
    }

    void Apply()
    {
        Heat = Mathf.InverseLerp(500f, 2200f, (float)temperature);
        float targetPlasma = Mathf.Clamp01((float)(flux / plasmaReferenceFlux));
        Plasma = Mathf.Lerp(Plasma, targetPlasma, 5f * Time.deltaTime);

        if (burnedUp)
        {

            burnTimer += Time.deltaTime;
            Heat = 1f;
            float s = Mathf.Clamp01(1f - burnTimer / Mathf.Max(0.01f, burnDuration));
            transform.localScale = baseScale * s;
            if (s <= 0f) foreach (var rend in renderers) rend.enabled = false;
        }

        transform.Rotate(tumbleAxis, tumbleSpeed * Time.deltaTime, Space.World);

        float t = Mathf.Clamp01(Heat + 0.3f * Plasma);
        float incand = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.05f, 1f, t));
        Color e = HeatColor(t) * (incand * emissionNits);
        float char01 = Mathf.Clamp01(Heat * 1.5f);

        for (int i = 0; i < renderers.Length; i++)
        {
            Color b = baseColors[i];
            Color charred = new Color(b.r * 0.25f, b.g * 0.25f, b.b * 0.25f, b.a);
            block.SetVector(EmissiveId, new Vector4(e.r, e.g, e.b, 1f));
            block.SetColor(BaseColorId, Color.Lerp(b, charred, char01));
            renderers[i].SetPropertyBlock(block);
        }
    }

    void OnGUI()
    {
        if (!showDebugHud) return;
        GUI.Label(new Rect(12, 12, 460, 110),
            $"Altitude  {AltitudeMeters / 1000f:F1} km\n" +
            $"Speed     {SpeedMetersPerSecond:F0} m/s\n" +
            $"Skin temp {TemperatureKelvin:F0} K  (fails at {failTemperature:F0})\n" +
            $"Flux      {flux / 1e6:F2} MW/m2   Heat {Heat:F2}  Plasma {Plasma:F2}");
    }
}
