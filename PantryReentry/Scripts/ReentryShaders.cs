using UnityEngine;

public static class ReentryShaders
{
    static ReentryShaderList list;
    static bool tried;

    public static Shader Find(string name)
    {
        if (!tried)
        {
            tried = true;
            list = Resources.Load<ReentryShaderList>("ReentryShaderList");
        }
        if (list != null && list.shaders != null)
        {
            foreach (Shader s in list.shaders)
                if (s != null && s.name == name) return s;
        }
        return Shader.Find(name);
    }
}
