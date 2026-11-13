using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// flecha amarilla plana que flota encima de la casa que se puede personalizar (la de las brochas).
// en las pruebas la gente no sabia cual de las 4 casas se podia cambiar: las otras 3 son de muestra.
// se crea sola en cualquier escena que tenga "Plano nuevo/Casa 1" (no hay que agregarla a la escena)
public class CasaPersonalizable : MonoBehaviour
{
    // casa que se puede modificar, dentro de "Plano nuevo"
    public Transform house;

    public Color color = new Color(1f, 0.82f, 0.1f);

    // borde oscuro alrededor de la flecha, para que se vea contra el cielo claro
    public Color outlineColor = new Color(0.08f, 0.08f, 0.08f);

    // cuanto sube y baja la flecha (metros) y que tan rapido
    public float bobHeight = 0.25f;
    public float bobSpeed = 2f;

    const string DefaultHouse = "Plano nuevo/Casa 1";

    Transform arrow;
    Material material;
    Mesh arrowMesh;
    Bounds houseBounds;
    Vector3 arrowBase;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Register()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        CreateIfNeeded();
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        CreateIfNeeded();
    }

    static void CreateIfNeeded()
    {
        if (FindFirstObjectByType<CasaPersonalizable>() != null) return;

        GameObject target = GameObject.Find(DefaultHouse);
        if (target == null) return;

        GameObject marker = new GameObject("Marca casa personalizable");
        marker.AddComponent<CasaPersonalizable>().house = target.transform;
    }

    void Start()
    {
        if (house == null)
        {
            GameObject target = GameObject.Find(DefaultHouse);
            if (target != null)
                house = target.transform;
        }

        if (house == null || !MeasureHouse())
        {
            enabled = false;
            return;
        }

        // Sprites/Default esta en los shaders que siempre se incluyen en el build y se ve por los dos lados
        material = new Material(Shader.Find("Sprites/Default"));

        // la punta queda un poco arriba del techo, en el centro de la casa
        arrowBase = new Vector3(houseBounds.center.x, houseBounds.max.y + 0.6f, houseBounds.center.z);

        arrowMesh = BuildArrowMesh(1.8f, 0.8f, 0.55f, 0.2f, 0.07f);
        arrow = new GameObject("Flecha", typeof(MeshFilter), typeof(MeshRenderer)).transform;
        arrow.SetParent(transform, false);
        arrow.position = arrowBase;
        arrow.GetComponent<MeshFilter>().sharedMesh = arrowMesh;

        MeshRenderer renderer = arrow.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    // caja de todo lo que se ve de la casa
    bool MeasureHouse()
    {
        bool found = false;
        foreach (Renderer renderer in house.GetComponentsInChildren<Renderer>())
        {
            if (!found)
            {
                houseBounds = renderer.bounds;
                found = true;
            }
            else
            {
                houseBounds.Encapsulate(renderer.bounds);
            }
        }
        return found;
    }

    void Update()
    {
        arrow.position = arrowBase + Vector3.up * (Mathf.Sin(Time.time * bobSpeed) * bobHeight);

        Camera view = Camera.main;
        if (view == null) return;

        // es plana: siempre de frente al jugador (solo de lado, sigue apuntando hacia abajo) para que no se vea de canto
        Vector3 toArrow = Vector3.ProjectOnPlane(arrow.position - view.transform.position, Vector3.up);
        if (toArrow.sqrMagnitude > 0.01f)
            arrow.rotation = Quaternion.LookRotation(toArrow, Vector3.up);

        // adentro de la casa no hace falta, y asi no se ve por alguna ventana o hueco del techo
        Vector3 head = view.transform.position;
        bool inside = head.x > houseBounds.min.x && head.x < houseBounds.max.x &&
                      head.z > houseBounds.min.z && head.z < houseBounds.max.z &&
                      head.y < houseBounds.max.y;
        arrow.gameObject.SetActive(!inside);
    }

    // flecha plana apuntando hacia abajo con la punta en el origen: triangulo abajo y palo encima.
    // primero va el borde oscuro (un poco mas grande) y encima la flecha amarilla: el shader dibuja en ese orden
    Mesh BuildArrowMesh(float totalHeight, float headHeight, float headHalfWidth, float shaftHalfWidth, float border)
    {
        List<Vector3> vertices = new List<Vector3>();
        List<Color> colors = new List<Color>();
        List<int> triangles = new List<int>();

        void Shape(float grow, Color shapeColor)
        {
            // el borde baja la punta un poco mas, para que quede del mismo grosor en la diagonal
            float tipY = -grow * 2f;
            float headW = headHalfWidth + grow * 2f;
            float shaftW = shaftHalfWidth + grow;
            float topY = totalHeight + grow;
            float headY = headHeight - grow * 0.5f;

            Vector3[] points =
            {
                new Vector3(0f, tipY, 0f),          // punta
                new Vector3(-headW, headY, 0f),     // esquinas del triangulo
                new Vector3(headW, headY, 0f),
                new Vector3(-shaftW, headY, 0f),    // palo
                new Vector3(shaftW, headY, 0f),
                new Vector3(-shaftW, topY, 0f),
                new Vector3(shaftW, topY, 0f)
            };

            int start = vertices.Count;
            foreach (Vector3 point in points)
            {
                vertices.Add(point);
                colors.Add(shapeColor);
            }

            int[] shape = { 0, 1, 2, 3, 5, 6, 3, 6, 4 };
            foreach (int index in shape)
                triangles.Add(start + index);
        }

        Shape(border, outlineColor);
        Shape(0f, color);

        Mesh mesh = new Mesh();
        mesh.name = "Flecha casa personalizable";
        mesh.SetVertices(vertices);
        mesh.SetColors(colors);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    void OnDestroy()
    {
        if (material != null) Destroy(material);
        if (arrowMesh != null) Destroy(arrowMesh);
    }
}
