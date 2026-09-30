using UnityEngine;

public class RadialButton : MonoBehaviour
{
    // quien creo el boton y su lugar en el menu (despues se usa para aplicar el material)
    public MaterialChanger Owner { get; private set; }
    public int Index { get; private set; }

    // material que representa este boton
    public Material Material { get; private set; }

    // hijo "CircleMesh": muestra el material como vista previa
    public Renderer preview;

    public void Setup(MaterialChanger owner, int index, Material material)
    {
        Owner = owner;
        Index = index;
        Material = material;
        name = "RadialButton " + index + (material != null ? " (" + material.name + ")" : "");

        if (preview == null)
        {
            Transform mesh = transform.Find("CircleMesh");
            if (mesh != null) preview = mesh.GetComponent<Renderer>();
        }

        if (preview != null && material != null)
            preview.sharedMaterial = material;
    }
}
