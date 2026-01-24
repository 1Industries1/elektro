using UnityEngine;

[ExecuteAlways]
public class TerrainFollower : MonoBehaviour
{
    public Terrain terrain;
    public float heightOffset = 0f;
    public bool alignToNormal = true;

    void Update()
    {
        if (!terrain) return;

        Vector3 pos = transform.position;

        float terrainHeight = terrain.SampleHeight(pos)
                              + terrain.transform.position.y;

        transform.position = new Vector3(
            pos.x,
            terrainHeight + heightOffset,
            pos.z
        );

        if (alignToNormal)
        {
            Vector3 normal = terrain.terrainData.GetInterpolatedNormal(
                (pos.x - terrain.transform.position.x) / terrain.terrainData.size.x,
                (pos.z - terrain.transform.position.z) / terrain.terrainData.size.z
            );

            transform.up = normal;
        }
    }
}
