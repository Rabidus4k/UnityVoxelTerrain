using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Разрушаемый терраин в стиле Deep Rock Galactic.
/// Хранит скалярное поле плотности (rock > isoLevel > воздух), режет его на чанки
/// и строит поверхность алгоритмом Marching Cubes. Копание и застройка — через Modify().
/// </summary>
public class VoxelTerrain : MonoBehaviour
{
    [Header("Размеры")]
    [Tooltip("Размер поля в вокселях (ячейках).")]
    public Vector3Int dimensions = new Vector3Int(64, 40, 64);
    [Tooltip("Размер одного вокселя в метрах.")]
    public float voxelSize = 1f;
    [Tooltip("Размер чанка в вокселях.")]
    public int chunkSize = 16;

    [Header("Поверхность")]
    [Range(0.05f, 0.95f)]
    public float isoLevel = 0.5f;
    public Material material;

    [Header("Генерация пещеры")]
    public int seed = 1337;
    [Tooltip("Масштаб шума пещер: меньше — крупнее полости.")]
    public float caveNoiseScale = 0.07f;
    [Tooltip("Радиус стартовой полости в центре (метры).")]
    public float spawnRoomRadius = 9f;
    [Tooltip("Толщина неразрушаемой внешней стенки в вокселях.")]
    public int borderThickness = 2;

    [Header("Сглаживание")]
    [Tooltip("Проходов размытия поля после генерации: скругляет острые грани рельефа.")]
    [Range(0, 4)]
    public int smoothIterations = 2;

    float[] density;          // (dims+1)^3 точек решётки
    Vector3Int pointCount;    // dimensions + 1
    TerrainChunk[] chunks;
    Vector3Int chunkCount;
    readonly HashSet<int> dirtyChunks = new HashSet<int>();

    void Awake()
    {
        if (material == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            material = new Material(shader) { color = new Color(0.55f, 0.42f, 0.32f) };
        }

        pointCount = dimensions + Vector3Int.one;
        density = new float[pointCount.x * pointCount.y * pointCount.z];
        Generate();
        SmoothField(smoothIterations);
        CreateChunks();
    }

    void LateUpdate()
    {
        if (dirtyChunks.Count == 0) return;
        foreach (int index in dirtyChunks)
            chunks[index].Rebuild();
        dirtyChunks.Clear();
    }

    // ---------------------------------------------------------------- генерация

    void Generate()
    {
        var rng = new System.Random(seed);
        Vector3 noiseOffset = new Vector3(rng.Next(-9999, 9999), rng.Next(-9999, 9999), rng.Next(-9999, 9999));
        Vector3 center = (Vector3)dimensions * 0.5f;

        for (int z = 0; z < pointCount.z; z++)
        for (int y = 0; y < pointCount.y; y++)
        for (int x = 0; x < pointCount.x; x++)
        {
            Vector3 p = new Vector3(x, y, z);

            // Сплошная порода, изъеденная 3D-шумом — блобовые пещеры как в DRG.
            float noise = Noise3D((p + noiseOffset) * caveNoiseScale);
            float d = Mathf.Clamp01(0.62f + (noise - 0.5f) * 1.6f);

            // Гарантированная стартовая комната в центре.
            float distToCenter = Vector3.Distance(p, center) * voxelSize;
            float room = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(spawnRoomRadius - 2f, spawnRoomRadius + 2f, distToCenter));
            d = Mathf.Min(d, room);

            // Внешняя оболочка всегда сплошная, чтобы мир был запечатан.
            if (IsBorder(x, y, z))
                d = 1f;

            density[PointIndex(x, y, z)] = d;
        }
    }

    static float Noise3D(Vector3 p)
    {
        float xy = Mathf.PerlinNoise(p.x, p.y);
        float yz = Mathf.PerlinNoise(p.y, p.z);
        float xz = Mathf.PerlinNoise(p.x, p.z);
        float yx = Mathf.PerlinNoise(p.y + 31.7f, p.x + 17.3f);
        float zy = Mathf.PerlinNoise(p.z + 11.9f, p.y + 47.1f);
        float zx = Mathf.PerlinNoise(p.z + 5.3f, p.x + 23.5f);
        return (xy + yz + xz + yx + zy + zx) / 6f;
    }

    /// <summary>
    /// Размывает всё поле (среднее по 6 соседям и себе): бинарные "лесенки" 0/1
    /// превращаются в плавные переходы, и marching cubes даёт скруглённые грани.
    /// </summary>
    void SmoothField(int iterations)
    {
        if (iterations <= 0) return;
        var buffer = new float[density.Length];

        for (int it = 0; it < iterations; it++)
        {
            for (int z = 0; z < pointCount.z; z++)
            for (int y = 0; y < pointCount.y; y++)
            for (int x = 0; x < pointCount.x; x++)
            {
                int i = PointIndex(x, y, z);
                if (IsBorder(x, y, z)) { buffer[i] = 1f; continue; }

                // Не-краевые точки имеют всех 6 соседей: границы толщиной >= 1.
                float sum = density[i]
                    + density[PointIndex(x + 1, y, z)] + density[PointIndex(x - 1, y, z)]
                    + density[PointIndex(x, y + 1, z)] + density[PointIndex(x, y - 1, z)]
                    + density[PointIndex(x, y, z + 1)] + density[PointIndex(x, y, z - 1)];
                buffer[i] = sum / 7f;
            }
            (density, buffer) = (buffer, density);
        }
    }

    bool IsBorder(int x, int y, int z)
    {
        int b = Mathf.Max(1, borderThickness);
        return x < b || y < b || z < b
            || x >= pointCount.x - b || y >= pointCount.y - b || z >= pointCount.z - b;
    }

    void CreateChunks()
    {
        chunkCount = new Vector3Int(
            Mathf.CeilToInt(dimensions.x / (float)chunkSize),
            Mathf.CeilToInt(dimensions.y / (float)chunkSize),
            Mathf.CeilToInt(dimensions.z / (float)chunkSize));

        chunks = new TerrainChunk[chunkCount.x * chunkCount.y * chunkCount.z];

        for (int cz = 0; cz < chunkCount.z; cz++)
        for (int cy = 0; cy < chunkCount.y; cy++)
        for (int cx = 0; cx < chunkCount.x; cx++)
        {
            var origin = new Vector3Int(cx, cy, cz) * chunkSize;
            var size = Vector3Int.Min(new Vector3Int(chunkSize, chunkSize, chunkSize), dimensions - origin);
            var chunk = new TerrainChunk(this, origin, size, material);
            chunks[ChunkIndex(cx, cy, cz)] = chunk;
            chunk.Rebuild();
        }
    }

    // ---------------------------------------------------------------- деформация

    /// <summary>Выкопать сферу породы (как бур/кирка).</summary>
    public void Dig(Vector3 worldPos, float radius, float amount) => Modify(worldPos, radius, -amount);

    /// <summary>Достроить породу (как платформенная пушка).</summary>
    public void Build(Vector3 worldPos, float radius, float amount) => Modify(worldPos, radius, amount);

    /// <summary>
    /// Изменить плотность в сфере с мягким краем. delta &lt; 0 — копать, delta &gt; 0 — наращивать.
    /// </summary>
    public void Modify(Vector3 worldPos, float radius, float delta)
    {
        Vector3 local = transform.InverseTransformPoint(worldPos) / voxelSize;
        float r = radius / voxelSize;

        int minX = Mathf.Max(Mathf.FloorToInt(local.x - r), 0);
        int minY = Mathf.Max(Mathf.FloorToInt(local.y - r), 0);
        int minZ = Mathf.Max(Mathf.FloorToInt(local.z - r), 0);
        int maxX = Mathf.Min(Mathf.CeilToInt(local.x + r), pointCount.x - 1);
        int maxY = Mathf.Min(Mathf.CeilToInt(local.y + r), pointCount.y - 1);
        int maxZ = Mathf.Min(Mathf.CeilToInt(local.z + r), pointCount.z - 1);
        if (minX > maxX || minY > maxY || minZ > maxZ) return;

        for (int z = minZ; z <= maxZ; z++)
        for (int y = minY; y <= maxY; y++)
        for (int x = minX; x <= maxX; x++)
        {
            if (IsBorder(x, y, z)) continue; // внешнюю стенку не прокопать

            float dist = Vector3.Distance(new Vector3(x, y, z), local);
            if (dist > r) continue;

            // Плавный сферический фаллофф — мягкие "выеденные" края как у бура в DRG.
            float falloff = Mathf.SmoothStep(1f, 0f, dist / r);
            int i = PointIndex(x, y, z);
            density[i] = Mathf.Clamp01(density[i] + delta * falloff);
        }

        MarkDirty(minX, minY, minZ, maxX, maxY, maxZ);
    }

    /// <summary>
    /// Кисть-сглаживание: локально размывает поле в сфере, скругляя острые кромки
    /// (например, рваные края после копания). strength — доля размытия за вызов, 0..1.
    /// </summary>
    public void SmoothArea(Vector3 worldPos, float radius, float strength)
    {
        Vector3 local = transform.InverseTransformPoint(worldPos) / voxelSize;
        float r = radius / voxelSize;
        strength = Mathf.Clamp01(strength);

        int minX = Mathf.Max(Mathf.FloorToInt(local.x - r), 0);
        int minY = Mathf.Max(Mathf.FloorToInt(local.y - r), 0);
        int minZ = Mathf.Max(Mathf.FloorToInt(local.z - r), 0);
        int maxX = Mathf.Min(Mathf.CeilToInt(local.x + r), pointCount.x - 1);
        int maxY = Mathf.Min(Mathf.CeilToInt(local.y + r), pointCount.y - 1);
        int maxZ = Mathf.Min(Mathf.CeilToInt(local.z + r), pointCount.z - 1);
        if (minX > maxX || minY > maxY || minZ > maxZ) return;

        // Снимок региона (+1 по краям), чтобы усреднение шло по исходным значениям.
        int sMinX = Mathf.Max(minX - 1, 0), sMinY = Mathf.Max(minY - 1, 0), sMinZ = Mathf.Max(minZ - 1, 0);
        int sMaxX = Mathf.Min(maxX + 1, pointCount.x - 1), sMaxY = Mathf.Min(maxY + 1, pointCount.y - 1), sMaxZ = Mathf.Min(maxZ + 1, pointCount.z - 1);
        int sx = sMaxX - sMinX + 1, sy = sMaxY - sMinY + 1, sz = sMaxZ - sMinZ + 1;
        var snapshot = new float[sx * sy * sz];
        for (int z = sMinZ; z <= sMaxZ; z++)
        for (int y = sMinY; y <= sMaxY; y++)
        for (int x = sMinX; x <= sMaxX; x++)
            snapshot[(x - sMinX) + sx * ((y - sMinY) + sy * (z - sMinZ))] = density[PointIndex(x, y, z)];

        float Snap(int x, int y, int z)
        {
            x = Mathf.Clamp(x, sMinX, sMaxX);
            y = Mathf.Clamp(y, sMinY, sMaxY);
            z = Mathf.Clamp(z, sMinZ, sMaxZ);
            return snapshot[(x - sMinX) + sx * ((y - sMinY) + sy * (z - sMinZ))];
        }

        for (int z = minZ; z <= maxZ; z++)
        for (int y = minY; y <= maxY; y++)
        for (int x = minX; x <= maxX; x++)
        {
            if (IsBorder(x, y, z)) continue;

            float dist = Vector3.Distance(new Vector3(x, y, z), local);
            if (dist > r) continue;

            float average = (Snap(x, y, z)
                + Snap(x + 1, y, z) + Snap(x - 1, y, z)
                + Snap(x, y + 1, z) + Snap(x, y - 1, z)
                + Snap(x, y, z + 1) + Snap(x, y, z - 1)) / 7f;

            float falloff = Mathf.SmoothStep(1f, 0f, dist / r) * strength;
            int i = PointIndex(x, y, z);
            density[i] = Mathf.Lerp(density[i], average, falloff);
        }

        MarkDirty(minX, minY, minZ, maxX, maxY, maxZ);
    }

    void MarkDirty(int minX, int minY, int minZ, int maxX, int maxY, int maxZ)
    {
        // Ячейка (x,y,z) использует точки x..x+1, поэтому точка влияет и на ячейку слева.
        int cMinX = Mathf.Max((minX - 1) / chunkSize, 0);
        int cMinY = Mathf.Max((minY - 1) / chunkSize, 0);
        int cMinZ = Mathf.Max((minZ - 1) / chunkSize, 0);
        int cMaxX = Mathf.Min(maxX / chunkSize, chunkCount.x - 1);
        int cMaxY = Mathf.Min(maxY / chunkSize, chunkCount.y - 1);
        int cMaxZ = Mathf.Min(maxZ / chunkSize, chunkCount.z - 1);

        for (int cz = cMinZ; cz <= cMaxZ; cz++)
        for (int cy = cMinY; cy <= cMaxY; cy++)
        for (int cx = cMinX; cx <= cMaxX; cx++)
            dirtyChunks.Add(ChunkIndex(cx, cy, cz));
    }

    // ---------------------------------------------------------------- доступ к полю

    public float GetDensity(int x, int y, int z) => density[PointIndex(x, y, z)];

    /// <summary>Градиент плотности в точке решётки (центральные разности).</summary>
    public Vector3 GetGradient(int x, int y, int z)
    {
        float gx = GetDensityClamped(x + 1, y, z) - GetDensityClamped(x - 1, y, z);
        float gy = GetDensityClamped(x, y + 1, z) - GetDensityClamped(x, y - 1, z);
        float gz = GetDensityClamped(x, y, z + 1) - GetDensityClamped(x, y, z - 1);
        return new Vector3(gx, gy, gz);
    }

    float GetDensityClamped(int x, int y, int z)
    {
        x = Mathf.Clamp(x, 0, pointCount.x - 1);
        y = Mathf.Clamp(y, 0, pointCount.y - 1);
        z = Mathf.Clamp(z, 0, pointCount.z - 1);
        return density[PointIndex(x, y, z)];
    }

    /// <summary>Центр стартовой комнаты в мировых координатах — сюда ставить камеру/игрока.</summary>
    public Vector3 SpawnPoint => transform.TransformPoint((Vector3)dimensions * 0.5f * voxelSize);

    int PointIndex(int x, int y, int z) => x + pointCount.x * (y + pointCount.y * z);
    int ChunkIndex(int x, int y, int z) => x + chunkCount.x * (y + chunkCount.y * z);
}
