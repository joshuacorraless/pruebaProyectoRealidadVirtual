using TMPro;
using UnityEngine;

public static class MenuStyle
{
    public static Color Background => new Color(0.035f, 0.065f, 0.11f, 0.96f);
    public static Color Text => new Color(0.96f, 0.945f, 0.90f);
    public static Color Muted => new Color(0.73f, 0.77f, 0.80f);
    public static Color Accent => new Color(1f, 0.8f, 0.3f);
    public static Color Surface => new Color(0.12f, 0.15f, 0.19f);

    static TMP_FontAsset runtimeFont;

    public static TMP_FontAsset Font
    {
        get
        {
            if (runtimeFont != null) return runtimeFont;
            var source = Resources.Load<UnityEngine.Font>("Fonts/SpaceGrotesk-Regular");
            if (source == null) return TMP_Settings.defaultFontAsset;
            runtimeFont = TMP_FontAsset.CreateFontAsset(source);
            if (runtimeFont == null) return TMP_Settings.defaultFontAsset;
            runtimeFont.name = "Space Grotesk - Menús";
            runtimeFont.hideFlags = HideFlags.DontSave;
            Application.quitting += ReleaseFont;
            return runtimeFont;
        }
    }

    static void ReleaseFont()
    {
        Application.quitting -= ReleaseFont;
        // TMP elimina también el atlas y el material creados con esta fuente.
        if (runtimeFont != null) Object.Destroy(runtimeFont);
        runtimeFont = null;
    }

    public static TextMeshProUGUI Label(Transform parent, string content, Vector2 position,
        Vector2 size, float fontSize, TextAlignmentOptions alignment = TextAlignmentOptions.Left)
    {
        var label = new GameObject("Texto", typeof(RectTransform), typeof(TextMeshProUGUI))
            .GetComponent<TextMeshProUGUI>();
        label.transform.SetParent(parent, false);
        label.rectTransform.anchoredPosition = position;
        label.rectTransform.sizeDelta = size;
        label.font = Font;
        label.text = content;
        label.fontSize = fontSize;
        label.alignment = alignment;
        label.color = Text;
        label.raycastTarget = false;
        return label;
    }
}
