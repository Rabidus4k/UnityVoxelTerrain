using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Один чанк воксельного терраина: строит свой кусок поверхности алгоритмом
/// Marching Cubes по полю плотности из VoxelTerrain и обновляет MeshCollider.
/// Вершины свариваются по рёбрам решётки — соседние треугольники делят вершины,
/// поэтому затенение гладкое и без швов внутри чанка.
/// </summary>
public class TerrainChunk
{
    readonly VoxelTerrain terrain;
    readonly Vector3Int origin; // минимальная ячейка чанка в вокселях
    readonly Vector3Int size;   // размер чанка в ячейках

    readonly Mesh mesh;
    readonly MeshCollider collider;

    readonly List<Vector3> vertices = new List<Vector3>();
    readonly List<Vector3> normals = new List<Vector3>();
    readonly List<int> triangles = new List<int>();
    // Ребро решётки -> индекс вершины на нём (сварка вершин).
    readonly Dictionary<long, int> edgeVertexCache = new Dictionary<long, int>();

    public TerrainChunk(VoxelTerrain terrain, Vector3Int origin, Vector3Int size, Material material)
    {
        this.terrain = terrain;
        this.origin = origin;
        this.size = size;

        var go = new GameObject($"Chunk {origin.x}_{origin.y}_{origin.z}");
        go.transform.SetParent(terrain.transform, false);

        go.AddComponent<MeshFilter>().sharedMesh = mesh = new Mesh { name = go.name };
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        go.AddComponent<MeshRenderer>().sharedMaterial = material;
        collider = go.AddComponent<MeshCollider>();
    }

    public void Rebuild()
    {
        vertices.Clear();
        normals.Clear();
        triangles.Clear();
        edgeVertexCache.Clear();

        float iso = terrain.isoLevel;
        var cornerDensity = new float[8];
        var cornerPos = new Vector3Int[8];

        for (int z = 0; z < size.z; z++)
        for (int y = 0; y < size.y; y++)
        for (int x = 0; x < size.x; x++)
        {
            var cell = origin + new Vector3Int(x, y, z);

            int cubeIndex = 0;
            for (int i = 0; i < 8; i++)
            {
                cornerPos[i] = cell + MarchingCubesTables.Corners[i];
                cornerDensity[i] = terrain.GetDensity(cornerPos[i].x, cornerPos[i].y, cornerPos[i].z);
                if (cornerDensity[i] < iso)
                    cubeIndex |= 1 << i;
            }

            int[] edges = MarchingCubesTables.Triangulation[cubeIndex];
            for (int e = 0; e < edges.Length; e += 3)
            {
                int i0 = GetEdgeVertex(cornerPos, cornerDensity, edges[e], iso);
                int i1 = GetEdgeVertex(cornerPos, cornerDensity, edges[e + 1], iso);
                int i2 = GetEdgeVertex(cornerPos, cornerDensity, edges[e + 2], iso);

                // При нашей раскладке углов табличный обход смотрит внутрь породы,
                // поэтому все треугольники разворачиваются лицом в воздух.
                triangles.Add(i0);
                triangles.Add(i2);
                triangles.Add(i1);
            }
        }

        FinalizeNormals();

        mesh.Clear();
        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetTriangles(triangles, 0);

        // Переназначение заставляет физику перечитать геометрию.
        collider.sharedMesh = null;
        if (vertices.Count > 0)
            collider.sharedMesh = mesh;
    }

    /// <summary>Возвращает индекс вершины на ребре решётки, создавая её при первом обращении.</summary>
    int GetEdgeVertex(Vector3Int[] cornerPos, float[] cornerDensity, int edge, float iso)
    {
        int a = MarchingCubesTables.EdgeCornerA[edge];
        int b = MarchingCubesTables.EdgeCornerB[edge];
        Vector3Int pa = cornerPos[a];
        Vector3Int pb = cornerPos[b];

        // Рёбра решётки осевые: ключ — меньший конец плюс ось.
        Vector3Int mn = Vector3Int.Min(pa, pb);
        long axis = pa.x != pb.x ? 0L : pa.y != pb.y ? 1L : 2L;
        long key = (axis << 60) | ((long)mn.x << 40) | ((long)mn.y << 20) | (long)mn.z;
        if (edgeVertexCache.TryGetValue(key, out int index))
            return index;

        float da = cornerDensity[a];
        float db = cornerDensity[b];
        float t = Mathf.Approximately(db, da) ? 0.5f : Mathf.Clamp01((iso - da) / (db - da));
        vertices.Add(Vector3.Lerp(pa, pb, t) * terrain.voxelSize);

        // Нормаль — из градиента поля: плотность растёт вглубь породы, наружу — минус градиент.
        // На "плато" (плотность зажата в 0/1) градиент нулевой — чинится в FinalizeNormals.
        Vector3 gradient = Vector3.Lerp(
            terrain.GetGradient(pa.x, pa.y, pa.z),
            terrain.GetGradient(pb.x, pb.y, pb.z), t);
        normals.Add(gradient.sqrMagnitude > 1e-8f ? -gradient.normalized : Vector3.zero);

        index = vertices.Count - 1;
        edgeVertexCache[key] = index;
        return index;
    }

    /// <summary>
    /// Гладкие нормали: где градиент поля вырожден или смотрит внутрь, вершина получает
    /// усреднённую по площади нормаль прилегающих треугольников (классическое smooth shading).
    /// </summary>
    void FinalizeNormals()
    {
        var faceAccum = new Vector3[vertices.Count];
        for (int t = 0; t < triangles.Count; t += 3)
        {
            Vector3 v0 = vertices[triangles[t]];
            // Невзвешенный Cross даёт вклад, пропорциональный площади грани.
            Vector3 faceNormal = Vector3.Cross(vertices[triangles[t + 1]] - v0, vertices[triangles[t + 2]] - v0);
            faceAccum[triangles[t]] += faceNormal;
            faceAccum[triangles[t + 1]] += faceNormal;
            faceAccum[triangles[t + 2]] += faceNormal;
        }

        for (int i = 0; i < normals.Count; i++)
        {
            if (normals[i] != Vector3.zero && Vector3.Dot(normals[i], faceAccum[i]) >= 0f)
                continue; // градиентная нормаль здорова — она самая гладкая
            normals[i] = faceAccum[i].sqrMagnitude > 1e-12f ? faceAccum[i].normalized : Vector3.up;
        }
    }
}
