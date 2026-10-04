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

    RectTransform dialPointer;
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
        if (dialPointer != null)
            dialPointer.localRotation = Quaternion.Euler(0f, 0f, -(dayNightCycle.Hour - 12f) * 15f);
        if (timeLabel != null)
        {
            int minutes = Mathf.FloorToInt(dayNightCycle.Hour * 60f);
            timeLabel.text = $"{minutes / 60:00}:{minutes % 60:00}";
        }
    }

    void BuildDial()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;
        RectTransform panel = (RectTransform)canvas.transform;
        panel.sizeDelta = new Vector2(440f, 350f);
        panel.localScale = Vector3.one * 0.001f;
        Image background = panel.GetComponent<Image>();
        if (background == null) background = panel.gameObject.AddComponent<Image>();
        background.color = MenuStyle.Background;
        background.raycastTarget = false;

        RectTransform dial = (RectTransform)transform;
        dial.anchorMin = dial.anchorMax = new Vector2(0.5f, 0.5f);
        dial.sizeDelta = new Vector2(110f, 110f);
        dial.anchoredPosition = Vector2.zero;
        if (button != null)
        {
            button.navigation = new Navigation { mode = Navigation.Mode.None };
        }
        if (icon != null)
        {
            icon.raycastTarget = false;
        }

        dialPointer = new GameObject("Indicador de hora", typeof(RectTransform)).GetComponent<RectTransform>();
        dialPointer.SetParent(dial, false);
        dialPointer.sizeDelta = Vector2.zero;
        var pointer = new GameObject("Marca", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        pointer.transform.SetParent(dialPointer, false);
        pointer.rectTransform.sizeDelta = new Vector2(6f, 25f);
        pointer.rectTransform.anchoredPosition = new Vector2(0f, 64f);
        pointer.color = MenuStyle.Accent;
        pointer.raycastTarget = false;

        MenuStyle.Label(panel, "Hora del día", new Vector2(0f, 145f), new Vector2(420f, 38f), 26f,
            TextAlignmentOptions.Center);
        timeLabel = MenuStyle.Label(panel, "12:00", new Vector2(0f, 107f), new Vector2(420f, 34f), 24f,
            TextAlignmentOptions.Center);
        string help = Application.isEditor
            ? "J y L cambian la hora\nPresione T en el dial para día o noche\nCierre con 1"
            : "Cambie la hora con el stick derecho\nUse el gatillo en el dial para día o noche\nCierre con A";
        MenuStyle.Label(panel, help, new Vector2(0f, -125f), new Vector2(420f, 90f), 20f,
            TextAlignmentOptions.Center);
        UpdateIcon();
    }
}
