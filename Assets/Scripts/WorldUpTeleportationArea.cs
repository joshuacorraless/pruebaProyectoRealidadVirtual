using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

public class WorldUpTeleportationArea : TeleportationArea
{
    protected override void Awake()
    {
        base.Awake();
        // Imported floor meshes have rotated local axes. Replace XRI's local-up
        // filter in both selection and request generation with the world-up check.
        filterSelectionByHitNormal = false;
    }

    public override bool IsSelectableBy(IXRSelectInteractor interactor)
    {
        if (!base.IsSelectableBy(interactor)) return false;
        return !(interactor is XRRayInteractor ray) ||
            (ray.TryGetCurrent3DRaycastHit(out var hit) && IsWalkable(hit.normal));
    }

    protected override bool GenerateTeleportRequest(IXRInteractor interactor, RaycastHit hit,
        ref TeleportRequest request)
    {
        return IsWalkable(hit.normal) && base.GenerateTeleportRequest(interactor, hit, ref request);
    }

    bool IsWalkable(Vector3 normal)
    {
        return normal.sqrMagnitude > 0.5f &&
            Vector3.Angle(Vector3.up, normal) <= upNormalToleranceDegrees;
    }
}
