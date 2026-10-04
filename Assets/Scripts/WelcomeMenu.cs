using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;
using UnityEngine.XR.Interaction.Toolkit.UI;

public class WelcomeMenu : MonoBehaviour
{
    // menu de la hora, para abrirlo desde el boton de este cartel
    public ButtonInputDetector dayNightControls;

    // a donde lleva el boton "Comenzar"
    public Vector3 tourStart = new Vector3(3.5f, 0f, -3.5f);

    // que tan lejos del jugador aparece el cartel (metros)
    public float spawnDistance = 2.4f;

    // boton B del control derecho, o F1 en el teclado
    InputAction toggleAction = new InputAction("Ayuda", InputActionType.Button);

    // canvas del cartel, se arma en Start
    GameObject panel;

    TeleportationProvider teleportation;

    // textos de las 6 secciones, cambian entre Quest y simulador
    TextMeshProUGUI[] instructions = new TextMeshProUGUI[6];
    Button questTab;
    Button simulatorTab;

    void Awake()
    {
        toggleAction.AddBinding("<XRController>{RightHand}/secondaryButton");
        toggleAction.AddBinding("<Keyboard>/f1");
    }

    void OnEnable()
    {
        toggleAction.Enable();
    }

    void OnDisable()
    {
        toggleAction.Disable();
        CloseMenu();
    }

    void OnDestroy()
    {
        toggleAction.Dispose();
    }

    IEnumerator Start()
    {
        // espera un frame a que el visor ya tenga la posicion de la cabeza. despues el cartel se queda fijo
        yield return null;

        Camera head = Camera.main;
        if (head == null) yield break;

        teleportation = FindFirstObjectByType<TeleportationProvider>();

        BuildPanel(head);
        ShowControls(Application.isEditor);
    }

    void Update()
    {
        if (panel == null || !toggleAction.WasPressedThisFrame()) return;

        if (panel.activeSelf)
        {
            CloseMenu();
            return;
        }

        CloseOtherMenus();

        // el cartel no se mueve, solo se voltea hacia donde esta el jugador ahora
        if (Camera.main != null)
        {
            Vector3 direction = Vector3.ProjectOnPlane(
                panel.transform.position - Camera.main.transform.position, Vector3.up);
            if (direction.sqrMagnitude > 0.01f)
                panel.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
        }

        panel.SetActive(true);
    }

    public void CloseMenu()
    {
        if (panel != null)
            panel.SetActive(false);
    }

    void CloseOtherMenus()
    {
        if (dayNightControls != null)
            dayNightControls.CloseMenu();

        MaterialChanger.CloseActiveMenu();
    }

    // boton "Comenzar": cierra todo y teletransporta al inicio del recorrido
    void BeginTour()
    {
        CloseOtherMenus();

        if (teleportation != null)
        {
            TeleportRequest request = new TeleportRequest();
            request.destinationPosition = tourStart;
            request.destinationRotation = Quaternion.identity;
            request.matchOrientation = MatchOrientation.TargetUpAndForward;
            teleportation.QueueTeleportRequest(request);
        }

        CloseMenu();
    }

    // boton de la hora: cambia este cartel por el menu de la hora
    void OpenDayNight()
    {
        CloseMenu();
        MaterialChanger.CloseActiveMenu();

        if (dayNightControls != null && !dayNightControls.IsOpen)
            dayNightControls.ToggleMenu();
    }

    void BuildPanel(Camera head)
    {
        panel = new GameObject("Bienvenida", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler),
            typeof(TrackedDeviceGraphicRaycaster));
        panel.transform.SetParent(transform, false);

        // canvas en world space: necesita la camara para los eventos de UI
        Canvas canvas = panel.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = head;

        // sin esto el texto se ve borroso de cerca
        panel.GetComponent<CanvasScaler>().dynamicPixelsPerUnit = 10f;

        // 1200 x 900 unidades con escala 0.0018: un cartel de 2.16 x 1.62 metros
        RectTransform rect = (RectTransform)panel.transform;
        rect.sizeDelta = new Vector2(1200f, 900f);
        rect.localScale = Vector3.one * 0.0018f;

        // enfrente del jugador y derecho, aunque este mirando hacia arriba o hacia abajo
        Vector3 forward = Vector3.ProjectOnPlane(head.transform.forward, Vector3.up).normalized;
        if (forward.sqrMagnitude < 0.01f)
            forward = Vector3.forward;

        rect.position = head.transform.position + forward * spawnDistance;
        rect.rotation = Quaternion.LookRotation(forward, Vector3.up);

        GameObject background = new GameObject("Fondo", typeof(RectTransform), typeof(Image));
        background.transform.SetParent(rect, false);
        ((RectTransform)background.transform).sizeDelta = rect.sizeDelta;
        background.GetComponent<Image>().color = MenuStyle.Background;

        // encabezado: titulo a la izquierda y botones Quest / Simulador a la derecha
        MenuStyle.Label(rect, "Proyecto Realidad Virtual - Avance 02", new Vector2(0f, 387f), new Vector2(1080f, 64f), 46f);

        TextMeshProUGUI subtitle = MenuStyle.Label(rect, "Cómo usarlo", new Vector2(-270f, 333f), new Vector2(540f, 38f), 25f);
        subtitle.color = MenuStyle.Muted;

        questTab = CreateButton(rect, "Quest", new Vector2(245f, 333f), new Vector2(180f, 48f), ShowQuestControls);
        simulatorTab = CreateButton(rect, "Simulador", new Vector2(445f, 333f), new Vector2(180f, 48f), ShowSimulatorControls);

        // linea que separa el encabezado de las instrucciones
        GameObject separator = new GameObject("Separador", typeof(RectTransform), typeof(Image));
        separator.transform.SetParent(rect, false);

        Image line = separator.GetComponent<Image>();
        line.rectTransform.sizeDelta = new Vector2(1080f, 2f);
        line.rectTransform.anchoredPosition = new Vector2(0f, 294f);
        line.color = MenuStyle.Surface;
        line.raycastTarget = false;

        // 6 secciones en 2 columnas de 3
        string[] headings =
        {
            "Moverse", "Puertas y muebles", "Ver acabados",
            "Cambiar el acabado", "Hora del día", "Ayuda y teletransporte"
        };

        for (int i = 0; i < headings.Length; i++)
        {
            float x = i < 3 ? -280f : 280f;
            float y = 256f - (i % 3) * 158f;

            TextMeshProUGUI heading = MenuStyle.Label(rect, headings[i], new Vector2(x, y), new Vector2(520f, 30f), 26f);
            heading.color = MenuStyle.Accent;

            instructions[i] = MenuStyle.Label(rect, "", new Vector2(x, y - 79f), new Vector2(520f, 118f), 23f,
                TextAlignmentOptions.TopLeft);
        }

        // botones de abajo. "Comenzar" va en amarillo porque es el principal
        Button begin = CreateButton(rect, "Comenzar", new Vector2(-370f, -246f), new Vector2(340f, 64f), BeginTour);
        begin.image.color = MenuStyle.Accent;
        begin.GetComponentInChildren<TextMeshProUGUI>().color = MenuStyle.Background;

        CreateButton(rect, "Hora del día", new Vector2(0f, -246f), new Vector2(340f, 64f), OpenDayNight);
        CreateButton(rect, "Cerrar ayuda", new Vector2(370f, -246f), new Vector2(340f, 64f), CloseMenu);

        TextMeshProUGUI note = MenuStyle.Label(rect,
            "La ayuda queda aquí, donde empezó\nÁbrala con B en Quest o F1 en el simulador",
            new Vector2(-230f, -346f), new Vector2(620f, 100f), 22f);
        note.color = MenuStyle.Muted;

        MenuStyle.Label(rect,
            "<size=19>Grupo</size>\nAdrián Mora Rivera\nFelipe Lepiz Retana\nJoshua Corrales Retana",
            new Vector2(350f, -359f), new Vector2(380f, 118f), 23f, TextAlignmentOptions.TopRight);
    }

    void ShowQuestControls()
    {
        ShowControls(false);
    }

    void ShowSimulatorControls()
    {
        ShowControls(true);
    }

    // llena las 6 secciones con los controles del visor o los del simulador (teclado)
    void ShowControls(bool simulator)
    {
        string[] content;

        if (simulator)
        {
            content = new string[]
            {
                "En Play, haga clic en Game\nMantenga clic derecho para mirar\nCamine con Shift + I, J, K o L\nSi falta el mando derecho, presione ]",
                "Apunte a la puerta o mueble desde lejos\nMantenga G y mueva el mouse\nSin clic derecho; I aleja y K acerca\nJ y L giran; suelte G para dejarlo",
                "Apunte a la brocha y presione T\nPara un mueble, suelte G y presione T\nSuelte T antes de escoger el acabado",
                "Apunte a la muestra y presione T\nPase de página con J y L\nLa muestra del centro es el acabado original\nHay 36 acabados de pared y 4 de muebles",
                "Abra o cierre el menú con 1\nCambie la hora con J y L\nApunte al dial y presione T: día o noche\nUse los números de la fila superior",
                "F1 o 2 abren o cierran esta ayuda\nMantenga I y apunte al piso\nCuando el arco esté azul, suelte I\nCierre los menús antes de moverse"
            };
        }
        else
        {
            content = new string[]
            {
                "Stick izquierdo para caminar\nStick derecho a los lados para girar\nUse el rayo del mando para apuntar",
                "Grip es el botón del costado del mando\nApunte, mantenga Grip y mueva la mano\nSuelte Grip para dejar la puerta\no el mueble en su lugar",
                "Trigger es el gatillo del dedo índice\nApunte a la brocha y presione el gatillo\nEn un mueble, haga lo mismo\nPrimero suelte Grip si lo tiene agarrado",
                "Apunte a la muestra y presione el gatillo\nPase de página con el stick a los lados\nLa muestra del centro es el acabado original\nGatillo fuera de las muestras: cierra el menú",
                "Abra o cierre el menú con A\nMueva el stick derecho para cambiar la hora\nApunte al dial y presione el gatillo\npara alternar entre día y noche",
                "B del mando derecho abre esta ayuda\nMantenga el stick derecho hacia arriba\nApunte al piso; con el arco azul, suéltelo\nCierre los menús antes de moverse"
            };
        }

        for (int i = 0; i < instructions.Length; i++)
            instructions[i].text = content[i];

        // el boton elegido queda claro con el texto oscuro, el otro al reves
        SetTabSelected(questTab, !simulator);
        SetTabSelected(simulatorTab, simulator);
    }

    void SetTabSelected(Button tab, bool selected)
    {
        tab.image.color = selected ? MenuStyle.Text : MenuStyle.Surface;
        tab.GetComponentInChildren<TextMeshProUGUI>().color = selected ? MenuStyle.Background : MenuStyle.Text;
    }

    Button CreateButton(Transform parent, string title, Vector2 position, Vector2 size, UnityAction onClick)
    {
        GameObject buttonObject = new GameObject(title, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);

        RectTransform rect = (RectTransform)buttonObject.transform;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        Image image = buttonObject.GetComponent<Image>();
        image.color = MenuStyle.Surface;

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;

        // el color del boton se multiplica por estos: se aclara cuando el rayo le apunta y se oscurece al presionar
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(1.12f, 1.12f, 1.12f);
        colors.pressedColor = new Color(0.8f, 0.8f, 0.8f);
        colors.selectedColor = Color.white;
        button.colors = colors;

        button.navigation = new Navigation { mode = Navigation.Mode.None };
        button.onClick.AddListener(onClick);

        MenuStyle.Label(rect, title, Vector2.zero, size, 25f, TextAlignmentOptions.Center);

        return button;
    }
}
