using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// TerrainCollider requires com.unity.modules.terrainphysics in Packages/manifest.json.
public class HouseInteractions : MonoBehaviour
{
    public Transform house;
    public Terrain terrain;
    public MaterialChanger wallMaterials;

    Material wood;
    Material metal;
    Material[] furnitureFinishes;
    TerrainData originalTerrain;
    TerrainData runtimeTerrain;

    void Start()
    {
        if (house == null) house = GameObject.Find("Apartamento")?.transform;
        if (house == null) return;

        var renderers = house.GetComponentsInChildren<Renderer>();
        Renderer floor = null;
        foreach (var renderer in renderers)
        {
            if (renderer.name == "Losa baja") floor = renderer;
            if (renderer.name == "Fachada baja.005" && wallMaterials != null && wallMaterials.targetRenderer == null)
                wallMaterials.targetRenderer = renderer;
        }
        if (floor == null) return;

        // Copy an included material so Quest uses the same supported shader.
        wood = new Material(floor.sharedMaterial) { name = "Madera muebles y puertas", color = new Color(0.42f, 0.23f, 0.11f) };
        metal = new Material(floor.sharedMaterial) { name = "Herrajes", color = new Color(0.13f, 0.15f, 0.16f) };
        wood.name = "Natural mate";
        wood.SetFloat("_Smoothness", 0.2f);
        furnitureFinishes = new[]
        {
            wood,
            FurnitureFinish("Oscuro satinado", new Color(0.12f, 0.055f, 0.025f), 0.5f),
            FurnitureFinish("Blanco mate", new Color(0.86f, 0.84f, 0.79f), 0.15f),
            FurnitureFinish("Negro satinado", new Color(0.035f, 0.04f, 0.045f), 0.45f)
        };
        MakeDoor(renderers, "baja");
        MakeDoor(renderers, "alta");

        var origin = new Vector3(floor.bounds.min.x + 0.08f, floor.bounds.max.y, floor.bounds.min.z + 0.08f);
        MakeTable(origin + new Vector3(2.1f, 0.03f, 2.6f));
        MakeShelf(origin + new Vector3(3.1f, 0.03f, 5.5f));

        var footprint = floor.bounds;
        foreach (var renderer in renderers)
            if (renderer.name == "Losa sala baja" || renderer.name.StartsWith("Peldano") || renderer.name == "Descanso superior")
                footprint.Encapsulate(renderer.bounds);
        footprint.Expand(new Vector3(1.5f, 0f, 1.5f));
        ClearTerrain(footprint, floor.bounds.max.y - 0.03f);
    }

    void MakeDoor(Renderer[] renderers, string level)
    {
        Renderer left = null, right = null;
        foreach (var renderer in renderers)
        {
            if (renderer.name == "Marco entrada " + level) left = renderer;
            if (renderer.name == "Marco entrada " + level + ".001") right = renderer;
        }
        if (left == null || right == null) return;
        if (left.bounds.center.x > right.bounds.center.x) (left, right) = (right, left);

        float width = right.bounds.min.x - left.bounds.max.x - 0.06f;
        float height = left.bounds.size.y - 0.065f;
        var door = new GameObject("Puerta planta " + level);
        door.transform.SetParent(transform, false);
        door.transform.position = new Vector3(left.bounds.max.x + 0.03f,
            left.bounds.min.y + 0.025f + height / 2f, left.bounds.center.z);
        Part(door.transform, "Hoja", new Vector3(width / 2f, 0, 0), new Vector3(width, height, 0.045f), wood);
        Part(door.transform, "Manilla exterior", new Vector3(width - 0.12f, -0.1f, -0.07f), new Vector3(0.16f, 0.04f, 0.1f), metal);
        Part(door.transform, "Manilla interior", new Vector3(width - 0.12f, -0.1f, 0.07f), new Vector3(0.16f, 0.04f, 0.1f), metal);

        var body = door.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        door.AddComponent<HingedDoor>();
    }

    void MakeTable(Vector3 position)
    {
        var table = new GameObject("Mesa movible");
        table.transform.SetParent(transform, false);
        table.transform.position = position;
        Part(table.transform, "Sobre", new Vector3(0, 0.73f, 0), new Vector3(1.2f, 0.08f, 0.75f), wood);
        foreach (float x in new[] { -0.49f, 0.49f })
            foreach (float z in new[] { -0.27f, 0.27f })
                Part(table.transform, "Pata", new Vector3(x, 0.345f, z), new Vector3(0.07f, 0.69f, 0.07f), metal);
        MakeMovable(table);
    }

    void MakeShelf(Vector3 position)
    {
        var shelf = new GameObject("Estante movible");
        shelf.transform.SetParent(transform, false);
        shelf.transform.position = position;
        foreach (float x in new[] { -0.425f, 0.425f })
            Part(shelf.transform, "Lateral", new Vector3(x, 0.65f, 0), new Vector3(0.05f, 1.3f, 0.35f), wood);
        foreach (float y in new[] { 0.035f, 0.46f, 0.88f, 1.275f })
            Part(shelf.transform, "Repisa", new Vector3(0, y, 0), new Vector3(0.85f, 0.05f, 0.35f), wood);
        Part(shelf.transform, "Fondo", new Vector3(0, 0.65f, 0.165f), new Vector3(0.85f, 1.3f, 0.02f), wood);
        MakeMovable(shelf);
    }

    Material FurnitureFinish(string title, Color color, float smoothness)
    {
        var material = new Material(wood) { name = title, color = color };
        material.SetFloat("_Smoothness", smoothness);
        return material;
    }

    void MakeMovable(GameObject furniture)
    {
        // Keep the original ray/grab targets. Add a solid collision volume so
        // stair rails cannot slip between the legs or shelves and trap them.
        // These parts are axis-aligned cubes; use only the individual colliders
        // if future furniture needs usable openings.
        var bounds = new Bounds(Vector3.zero, Vector3.zero);
        foreach (var part in furniture.GetComponentsInChildren<BoxCollider>())
        {
            bounds.Encapsulate(new Bounds(part.transform.localPosition, part.transform.localScale));
        }
        var collider = furniture.AddComponent<BoxCollider>();
        collider.center = bounds.center;
        collider.size = bounds.size;

        var body = furniture.AddComponent<Rigidbody>();
        body.mass = 8f;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        body.maxLinearVelocity = 2.5f;
        body.maxAngularVelocity = 2f;
        body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        // Add XRGrab last on the active object, as before: its Awake/OnEnable
        // discover and register all colliders together with the interaction manager.
        var grab = furniture.AddComponent<XRGrabInteractable>();
        grab.movementType = XRBaseInteractable.MovementType.VelocityTracking;
        grab.limitLinearVelocity = true;
        grab.maxLinearVelocityDelta = 2.5f;
        grab.limitAngularVelocity = true;
        grab.maxAngularVelocityDelta = 2f;
        grab.useDynamicAttach = true;
        grab.throwOnDetach = false;

        if (wallMaterials == null) return;
        var targets = new List<Renderer>();
        foreach (var renderer in furniture.GetComponentsInChildren<Renderer>())
            if (renderer.sharedMaterial == wood) targets.Add(renderer);
        if (targets.Count == 0) return;
        var finishes = furniture.AddComponent<MaterialChanger>();
        finishes.radialMenuPrefab = wallMaterials.radialMenuPrefab;
        finishes.materials = furnitureFinishes;
        finishes.menuTitle = "Acabados de " + furniture.name.Replace(" movible", "").ToLowerInvariant();
        finishes.targetRenderer = targets[0];
        targets.RemoveAt(0);
        finishes.additionalRenderers = targets.ToArray();
    }

    static void Part(Transform parent, string name, Vector3 position, Vector3 size, Material material)
    {
        var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
        part.name = name;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = position;
        part.transform.localScale = size;
        part.GetComponent<Renderer>().sharedMaterial = material;
    }

    void ClearTerrain(Bounds footprint, float floorHeight)
    {
        if (terrain == null) terrain = Terrain.activeTerrain;
        if (terrain == null || terrain.terrainData == null) return;
        originalTerrain = terrain.terrainData;
        runtimeTerrain = Instantiate(originalTerrain);
        runtimeTerrain.name = originalTerrain.name + " (despejado en Play)";
        terrain.terrainData = runtimeTerrain;
        var collider = terrain.GetComponent<TerrainCollider>();
        if (collider != null) collider.terrainData = runtimeTerrain;

        var offset = terrain.transform.position;
        var size = runtimeTerrain.size;
        // Terrain rocks use tree prototypes too. Include their actual horizontal
        // extent so a rock centred just outside the house cannot cross its walls.
        var trees = new List<TreeInstance>();
        var prototypes = runtimeTerrain.treePrototypes;
        foreach (var tree in runtimeTerrain.treeInstances)
        {
            var point = offset + Vector3.Scale(tree.position, size);
            float radius = 0f;
            var prefab = prototypes[tree.prototypeIndex].prefab;
            if (prefab != null)
                foreach (var renderer in prefab.GetComponentsInChildren<Renderer>())
                    radius = Mathf.Max(radius, (renderer.bounds.center - prefab.transform.position).magnitude + renderer.bounds.extents.magnitude);
            radius *= tree.widthScale;
            if (point.x + radius < footprint.min.x || point.x - radius > footprint.max.x ||
                point.z + radius < footprint.min.z || point.z - radius > footprint.max.z)
                trees.Add(tree);
        }
        runtimeTerrain.SetTreeInstances(trees.ToArray(), true);

        int resolution = runtimeTerrain.detailResolution;
        if (resolution > 0)
        {
            var area = TerrainArea(footprint, offset, size, resolution, resolution - 1);
            for (int layer = 0; layer < runtimeTerrain.detailPrototypes.Length; layer++)
                runtimeTerrain.SetDetailLayer(area.x, area.y, layer, new int[area.height, area.width]);
        }

        resolution = runtimeTerrain.heightmapResolution;
        var heightsArea = TerrainArea(footprint, offset, size, resolution - 1, resolution - 1);
        var heights = runtimeTerrain.GetHeights(heightsArea.x, heightsArea.y, heightsArea.width, heightsArea.height);
        float ceiling = Mathf.Clamp01((floorHeight - offset.y) / size.y);
        for (int z = 0; z < heightsArea.height; z++)
            for (int x = 0; x < heightsArea.width; x++)
                heights[z, x] = Mathf.Min(heights[z, x], ceiling);
        runtimeTerrain.SetHeights(heightsArea.x, heightsArea.y, heights);
        terrain.Flush();
    }

    static RectInt TerrainArea(Bounds bounds, Vector3 offset, Vector3 size, int scale, int maxIndex)
    {
        int x = Mathf.Clamp(Mathf.FloorToInt((bounds.min.x - offset.x) / size.x * scale), 0, maxIndex);
        int z = Mathf.Clamp(Mathf.FloorToInt((bounds.min.z - offset.z) / size.z * scale), 0, maxIndex);
        int endX = Mathf.Clamp(Mathf.CeilToInt((bounds.max.x - offset.x) / size.x * scale), x, maxIndex);
        int endZ = Mathf.Clamp(Mathf.CeilToInt((bounds.max.z - offset.z) / size.z * scale), z, maxIndex);
        return new RectInt(x, z, endX - x + 1, endZ - z + 1);
    }

    void OnDestroy()
    {
        if (terrain != null && runtimeTerrain != null)
        {
            terrain.terrainData = originalTerrain;
            var collider = terrain.GetComponent<TerrainCollider>();
            if (collider != null) collider.terrainData = originalTerrain;
        }
        if (runtimeTerrain != null) Destroy(runtimeTerrain);
        if (furnitureFinishes != null)
            for (int i = 1; i < furnitureFinishes.Length; i++) Destroy(furnitureFinishes[i]);
        if (wood != null) Destroy(wood);
        if (metal != null) Destroy(metal);
    }
}
