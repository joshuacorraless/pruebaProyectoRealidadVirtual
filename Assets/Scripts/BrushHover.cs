using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// la brocha crece y se ilumina mientras un control la apunta
public class BrushHover : MonoBehaviour
{
    // hijos "Sprite" (el rodillo) y "Fondo" (el circulo de atras). si no se asignan se buscan por nombre
    public SpriteRenderer icon;
    public SpriteRenderer background;

    // cuanto crece con el rayo encima
    public float hoverScale = 1.2f;

    // colores con el rayo encima: el rodillo en el amarillo de los menus y el fondo casi opaco
    public Color hoverIconColor = MenuStyle.Accent;
    public Color hoverBackgroundColor = new Color(0.035f, 0.065f, 0.11f, 0.9f);

    // que tan rapido entra y sale el efecto
    public float speed = 10f;

    XRBaseInteractable interactable;

    // 0 sin rayo, 1 con el rayo encima. va cambiando poco a poco
    float amount;

    Vector3 iconScale;
    Vector3 backgroundScale;
    Color iconColor;
    Color backgroundColor;

    void Awake()
    {
        interactable = GetComponent<XRBaseInteractable>();

        if (icon == null)
        {
            Transform child = transform.Find("Sprite");
            if (child != null) icon = child.GetComponent<SpriteRenderer>();
        }

        if (background == null)
        {
            Transform child = transform.Find("Fondo");
            if (child != null) background = child.GetComponent<SpriteRenderer>();
        }

        if (icon != null)
        {
            iconScale = icon.transform.localScale;
            iconColor = icon.color;
        }

        if (background != null)
        {
            backgroundScale = background.transform.localScale;
            backgroundColor = background.color;
        }
    }

    void Update()
    {
        // la mirada ya esta apagada en el interactable (MaterialChanger), asi que solo cuentan los controles
        float target = interactable != null && interactable.isHovered ? 1f : 0f;
        if (amount == target) return;

        amount = Mathf.MoveTowards(amount, target, speed * Time.deltaTime);

        // entra rapido y frena al final
        float t = Mathf.SmoothStep(0f, 1f, amount);
        float scale = Mathf.Lerp(1f, hoverScale, t);

        if (icon != null)
        {
            icon.transform.localScale = iconScale * scale;
            icon.color = Color.Lerp(iconColor, hoverIconColor, t);
        }

        if (background != null)
        {
            background.transform.localScale = backgroundScale * scale;
            background.color = Color.Lerp(backgroundColor, hoverBackgroundColor, t);
        }
    }
}
