using UnityEngine;

[DefaultExecutionOrder(100)]
public class ReentryCameraFX : MonoBehaviour
{
    [Header("Hook these up in the Inspector")]
    public ReentryBody body;
    public Light fireLight;

    [Header("Camera shake")]
    [Tooltip("Metres of wobble at full heat, measured when the camera is 10 m away. It scales up with distance so the shake always looks the same size on screen.")]
    public float shakeAmount = 0.06f;
    public float shakeSpeed = 25f;

    [Header("Firelight")]
    [Tooltip("Brightness at full heat. HDRP uses real-world units, so tune this by eye.")]
    public float maxIntensity = 200000f;
    public float flickerAmount = 0.25f;

    Vector3 basePos;
    ReentryOrbitCamera orbit;
    float startScale = 1f;

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

    void Start()
    {
        basePos = transform.localPosition;
        orbit = GetComponent<ReentryOrbitCamera>();
        if (body == null) body = FindAnyObjectByType<ReentryBody>();
        if (body != null) startScale = Mathf.Max(0.0001f, body.transform.localScale.x);
        if (fireLight != null)
        {
            fireLight.shadows = LightShadows.None;
        }
    }

    void LateUpdate()
    {
        if (body == null) return;

        float heat = Mathf.Clamp01(body.Heat);
        float plasma = Mathf.Clamp01(body.Plasma);

        float presence = Mathf.Clamp01(body.transform.localScale.x / startScale);
        float intensity01 = Mathf.Clamp01(Mathf.Max(heat * 0.6f, plasma)) * presence;

        float t = Time.time * shakeSpeed;
        Vector3 shake = new Vector3(
            Mathf.PerlinNoise(t, 0.0f) - 0.5f,
            Mathf.PerlinNoise(0.0f, t) - 0.5f,
            Mathf.PerlinNoise(t, t) - 0.5f) * 2f;
        float dist = Vector3.Distance(transform.position, body.transform.position);
        float distScale = Mathf.Max(0.5f, dist / 10f);
        Vector3 offset = shake * (shakeAmount * ReentrySettings.Shake * intensity01 * distScale);
        if (orbit != null && orbit.enabled)
            transform.position += offset;
        else
            transform.localPosition = basePos + offset;

        if (fireLight != null)
        {
            float flicker = 1f + (Mathf.PerlinNoise(Time.time * 18f, 3.7f) - 0.5f) * 2f * flickerAmount;
            float level = intensity01 * flicker * maxIntensity;
            fireLight.color = HeatColor(intensity01);
            fireLight.intensity = level;
        }
    }
}
