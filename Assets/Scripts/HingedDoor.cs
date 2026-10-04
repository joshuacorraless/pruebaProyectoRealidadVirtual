using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

[RequireComponent(typeof(Rigidbody))]
public class HingedDoor : XRBaseInteractable
{
    Rigidbody body;
    Transform contactPoint;
    Vector3 pivot;
    Quaternion closedRotation;
    float angle;
    float targetAngle;
    float previousBearing;
    bool hasBearing;

    protected override void Awake()
    {
        base.Awake();
        body = GetComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
        pivot = body.position;
        closedRotation = body.rotation;
        selectMode = InteractableSelectMode.Single;
        contactPoint = new GameObject("Punto de agarre").transform;
        contactPoint.SetParent(transform, false);
    }

    public override Transform GetAttachTransform(IXRInteractor interactor)
    {
        return contactPoint != null ? contactPoint : transform;
    }

    protected override void OnSelectEntered(SelectEnterEventArgs args)
    {
        base.OnSelectEntered(args);
        hasBearing = TryGetBearing(out previousBearing);
        if (hasBearing) contactPoint.position = args.interactorObject.GetAttachTransform(this).position;
        targetAngle = angle;
    }

    public override void ProcessInteractable(XRInteractionUpdateOrder.UpdatePhase phase)
    {
        base.ProcessInteractable(phase);
        if (!isSelected || phase != XRInteractionUpdateOrder.UpdatePhase.Fixed) return;
        if (!TryGetBearing(out float bearing))
        {
            hasBearing = false;
            return;
        }

        if (hasBearing)
            targetAngle = Mathf.Clamp(targetAngle + Mathf.DeltaAngle(previousBearing, bearing), -100f, 0f);
        previousBearing = bearing;
        hasBearing = true;
        angle = Mathf.MoveTowards(angle, targetAngle, 240f * Time.fixedDeltaTime);

        // A fixed kinematic pivot cannot be pulled away by the grab or hinge solver.
        body.MovePosition(pivot);
        body.MoveRotation(closedRotation * Quaternion.AngleAxis(angle, Vector3.up));
    }

    bool TryGetBearing(out float bearing)
    {
        var direction = Quaternion.Inverse(closedRotation) *
            (interactorsSelecting[0].GetAttachTransform(this).position - pivot);
        float distance = direction.sqrMagnitude;
        bearing = 0f;
        // Ignore invalid tracking and a grip directly on the hinge axis.
        if (float.IsNaN(distance) || float.IsInfinity(distance)) return false;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0004f) return false;
        bearing = Mathf.Atan2(-direction.z, direction.x) * Mathf.Rad2Deg;
        return true;
    }
}
