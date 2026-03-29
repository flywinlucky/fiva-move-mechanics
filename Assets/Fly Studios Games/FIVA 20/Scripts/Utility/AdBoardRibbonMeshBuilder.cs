using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class AdBoardRibbonMeshBuilder : MonoBehaviour
{
    public enum TilingMode
    {
        UseMeshUv,
        AutoFromRendererBounds
    }

    [Header("Path Points (minimum 2)")]
    [SerializeField]
    List<Transform> pathPoints = new List<Transform>();

    [SerializeField]
    bool closeLoop = true;

    [Header("Board Shape")]
    [SerializeField]
    [Min(0.1f)]
    float boardHeight = 1.1f;

    [SerializeField]
    float verticalOffset = 0f;

    [SerializeField]
    bool faceOutward = true;

    [Header("UV Tiling (Mesh)")]
    [SerializeField]
    [Min(0.25f)]
    float tileWorldWidth = 4f;

    [SerializeField]
    [Min(0.1f)]
    float tileWorldHeight = 1.1f;

    [SerializeField]
    bool clampMinimumOneTile = true;

    [Header("Banner Material")]
    [SerializeField]
    [Min(0)]
    int materialIndex = 0;

    [SerializeField]
    TilingMode tilingMode = TilingMode.UseMeshUv;

    [SerializeField]
    bool autoTiling = true;

    [SerializeField]
    bool keepTextureAspect = true;

    [Header("Playlist")]
    [SerializeField]
    List<Texture> bannerTextures = new List<Texture>();

    [SerializeField]
    [Min(0.2f)]
    float textureSwitchInterval = 6f;

    [SerializeField]
    bool randomizeStartTexture = true;

    [Header("Scroll")]
    [SerializeField]
    Vector2 scrollDirection = Vector2.right;

    [SerializeField]
    [Range(0f, 3f)]
    float scrollSpeed = 0.2f;

    [Header("Build")]
    [SerializeField]
    bool rebuildContinuouslyInEditor = true;

    [SerializeField]
    bool rebuildAtRuntimeStart = true;

    [SerializeField]
    bool applyOnAwake = true;

    MeshFilter meshFilter;
    Mesh generatedMesh;
    MeshRenderer meshRenderer;
    Material runtimeMaterial;
    bool ownsRuntimeMaterialInstance;
    int activeTextureIndex = -1;
    float nextTextureSwitchTime;
    Vector2 scrollOffset;
    string texturePropertyName;

    static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
    static readonly int MainTexId = Shader.PropertyToID("_MainTex");

    void Awake()
    {
        EnsureComponents();

        InitializeRuntimeMaterial();

        if (applyOnAwake)
            ForceRefresh();

        if (Application.isPlaying && rebuildAtRuntimeStart)
            RebuildMesh();
    }

    void OnEnable()
    {
        EnsureComponents();

        nextTextureSwitchTime = Time.time + Mathf.Max(0.2f, textureSwitchInterval);

        if (!Application.isPlaying && rebuildContinuouslyInEditor)
            RebuildMesh();
    }

    void OnDisable()
    {
        if (runtimeMaterial != null)
        {
            runtimeMaterial.mainTextureOffset = Vector2.zero;
            runtimeMaterial.mainTextureScale = Vector2.one;
        }
    }

    void OnDestroy()
    {
        if (ownsRuntimeMaterialInstance && runtimeMaterial != null)
        {
            Destroy(runtimeMaterial);
            runtimeMaterial = null;
        }

        ownsRuntimeMaterialInstance = false;
    }

    void Update()
    {
        if (Application.isPlaying)
        {
            UpdateTexturePlaylist();
            UpdateScroll();
            return;
        }

        if (!rebuildContinuouslyInEditor)
            return;

        RebuildMesh();
    }

    public void ForceRefresh()
    {
        EnsureComponents();
        InitializeRuntimeMaterial();

        PickStartTexture();
        ApplyActiveTexture();
        ApplyAutoTiling();
    }

    public void SetTexturePlaylist(List<Texture> textures)
    {
        bannerTextures = textures ?? new List<Texture>();
        activeTextureIndex = -1;
        ForceRefresh();
    }

    public void RebuildMesh()
    {
        EnsureComponents();

        List<Vector3> validPoints = GetValidWorldPoints();
        int pointCount = validPoints.Count;
        if (pointCount < 2)
        {
            ClearMesh();
            return;
        }

        bool isClosed = closeLoop && pointCount >= 3;
        int segmentCount = isClosed ? pointCount : pointCount - 1;
        if (segmentCount <= 0)
        {
            ClearMesh();
            return;
        }

        Vector3 center = ComputeCenter(validPoints);
        float[] segmentLengths = new float[segmentCount];
        float totalLength = 0f;

        for (int i = 0; i < segmentCount; i++)
        {
            int next = (i + 1) % pointCount;
            float segLen = Vector3.Distance(validPoints[i], validPoints[next]);
            segmentLengths[i] = segLen;
            totalLength += segLen;
        }

        if (totalLength <= 0.0001f)
        {
            ClearMesh();
            return;
        }

        int vertexColumns = segmentCount + 1;
        Vector3[] vertices = new Vector3[vertexColumns * 2];
        Vector2[] uvs = new Vector2[vertices.Length];
        int[] triangles = new int[segmentCount * 6];

        float tileXCount = totalLength / Mathf.Max(0.01f, tileWorldWidth);
        float tileYCount = Mathf.Max(0.01f, boardHeight / Mathf.Max(0.01f, tileWorldHeight));

        if (clampMinimumOneTile)
        {
            tileXCount = Mathf.Max(1f, tileXCount);
            tileYCount = Mathf.Max(1f, tileYCount);
        }

        float cumulativeLength = 0f;
        for (int i = 0; i < vertexColumns; i++)
        {
            int pointIndex = i % pointCount;
            Vector3 baseWorld = validPoints[pointIndex] + (Vector3.up * verticalOffset);
            Vector3 topWorld = baseWorld + (Vector3.up * boardHeight);

            Vector3 baseLocal = transform.InverseTransformPoint(baseWorld);
            Vector3 topLocal = transform.InverseTransformPoint(topWorld);

            int baseVert = i * 2;
            int topVert = baseVert + 1;

            vertices[baseVert] = baseLocal;
            vertices[topVert] = topLocal;

            float u = (cumulativeLength / Mathf.Max(0.0001f, totalLength)) * tileXCount;
            uvs[baseVert] = new Vector2(u, 0f);
            uvs[topVert] = new Vector2(u, tileYCount);

            if (i < segmentCount)
                cumulativeLength += segmentLengths[i];
        }

        for (int i = 0; i < segmentCount; i++)
        {
            int v0 = i * 2;
            int v1 = v0 + 1;
            int v2 = v0 + 2;
            int v3 = v0 + 3;

            int tri = i * 6;
            if (faceOutward)
            {
                triangles[tri + 0] = v0;
                triangles[tri + 1] = v1;
                triangles[tri + 2] = v2;

                triangles[tri + 3] = v2;
                triangles[tri + 4] = v1;
                triangles[tri + 5] = v3;
            }
            else
            {
                triangles[tri + 0] = v0;
                triangles[tri + 1] = v2;
                triangles[tri + 2] = v1;

                triangles[tri + 3] = v2;
                triangles[tri + 4] = v3;
                triangles[tri + 5] = v1;
            }
        }

        EnsureRuntimeMesh();
        generatedMesh.Clear();
        generatedMesh.vertices = vertices;
        generatedMesh.uv = uvs;
        generatedMesh.triangles = triangles;
        generatedMesh.RecalculateNormals();
        generatedMesh.RecalculateBounds();

        AutoOrientFaceDirection(center);
        ApplyAutoTiling();
    }

    List<Vector3> GetValidWorldPoints()
    {
        List<Vector3> points = new List<Vector3>();
        if (pathPoints == null)
            return points;

        for (int i = 0; i < pathPoints.Count; i++)
        {
            Transform point = pathPoints[i];
            if (point != null)
                points.Add(point.position);
        }

        return points;
    }

    void AutoOrientFaceDirection(Vector3 center)
    {
        if (generatedMesh == null)
            return;

        if (!faceOutward)
            return;

        if (meshFilter == null || meshFilter.sharedMesh == null)
            return;

        // Keep helper available for future extensions; normal direction is currently controlled by triangle winding.
        _ = center;
    }

    Vector3 ComputeCenter(List<Vector3> points)
    {
        if (points == null || points.Count == 0)
            return transform.position;

        Vector3 sum = Vector3.zero;
        for (int i = 0; i < points.Count; i++)
            sum += points[i];

        return sum / points.Count;
    }

    void EnsureComponents()
    {
        if (meshFilter == null)
            meshFilter = GetComponent<MeshFilter>();

        if (meshRenderer == null)
            meshRenderer = GetComponent<MeshRenderer>();

        EnsureRuntimeMesh();
    }

    void InitializeRuntimeMaterial()
    {
        if (meshRenderer == null)
            return;

        Material[] materials;
        if (Application.isPlaying)
        {
            materials = meshRenderer.materials;
            ownsRuntimeMaterialInstance = true;
        }
        else
        {
            materials = meshRenderer.sharedMaterials;
            ownsRuntimeMaterialInstance = false;
        }

        if (materials == null || materials.Length == 0)
            return;

        materialIndex = Mathf.Clamp(materialIndex, 0, materials.Length - 1);
        runtimeMaterial = materials[materialIndex];
        texturePropertyName = ResolveTextureProperty(runtimeMaterial);
    }

    void UpdateTexturePlaylist()
    {
        if (runtimeMaterial == null)
            return;

        if (bannerTextures == null || bannerTextures.Count <= 1)
            return;

        if (Time.time < nextTextureSwitchTime)
            return;

        nextTextureSwitchTime = Time.time + Mathf.Max(0.2f, textureSwitchInterval);

        int nextIndex = activeTextureIndex + 1;
        if (nextIndex >= bannerTextures.Count)
            nextIndex = 0;

        activeTextureIndex = nextIndex;
        ApplyActiveTexture();
        ApplyAutoTiling();
    }

    void UpdateScroll()
    {
        if (runtimeMaterial == null)
            return;

        Vector2 direction = scrollDirection.sqrMagnitude > 0.0001f
            ? scrollDirection.normalized
            : Vector2.right;

        scrollOffset += direction * Mathf.Max(0f, scrollSpeed) * Time.deltaTime;
        scrollOffset.x = Repeat01(scrollOffset.x);
        scrollOffset.y = Repeat01(scrollOffset.y);

        runtimeMaterial.mainTextureOffset = scrollOffset;
        runtimeMaterial.SetTextureOffset(texturePropertyName, scrollOffset);
    }

    void PickStartTexture()
    {
        if (bannerTextures == null || bannerTextures.Count == 0)
        {
            activeTextureIndex = -1;
            return;
        }

        if (activeTextureIndex >= 0 && activeTextureIndex < bannerTextures.Count)
            return;

        activeTextureIndex = randomizeStartTexture
            ? Random.Range(0, bannerTextures.Count)
            : 0;
    }

    void ApplyActiveTexture()
    {
        if (runtimeMaterial == null)
            return;

        Texture texture = null;
        if (bannerTextures != null
            && activeTextureIndex >= 0
            && activeTextureIndex < bannerTextures.Count)
        {
            texture = bannerTextures[activeTextureIndex];
        }

        runtimeMaterial.mainTexture = texture;
        runtimeMaterial.SetTexture(texturePropertyName, texture);
    }

    void ApplyAutoTiling()
    {
        if (!autoTiling || runtimeMaterial == null)
            return;

        if (tilingMode == TilingMode.UseMeshUv)
        {
            Vector2 identity = Vector2.one;
            runtimeMaterial.mainTextureScale = identity;
            runtimeMaterial.SetTextureScale(texturePropertyName, identity);
            return;
        }

        if (meshRenderer == null)
            return;

        Bounds bounds = meshRenderer.bounds;
        float wallLength = Mathf.Max(bounds.size.x, bounds.size.z);
        float wallHeight = Mathf.Max(0.01f, bounds.size.y);

        float tilesX = Mathf.Max(1f, wallLength / Mathf.Max(0.01f, tileWorldWidth));
        float tilesY;

        Texture activeTexture = runtimeMaterial.mainTexture;
        if (keepTextureAspect && activeTexture != null && activeTexture.height > 0)
        {
            float wallAspect = wallLength / wallHeight;
            float textureAspect = (float)activeTexture.width / activeTexture.height;
            tilesY = Mathf.Max(1f, tilesX / Mathf.Max(0.01f, wallAspect * textureAspect));
        }
        else
        {
            tilesY = Mathf.Max(1f, wallHeight / Mathf.Max(0.01f, tileWorldHeight));
        }

        Vector2 tiling = new Vector2(tilesX, tilesY);
        runtimeMaterial.mainTextureScale = tiling;
        runtimeMaterial.SetTextureScale(texturePropertyName, tiling);
    }

    string ResolveTextureProperty(Material mat)
    {
        if (mat == null)
            return "_MainTex";

        if (mat.HasProperty(BaseMapId))
            return "_BaseMap";

        if (mat.HasProperty(MainTexId))
            return "_MainTex";

        return "_MainTex";
    }

    float Repeat01(float value)
    {
        value %= 1f;
        if (value < 0f)
            value += 1f;
        return value;
    }

    void EnsureRuntimeMesh()
    {
        if (meshFilter == null)
            return;

        if (generatedMesh != null)
        {
            meshFilter.sharedMesh = generatedMesh;
            return;
        }

        if (meshFilter.sharedMesh != null && meshFilter.sharedMesh.name == "AdBoardRibbonMesh")
        {
            generatedMesh = meshFilter.sharedMesh;
            return;
        }

        generatedMesh = new Mesh
        {
            name = "AdBoardRibbonMesh"
        };
        generatedMesh.MarkDynamic();
        meshFilter.sharedMesh = generatedMesh;
    }

    void ClearMesh()
    {
        EnsureRuntimeMesh();
        if (generatedMesh != null)
            generatedMesh.Clear();
    }

    void OnValidate()
    {
        materialIndex = Mathf.Max(0, materialIndex);
        boardHeight = Mathf.Max(0.1f, boardHeight);
        tileWorldWidth = Mathf.Max(0.25f, tileWorldWidth);
        tileWorldHeight = Mathf.Max(0.1f, tileWorldHeight);
        textureSwitchInterval = Mathf.Max(0.2f, textureSwitchInterval);
        scrollSpeed = Mathf.Clamp(scrollSpeed, 0f, 3f);

        EnsureComponents();
        InitializeRuntimeMaterial();
        ForceRefresh();

        if (!Application.isPlaying && rebuildContinuouslyInEditor)
            RebuildMesh();
    }
}
