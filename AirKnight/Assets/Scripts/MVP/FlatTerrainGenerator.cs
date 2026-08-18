using UnityEngine;

public sealed class FlatTerrainGenerator : MonoBehaviour
{
    // Covers the 4:3 far-clip width at the maximum battle-camera zoom, plus margin.
    [SerializeField] Vector2 size = new(28000f, 28000f);
    [SerializeField] float surfaceHeight;
    [SerializeField] Color terrainColor = new(0.22f, 0.38f, 0.18f, 1f);
    [SerializeField] bool generateOnStart = true;
    GameObject generatedTerrain;

    void Start()
    {
        if (generateOnStart && transform.Find("FlatTerrain") == null)
            Generate();
    }

    [ContextMenu("Generate Flat Terrain")]
    public void Generate()
    {
        Transform existing = transform.Find("FlatTerrain");
        if (existing != null)
        {
            if (Application.isPlaying) Destroy(existing.gameObject);
            else DestroyImmediate(existing.gameObject);
        }

        generatedTerrain = GameObject.CreatePrimitive(PrimitiveType.Cube);
        generatedTerrain.name = "FlatTerrain";
        generatedTerrain.transform.SetParent(transform, false);
        generatedTerrain.transform.position = new Vector3(0f, surfaceHeight - 0.5f, 0f);
        generatedTerrain.transform.localScale = new Vector3(size.x, 1f, size.y);

        Renderer renderer = generatedTerrain.GetComponent<Renderer>();
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader != null)
        {
            Material material = new(shader) { name = "Flat Terrain Material" };
            material.color = terrainColor;
            renderer.material = material;
        }
    }
}
