using UnityEngine;

public static class ReentryText
{
    public const string TitleTop = "PANTRY";
    public const string TitleBottom = "REENTRY";
    public const string Tagline = "throw things into the atmosphere!";

    public const string SelectHeading = "select an item";
    public const string SelectSub = "";

    public static readonly string[] Blurbs =
    {
        "about to turn into an oven",
        "you dont want to be sitting on this one",
        "baked beans!",
        "the views from this one are amazing!",
        "beethoven would be proud",
        "about to be a drying machine!",
        "the air drives where it wants",
        "better than the toilet!",
    };

    public const string BurnedHeadline = "BURNED UP";
    public const string SurvivedHeadline = "SURVIVED";

    public const string Burn85 = "the {0} didnt make it.";
    public const string Burn70 = "the {0} didnt make it.";
    public const string Burn50 = "the {0} didnt make it.";
    public const string Burn30 = "the {0} didnt make it.";
    public const string BurnLow = "the {0} didnt make it.";
    public const string LandFast = "the {0} survived, but hit the ground at {2} m/s.";
    public const string LandSoft = "the {0} made it all the way down.";
}

public static class ReentryFont
{
    static Font font;

    public static Font Get()
    {
        if (font == null)
            font = Font.CreateDynamicFontFromOSFont(new[] { "Arial", "Helvetica", "Liberation Sans", "DejaVu Sans" }, 32);
        return font;
    }

    public static void SetColor(GUIStyle s, Color c)
    {
        s.normal.textColor = c;
        s.hover.textColor = c;
        s.active.textColor = c;
        s.focused.textColor = c;
        s.onNormal.textColor = c;
        s.onHover.textColor = c;
        s.onActive.textColor = c;
        s.onFocused.textColor = c;
    }

    public static GUIStyle Apply(GUIStyle s)
    {
        s.font = Get();
        s.fontStyle = FontStyle.Normal;
        return s;
    }
}
