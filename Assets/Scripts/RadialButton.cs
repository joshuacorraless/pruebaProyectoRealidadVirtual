using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class RadialButton : MonoBehaviour, IPointerEnterHandler, IPointerDownHandler
{
    public MaterialChanger Owner { get; private set; }
    public int Index { get; private set; }
    public Material Material { get; private set; }
    public Renderer preview;

    Image background;
    Color normalColor;
    Vector3 normalScale;

    void Awake()
    {
        normalScale = transform.localScale;
        background = GetComponent<Image>();
        normalColor = MenuStyle.Surface;
        if (preview == null)
        {
            Transform mesh = transform.Find("CircleMesh");
            if (mesh != null) preview = mesh.GetComponent<Renderer>();
        }
    }

    public void Setup(MaterialChanger owner, int index, Material material)
    {
        Owner = owner;
        Index = index;
        Material = material;
        name = material != null ? material.name : "Control " + index;
        if (background != null && index < 0) background.sprite = null;
        SetHighlighted(false);
        if (preview != null) preview.enabled = material != null;
        if (preview != null && material != null)
        {
            preview.sharedMaterial = material;
            // La muestra queda delante de la imagen UI; el borde muestra la selección.
            preview.transform.localPosition = new Vector3(0f, 0f, -0.5f);
            var rect = (RectTransform)transform;
            preview.transform.localScale = new Vector3(rect.rect.width * 0.88f, 0.1f, rect.rect.height * 0.88f);
            preview.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            preview.receiveShadows = false;
        }
    }

    public void OnPointerEnter(PointerEventData eventData) => Owner.SelectMaterial(Index);

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        // El joystick puede haber cambiado la selección sin mover el rayo.
        Owner.ConfirmSelection();
    }

    public void SetHighlighted(bool highlighted)
    {
        transform.localScale = normalScale * (highlighted ? 1.05f : 1f);
        if (background != null) background.color = highlighted ? MenuStyle.Accent : normalColor;
        if (Index < 0)
        {
            var label = GetComponentInChildren<TextMeshProUGUI>();
            if (label != null) label.color = highlighted ? MenuStyle.Background : MenuStyle.Text;
        }
    }
}
