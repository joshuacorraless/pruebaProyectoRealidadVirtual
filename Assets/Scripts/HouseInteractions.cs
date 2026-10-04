using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// arma al empezar lo que no viene en el modelo de Blender: las puertas, los muebles y el hueco del terreno.
// para el TerrainCollider hace falta com.unity.modules.terrainphysics en Packages/manifest.json
public class HouseInteractions : MonoBehaviour
{
    // modelo de la casa. si no se asigna se busca por nombre ("Apartamento")
    public Transform house;

    // terreno donde esta la casa. si no se asigna se usa el terreno activo
    public Terrain terrain;

    // brocha de las paredes. los muebles usan el mismo prefab de menu
    public MaterialChanger wallMaterials;

    // cuanto se despeja el terreno alrededor de la casa (metros)
    public float terrainMargin = 0.75f;

    Material wood;
    Material metal;

    // acabados que se pueden elegir para los muebles. el primero es wood
    Material[] furnitureFinishes;

    // el terreno se copia para no modificar el asset. al salir se devuelve el original
    TerrainData originalTerrain;
    TerrainData runtimeTerrain;

    void Start()
    {
        if (house == null)
        {
            GameObject houseObject = GameObject.Find("Apartamento");
            if (houseObject != null)
                house = houseObject.transform;
        }

        if (house == null) return;

        Renderer[] renderers = house.GetComponentsInChildren<Renderer>();

        // piso de la planta baja: de aqui salen el material base y las posiciones de los muebles
        Renderer floor = null;
        foreach (Renderer renderer in renderers)
        {
            if (renderer.name == "Losa baja")
                floor = renderer;

            // pared que pinta la brocha, si no se asigno a mano
            if (renderer.name == "Fachada baja.005" && wallMaterials != null && wallMaterials.targetRenderer == null)
                wallMaterials.targetRenderer = renderer;
        }

        if (floor == null) return;

        // se copia el material del piso para usar un shader que ya esta incluido en el build del Quest
        wood = CreateFinish(floor.sharedMaterial, "Natural mate", new Color(0.42f, 0.23f, 0.11f), 0.2f);

        metal = new Material(floor.sharedMaterial);
        metal.name = "Herrajes";
        metal.color = new Color(0.13f, 0.15f, 0.16f);

        furnitureFinishes = new Material[]
        {
            wood,
            CreateFinish(wood, "Oscuro satinado", new Color(0.12f, 0.055f, 0.025f), 0.5f),
            CreateFinish(wood, "Blanco mate", new Color(0.86f, 0.84f, 0.79f), 0.15f),
            CreateFinish(wood, "Negro satinado", new Color(0.035f, 0.04f, 0.045f), 0.45f)
        };

        CreateDoor(renderers, "baja");
        CreateDoor(renderers, "alta");

        // esquina del piso, para colocar los muebles midiendo desde ahi
        Vector3 corner = new Vector3(floor.bounds.min.x + 0.08f, floor.bounds.max.y, floor.bounds.min.z + 0.08f);
        CreateTable(corner + new Vector3(2.1f, 0.03f, 2.6f));
        CreateShelf(corner + new Vector3(3.1f, 0.03f, 5.5f));

        // area que ocupa la casa: piso, sala y gradas
        Bounds footprint = floor.bounds;
        foreach (Renderer renderer in renderers)
        {
            if (renderer.name == "Losa sala baja" || renderer.name.StartsWith("Peldano") || renderer.name == "Descanso superior")
                footprint.Encapsulate(renderer.bounds);
        }
        footprint.Expand(new Vector3(terrainMargin * 2f, 0f, terrainMargin * 2f));

        ClearTerrain(footprint, floor.bounds.max.y - 0.03f);
    }

    Material CreateFinish(Material source, string finishName, Color color, float smoothness)
    {
        Material material = new Material(source);
        material.name = finishName;
        material.color = color;
        material.SetFloat("_Smoothness", smoothness);
        return material;
    }

    // la puerta se arma entre los dos marcos de la entrada ("Marco entrada baja" y "Marco entrada baja.001")
    void CreateDoor(Renderer[] renderers, string level)
    {
        Renderer left = null;
        Renderer right = null;

        foreach (Renderer renderer in renderers)
        {
            if (renderer.name == "Marco entrada " + level)
                left = renderer;
            if (renderer.name == "Marco entrada " + level + ".001")
                right = renderer;
        }

        if (left == null || right == null) return;

        // el de la izquierda es el de menor x
        if (left.bounds.center.x > right.bounds.center.x)
        {
            Renderer temp = left;
            left = right;
            right = temp;
        }

        // un poco mas chica que el hueco para que no roce los marcos
        float width = right.bounds.min.x - left.bounds.max.x - 0.06f;
        float height = left.bounds.size.y - 0.065f;

        // el objeto de la puerta queda en el borde del marco izquierdo: ahi esta la bisagra
        GameObject door = new GameObject("Puerta planta " + level);
        door.transform.SetParent(transform, false);
        door.transform.position = new Vector3(
            left.bounds.max.x + 0.03f, left.bounds.min.y + 0.025f + height / 2f, left.bounds.center.z);

        CreatePart(door.transform, "Hoja", new Vector3(width / 2f, 0f, 0f), new Vector3(width, height, 0.045f), wood);
        CreatePart(door.transform, "Manilla exterior", new Vector3(width - 0.12f, -0.1f, -0.07f), new Vector3(0.16f, 0.04f, 0.1f), metal);
        CreatePart(door.transform, "Manilla interior", new Vector3(width - 0.12f, -0.1f, 0.07f), new Vector3(0.16f, 0.04f, 0.1f), metal);

        // kinematic: la puerta solo gira cuando HingedDoor la mueve
        Rigidbody body = door.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

        door.AddComponent<HingedDoor>();
    }

    void CreateTable(Vector3 position)
    {
        GameObject table = new GameObject("Mesa movible");
        table.transform.SetParent(transform, false);
        table.transform.position = position;

        CreatePart(table.transform, "Sobre", new Vector3(0f, 0.73f, 0f), new Vector3(1.2f, 0.08f, 0.75f), wood);

        // una pata en cada esquina
        float[] legX = { -0.49f, 0.49f };
        float[] legZ = { -0.27f, 0.27f };
        foreach (float x in legX)
        {
            foreach (float z in legZ)
                CreatePart(table.transform, "Pata", new Vector3(x, 0.345f, z), new Vector3(0.07f, 0.69f, 0.07f), metal);
        }

        MakeMovable(table);
    }

    void CreateShelf(Vector3 position)
    {
        GameObject shelf = new GameObject("Estante movible");
        shelf.transform.SetParent(transform, false);
        shelf.transform.position = position;

        float[] sideX = { -0.425f, 0.425f };
        foreach (float x in sideX)
            CreatePart(shelf.transform, "Lateral", new Vector3(x, 0.65f, 0f), new Vector3(0.05f, 1.3f, 0.35f), wood);

        float[] boardY = { 0.035f, 0.46f, 0.88f, 1.275f };
        foreach (float y in boardY)
            CreatePart(shelf.transform, "Repisa", new Vector3(0f, y, 0f), new Vector3(0.85f, 0.05f, 0.35f), wood);

        CreatePart(shelf.transform, "Fondo", new Vector3(0f, 0.65f, 0.165f), new Vector3(0.85f, 1.3f, 0.02f), wood);

        MakeMovable(shelf);
    }

    // cada pieza es un cubo estirado, position y size relativos al mueble
    void CreatePart(Transform parent, string partName, Vector3 position, Vector3 size, Material material)
    {
        GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
        part.name = partName;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = position;
        part.transform.localScale = size;
        part.GetComponent<Renderer>().sharedMaterial = material;
    }

    // deja el mueble listo para agarrarlo con Grip y cambiarle el acabado con el trigger
    void MakeMovable(GameObject furniture)
    {
        // caja que cubre todo el mueble. solo con los colliders de cada pieza, la baranda
        // de las gradas se mete entre las patas o las repisas y el mueble se queda trabado
        Bounds bounds = new Bounds(Vector3.zero, Vector3.zero);
        foreach (BoxCollider part in furniture.GetComponentsInChildren<BoxCollider>())
            bounds.Encapsulate(new Bounds(part.transform.localPosition, part.transform.localScale));

        BoxCollider box = furniture.AddComponent<BoxCollider>();
        box.center = bounds.center;
        box.size = bounds.size;

        // no se vuelca (rotacion x y z congeladas) y no sale volando al soltarlo
        Rigidbody body = furniture.AddComponent<Rigidbody>();
        body.mass = 8f;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        body.maxLinearVelocity = 2.5f;
        body.maxAngularVelocity = 2f;
        body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        // el XRGrabInteractable va de ultimo: al agregarlo registra los colliders que ya tiene el mueble
        XRGrabInteractable grab = furniture.AddComponent<XRGrabInteractable>();
        grab.movementType = XRBaseInteractable.MovementType.VelocityTracking;
        grab.limitLinearVelocity = true;
        grab.maxLinearVelocityDelta = 2.5f;
        grab.limitAngularVelocity = true;
        grab.maxAngularVelocityDelta = 2f;
        grab.useDynamicAttach = true;
        grab.throwOnDetach = false;

        if (wallMaterials == null) return;

        // solo las piezas de madera cambian de acabado, las de metal no
        List<Renderer> woodParts = new List<Renderer>();
        foreach (Renderer renderer in furniture.GetComponentsInChildren<Renderer>())
        {
            if (renderer.sharedMaterial == wood)
                woodParts.Add(renderer);
        }

        if (woodParts.Count == 0) return;

        MaterialChanger finishes = furniture.AddComponent<MaterialChanger>();
        finishes.radialMenuPrefab = wallMaterials.radialMenuPrefab;
        finishes.materials = furnitureFinishes;
        finishes.targetRenderer = woodParts[0];
        woodParts.RemoveAt(0);
        finishes.additionalRenderers = woodParts.ToArray();
    }

    // quita arboles, piedras y zacate de donde esta la casa, y baja el terreno para que no atraviese el piso
    void ClearTerrain(Bounds footprint, float floorHeight)
    {
        if (terrain == null)
            terrain = Terrain.activeTerrain;

        if (terrain == null || terrain.terrainData == null) return;

        originalTerrain = terrain.terrainData;
        runtimeTerrain = Instantiate(originalTerrain);
        runtimeTerrain.name = originalTerrain.name + " (copia)";
        terrain.terrainData = runtimeTerrain;

        TerrainCollider terrainCollider = terrain.GetComponent<TerrainCollider>();
        if (terrainCollider != null)
            terrainCollider.terrainData = runtimeTerrain;

        Vector3 origin = terrain.transform.position;
        Vector3 size = runtimeTerrain.size;

        // arboles: se quedan solo los que estan fuera. las piedras del terreno tambien son "arboles"
        List<TreeInstance> trees = new List<TreeInstance>();
        TreePrototype[] prototypes = runtimeTerrain.treePrototypes;

        foreach (TreeInstance tree in runtimeTerrain.treeInstances)
        {
            // la posicion del arbol viene de 0 a 1 dentro del terreno
            Vector3 point = origin + Vector3.Scale(tree.position, size);

            // se toma en cuenta lo ancho del arbol, porque uno sembrado apenas afuera igual puede meterse a la casa
            float radius = 0f;
            GameObject prefab = prototypes[tree.prototypeIndex].prefab;
            if (prefab != null)
            {
                foreach (Renderer renderer in prefab.GetComponentsInChildren<Renderer>())
                {
                    float reach = (renderer.bounds.center - prefab.transform.position).magnitude + renderer.bounds.extents.magnitude;
                    radius = Mathf.Max(radius, reach);
                }
            }
            radius *= tree.widthScale;

            bool outside = point.x + radius < footprint.min.x || point.x - radius > footprint.max.x ||
                           point.z + radius < footprint.min.z || point.z - radius > footprint.max.z;
            if (outside)
                trees.Add(tree);
        }

        runtimeTerrain.SetTreeInstances(trees.ToArray(), true);

        // zacate: se pone en 0 dentro del area en todas las capas de detalle
        int detailResolution = runtimeTerrain.detailResolution;
        if (detailResolution > 0)
        {
            RectInt area = TerrainArea(footprint, origin, size, detailResolution, detailResolution - 1);
            for (int layer = 0; layer < runtimeTerrain.detailPrototypes.Length; layer++)
                runtimeTerrain.SetDetailLayer(area.x, area.y, layer, new int[area.height, area.width]);
        }

        // alturas: dentro del area el terreno no puede quedar mas alto que el piso
        int heightResolution = runtimeTerrain.heightmapResolution;
        RectInt heightArea = TerrainArea(footprint, origin, size, heightResolution - 1, heightResolution - 1);
        float[,] heights = runtimeTerrain.GetHeights(heightArea.x, heightArea.y, heightArea.width, heightArea.height);

        // las alturas del terreno van de 0 a 1
        float maxHeight = Mathf.Clamp01((floorHeight - origin.y) / size.y);

        for (int z = 0; z < heightArea.height; z++)
        {
            for (int x = 0; x < heightArea.width; x++)
                heights[z, x] = Mathf.Min(heights[z, x], maxHeight);
        }

        runtimeTerrain.SetHeights(heightArea.x, heightArea.y, heights);
        terrain.Flush();
    }

    // pasa un area en metros a casillas del terreno. scale es cuantas casillas tiene el terreno de lado
    RectInt TerrainArea(Bounds bounds, Vector3 origin, Vector3 size, int scale, int maxIndex)
    {
        int x = Mathf.Clamp(Mathf.FloorToInt((bounds.min.x - origin.x) / size.x * scale), 0, maxIndex);
        int z = Mathf.Clamp(Mathf.FloorToInt((bounds.min.z - origin.z) / size.z * scale), 0, maxIndex);
        int endX = Mathf.Clamp(Mathf.CeilToInt((bounds.max.x - origin.x) / size.x * scale), x, maxIndex);
        int endZ = Mathf.Clamp(Mathf.CeilToInt((bounds.max.z - origin.z) / size.z * scale), z, maxIndex);

        return new RectInt(x, z, endX - x + 1, endZ - z + 1);
    }

    void OnDestroy()
    {
        // devuelve el terreno original y borra lo que se creo al empezar
        if (terrain != null && runtimeTerrain != null)
        {
            terrain.terrainData = originalTerrain;

            TerrainCollider terrainCollider = terrain.GetComponent<TerrainCollider>();
            if (terrainCollider != null)
                terrainCollider.terrainData = originalTerrain;
        }

        if (runtimeTerrain != null)
            Destroy(runtimeTerrain);

        if (furnitureFinishes != null)
        {
            foreach (Material finish in furnitureFinishes)
                Destroy(finish);
        }

        if (metal != null)
            Destroy(metal);
    }
}
