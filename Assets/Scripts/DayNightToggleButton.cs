using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DayNightToggleButton : MonoBehaviour
{
    public DayNightCycle dayNightCycle;
    public Button button;

    public Image icon;
    public Sprite toNightSprite;
    public Sprite toDaySprite;

    // marca amarilla que gira alrededor del boton segun la hora
    RectTransform dialPointer;

    // texto con la hora (12:00)
    TextMeshProUGUI timeLabel;

    void Awake()
    {
        if (button == null)
            button = GetComponent<Button>();
        if (dayNightCycle == null)
            dayNightCycle = FindFirstObjectByType<DayNightCycle>();

        BuildDial();
    }

    void OnEnable()
    {
        if (button != null)
            button.onClick.AddListener(OnClick);
    }

    void OnDisable()
    {
        if (button != null)
            button.onClick.RemoveListener(OnClick);
    }

    void OnClick()
    {
        if (dayNightCycle != null)
            dayNightCycle.ToggleDayNight();

        UpdateIcon();
    }

    void Update()
    {
        UpdateIcon();
    }

    void UpdateIcon()
    {
        if (dayNightCycle == null) return;

        if (icon != null)
            icon.sprite = dayNightCycle.IsDay ? toNightSprite : toDaySprite;

        // las 12 quedan arriba y da una vuelta en 24 horas (15 grados por hora)
        if (dialPointer != null)
            dialPointer.localRotation = Quaternion.Euler(0f, 0f, -(dayNightCycle.Hour - 12f) * 15f);

        if (timeLabel != null)
        {
            int minutes = Mathf.FloorToInt(dayNightCycle.Hour * 60f);
            timeLabel.text = (minutes / 60).ToString("00") + ":" + (minutes % 60).ToString("00");
        }
    }

    // arma el panel alrededor del boton: fondo, marca de la hora y textos
    void BuildDial()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;

        RectTransform panel = (RectTransform)canvas.transform;
        panel.sizeDelta = new Vector2(440f, 350f);
        panel.localScale = Vector3.one * 0.001f;

        Image background = panel.GetComponent<Image>();
        if (background == null)
            background = panel.gameObject.AddComponent<Image>();
        background.color = MenuStyle.Background;
        background.raycastTarget = false;

        // el boton queda en el centro del panel
        RectTransform dial = (RectTransform)transform;
        dial.anchorMin = new Vector2(0.5f, 0.5f);
        dial.anchorMax = new Vector2(0.5f, 0.5f);
        dial.sizeDelta = new Vector2(110f, 110f);
        dial.anchoredPosition = Vector2.zero;

        if (button != null)
            button.navigation = new Navigation { mode = Navigation.Mode.None };

        // el icono no debe taparle el rayo al boton
        if (icon != null)
            icon.raycastTarget = false;

        // objeto vacio en el centro: al girarlo, la marca da la vuelta por el borde
        GameObject pointerObject = new GameObject("Indicador de hora", typeof(RectTransform));
        dialPointer = (RectTransform)pointerObject.transform;
        dialPointer.SetParent(dial, false);
        dialPointer.sizeDelta = Vector2.zero;

        GameObject markObject = new GameObject("Marca", typeof(RectTransform), typeof(Image));
        markObject.transform.SetParent(dialPointer, false);

        Image mark = markObject.GetComponent<Image>();
        mark.rectTransform.sizeDelta = new Vector2(6f, 25f);
        mark.rectTransform.anchoredPosition = new Vector2(0f, 64f);
        mark.color = MenuStyle.Accent;
        mark.raycastTarget = false;

        MenuStyle.Label(panel, "Hora del día", new Vector2(0f, 145f), new Vector2(420f, 38f), 26f,
            TextAlignmentOptions.Center);
        timeLabel = MenuStyle.Label(panel, "12:00", new Vector2(0f, 107f), new Vector2(420f, 34f), 24f,
            TextAlignmentOptions.Center);

        // en el editor se usa el simulador, con teclado
        string help;
        if (Application.isEditor)
            help = "J y L cambian la hora\nPresione T en el dial para día o noche\nCierre con 1";
        else
            help = "Cambie la hora con el stick derecho\nUse el gatillo en el dial para día o noche\nCierre con A";

        MenuStyle.Label(panel, help, new Vector2(0f, -125f), new Vector2(420f, 90f), 20f,
            TextAlignmentOptions.Center);

        UpdateIcon();
    }
}
