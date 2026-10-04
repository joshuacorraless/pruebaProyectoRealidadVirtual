using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

// TeleportationArea para los pisos importados de Blender. esas mallas vienen con los ejes rotados,
// y la del XRI revisa la inclinacion con el "arriba" del objeto, asi que rechazaba pisos que si estan planos.
// aqui se revisa con el arriba del mundo
public class WorldUpTeleportationArea : TeleportationArea
{
    protected override void Awake()
    {
        base.Awake();

        // se apaga el filtro del XRI, lo reemplaza IsWalkable
        filterSelectionByHitNormal = false;
    }

    // el arco solo se pone valido si el rayo pega en algo caminable
    public override bool IsSelectableBy(IXRSelectInteractor interactor)
    {
        if (!base.IsSelectableBy(interactor)) return false;

        XRRayInteractor ray = interactor as XRRayInteractor;
        if (ray == null) return true;

        RaycastHit hit;
        return ray.TryGetCurrent3DRaycastHit(out hit) && IsWalkable(hit.normal);
    }

    protected override bool GenerateTeleportRequest(IXRInteractor interactor, RaycastHit raycastHit, ref TeleportRequest teleportRequest)
    {
        if (!IsWalkable(raycastHit.normal)) return false;

        return base.GenerateTeleportRequest(interactor, raycastHit, ref teleportRequest);
    }

    // true si la superficie esta lo bastante plana para pararse (misma tolerancia del XRI)
    bool IsWalkable(Vector3 normal)
    {
        if (normal.sqrMagnitude < 0.5f) return false;

        return Vector3.Angle(Vector3.up, normal) <= upNormalToleranceDegrees;
    }
}
