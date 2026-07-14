# Voxel Destructible Terrain — Deep Rock Galactic Style

> A Unity prototype of fully destructible voxel terrain. A sealed cave system is
> generated procedurally from 3D noise, meshed with **Marching Cubes**, and can be
> **dug, built up, and smoothed** in real time — just like the rock in *Deep Rock Galactic*.

<sub>🇬🇧 English · [🇷🇺 Русский](#-русский)</sub>

---

## 🇬🇧 English

### Overview

This project is a self-contained sandbox demonstrating a **smooth, editable voxel
world**. Instead of blocky Minecraft-style cubes, the terrain stores a continuous
**scalar density field** (rock → iso-surface → air) and reconstructs a smooth surface
with the Marching Cubes algorithm. Every dig or build edits the density field and only
the affected chunks are re-meshed, so deformation stays fast and seamless.

There is no gameplay layer yet — it's a **technology prototype** you can fly around and
carve.

### Features

- **Procedural caves** — solid rock eaten away by multi-directional 3D Perlin noise
  produces DRG-like blobby cavities, with a guaranteed spawn room in the centre and an
  indestructible outer shell that keeps the world sealed.
- **Marching Cubes meshing** — the density field is triangulated into a smooth surface.
  Vertices are welded along shared lattice edges, so shading is seamless inside a chunk.
- **Smooth normals** — normals come from the density gradient, with an area-weighted
  face-normal fallback on degenerate "plateaus" for clean smooth shading.
- **Chunked & incremental** — the world is split into chunks; edits mark only the
  touched chunks dirty and rebuild them in `LateUpdate`.
- **Real-time deformation** — three brushes with soft spherical falloff:
  - **Dig** — remove rock (drill / pickaxe).
  - **Build** — add rock (platform gun).
  - **Smooth** — locally blur the field to round off jagged edges.
- **Field smoothing pass** — an optional post-generation blur rounds sharp terrain edges.
- **Free-fly camera** — spawns inside the start room for immediate testing.

### Controls

| Input | Action |
|-------|--------|
| **W A S D** | Move |
| **Space / Ctrl** | Ascend / descend |
| **Shift** | Sprint |
| **Mouse** | Look around |
| **LMB** (hold) | Dig rock |
| **RMB** (hold) | Build rock |
| **MMB** (hold) | Smooth surface |
| **Mouse wheel** | Adjust brush radius |
| **Esc** | Release cursor · **LMB** re-locks it |

### Requirements

- **Unity 6000.3.14f1** (Unity 6.3)
- **Universal Render Pipeline** (URP 17.3)
- **Input System** package (1.19)

### Getting Started

1. Open the project in the Unity version above.
2. Open the scene in `Assets/Scenes/`.
3. Add a `VoxelTerrain` component to a GameObject in the scene (or use the prepared one).
4. Add a camera with the `FreeFlyCamera` and `TerrainDeformer` components.
5. Press **Play** — you'll spawn inside the cave. Start carving.

### Architecture

```
Assets/Script/
├── Terrain/
│   ├── VoxelTerrain.cs          # Density field: generation, smoothing, Dig/Build/SmoothArea, chunk management
│   ├── TerrainChunk.cs          # Marching Cubes meshing of one chunk + MeshCollider
│   └── MarchingCubesTables.cs   # Edge/corner/triangulation lookup tables
└── Player/
    ├── FreeFlyCamera.cs         # WASD + mouse fly camera, spawns in the start room
    └── TerrainDeformer.cs       # Raycast brush: dig / build / smooth, radius control
```

**Data flow.** `VoxelTerrain` owns a `float[]` density field over a `(dims+1)³` lattice.
On `Awake` it generates the field, applies a smoothing blur, and splits the volume into
`TerrainChunk`s. Each chunk runs Marching Cubes over its cells, welding vertices along
shared lattice edges and deriving normals from the field gradient. `TerrainDeformer`
raycasts from the camera and calls `Dig` / `Build` / `SmoothArea`, which edit the density
sphere with a soft `SmoothStep` falloff and mark the overlapping chunks dirty; dirty
chunks are rebuilt once per frame in `LateUpdate`.

### Key Parameters (`VoxelTerrain`)

| Field | Meaning |
|-------|---------|
| `dimensions` | World size in voxels |
| `voxelSize` | Size of one voxel in metres |
| `chunkSize` | Chunk size in voxels |
| `isoLevel` | Density threshold separating rock from air |
| `seed`, `caveNoiseScale` | Cave noise seed and scale (smaller scale → larger cavities) |
| `spawnRoomRadius` | Radius of the guaranteed central spawn room |
| `borderThickness` | Thickness of the indestructible outer shell |
| `smoothIterations` | Post-generation blur passes |

---

## 🇷🇺 Русский

### Обзор

Проект — самодостаточная песочница, демонстрирующая **гладкий редактируемый воксельный
мир**. Вместо кубов в стиле Minecraft терраин хранит непрерывное **скалярное поле
плотности** (порода → изоповерхность → воздух) и восстанавливает гладкую поверхность
алгоритмом Marching Cubes. Любое копание или застройка правит поле плотности, и
перестраиваются только затронутые чанки — поэтому деформация быстрая и бесшовная.

Игрового слоя пока нет — это **технологический прототип**, по которому можно летать и
который можно вырезать.

### Возможности

- **Процедурные пещеры** — сплошная порода, изъеденная разнонаправленным 3D-шумом
  Перлина, даёт блобовые полости в духе DRG; в центре гарантирована стартовая комната,
  а неразрушаемая внешняя оболочка держит мир запечатанным.
- **Меширование Marching Cubes** — поле плотности триангулируется в гладкую поверхность.
  Вершины свариваются по общим рёбрам решётки, поэтому затенение внутри чанка бесшовное.
- **Гладкие нормали** — нормали берутся из градиента поля, а на вырожденных «плато»
  подменяются усреднённой по площади нормалью граней для чистого smooth shading.
- **Чанки и инкрементальность** — мир разбит на чанки; правка помечает «грязными» только
  затронутые чанки и перестраивает их в `LateUpdate`.
- **Деформация в реальном времени** — три кисти с мягким сферическим фаллоффом:
  - **Копать** — снимать породу (бур / кирка).
  - **Строить** — наращивать породу (платформенная пушка).
  - **Сглаживать** — локально размывать поле, скругляя рваные края.
- **Проход сглаживания поля** — опциональное размытие после генерации скругляет острые
  грани рельефа.
- **Летающая камера** — стартует внутри спавн-комнаты для мгновенного теста.

### Управление

| Ввод | Действие |
|------|----------|
| **W A S D** | Движение |
| **Space / Ctrl** | Вверх / вниз |
| **Shift** | Ускорение |
| **Мышь** | Обзор |
| **ЛКМ** (удерживать) | Копать породу |
| **ПКМ** (удерживать) | Строить породу |
| **СКМ** (удерживать) | Сглаживать поверхность |
| **Колесо мыши** | Радиус кисти |
| **Esc** | Отпустить курсор · **ЛКМ** возвращает захват |

### Требования

- **Unity 6000.3.14f1** (Unity 6.3)
- **Universal Render Pipeline** (URP 17.3)
- Пакет **Input System** (1.19)

### Запуск

1. Откройте проект в указанной выше версии Unity.
2. Откройте сцену в `Assets/Scenes/`.
3. Добавьте компонент `VoxelTerrain` на объект сцены (или используйте готовый).
4. Добавьте камеру с компонентами `FreeFlyCamera` и `TerrainDeformer`.
5. Нажмите **Play** — вы окажетесь внутри пещеры. Начинайте копать.

### Архитектура

```
Assets/Script/
├── Terrain/
│   ├── VoxelTerrain.cs          # Поле плотности: генерация, сглаживание, Dig/Build/SmoothArea, управление чанками
│   ├── TerrainChunk.cs          # Меширование одного чанка алгоритмом Marching Cubes + MeshCollider
│   └── MarchingCubesTables.cs   # Таблицы рёбер / углов / триангуляции
└── Player/
    ├── FreeFlyCamera.cs         # Летающая камера WASD + мышь, спавн в стартовой комнате
    └── TerrainDeformer.cs       # Кисть по рейкасту: копать / строить / сглаживать, управление радиусом
```

**Поток данных.** `VoxelTerrain` владеет полем плотности `float[]` на решётке `(dims+1)³`.
На `Awake` он генерирует поле, применяет размытие и делит объём на `TerrainChunk`.
Каждый чанк прогоняет Marching Cubes по своим ячейкам, сваривая вершины по общим рёбрам
решётки и вычисляя нормали из градиента поля. `TerrainDeformer` пускает рейкаст от камеры
и вызывает `Dig` / `Build` / `SmoothArea`, которые правят сферу плотности с мягким
фаллоффом `SmoothStep` и помечают пересекаемые чанки грязными; грязные чанки
перестраиваются раз за кадр в `LateUpdate`.

### Ключевые параметры (`VoxelTerrain`)

| Поле | Смысл |
|------|-------|
| `dimensions` | Размер мира в вокселях |
| `voxelSize` | Размер одного вокселя в метрах |
| `chunkSize` | Размер чанка в вокселях |
| `isoLevel` | Порог плотности, разделяющий породу и воздух |
| `seed`, `caveNoiseScale` | Сид и масштаб шума пещер (меньше масштаб → крупнее полости) |
| `spawnRoomRadius` | Радиус гарантированной центральной спавн-комнаты |
| `borderThickness` | Толщина неразрушаемой внешней оболочки |
| `smoothIterations` | Проходы размытия после генерации |

---

<sub>Made with Unity 6 · URP · Marching Cubes</sub>
