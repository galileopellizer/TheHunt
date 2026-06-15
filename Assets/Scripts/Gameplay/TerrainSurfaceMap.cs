using UnityEngine;

/// <summary>
/// Attach to a Terrain object.
/// Maps each terrain layer index to a surface type for footstep detection.
/// </summary>
public class TerrainSurfaceMap : MonoBehaviour
{
    [SerializeField] private TerrainLayerMapping[] mappings;

    public SurfaceType GetSurface(int layerIndex)
    {
        foreach (var m in mappings)
            if (m.terrainLayerIndex == layerIndex)
                return m.surface;
        return SurfaceType.Gravel; // fallback
    }
}
