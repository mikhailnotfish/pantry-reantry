using UnityEngine;

public static class ReentrySettings
{
    public static float Master = 1f;
    public static float Music = 1f;
    public static float Sfx = 1f;
    public static float Shake = 1f;
    public static bool TerrainDetail = true;
    public static bool Fullscreen = true;
    public static bool VSync = true;
    public static int FpsCap = 0;

    public static readonly int[] FpsOptions = { 0, 30, 60, 120 };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init()
    {
        Fullscreen = Screen.fullScreen;
        VSync = QualitySettings.vSyncCount > 0;
        Load();
        ApplyDisplay();
    }

    static void Load()
    {
        Master = PlayerPrefs.GetFloat("pr_master", 1f);
        Music = PlayerPrefs.GetFloat("pr_music", 1f);
        Sfx = PlayerPrefs.GetFloat("pr_sfx", 1f);
        Shake = PlayerPrefs.GetFloat("pr_shake", 1f);
        TerrainDetail = PlayerPrefs.GetInt("pr_terrain", 1) == 1;
        if (PlayerPrefs.HasKey("pr_full")) Fullscreen = PlayerPrefs.GetInt("pr_full") == 1;
        if (PlayerPrefs.HasKey("pr_vsync")) VSync = PlayerPrefs.GetInt("pr_vsync") == 1;
        FpsCap = PlayerPrefs.GetInt("pr_fps", 0);
    }

    public static void Save()
    {
        PlayerPrefs.SetFloat("pr_master", Master);
        PlayerPrefs.SetFloat("pr_music", Music);
        PlayerPrefs.SetFloat("pr_sfx", Sfx);
        PlayerPrefs.SetFloat("pr_shake", Shake);
        PlayerPrefs.SetInt("pr_terrain", TerrainDetail ? 1 : 0);
        PlayerPrefs.SetInt("pr_full", Fullscreen ? 1 : 0);
        PlayerPrefs.SetInt("pr_vsync", VSync ? 1 : 0);
        PlayerPrefs.SetInt("pr_fps", FpsCap);
        PlayerPrefs.Save();
    }

    public static void ApplyDisplay()
    {
        if (Screen.fullScreen != Fullscreen) Screen.fullScreen = Fullscreen;
        QualitySettings.vSyncCount = VSync ? 1 : 0;
        Application.targetFrameRate = FpsCap > 0 ? FpsCap : -1;
    }
}
