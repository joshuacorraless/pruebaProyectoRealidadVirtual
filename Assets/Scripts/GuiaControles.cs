using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// al lado del cartel de ayuda (WelcomeMenu) dibuja los dos mandos del Quest con lo que hace cada boton, y deja mas
// claros los textos de la pestana Quest. en las pruebas la gente no sabia cual boton era A o B, que se podia caminar
// sin teletransporte, como abrir las puertas ni para que eran las brochas. WelcomeMenu no se toca
[RequireComponent(typeof(WelcomeMenu))]
public class GuiaControles : MonoBehaviour
{
    // circulo con el que se dibujan los mandos y sus botones
    public Sprite circle;

    // un color por boton, el mismo en el dibujo y en la lista
    static readonly Color StickColor = new Color(0.45f, 0.75f, 1f);
    static readonly Color TriggerColor = new Color(1f, 0.55f, 0.35f);
    static readonly Color GripColor = new Color(0.5f, 0.88f, 0.55f);

    // titulos de las 6 secciones del cartel, en el orden de WelcomeMenu
    static readonly string[] OriginalHeadings =
    {
        "Moverse", "Puertas y muebles", "Ver acabados",
        "Cambiar el acabado", "Hora del día", "Ayuda y teletransporte"
    };

    static readonly string[] Headings =
    {
        "Moverse", "Puertas y muebles", "Brochas: para qué son",
        "Brochas: cómo se usan", "Hora del día", "Ayuda y teletransporte"
    };

    // textos de la pestana Quest. null deja el de WelcomeMenu
    static readonly string[] QuestTexts =
    {
        "Stick izquierdo: camine por la casa,\nno hace falta teletransportarse\nStick derecho a los lados: gire\nApunte con el rayo de cualquier mando",
        "La manilla amarilla es de donde se agarra\nApunte a ella y mantenga Grip (costado)\nJale o empuje la mano y suelte Grip\nLos muebles se mueven igual",
        "Las brochas flotan junto a paredes y pisos\nCada una cambia el acabado de su cuarto:\nla de pared pinta las paredes de ese lado\ny la de piso, el piso de esa sala",
        "Apunte a la brocha y presione el gatillo\nEscoja una muestra con el gatillo\nPase de página con el stick a los lados\nEn un mueble: suelte Grip y use el gatillo",
        null,
        "B del mando derecho abre esta ayuda\nStick derecho hacia arriba y apunte al piso\ncuando el arco esté azul, suelte el stick\nCierre los menús antes de moverse"
    };

    TextMeshProUGUI[] instructions = new TextMeshProUGUI[6];

    IEnumerator Start()
    {
        // se espera a que WelcomeMenu arme el cartel (lo hace cuando ya hay camara)
        Transform panel = null;
        while (panel == null)
        {
            yield return null;
            panel = transform.Find("Bienvenida");
        }

        FindSections(panel);
        BuildGuide((RectTransform)panel);

        // la pestana Quest de WelcomeMenu pone sus textos; este listener va despues y los cambia por los de aqui
        foreach (Button tab in panel.GetComponentsInChildren<Button>(true))
        {
            if (tab.name == "Quest")
                tab.onClick.AddListener(ShowQuestTexts);
        }

        // en el visor el cartel abre en la pestana Quest
        if (!Application.isEditor)
            ShowQuestTexts();
    }

    // el texto de cada seccion es el que sigue a su titulo en el cartel
    void FindSections(Transform panel)
    {
        foreach (TextMeshProUGUI label in panel.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            // "Hora del día" tambien es un boton: solo cuentan los titulos que van directo en el cartel
            int i = System.Array.IndexOf(OriginalHeadings, label.text);
            if (i < 0 || label.transform.parent != panel) continue;

            label.text = Headings[i];
            instructions[i] = panel.GetChild(label.transform.GetSiblingIndex() + 1).GetComponent<TextMeshProUGUI>();
        }
    }

    void ShowQuestTexts()
    {
        for (int i = 0; i < instructions.Length; i++)
        {
            if (QuestTexts[i] != null && instructions[i] != null)
                instructions[i].text = QuestTexts[i];
        }
    }

    // panel de los mandos pegado al borde derecho del cartel y girado hacia el jugador. es hijo del cartel: se abre,
    // se cierra y se voltea con el
    void BuildGuide(RectTransform panel)
    {
        GameObject guideObject = new GameObject("Guia de botones", typeof(RectTransform), typeof(Image));
        RectTransform guide = (RectTransform)guideObject.transform;
        guide.SetParent(panel, false);
        guide.sizeDelta = new Vector2(820f, 900f);
        guide.pivot = new Vector2(0f, 0.5f);
        guide.anchoredPosition = new Vector2(panel.sizeDelta.x / 2f + 20f, 0f);
        guide.localRotation = Quaternion.Euler(0f, 30f, 0f);

        Image background = guideObject.GetComponent<Image>();
        background.color = MenuStyle.Background;
        background.raycastTarget = false;

        MenuStyle.Label(guide, "Botones del Quest", new Vector2(0f, 390f), new Vector2(760f, 60f), 42f, TextAlignmentOptions.Center);

        // los mandos son espejo: el stick va hacia afuera y los botones de letras hacia el centro
        BuildController(guide, -205f, "Mando izquierdo", -1f, "Y", "X", new string[]
        {
            "<b>Stick</b>: caminar, sin teletransporte",
            "<b>Gatillo</b>: escoger brochas,\nmuestras y botones",
            "<b>Grip</b>: agarrar puertas y muebles",
            "<b>X</b>: cinta métrica, apunte y estire\n<b>Y</b>: cerrar la cinta métrica"
        });
        BuildController(guide, 205f, "Mando derecho", 1f, "B", "A", new string[]
        {
            "<b>Stick</b> a los lados: girar\n<b>Stick</b> arriba: teletransporte",
            "<b>Gatillo</b> y <b>Grip</b>: igual que\nen el izquierdo",
            "<b>A</b>: menú de la hora del día",
            "<b>B</b>: abrir o cerrar esta ayuda"
        });

        TextMeshProUGUI tip = MenuStyle.Label(guide,
            "Puertas: agarre la <color=#FFCC4D>manilla amarilla</color> con Grip y jale\n" +
            "Brochas: solo en la casa de la <color=#FFCC4D>flecha amarilla</color>, con el gatillo\n" +
            "Cinta métrica: mantenga X, apunte y suelte. <color=#FFCC4D>Y la cierra</color>\n" +
            "En el simulador: Shift + 1 es X y Shift + 2 es Y",
            new Vector2(0f, -385f), new Vector2(760f, 120f), 21f, TextAlignmentOptions.Center);
        tip.color = MenuStyle.Muted;
    }

    // un mando visto desde arriba (cuerpo, stick y dos botones de letras), las dos teclas de atras y su lista
    void BuildController(Transform guide, float x, string title, float side, string upperLetter, string lowerLetter, string[] actions)
    {
        TextMeshProUGUI heading = MenuStyle.Label(guide, title, new Vector2(x, 320f), new Vector2(380f, 36f), 27f, TextAlignmentOptions.Center);
        heading.color = MenuStyle.Accent;

        Vector2 center = new Vector2(x, 180f);
        Circle(guide, center, 220f, MenuStyle.Surface, null);
        Circle(guide, center + new Vector2(side * 45f, 25f), 78f, StickColor, null);
        Circle(guide, center + new Vector2(-side * 50f, 50f), 52f, MenuStyle.Accent, upperLetter);
        Circle(guide, center + new Vector2(-side * 30f, -35f), 52f, MenuStyle.Accent, lowerLetter);

        // gatillo y grip no se ven desde arriba: van como etiquetas debajo del dibujo
        Row(guide, new Vector2(x, 45f), TriggerColor, "Gatillo: dedo índice");
        Row(guide, new Vector2(x, 5f), GripColor, "Grip: costado, dedo medio");

        Color[] colors = { StickColor, TriggerColor, GripColor, MenuStyle.Accent };
        for (int i = 0; i < actions.Length; i++)
            Row(guide, new Vector2(x, -65f - i * 72f), colors[i], actions[i]);
    }

    // punto de color y texto a la derecha. position es el centro de la fila
    void Row(Transform parent, Vector2 position, Color color, string content)
    {
        Circle(parent, position + new Vector2(-170f, 0f), 22f, color, null);
        MenuStyle.Label(parent, content, position + new Vector2(17f, 0f), new Vector2(340f, 66f), 21f);
    }

    void Circle(Transform parent, Vector2 position, float size, Color color, string letter)
    {
        GameObject circleObject = new GameObject("Circulo", typeof(RectTransform), typeof(Image));
        circleObject.transform.SetParent(parent, false);

        RectTransform rect = (RectTransform)circleObject.transform;
        rect.anchoredPosition = position;
        rect.sizeDelta = Vector2.one * size;

        Image image = circleObject.GetComponent<Image>();
        image.sprite = circle;
        image.color = color;
        image.raycastTarget = false;

        if (letter != null)
        {
            TextMeshProUGUI label = MenuStyle.Label(rect, letter, Vector2.zero, rect.sizeDelta, size * 0.55f, TextAlignmentOptions.Center);
            label.color = MenuStyle.Background;
        }
    }
}
