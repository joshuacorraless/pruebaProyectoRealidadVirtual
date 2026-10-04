using System.Collections.Generic;
using UnityEngine;

public class RadialMenu : MonoBehaviour
{
    // indices que no son un material: nada apuntado, y el boton del centro
    public const int None = -1;
    public const int RestoreOriginal = -2;

    // prefab del boton (Assets/Prefabs/Button)
    public RadialButton buttonPrefab;

    // donde se crean los botones (Panel)
    public RectTransform buttonContainer;

    // circulo negro (Background): los botones se centran sobre su borde
    public RectTransform circle;

    // distancia del centro a cada boton, en unidades del canvas. solo se usa si no hay circulo
    public float radius = 32.5f;

    // cuantos materiales se muestran a la vez. si hay mas, se pasa de pagina
    public int pageSize = 6;

    // que tan grande es el boton del centro comparado con los demas
    public float centerButtonScale = 0.6f;

    // boton al que apunta el rayo
    public int SelectedIndex { get; private set; } = None;

    MaterialChanger owner;
    Material[] materials;
    Material originalMaterial;
    int page;

    // botones de la pagina que se esta viendo
    readonly List<RadialButton> buttons = new List<RadialButton>();

    void Awake()
    {
        if (buttonContainer == null)
        {
            Transform panel = transform.Find("Panel");
            buttonContainer = panel != null ? (RectTransform)panel : (RectTransform)transform;
        }

        if (circle == null)
            circle = buttonContainer.Find("Background") as RectTransform;

        if (circle != null)
            radius = circle.rect.width * 0.5f;

        // canvas en world space: necesita la camara para los eventos de UI
        Canvas canvas = GetComponent<Canvas>();
        if (canvas != null && canvas.worldCamera == null)
            canvas.worldCamera = Camera.main;
    }

    // originalMaterial es el del boton del centro. si es null no hay boton del centro
    public void SpawnButtons(MaterialChanger owner, Material[] materials, Material originalMaterial)
    {
        if (buttonPrefab == null || materials == null || materials.Length == 0) return;

        this.owner = owner;
        this.materials = materials;
        this.originalMaterial = originalMaterial;
        page = 0;

        ShowPage();
    }

    // direction 1 es la pagina siguiente y -1 la anterior. despues de la ultima vuelve a la primera
    public void ChangePage(int direction)
    {
        if (materials == null) return;

        int pageCount = Mathf.CeilToInt(materials.Length / (float)pageSize);
        if (pageCount <= 1) return;

        page = (page + direction + pageCount) % pageCount;
        ShowPage();
    }

    void ShowPage()
    {
        // quita los botones de la pagina anterior
        foreach (RadialButton button in buttons)
            Destroy(button.gameObject);
        buttons.Clear();
        SelectedIndex = None;

        int first = page * pageSize;
        int count = Mathf.Min(pageSize, materials.Length - first);
        float step = 360f / count;

        for (int i = 0; i < count; i++)
        {
            // empieza arriba (90 grados) y va en sentido del reloj
            float angle = (90f - i * step) * Mathf.Deg2Rad;
            Vector2 position = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;

            CreateButton(first + i, materials[first + i], position, 1f);
        }

        // boton del centro: mas chico, con el material que tenia el objeto al cargar la escena
        if (originalMaterial != null)
            CreateButton(RestoreOriginal, originalMaterial, Vector2.zero, centerButtonScale);
    }

    void CreateButton(int index, Material material, Vector2 position, float scale)
    {
        RadialButton button = Instantiate(buttonPrefab, buttonContainer);
        button.gameObject.SetActive(true);
        ((RectTransform)button.transform).anchoredPosition = position;
        button.transform.localScale *= scale;
        button.Setup(owner, index, material);
        buttons.Add(button);
    }

    // marca el boton con ese indice y desmarca los demas
    public void Select(int index)
    {
        SelectedIndex = index;

        foreach (RadialButton button in buttons)
            button.SetHighlighted(button.Index == index);
    }
}
