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
    public ButtonInputDetector dayNightControls;
    public Vector3 tourStart = new Vector3(3.5f, 0f, -3.5f);

    readonly InputAction toggle = new InputAction("Ayuda", InputActionType.Button);
    GameObject panel;
    TeleportationProvider teleportation;
    readonly TextMeshProUGUI[] instructions = new TextMeshProUGUI[6];
    Button questTab;
    Button simulatorTab;

    void Awake()
    {
        toggle.AddBinding("<XRController>{RightHand}/secondaryButton");
        toggle.AddBinding("<Keyboard>/f1");
    }

    void OnEnable() => toggle.Enable();

    IEnumerator Start()
    {
        // Esperar el primer seguimiento de la cabeza; después el cartel queda fijo.
        yield return null;
        var camera = Camera.main;
        if (camera == null) yield break;
        teleportation = FindFirstObjectByType<TeleportationProvider>();

        panel = new GameObject("Bienvenida", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler),
            typeof(TrackedDeviceGraphicRaycaster));
        panel.transform.SetParent(transform, false);
        var canvas = panel.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = camera;
        var rect = (RectTransform)panel.transform;
        panel.GetComponent<CanvasScaler>().dynamicPixelsPerUnit = 10f;
        rect.sizeDelta = new Vector2(1200f, 900f);
        rect.localScale = Vector3.one * 0.0018f;
        var forward = Vector3.ProjectOnPlane(camera.transform.forward, Vector3.up).normalized;
        if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
        rect.position = camera.transform.position + forward * 2.4f;
        rect.rotation = Quaternion.LookRotation(forward, Vector3.up);

        var background = new GameObject("Fondo", typeof(RectTransform), typeof(Image));
        background.transform.SetParent(rect, false);
        var backgroundRect = (RectTransform)background.transform;
        backgroundRect.sizeDelta = rect.sizeDelta;
        background.GetComponent<Image>().color = MenuStyle.Background;

        MenuStyle.Label(rect, "Proyecto Realidad Virtual - Avance 02", new Vector2(0, 387),
            new Vector2(1080, 64), 46);
        MenuStyle.Label(rect, "Cómo usarlo", new Vector2(-270, 333),
            new Vector2(540, 38), 25).color = MenuStyle.Muted;
        questTab = MakeButton(rect, "Quest", new Vector2(245, 333), new Vector2(180, 48),
            () => ShowControls(false));
        simulatorTab = MakeButton(rect, "Simulador", new Vector2(445, 333), new Vector2(180, 48),
            () => ShowControls(true));

        var separator = new GameObject("Separador", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        separator.transform.SetParent(rect, false);
        separator.rectTransform.sizeDelta = new Vector2(1080, 2);
        separator.rectTransform.anchoredPosition = new Vector2(0, 294);
        separator.color = MenuStyle.Surface;
        separator.raycastTarget = false;

        string[] headings = { "Moverse", "Puertas y muebles", "Ver acabados", "Cambiar el acabado", "Hora del día", "Ayuda y teletransporte" };
        for (int i = 0; i < headings.Length; i++)
        {
            float x = i < 3 ? -280f : 280f;
            float y = 256f - (i % 3) * 158f;
            MenuStyle.Label(rect, headings[i], new Vector2(x, y), new Vector2(520, 30), 26).color = MenuStyle.Accent;
            instructions[i] = MenuStyle.Label(rect, "", new Vector2(x, y - 79),
                new Vector2(520, 118), 23, TextAlignmentOptions.TopLeft);
        }

        var begin = MakeButton(rect, "Comenzar", new Vector2(-370, -246), new Vector2(340, 64), BeginTour);
        begin.image.color = MenuStyle.Accent;
        begin.GetComponentInChildren<TextMeshProUGUI>().color = MenuStyle.Background;
        MakeButton(rect, "Hora del día", new Vector2(0, -246), new Vector2(340, 64), OpenDayNight);
        MakeButton(rect, "Cerrar ayuda", new Vector2(370, -246), new Vector2(340, 64), CloseMenu);
        MenuStyle.Label(rect, "La ayuda queda aquí, donde empezó\nÁbrala con B en Quest o F1 en el simulador",
            new Vector2(-230, -346), new Vector2(620, 100), 22).color = MenuStyle.Muted;
        MenuStyle.Label(rect, "<size=19>Grupo</size>\nAdrián Mora Rivera\nFelipe Lepiz Retana\nJoshua Corrales Retana",
            new Vector2(350, -359), new Vector2(380, 118), 23, TextAlignmentOptions.TopRight);
        ShowControls(Application.isEditor);
    }

    void ShowControls(bool simulator)
    {
        string[] content = simulator ? new[]
        {
            "En Play, haga clic en Game\nMantenga clic derecho para mirar\nCamine con Shift + I, J, K o L\nSi falta el mando derecho, presione ]",
            "Apunte a la puerta o mueble desde lejos\nMantenga G y mueva el mouse\nSin clic derecho; I aleja y K acerca\nJ y L giran; suelte G para dejarlo",
            "Apunte a la brocha y presione T\nPara un mueble, suelte G y presione T\nSuelte T antes de escoger el acabado",
            "Apunte a la muestra y presione T\nUse Anterior o Siguiente para ver más\nTambién puede pasar de página con 3\nHay 36 acabados de pared y 4 de muebles",
            "Abra o cierre el menú con 1\nCambie la hora con J y L\nApunte al dial y presione T: día o noche\nUse los números de la fila superior",
            "F1 o 2 abren o cierran esta ayuda\nMantenga I y apunte al piso\nCuando el arco esté azul, suelte I\nCierre los menús antes de moverse"
        } : new[]
        {
            "Stick izquierdo para caminar\nStick derecho a los lados para girar\nUse el rayo del mando para apuntar",
            "Grip es el botón del costado del mando\nApunte, mantenga Grip y mueva la mano\nSuelte Grip para dejar la puerta\no el mueble en su lugar",
            "Trigger es el gatillo del dedo índice\nApunte a la brocha y presione el gatillo\nEn un mueble, haga lo mismo\nPrimero suelte Grip si lo tiene agarrado",
            "Apunte al acabado y presione el gatillo\nTambién puede escoger con el stick\nUse Anterior o Siguiente para ver más\nPresione el stick para pasar de página",
            "Abra o cierre el menú con A\nMueva el stick derecho para cambiar la hora\nApunte al dial y presione el gatillo\npara alternar entre día y noche",
            "B del mando derecho abre esta ayuda\nMantenga el stick derecho hacia arriba\nApunte al piso; con el arco azul, suéltelo\nCierre los menús antes de moverse"
        };
        for (int i = 0; i < instructions.Length; i++) instructions[i].text = content[i];
        questTab.image.color = simulator ? MenuStyle.Surface : MenuStyle.Text;
        simulatorTab.image.color = simulator ? MenuStyle.Text : MenuStyle.Surface;
        questTab.GetComponentInChildren<TextMeshProUGUI>().color = simulator ? MenuStyle.Text : MenuStyle.Background;
        simulatorTab.GetComponentInChildren<TextMeshProUGUI>().color = simulator ? MenuStyle.Background : MenuStyle.Text;
    }

    void Update()
    {
        if (panel == null || !toggle.WasPressedThisFrame()) return;
        bool opening = !panel.activeSelf;
        if (opening)
        {
            dayNightControls?.CloseMenu();
            MaterialChanger.CloseActiveMenu();
            if (Camera.main != null)
            {
                var direction = Vector3.ProjectOnPlane(panel.transform.position - Camera.main.transform.position, Vector3.up);
                if (direction.sqrMagnitude > 0.01f)
                    panel.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
            }
        }
        panel.SetActive(opening);
    }

    void BeginTour()
    {
        dayNightControls?.CloseMenu();
        MaterialChanger.CloseActiveMenu();
        if (teleportation != null)
            teleportation.QueueTeleportRequest(new TeleportRequest
            {
                destinationPosition = tourStart,
                destinationRotation = Quaternion.identity,
                matchOrientation = MatchOrientation.TargetUpAndForward
            });
        panel.SetActive(false);
    }

    public void CloseMenu()
    {
        if (panel != null) panel.SetActive(false);
    }

    void OpenDayNight()
    {
        panel.SetActive(false);
        MaterialChanger.CloseActiveMenu();
        if (dayNightControls != null && !dayNightControls.IsOpen)
            dayNightControls.ToggleMenu();
    }

    static Button MakeButton(Transform parent, string title, Vector2 position, Vector2 size, UnityAction action)
    {
        var go = new GameObject(title, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        var image = go.GetComponent<Image>();
        image.color = MenuStyle.Surface;
        var button = go.GetComponent<Button>();
        button.targetGraphic = image;
        var colors = button.colors;
        colors.highlightedColor = new Color(1.12f, 1.12f, 1.12f);
        colors.pressedColor = new Color(0.8f, 0.8f, 0.8f);
        colors.selectedColor = Color.white;
        button.colors = colors;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        button.onClick.AddListener(action);
        MenuStyle.Label(rect, title, Vector2.zero, rect.sizeDelta, 25, TextAlignmentOptions.Center);
        return button;
    }

    void OnDisable()
    {
        toggle.Disable();
        if (panel != null) panel.SetActive(false);
    }

    void OnDestroy() => toggle.Dispose();
}
