using UnityEngine;
using UnityEngine.EventSystems;

public class RadialButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler
{
    // quien creo el boton y su lugar en la lista de materiales
    public MaterialChanger Owner { get; private set; }
    public int Index { get; private set; }

    // material que representa este boton
    public Material Material { get; private set; }

    // hijo "CircleMesh": muestra el material como vista previa
    public Renderer preview;

    // cuanto crece el boton cuando el rayo le apunta
    public float highlightScale = 1.15f;

    Vector3 normalScale;

    public void Setup(MaterialChanger owner, int index, Material material)
    {
        Owner = owner;
        Index = index;
        Material = material;
        name = "RadialButton " + index + (material != null ? " (" + material.name + ")" : "");
        normalScale = transform.localScale;

        if (preview == null)
        {
            Transform mesh = transform.Find("CircleMesh");
            if (mesh != null) preview = mesh.GetComponent<Renderer>();
        }

        if (preview != null && material != null)
            preview.sharedMaterial = material;
    }

    // el rayo entra al boton: queda marcado
    public void OnPointerEnter(PointerEventData eventData)
    {
        Owner.SelectMaterial(Index);
    }

    // el rayo sale del boton: se desmarca
    public void OnPointerExit(PointerEventData eventData)
    {
        Owner.DeselectMaterial(Index);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        Owner.ConfirmSelection();
    }

    public void SetHighlighted(bool highlighted)
    {
        transform.localScale = highlighted ? normalScale * highlightScale : normalScale;
    }
}
