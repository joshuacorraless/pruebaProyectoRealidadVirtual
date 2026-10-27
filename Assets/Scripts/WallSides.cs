using System.Collections.Generic;
using UnityEngine;

// parte cada pared de la casa en dos lados (dos submeshes) para pintar por dentro y por fuera por separado.
// las mallas se leen al empezar: Apartamento.fbx necesita Read/Write en el Import Settings,
// y las paredes no pueden ser Batching Static porque Unity junta sus mallas al hacer el build
public class WallSides : MonoBehaviour
{
    // modelo de la casa. si no se asigna se busca por nombre ("Apartamento")
    public Transform house;

    // las piezas que tienen alguno de estos materiales son paredes (Repello marfil, Paredes interiores)
    public Material[] wallMaterials = new Material[0];

    // cuantos metros de pared cubre la textura antes de repetirse
    public float textureSize = 2f;

    // pared partida -> eje del grosor en local, apuntando hacia + del mundo. la cara que mira hacia ese eje es el slot 0
    readonly Dictionary<Renderer, Vector3> splitWalls = new Dictionary<Renderer, Vector3>();

    readonly List<Mesh> createdMeshes = new List<Mesh>();

    void Awake()
    {
        if (house == null)
        {
            GameObject houseObject = GameObject.Find("Apartamento");
            if (houseObject != null)
                house = houseObject.transform;
        }

        if (house == null) return;

        foreach (MeshRenderer renderer in house.GetComponentsInChildren<MeshRenderer>())
        {
            if (IsWall(renderer))
                Split(renderer);
        }
    }

    void Start()
    {
        // cada brocha pinta el lado de su pared que queda hacia ella. las demas paredes del grupo
        // estan en el mismo plano, asi que tienen ese lado en el mismo slot
        foreach (MaterialChanger changer in FindObjectsByType<MaterialChanger>(FindObjectsSortMode.None))
        {
            Renderer wall = changer.targetRenderer;
            if (wall == null || !splitWalls.TryGetValue(wall, out Vector3 axis)) continue;

            Vector3 side = wall.transform.TransformDirection(axis);
            Vector3 toChanger = changer.transform.position - wall.bounds.center;
            changer.materialSlot = Vector3.Dot(toChanger, side) >= 0f ? 0 : 1;
        }
    }

    bool IsWall(MeshRenderer renderer)
    {
        if (renderer.sharedMaterials.Length != 1) return false;

        Material material = renderer.sharedMaterial;
        foreach (Material wallMaterial in wallMaterials)
        {
            if (wallMaterial != null && material == wallMaterial)
                return true;
        }
        return false;
    }

    void Split(MeshRenderer renderer)
    {
        MeshFilter filter = renderer.GetComponent<MeshFilter>();
        if (filter == null || filter.sharedMesh == null) return;

        Mesh source = filter.sharedMesh;
        if (!source.isReadable || renderer.isPartOfStaticBatch)
        {
            Debug.LogWarning("WallSides: no se puede partir " + renderer.name +
                " (falta Read/Write en el modelo o la pared es Batching Static)", renderer);
            return;
        }

        // el grosor de la pared es el lado mas corto de la malla
        Vector3 size = source.bounds.size;
        Vector3 axis;
        if (size.x <= size.y && size.x <= size.z)
            axis = Vector3.right;
        else if (size.y <= size.z)
            axis = Vector3.up;
        else
            axis = Vector3.forward;

        // el slot 0 siempre es la cara que mira hacia +x, +y o +z del mundo, como venga girada la malla.
        // asi dos paredes del mismo plano tienen cada lado en el mismo slot
        Vector3 world = renderer.transform.TransformDirection(axis);
        float largest = Mathf.Abs(world.x) >= Mathf.Abs(world.z) ? world.x : world.z;
        if (Mathf.Abs(world.y) > Mathf.Abs(largest))
            largest = world.y;
        if (largest < 0f)
            axis = -axis;

        Vector3[] normals = source.normals;
        int[] triangles = source.triangles;
        List<int> front = new List<int>();
        List<int> back = new List<int>();

        for (int i = 0; i < triangles.Length; i += 3)
        {
            Vector3 normal = normals[triangles[i]] + normals[triangles[i + 1]] + normals[triangles[i + 2]];

            // los cantos, arriba y abajo no miran a ningun lado: se quedan con el slot 0
            List<int> side = Vector3.Dot(normal.normalized, axis) < -0.5f ? back : front;
            side.Add(triangles[i]);
            side.Add(triangles[i + 1]);
            side.Add(triangles[i + 2]);
        }

        if (front.Count == 0 || back.Count == 0) return;

        Mesh mesh = Instantiate(source);
        mesh.name = source.name + " (dos lados)";
        mesh.subMeshCount = 2;
        mesh.SetTriangles(front, 0);
        mesh.SetTriangles(back, 1);
        mesh.uv = WorldUVs(source, renderer.transform);
        createdMeshes.Add(mesh);

        // el MeshCollider se queda con la malla original, no hace falta cambiarlo
        filter.sharedMesh = mesh;

        Material material = renderer.sharedMaterial;
        renderer.sharedMaterials = new Material[] { material, material };

        splitWalls.Add(renderer, axis);
    }

    // las UVs del modelo son las del cubo de Blender: cada cara usa el mismo cuadro de la textura, mida lo que mida.
    // aqui la textura se proyecta segun la posicion en metros, asi todas las paredes tienen la misma escala
    // y las que estan en el mismo plano siguen la textura sin cortes
    Vector2[] WorldUVs(Mesh source, Transform wall)
    {
        Vector3[] vertices = source.vertices;
        Vector3[] normals = source.normals;
        Vector2[] uvs = new Vector2[vertices.Length];

        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 point = wall.TransformPoint(vertices[i]);
            Vector3 normal = wall.TransformDirection(normals[i]);
            float x = Mathf.Abs(normal.x);
            float y = Mathf.Abs(normal.y);
            float z = Mathf.Abs(normal.z);

            // se proyecta desde el lado hacia el que mira la cara
            Vector2 uv;
            if (x >= y && x >= z)
                uv = new Vector2(point.z, point.y);
            else if (z >= y)
                uv = new Vector2(point.x, point.y);
            else
                uv = new Vector2(point.x, point.z);

            uvs[i] = uv / textureSize;
        }

        return uvs;
    }

    void OnDestroy()
    {
        foreach (Mesh mesh in createdMeshes)
            Destroy(mesh);
    }
}
