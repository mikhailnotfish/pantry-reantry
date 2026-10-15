using UnityEngine;

[RequireComponent(typeof(ReentryBody))]
public class ReentryTrail : MonoBehaviour
{
    [Header("Trail")]
    public float particlesPerSecond = 600f;
    public float noiseStrength = 1.2f;
    public float trailSpeed = 14f;
    public float lifetime = 2.5f;
    public float startSize = 1.0f;
    public float endSize = 3.5f;
    public float brightness = 2.5f;

    [Header("Optional")]
    [Tooltip("Leave empty to use the built-in Reentry/AdditiveParticle shader.")]
    public Material materialOverride;

    ReentryBody body;
    ParticleSystem ps;

    void Start()
    {
        body = GetComponent<ReentryBody>();

        var go = new GameObject("Reentry Trail");
        go.transform.SetParent(transform, false);
        ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = true;
        main.playOnAwake = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = lifetime;
        main.startSpeed = trailSpeed;
        main.startSize = 1f;
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = Color.white;
        main.gravityModifier = 0f;
        main.maxParticles = 6000;

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 6f;
        shape.radius = 0.25f;

        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = noiseStrength;
        noise.frequency = 0.35f;
        noise.scrollSpeed = 0.6f;
        noise.damping = true;
        noise.quality = ParticleSystemNoiseQuality.Medium;

        var size = ps.sizeOverLifetime;
        size.enabled = true;

        size.size = new ParticleSystem.MinMaxCurve(endSize,
            AnimationCurve.EaseInOut(0f, startSize / Mathf.Max(0.01f, endSize), 1f, 1f));

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(
            new[] {
                new GradientColorKey(new Color(1.0f, 0.92f, 0.75f), 0.0f),
                new GradientColorKey(new Color(1.0f, 0.40f, 0.06f), 0.35f),
                new GradientColorKey(new Color(0.45f, 0.05f, 0.00f), 1.0f)
            },
            new[] {
                new GradientAlphaKey(0.0f, 0.0f),
                new GradientAlphaKey(1.0f, 0.08f),
                new GradientAlphaKey(0.6f, 0.5f),
                new GradientAlphaKey(0.0f, 1.0f)
            });
        col.color = g;

        var rend = go.GetComponent<ParticleSystemRenderer>();
        rend.renderMode = ParticleSystemRenderMode.Billboard;
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rend.receiveShadows = false;

        Material mat = materialOverride;
        if (mat == null)
        {
            Shader sh = ReentryShaders.Find("Reentry/AdditiveParticle");
            if (sh == null)
            {
                Debug.LogError("[ReentryTrail] Couldn't find shader 'Reentry/AdditiveParticle'. Is ReentryParticle.shader in your Assets folder?");
                return;
            }
            mat = new Material(sh);
        }
        mat.SetFloat("_Intensity", brightness);
        rend.material = mat;

        ps.Play();
    }

    void Update()
    {
        if (ps == null || body == null) return;

        float a = body.entryAngleDeg * Mathf.Deg2Rad;
        Vector3 flow = new Vector3(Mathf.Cos(a), -Mathf.Sin(a), 0f);
        ps.transform.rotation = Quaternion.LookRotation(-flow, Vector3.up);

        float p = Mathf.Clamp01(body.Plasma * 1.5f);
        var em = ps.emission;
        em.rateOverTime = p > 0.01f ? particlesPerSecond : 0f;

        var main = ps.main;
        main.startColor = new Color(1f, 1f, 1f, Mathf.Lerp(0.12f, 1f, p));
    }
}
