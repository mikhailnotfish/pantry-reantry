using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

public class ReentryPlanetDetail : MonoBehaviour
{
    [Tooltip("Turn the extra terrain detail on or off.")]
    public bool detailEnabled = true;
    [Range(0f, 3f), Tooltip("How strong the colour patches on the land are.")]
    public float colourVariation = 1f;
    [Range(0f, 3f), Tooltip("How strong the bumps and shadows on mountains are. 0 = flat.")]
    public float relief = 1.2f;
    [Tooltip("Tick this if the fine detail slides the opposite way to the continents.")]
    public bool reverseDetail = false;
    [Tooltip("Size of the biggest features in metres. Smaller = busier from high up.")]
    public float biggestFeature = 60000f;

    ReentrySky sky;
    ReentryBody body;
    Material mat;
    CustomPassVolume volume;
    float groundMetres;
    bool failed;

    void Start()
    {
        sky = GetComponent<ReentrySky>();
        body = sky != null ? sky.body : FindAnyObjectByType<ReentryBody>();
        if (sky == null) { Debug.LogWarning("[ReentryPlanetDetail] Needs to sit on the same object as ReentrySky."); enabled = false; return; }

        Shader sh = ReentryShaders.Find("Hidden/Reentry/PlanetDetail");
        if (sh == null)
        {
            Debug.LogWarning("[ReentryPlanetDetail] Couldn't find the shader. Add ReentryPlanetDetail.shader to your project (next to your other scripts). The planet will just look softer without it.");
            enabled = false;
            return;
        }
        mat = new Material(sh);

        try
        {
            var go = new GameObject("Reentry Planet Detail Pass");
            go.transform.SetParent(transform, false);
            volume = go.AddComponent<CustomPassVolume>();
            volume.isGlobal = true;
            volume.injectionPoint = CustomPassInjectionPoint.BeforePostProcess;
            var pass = volume.AddPassOfType(typeof(FullScreenCustomPass)) as FullScreenCustomPass;
            pass.name = "Planet Detail";
            pass.fullscreenPassMaterial = mat;
            SetMember(pass, "materialPassName", "Custom Pass 0");
            SetMember(pass, "materialPassIndex", 0);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[ReentryPlanetDetail] Couldn't set up the terrain pass: " + e.Message);
            enabled = false;
        }
    }

    static void SetMember(object o, string name, object value)
    {
        try
        {
            const BindingFlags f = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            FieldInfo fi = o.GetType().GetField(name, f);
            if (fi != null) { fi.SetValue(o, value); return; }
            PropertyInfo pi = o.GetType().GetProperty(name, f);
            if (pi != null && pi.CanWrite) pi.SetValue(o, value);
        }
        catch (Exception) { }
    }

    void LateUpdate()
    {
        if (mat == null || sky == null || volume == null) return;
        volume.enabled = detailEnabled && ReentrySettings.TerrainDetail && sky.detailedPlanet;

        Camera cam = Camera.main;
        if (cam == null) return;

        float radius = sky.planetRadius;
        float altitude = body != null ? Mathf.Max(10f, body.AltitudeMeters) : 100000f;
        Vector3 anchor = body != null ? body.transform.position : Vector3.zero;
        Vector3 centre = anchor + Vector3.down * (radius + altitude);

        Vector3 camPos = cam.transform.position;

        double dx = camPos.x - centre.x, dy = camPos.y - centre.y, dz = camPos.z - centre.z;
        double dist = Math.Sqrt(dx * dx + dy * dy + dz * dz);
        float height = Mathf.Max(1f, (float)(dist - radius));

        if (body != null && sky.scrollPlanet)
            groundMetres += body.SpeedMetersPerSecond * body.timeScale * Time.deltaTime * sky.scrollMultiplier;
        float a = body != null ? body.entryAngleDeg * Mathf.Deg2Rad : 0f;
        float sign = reverseDetail ? -1f : 1f;
        Vector2 offset = new Vector2(Mathf.Cos(a) * groundMetres * sign, 0f);

        Vector3 toSun = sky.sun != null ? -sky.sun.transform.forward : Vector3.up;

        mat.SetVector("_PlanetCenterRel", centre - camPos);
        mat.SetFloat("_PlanetRadius", radius);
        mat.SetFloat("_CamHeight", height);
        mat.SetVector("_CamWorld", camPos - anchor);
        mat.SetVector("_GroundOffset", offset);
        mat.SetVector("_SunDir", toSun);
        mat.SetFloat("_Strength", colourVariation);
        mat.SetFloat("_Relief", relief);
        mat.SetFloat("_BaseWave", Mathf.Max(1000f, biggestFeature));
    }

    void OnDestroy()
    {
        if (mat != null) Destroy(mat);
    }
}
