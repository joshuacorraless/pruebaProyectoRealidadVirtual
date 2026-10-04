using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RadialMenu : MonoBehaviour
{
    public const int Close = -1;
    public const int PreviousPage = -2;
    public const int NextPage = -3;
    public const int RestoreOriginal = -4;
    const int PageSize = 6;

    public RadialButton buttonPrefab;
    public RectTransform buttonContainer;
    public RectTransform circle;
    public float radius = 32.5f;

    public int SelectedIndex { get; private set; } = Close;
    MaterialChanger owner;
    Material[] materials;
    RadialButton[] buttons;
    RadialButton closeButton, previousButton, nextButton, restoreButton;
    TextMeshProUGUI pageLabel, selectionLabel;
    TextMeshProUGUI[] optionLabels;
    int page;
    int PageCount => Mathf.CeilToInt(materials.Length / (float)PageSize);

    void Awake()
    {
        if (buttonContainer == null)
        {
            Transform panel = transform.Find("Panel");
            buttonContainer = panel != null ? (RectTransform)panel : (RectTransform)transform;
        }
        if (circle == null) circle = buttonContainer.Find("Background") as RectTransform;
        if (circle != null) circle.gameObject.SetActive(false);
        radius = 205f;

        var root = (RectTransform)transform;
        root.sizeDelta = new Vector2(860f, 880f);
        root.localScale = Vector3.one * 0.00125f;
        buttonContainer.anchorMin = buttonContainer.anchorMax = new Vector2(0.5f, 0.5f);
        buttonContainer.anchoredPosition = Vector2.zero;
        buttonContainer.sizeDelta = root.sizeDelta;
        var background = buttonContainer.GetComponent<Image>();
        if (background == null) background = buttonContainer.gameObject.AddComponent<Image>();
        background.color = MenuStyle.Background;
        background.raycastTarget = true;

        Canvas canvas = GetComponent<Canvas>();
        if (canvas != null && canvas.worldCamera == null) canvas.worldCamera = Camera.main;
        var scaler = GetComponent<CanvasScaler>();
        if (scaler != null) scaler.dynamicPixelsPerUnit = 1f;
    }

    public void SpawnButtons(MaterialChanger menuOwner, Material[] options)
    {
        if (buttonPrefab == null || options == null || options.Length == 0) return;
        owner = menuOwner;
        materials = options;
        buttons = new RadialButton[Mathf.Min(PageSize, materials.Length)];
        optionLabels = new TextMeshProUGUI[buttons.Length];
        for (int i = 0; i < buttons.Length; i++)
        {
            buttons[i] = Instantiate(buttonPrefab, buttonContainer);
            buttons[i].gameObject.SetActive(true);
            var rect = (RectTransform)buttons[i].transform;
            rect.sizeDelta = new Vector2(108f, 108f);
            optionLabels[i] = Label("", Vector2.zero, new Vector2(190f, 34f), 19f);
        }

        Label(owner.menuTitle, new Vector2(0, 370f), new Vector2(770f, 54f), 38f);
        var subtitle = Label($"{materials.Length} acabados", new Vector2(0, 328f), new Vector2(770f, 30f), 20f);
        subtitle.color = MenuStyle.Muted;
        selectionLabel = Label("", new Vector2(0, 38f), new Vector2(260f, 68f), 22f);
        closeButton = Control("Cerrar", Close, new Vector2(0, -34f), new Vector2(142f, 48f));
        if (owner.CanRestoreOriginal)
            restoreButton = Control("Restaurar original", RestoreOriginal, new Vector2(0, -96f), new Vector2(220f, 48f));
        if (PageCount > 1)
        {
            previousButton = Control("Anterior", PreviousPage, new Vector2(-246f, -328f), new Vector2(186f, 50f));
            nextButton = Control("Siguiente", NextPage, new Vector2(246f, -328f), new Vector2(186f, 50f));
            pageLabel = Label("", new Vector2(0, -328f), new Vector2(220f, 42f), 21f);
        }
        string helpText = Application.isEditor
            ? "Apunte al acabado y presione T\n" + (PageCount > 1
                ? "Para ver más, presione 3 o use Anterior y Siguiente"
                : "También puede escoger con I, J, K o L")
            : "Apunte al acabado y presione el gatillo\n" + (PageCount > 1
                ? "Para ver más, presione el stick o use Anterior y Siguiente"
                : "También puede escoger con el stick");
        var help = Label(helpText,
            new Vector2(0, -392f), new Vector2(790f, 66f), 19f);
        help.color = MenuStyle.Muted;
        ShowPage();
    }

    void ShowPage()
    {
        int count = Mathf.Min(PageSize, materials.Length - page * PageSize);
        for (int i = 0; i < buttons.Length; i++)
        {
            buttons[i].gameObject.SetActive(i < count);
            optionLabels[i].gameObject.SetActive(i < count);
            if (i >= count) continue;
            float angle = (90f - i * 360f / count) * Mathf.Deg2Rad;
            Vector2 position = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            ((RectTransform)buttons[i].transform).anchoredPosition = position;
            int index = page * PageSize + i;
            buttons[i].Setup(owner, index, materials[index]);
            optionLabels[i].rectTransform.anchoredPosition = position + new Vector2(0f, -75f);
            optionLabels[i].text = OptionName(index);
        }
        if (pageLabel != null) pageLabel.text = $"Página {page + 1} de {PageCount}";
        Select(page * PageSize);
    }

    public void ChangePage(int direction)
    {
        if (materials == null || PageCount <= 1) return;
        int selected = SelectedIndex;
        page = (page + direction + PageCount) % PageCount;
        ShowPage();
        if (selected == PreviousPage || selected == NextPage) Select(selected);
    }

    public void SelectDirection(Vector2 stick)
    {
        if (materials == null) return;
        int count = Mathf.Min(PageSize, materials.Length - page * PageSize);
        float angle = Mathf.Repeat(90f - Mathf.Atan2(stick.y, stick.x) * Mathf.Rad2Deg, 360f);
        Select(page * PageSize + Mathf.RoundToInt(angle / (360f / count)) % count);
    }

    public void Select(int index)
    {
        SelectedIndex = index;
        if (buttons != null)
            foreach (var button in buttons) button.SetHighlighted(button.Index == index);
        if (closeButton != null) closeButton.SetHighlighted(index == Close);
        if (previousButton != null) previousButton.SetHighlighted(index == PreviousPage);
        if (nextButton != null) nextButton.SetHighlighted(index == NextPage);
        if (restoreButton != null) restoreButton.SetHighlighted(index == RestoreOriginal);
        if (selectionLabel != null)
            selectionLabel.text = index == RestoreOriginal ? "Estilo original" :
                index >= 0 && index < materials.Length ? OptionName(index) : "Elija un acabado";
    }

    string OptionName(int index)
    {
        if (materials[index] == null) return "Sin material";
        string materialName = materials[index].name;
        if (materialName.StartsWith("wall"))
            return "Diseño " + (index / 2 + 1).ToString("00") + (index % 2 == 0 ? " A" : " B");
        return materialName;
    }

    RadialButton Control(string title, int index, Vector2 position, Vector2 size)
    {
        var button = Instantiate(buttonPrefab, buttonContainer);
        button.gameObject.SetActive(true);
        var rect = (RectTransform)button.transform;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        button.Setup(owner, index, null);
        MenuStyle.Label(rect, title, Vector2.zero, size, 22f, TextAlignmentOptions.Center);
        return button;
    }

    TextMeshProUGUI Label(string content, Vector2 position, Vector2 size, float fontSize)
    {
        return MenuStyle.Label(buttonContainer, content, position, size, fontSize, TextAlignmentOptions.Center);
    }
}
