using UnityEngine;
using UnityEngine.UI;

public class DayNightToggleButton : MonoBehaviour
{
    public DayNightCycle dayNightCycle;
    public Button button;

    public Image icon;
    public Sprite toNightSprite;
    public Sprite toDaySprite;

    void Awake()
    {
        if (button == null)
            button = GetComponent<Button>();
        if (dayNightCycle == null)
            dayNightCycle = FindFirstObjectByType<DayNightCycle>();
    }

    void OnEnable()
    {
        button.onClick.AddListener(OnClick);
    }

    void OnDisable()
    {
        button.onClick.RemoveListener(OnClick);
    }

    void OnClick()
    {
        dayNightCycle.ToggleDayNight();
        UpdateIcon();
    }

    void Update()
    {
        UpdateIcon();
    }

    void UpdateIcon()
    {
        if (icon != null)
            icon.sprite = dayNightCycle.IsDay ? toNightSprite : toDaySprite;
    }
}
