using TMPro;
using UnityEngine;

// colores y textos que comparten el menu de bienvenida y el de la hora, para que se vean iguales
public static class MenuStyle
{
    // fondo de los paneles
    public static readonly Color Background = new Color(0.035f, 0.065f, 0.11f, 0.96f);

    // texto normal y texto secundario (mas apagado)
    public static readonly Color Text = new Color(0.96f, 0.945f, 0.90f);
    public static readonly Color Muted = new Color(0.73f, 0.77f, 0.80f);

    // amarillo para lo que hay que resaltar
    public static readonly Color Accent = new Color(1f, 0.8f, 0.3f);

    // fondo de los botones
    public static readonly Color Surface = new Color(0.12f, 0.15f, 0.19f);

    // fuente de los menus (Assets/Resources/Fonts). se crea la primera vez que se pide
    static TMP_FontAsset font;

    public static TMP_FontAsset GetFont()
    {
        if (font == null)
        {
            Font source = Resources.Load<Font>("Fonts/SpaceGrotesk-Regular");
            if (source != null)
                font = TMP_FontAsset.CreateFontAsset(source);
        }

        // si no se pudo cargar se usa la fuente por defecto de TextMeshPro
        return font != null ? font : TMP_Settings.defaultFontAsset;
    }

    // crea un texto dentro de parent. position y size en unidades del canvas
    public static TextMeshProUGUI Label(Transform parent, string content, Vector2 position, Vector2 size,
        float fontSize, TextAlignmentOptions alignment = TextAlignmentOptions.Left)
    {
        GameObject labelObject = new GameObject("Texto", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(parent, false);

        TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
        label.rectTransform.anchoredPosition = position;
        label.rectTransform.sizeDelta = size;
        label.font = GetFont();
        label.text = content;
        label.fontSize = fontSize;
        label.alignment = alignment;
        label.color = Text;

        // el texto no debe tapar el rayo de los botones
        label.raycastTarget = false;

        return label;
    }
}
