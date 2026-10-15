#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public class ReentryShaderInclude : IPreprocessBuildWithReport
{
    public int callbackOrder { get { return 0; } }

    public void OnPreprocessBuild(BuildReport report)
    {
        AddAll();
    }

    [MenuItem("Tools/Pantry Reentry/Add shaders to build")]
    public static void AddAll()
    {
        var found = new List<Shader>();
        foreach (string guid in AssetDatabase.FindAssets("t:Shader"))
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(AssetDatabase.GUIDToAssetPath(guid));
            if (shader == null) continue;
            if (shader.name.StartsWith("Reentry/") || shader.name.StartsWith("Hidden/Reentry/")) found.Add(shader);
        }
        var lit = Shader.Find("HDRP/Lit");
        if (lit != null) found.Add(lit);

        if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
        const string path = "Assets/Resources/ReentryShaderList.asset";
        var list = AssetDatabase.LoadAssetAtPath<ReentryShaderList>(path);
        if (list == null)
        {
            list = ScriptableObject.CreateInstance<ReentryShaderList>();
            AssetDatabase.CreateAsset(list, path);
        }
        list.shaders = found.ToArray();
        EditorUtility.SetDirty(list);
        AssetDatabase.SaveAssets();
        Debug.Log("[Pantry Reentry] Shader list updated with " + found.Count + " shader(s): " + string.Join(", ", found.ConvertAll(s => s.name).ToArray()));
    }
}
#endif
