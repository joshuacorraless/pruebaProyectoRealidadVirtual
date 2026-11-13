using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

// cinta metrica con el mando izquierdo: X marca el punto A donde apunta el rayo y la cinta se estira hasta donde se
// apunte despues (punto B). se puede usar de dos formas:
//  - mantener X, apuntar al punto B y soltar
//  - tocar X en el punto A y tocarlo otra vez en el punto B
// Y borra la medida. la cinta se ve amarilla y punteada y el numero de metros va cambiando mientras se estira.
// se crea sola al empezar (no hay que agregarla a la escena); si ya hay una en la escena se usa esa
public class CintaMetrica : MonoBehaviour
{
    // X y Y del mando izquierdo, que no se usaban para nada
    InputAction markAction = new InputAction("MarcarCinta", InputActionType.Button, "<XRController>{LeftHand}/primaryButton");
    InputAction clearAction = new InputAction("BorrarCinta", InputActionType.Button, "<XRController>{LeftHand}/secondaryButton");

    // mando desde donde sale el rayo. si no se asigna se busca "Left Controller" del XR Origin
    public Transform controller;

    // el plano de Revit se agrando 1.25 veces en Unity para caminar mas comodo (ArmarPlanoNuevo), asi que cada metro
    // del mundo no es un metro del plano. con 0 se lee la escala de "Plano nuevo" y se mide en metros del plano.
    // con 1 se mide en metros del mundo. los muebles no se agrandaron: medidos con la escala del plano salen mas chicos
    public float worldScale = 0f;

    // hasta donde llega el rayo (metros del mundo)
    public float maxDistance = 40f;

    // amarillo de la cinta, el mismo de las manillas
    public Color tapeColor = new Color(1f, 0.82f, 0.1f);

    // grosor de la cinta y cuantas rayitas lleva por metro del mundo
    public float tapeWidth = 0.012f;
    public float dashesPerMeter = 8f;

    // si se suelta X antes de este tiempo se toma como un toque (el punto B se marca con el siguiente toque)
    const float TapTime = 0.3f;

    enum State { Idle, Stretching, Fixed }
    State state = State.Idle;

    Vector3 pointA;
    Vector3 pointB;
    float pressTime;

    // si el rayo no pega en nada, el punto queda en el aire a la ultima distancia a la que pego
    float lastHitDistance = 1.5f;

    float nextControllerSearch;

    LineRenderer line;
    Transform markerA;
    Transform markerB;
    Transform labelPivot;
    TextMeshPro label;
    Material lineMaterial;
    Material markerMaterial;
    Texture2D dashTexture;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void CreateOnStart()
    {
        if (FindFirstObjectByType<CintaMetrica>() != null) return;

        GameObject tape = new GameObject("Cinta metrica");
        tape.AddComponent<CintaMetrica>();
        DontDestroyOnLoad(tape);
    }

    void Awake()
    {
        BuildVisuals();
        SetVisible(false);
    }

    void OnEnable()
    {
        markAction.Enable();
        clearAction.Enable();
    }

    void OnDisable()
    {
        markAction.Disable();
        clearAction.Disable();
    }

    void Update()
    {
        // el XR Origin puede aparecer despues o cambiar de escena: se busca de vez en cuando hasta encontrarlo
        if (controller == null || !controller.gameObject.activeInHierarchy)
        {
            if (Time.unscaledTime < nextControllerSearch) return;
            nextControllerSearch = Time.unscaledTime + 1f;
            controller = FindController();
            if (controller == null) return;
        }

        if (clearAction.WasPressedThisFrame())
        {
            state = State.Idle;
            SetVisible(false);
            return;
        }

        if (markAction.WasPressedThisFrame())
        {
            if (state == State.Stretching)
            {
                // segundo toque: aqui queda el punto B
                pointB = AimPoint();
                state = State.Fixed;
            }
            else
            {
                pointA = AimPoint();
                pointB = pointA;
                pressTime = Time.unscaledTime;
                state = State.Stretching;
                SetVisible(true);
            }
        }
        else if (markAction.WasReleasedThisFrame() && state == State.Stretching && Time.unscaledTime - pressTime > TapTime)
        {
            // se mantuvo X mientras se estiraba: al soltar queda el punto B
            pointB = AimPoint();
            state = State.Fixed;
        }

        if (state == State.Stretching)
            pointB = AimPoint();

        if (state != State.Idle)
            UpdateVisuals();
    }

    Transform FindController()
    {
        foreach (Transform candidate in FindObjectsByType<Transform>(FindObjectsSortMode.None))
        {
            if (candidate.name == "Left Controller" && candidate.gameObject.activeInHierarchy)
                return candidate;
        }
        return null;
    }

    // punto donde pega el rayo del mando (paredes, pisos, muebles). los triggers no cuentan
    Vector3 AimPoint()
    {
        Ray ray = new Ray(controller.position, controller.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, maxDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            lastHitDistance = hit.distance;
            return hit.point;
        }

        return ray.GetPoint(Mathf.Clamp(lastHitDistance, 0.3f, 5f));
    }

    float CurrentScale()
    {
        if (worldScale > 0f) return worldScale;

        GameObject plan = GameObject.Find("Plano nuevo");
        if (plan == null) return 1f;

        float scale = plan.transform.lossyScale.x;
        return scale > 0.01f ? scale : 1f;
    }

    void UpdateVisuals()
    {
        line.SetPosition(0, pointA);
        line.SetPosition(1, pointB);
        markerA.position = pointA;
        markerB.position = pointB;

        float meters = Vector3.Distance(pointA, pointB) / CurrentScale();
        label.text = meters.ToString("0.00", CultureInfo.InvariantCulture) + " m";

        // el numero va en medio de la cinta, un poco arriba, mirando al jugador y del mismo tamano a cualquier distancia
        Camera view = Camera.main;
        Vector3 middle = (pointA + pointB) / 2f;
        if (view != null)
        {
            Vector3 toLabel = middle - view.transform.position;
            float distance = Mathf.Max(toLabel.magnitude, 0.3f);
            float size = 0.45f * distance;

            labelPivot.localScale = Vector3.one * size;
            labelPivot.position = middle + Vector3.up * 0.06f * size - toLabel.normalized * 0.05f;
            labelPivot.rotation = Quaternion.LookRotation(toLabel, Vector3.up);
        }
        else
        {
            labelPivot.position = middle + Vector3.up * 0.05f;
        }
    }

    void SetVisible(bool visible)
    {
        line.enabled = visible;
        markerA.gameObject.SetActive(visible);
        markerB.gameObject.SetActive(visible);
        labelPivot.gameObject.SetActive(visible);
    }

    void BuildVisuals()
    {
        // rayitas: un pixel blanco y uno transparente que se repiten a lo largo de la cinta. el color lo pone la linea.
        // Sprites/Default esta en los shaders que siempre se incluyen en el build
        dashTexture = new Texture2D(2, 1, TextureFormat.RGBA32, false);
        dashTexture.SetPixels(new[] { Color.white, Color.clear });
        dashTexture.filterMode = FilterMode.Point;
        dashTexture.wrapMode = TextureWrapMode.Repeat;
        dashTexture.Apply();

        Shader unlit = Shader.Find("Sprites/Default");

        lineMaterial = new Material(unlit);
        lineMaterial.mainTexture = dashTexture;

        line = gameObject.AddComponent<LineRenderer>();
        line.sharedMaterial = lineMaterial;
        line.useWorldSpace = true;
        line.positionCount = 2;
        line.widthMultiplier = tapeWidth;
        line.startColor = tapeColor;
        line.endColor = tapeColor;
        line.textureMode = LineTextureMode.Tile;
        line.textureScale = new Vector2(dashesPerMeter, 1f);
        line.numCapVertices = 0;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;

        markerMaterial = new Material(unlit);
        markerMaterial.color = tapeColor;
        markerA = CreateMarker("Punto A");
        markerB = CreateMarker("Punto B");

        labelPivot = new GameObject("Metros").transform;
        labelPivot.SetParent(transform, false);

        label = new GameObject("Texto", typeof(TextMeshPro)).GetComponent<TextMeshPro>();
        label.transform.SetParent(labelPivot, false);
        label.font = MenuStyle.GetFont();
        label.fontSize = 1f;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.rectTransform.sizeDelta = new Vector2(2f, 0.5f);
        label.color = tapeColor;
        label.outlineWidth = 0.3f;
        label.outlineColor = new Color32(20, 20, 20, 255);
    }

    // bolita en cada punta para que se vea donde quedo marcado
    Transform CreateMarker(string markerName)
    {
        GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        marker.name = markerName;
        Destroy(marker.GetComponent<Collider>());
        marker.transform.SetParent(transform, false);
        marker.transform.localScale = Vector3.one * 0.03f;

        Renderer renderer = marker.GetComponent<Renderer>();
        renderer.sharedMaterial = markerMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return marker.transform;
    }

    void OnDestroy()
    {
        markAction.Dispose();
        clearAction.Dispose();

        if (lineMaterial != null) Destroy(lineMaterial);
        if (markerMaterial != null) Destroy(markerMaterial);
        if (dashTexture != null) Destroy(dashTexture);
    }
}
