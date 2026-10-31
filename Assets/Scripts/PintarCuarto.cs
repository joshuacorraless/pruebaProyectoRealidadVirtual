using UnityEngine;

// una brocha por cuarto: el acabado que se escoge con la brocha (MaterialChanger) se copia al lado de adentro de
// todas las paredes del cuarto. la brocha pinta su pared y este script pasa ese material a las demas
[RequireComponent(typeof(MaterialChanger))]
public class PintarCuarto : MonoBehaviour
{
    // las paredes del cuarto y, de cada una, el slot del lado que da hacia el cuarto
    public Renderer[] walls = new Renderer[0];
    public int[] slots = new int[0];

    MaterialChanger brush;
    Material painted;

    void Start()
    {
        brush = GetComponent<MaterialChanger>();
    }

    void LateUpdate()
    {
        if (brush.targetRenderer == null) return;

        Material[] own = brush.targetRenderer.sharedMaterials;
        if (brush.materialSlot < 0 || brush.materialSlot >= own.Length || own[brush.materialSlot] == painted) return;

        painted = own[brush.materialSlot];
        for (int i = 0; i < walls.Length; i++)
        {
            Material[] materials = walls[i].sharedMaterials;
            if (slots[i] >= materials.Length) continue;
            materials[slots[i]] = painted;
            walls[i].sharedMaterials = materials;
        }
    }
}
