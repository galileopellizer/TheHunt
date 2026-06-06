using UnityEngine;

public static class TransformExtensions
{
    /// <summary>Recursively searches all children for a transform with the given name.</summary>
    public static Transform FindDeepChild(this Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name) return child;
            var result = child.FindDeepChild(name);
            if (result != null) return result;
        }
        return null;
    }
}
