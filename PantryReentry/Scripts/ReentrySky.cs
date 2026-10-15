using System;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

public class ReentrySky : MonoBehaviour
{
    [Header("Hook up (both are found automatically if left empty)")]
    public ReentryBody body;
    public Light sun;

    [Header("Sun")]
    [Tooltip("Degrees above the horizon.")]
    public float sunElevation = 25f;
    [Tooltip("Compass direction the sunlight travels. The default camera looks along +Z, so 40 puts the sun behind and beside it. Around 180 puts the sun in front of the camera, which is blinding.")]
    public float sunAzimuth = 40f;
    [Tooltip("Brightness in lux. Real sunlight in space is about 120000.")]
    public float sunLux = 120000f;

    [Header("Exposure")]
    [Tooltip("Exposure value. Lower = brighter picture. 13.5 balances a sunlit planet against a glowing object. Try 15 if the planet is washed out, 12 if the glow is too faint.")]
    public float exposureEV = 13.5f;
    [Tooltip("Let the camera adapt to what it sees, within a narrow range around the value above. Off by default.")]
    public bool autoExposure = false;

    [Header("Planet")]
    public float planetRadius = 6371000f;
    public Color groundTint = new Color(0.10f, 0.22f, 0.38f);

    [Header("Planet surface (continents, clouds, city lights)")]
    [Tooltip("Paint the planet with generated continents, oceans, ice caps and clouds instead of a plain blue ball.")]
    public bool detailedPlanet = true;
    [Tooltip("Your own planet picture (a Cubemap) instead of the generated one. Leave empty to generate.")]
    public Cubemap planetTextureOverride;
    [Tooltip("Size of the generated planet picture. Fine detail close up now comes from the terrain layer (ReentryPlanetDetail), so 1024 is plenty and loads fast. 2048 uses about 4x the memory.")]
    public int baseResolution = 1024;
    [Tooltip("Change this number to get a different planet.")]
    public int seed = 7;
    [Range(0.1f, 0.6f), Tooltip("How much of the planet is land.")]
    public float landAmount = 0.32f;
    [Range(0f, 1f), Tooltip("Flat clouds painted onto the planet. ONLY used when Volumetric Clouds below is unticked; otherwise there are no painted clouds, so they can't clash with the 3D ones.")]
    public float paintedClouds = 0.6f;
    [Tooltip("Glowing cities. You only see them on the night side: set Sun Elevation to a negative number such as -20.")]
    public bool cityLights = true;
    [Tooltip("City light brightness. HDRP exposure is set for sunlight, so this needs to be big.")]
    public float cityLightBrightness = 4000f;

    [Header("Volumetric clouds (3D clouds)")]
    [Tooltip("Real 3D clouds you can see the depth of. These are heavy for the graphics card: untick if the game runs badly.")]
    public bool volumetricClouds = true;
    [Tooltip("Cloud preset: Sparse, Cloudy, Overcast or Stormy.")]
    public string cloudPreset = "Cloudy";
    [Range(0f, 1f)]
    public float cloudDensity = 0.55f;
    [Tooltip("Height of the bottom of the clouds above the ground, in metres.")]
    public float cloudBottom = 1500f;
    [Tooltip("How thick the cloud layer is, in metres.")]
    public float cloudThickness = 3000f;
    [Tooltip("Quality of the clouds. More steps look better but cost more. 32 is the minimum, 64 is good.")]
    public int cloudSteps = 48;
    [Tooltip("Make the clouds cast shadows on the ground. Heavy.")]
    public bool cloudShadows = false;

    [Header("Where you fall")]
    [Tooltip("Latitude of the spot directly below the object when it starts (-90 south pole, 90 north pole).")]
    public float startLatitude = 15f;
    [Tooltip("Longitude of the spot directly below the object at the start.")]
    public float startLongitude = 30f;
    [Tooltip("Which way the continents are lined up relative to your flight path, in degrees.")]
    public float startHeading = 0f;
    [Tooltip("Make the ground slide past underneath, so you can see how fast you're going.")]
    public bool scrollPlanet = true;
    [Tooltip("1 = the real speed (times the simulation speed). Higher exaggerates it.")]
    public float scrollMultiplier = 1f;
    [Tooltip("Tick this if the ground slides the wrong way (it should flow backwards past you).")]
    public bool reverseScroll = false;
    [Tooltip("Tick this if the planet turns the wrong way when the ground slides, or the start spot is not where you set it.")]
    public bool invertRotation = false;

    Volume volume;
    VolumeProfile profile;
    VisualEnvironment env;
    PhysicallyBasedSky sky;

    Task<ReentryPlanetGen.Result> quickTask, fullTask;
    int fullSize;
    int[] variants;
    Cubemap currentColor, currentEmission;
    static ReentryPlanetGen.Result cachedResult;
    static string cachedKey;
    static int[] cachedVariants;
    float scrollDegrees;
    bool textureApplied;

    void Awake()
    {
        if (body == null) body = FindAnyObjectByType<ReentryBody>();

        if (sun == null)
        {
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (l.type == LightType.Directional) { sun = l; break; }
            }
        }
        if (sun == null)
        {
            var sg = new GameObject("Sun");
            sun = sg.AddComponent<Light>();
            sun.type = LightType.Directional;
        }
        sun.transform.rotation = Quaternion.Euler(sunElevation, sunAzimuth, 0f);
        sun.intensity = sunLux;
        sun.color = Color.white;

        var go = new GameObject("Reentry Sky Volume");
        go.transform.SetParent(transform, false);
        volume = go.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 100f;
        profile = ScriptableObject.CreateInstance<VolumeProfile>();
        volume.sharedProfile = profile;

        env = profile.Add<VisualEnvironment>(true);
        env.skyType.value = (int)SkyType.PhysicallyBased;
        env.skyAmbientMode.value = SkyAmbientMode.Dynamic;

        env.centerMode.value = VisualEnvironment.PlanetMode.Manual;
        env.renderingSpace.value = RenderingSpace.World;
        env.planetRadius.value = planetRadius / 1000f;

        sky = profile.Add<PhysicallyBasedSky>(true);
        sky.type.value = PhysicallyBasedSkyModel.EarthAdvanced;
        sky.groundTint.value = groundTint;
        UpdatePlanetPosition();

        try { SetupPlanetSurface(); }
        catch (Exception e) { Debug.LogWarning("[ReentrySky] Planet surface setup failed, using a plain planet: " + e); }
        try { SetupVolumetricClouds(); }
        catch (Exception e) { Debug.LogWarning("[ReentrySky] Cloud setup failed: " + e.Message); }
        if (detailedPlanet && GetComponent<ReentryPlanetDetail>() == null)
            gameObject.AddComponent<ReentryPlanetDetail>();

        var exposure = profile.Add<Exposure>(true);
        if (autoExposure)
        {
            exposure.mode.value = ExposureMode.Automatic;
            exposure.limitMin.value = exposureEV - 1.5f;
            exposure.limitMax.value = exposureEV + 1.5f;
        }
        else
        {
            exposure.mode.value = ExposureMode.Fixed;
            exposure.fixedExposure.value = exposureEV;
        }

        var fog = profile.Add<Fog>(true);
        fog.enabled.value = false;
    }

    void LateUpdate()
    {
        if (env == null) return;
        UpdatePlanetPosition();
        PollPlanetTexture();
        UpdatePlanetRotation();
    }

    void SetupPlanetSurface()
    {
        if (!detailedPlanet) return;

        if (planetTextureOverride != null)
        {
            ApplyGroundTexture(planetTextureOverride, null);
            return;
        }

        fullSize = Mathf.Clamp(Mathf.ClosestPowerOfTwo(baseResolution), 256, 4096);
        string key = fullSize + "|" + seed + "|" + landAmount.ToString("F3") + "|" + CloudBake().ToString("F3") + "|" + cityLights;
        if (cachedResult != null && cachedKey == key)
        {
            BuildAndApply(cachedResult);
            return;
        }
        cachedKey = key;

        if (cachedVariants == null)
        {
            try { cachedVariants = ReentryPlanetGen.Calibrate(); }
            catch (Exception e) { Debug.LogWarning("[ReentrySky] Cubemap check failed, using defaults: " + e.Message); cachedVariants = new int[6]; }
        }
        variants = cachedVariants;
        Debug.Log("[ReentrySky] Generating planet (" + fullSize + "px)...");

        int quick = Mathf.Min(512, fullSize);
        quickTask = ReentryPlanetGen.GenerateAsync(quick, seed, landAmount, CloudBake(), cityLights, variants, Threads());
    }

    float CloudBake() { return volumetricClouds ? 0f : paintedClouds; }

    static int Threads() { return Mathf.Max(1, SystemInfo.processorCount / 2); }

    void PollPlanetTexture()
    {
        if (quickTask != null && quickTask.IsCompleted)
        {
            if (quickTask.IsFaulted)
            {
                Debug.LogWarning("[ReentrySky] Couldn't generate the planet: " + quickTask.Exception);
                cachedKey = null;
            }
            else
            {
                var r = quickTask.Result;
                BuildAndApply(r);
                if (fullSize > r.size)
                    fullTask = ReentryPlanetGen.GenerateAsync(fullSize, seed, landAmount, CloudBake(), cityLights, variants, Threads());
                else
                    cachedResult = r;
            }
            quickTask = null;
        }

        if (fullTask != null && fullTask.IsCompleted)
        {
            if (fullTask.IsFaulted)
                Debug.LogWarning("[ReentrySky] Couldn't generate the sharp planet: " + fullTask.Exception);
            else
            {
                cachedResult = fullTask.Result;
                BuildAndApply(cachedResult);
            }
            fullTask = null;
        }
    }

    void BuildAndApply(ReentryPlanetGen.Result r)
    {
        try { BuildAndApplyInner(r); }
        catch (Exception e) { Debug.LogWarning("[ReentrySky] Couldn't build the planet picture (out of memory? try Base Resolution 512): " + e.Message); }
    }

    void BuildAndApplyInner(ReentryPlanetGen.Result r)
    {
        Cubemap color = ReentryPlanetGen.MakeCubemap(r.color, r.size, "Generated Planet");
        Cubemap emission = (r.emission != null) ? ReentryPlanetGen.MakeCubemap(r.emission, r.emissionSize, "Generated City Lights") : null;
        ApplyGroundTexture(color, emission);

        if (currentColor != null && currentColor != color) Destroy(currentColor);
        if (currentEmission != null && currentEmission != emission) Destroy(currentEmission);
        currentColor = color;
        currentEmission = emission;
    }

    void ApplyGroundTexture(Cubemap color, Cubemap emission)
    {
        if (sky == null) return;
        sky.groundColorTexture.overrideState = true;
        sky.groundColorTexture.value = color;
        sky.groundTint.overrideState = true;
        sky.groundTint.value = Color.white;
        if (emission != null)
        {
            sky.groundEmissionTexture.overrideState = true;
            sky.groundEmissionTexture.value = emission;
            sky.groundEmissionMultiplier.overrideState = true;
            sky.groundEmissionMultiplier.value = cityLightBrightness;
        }
        textureApplied = true;
    }

    void SetupVolumetricClouds()
    {
        if (!volumetricClouds) return;

        CheckCloudSupport();

        var clouds = profile.Add<VolumetricClouds>(true);
        clouds.enable.overrideState = true;
        clouds.enable.value = true;

        SetCloud(clouds, "cloudControl", "Simple");
        SetCloud(clouds, "cloudPreset", cloudPreset);
        SetCloud(clouds, "bottomAltitude", cloudBottom);
        SetCloud(clouds, "altitudeRange", cloudThickness);
        SetCloud(clouds, "densityMultiplier", cloudDensity);
        SetCloud(clouds, "numPrimarySteps", Mathf.Clamp(cloudSteps, 32, 256));
        SetCloud(clouds, "numLightSteps", 4);
        SetCloud(clouds, "shadows", cloudShadows);

        SetCloud(clouds, "fadeInMode", "Manual");
        SetCloud(clouds, "fadeInStart", 0f);
        SetCloud(clouds, "fadeInDistance", 1f);
    }

    static void CheckCloudSupport()
    {
        try
        {
            var asset = GraphicsSettings.currentRenderPipeline as HDRenderPipelineAsset;
            if (asset == null) return;
            object settings = asset.GetType().GetProperty("currentPlatformRenderPipelineSettings").GetValue(asset);
            FieldInfo f = settings.GetType().GetField("supportVolumetricClouds");
            if (f != null && !(bool)f.GetValue(settings))
            {
                Debug.LogWarning("[ReentrySky] Volumetric clouds are switched OFF in your HDRP asset, so no 3D clouds can show. " +
                                 "Fix: click your HDRP asset (Project window, search 'HDRP'), open Lighting, tick 'Volumetric Clouds'. " +
                                 "The painted clouds still work in the meantime.");
            }
            else if (f != null)
            {
                Debug.Log("[ReentrySky] Volumetric clouds are allowed by the HDRP asset.");
            }
        }
        catch (Exception e)
        {
            Debug.Log("[ReentrySky] Couldn't check the HDRP cloud setting: " + e.Message);
        }
    }

    static void SetCloud(object component, string name, object value)
    {
        try
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            Type t = component.GetType();

            FieldInfo field = t.GetField(name, flags) ?? t.GetField("m_" + char.ToUpper(name[0]) + name.Substring(1), flags);
            if (field != null && typeof(UnityEngine.Rendering.VolumeParameter).IsAssignableFrom(field.FieldType))
            {
                var param = (UnityEngine.Rendering.VolumeParameter)field.GetValue(component);
                param.overrideState = true;
                PropertyInfo valueProp = param.GetType().GetProperty("value");
                valueProp.SetValue(param, ConvertTo(valueProp.PropertyType, value));
                return;
            }

            PropertyInfo prop = t.GetProperty(name, flags);
            if (prop != null && prop.CanWrite)
            {
                prop.SetValue(component, ConvertTo(prop.PropertyType, value));
                return;
            }
            Debug.Log("[ReentrySky] This HDRP version has no cloud setting called '" + name + "'. Skipping it.");
        }
        catch (Exception e)
        {
            Debug.Log("[ReentrySky] Couldn't set cloud setting '" + name + "': " + e.Message);
        }
    }

    static object ConvertTo(Type type, object value)
    {
        if (type.IsEnum)
            return value is string ? Enum.Parse(type, (string)value, true) : Enum.ToObject(type, value);
        if (type == typeof(Color)) return value;
        return Convert.ChangeType(value, type);
    }

    void UpdatePlanetRotation()
    {
        if (sky == null || !textureApplied) return;

        if (scrollPlanet && body != null)
        {
            float radians = body.SpeedMetersPerSecond * body.timeScale * Time.deltaTime / planetRadius;
            scrollDegrees += radians * Mathf.Rad2Deg * scrollMultiplier * (reverseScroll ? -1f : 1f);
        }

        Quaternion start = Quaternion.AngleAxis(startHeading, Vector3.up) *
                           Quaternion.FromToRotation(ReentryPlanetGen.LatLon(startLatitude, startLongitude), Vector3.up);
        Quaternion q = Quaternion.Euler(0f, 0f, scrollDegrees) * start;
        if (invertRotation) q = Quaternion.Inverse(q);

        sky.planetRotation.overrideState = true;
        sky.planetRotation.value = q.eulerAngles;
    }

    void UpdatePlanetPosition()
    {
        float altitude = body != null ? Mathf.Max(10f, body.AltitudeMeters) : 100000f;
        Vector3 anchor = body != null ? body.transform.position : Vector3.zero;
        Vector3 centerMetres = anchor + Vector3.down * (planetRadius + altitude);
        env.planetCenter.value = centerMetres / 1000f;
    }

    void OnDestroy()
    {
        if (currentColor != null) Destroy(currentColor);
        if (currentEmission != null) Destroy(currentEmission);
        if (profile != null) Destroy(profile);
    }
}
