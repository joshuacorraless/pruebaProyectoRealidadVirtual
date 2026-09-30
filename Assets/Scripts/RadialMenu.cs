using UnityEngine;

public class RadialMenu : MonoBehaviour
{
    // prefab del boton (Assets/Prefabs/Button)
    public RadialButton buttonPrefab;

    // donde se crean los botones (Panel)
    public RectTransform buttonContainer;

    // circulo negro (Background): los botones se centran sobre su borde
    public RectTransform circle;

    // distancia del centro a cada boton, en unidades del canvas. solo se usa si no hay circulo
    public float radius = 32.5f;

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

    public void SpawnButtons(MaterialChanger owner, Material[] materials)
    {
        if (buttonPrefab == null || materials == null || materials.Length == 0) return;

        int count = materials.Length;
        float step = 360f / count;

        for (int i = 0; i < count; i++)
        {
            // empieza arriba (90 grados) y va en sentido del reloj
            float angle = (90f - i * step) * Mathf.Deg2Rad;
            Vector2 position = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;

            RadialButton button = Instantiate(buttonPrefab, buttonContainer);
            button.gameObject.SetActive(true);
            ((RectTransform)button.transform).anchoredPosition = position;
            button.Setup(owner, i, materials[i]);
        }
    }
}
