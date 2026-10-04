using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

// puerta que se abre agarrandola con Grip y moviendo la mano. gira sobre su borde, donde esta la bisagra
[RequireComponent(typeof(Rigidbody))]
public class HingedDoor : XRBaseInteractable
{
    // cuanto abre la puerta (grados). cerrada es 0 y abre hacia el lado negativo
    public float maxOpenAngle = 100f;

    // que tan rapido sigue a la mano (grados por segundo)
    public float turnSpeed = 240f;

    Rigidbody body;

    // posicion y rotacion de la puerta cerrada. la bisagra nunca se mueve de aqui
    Vector3 pivot;
    Quaternion closedRotation;

    // punto de la puerta donde la agarro el control, para que el rayo se quede ahi
    Transform contactPoint;

    // angulo actual y angulo al que la mano la quiere llevar
    float angle;
    float targetAngle;

    // direccion de la mano vista desde la bisagra en el frame anterior
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

        // solo una mano a la vez
        selectMode = InteractableSelectMode.Single;

        GameObject contactObject = new GameObject("Punto de agarre");
        contactPoint = contactObject.transform;
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
        if (hasBearing)
            contactPoint.position = args.interactorObject.GetAttachTransform(this).position;

        targetAngle = angle;
    }

    public override void ProcessInteractable(XRInteractionUpdateOrder.UpdatePhase updatePhase)
    {
        base.ProcessInteractable(updatePhase);

        // la puerta es fisica, se mueve en el paso Fixed
        if (!isSelected || updatePhase != XRInteractionUpdateOrder.UpdatePhase.Fixed) return;

        float bearing;
        if (!TryGetBearing(out bearing))
        {
            hasBearing = false;
            return;
        }

        // lo que giro la mano alrededor de la bisagra desde el frame anterior se le suma a la puerta
        if (hasBearing)
            targetAngle = Mathf.Clamp(targetAngle + Mathf.DeltaAngle(previousBearing, bearing), -maxOpenAngle, 0f);

        previousBearing = bearing;
        hasBearing = true;

        angle = Mathf.MoveTowards(angle, targetAngle, turnSpeed * Time.fixedDeltaTime);

        // siempre en el mismo pivot: la mano solo la hace girar, no la puede jalar fuera del marco
        body.MovePosition(pivot);
        body.MoveRotation(closedRotation * Quaternion.AngleAxis(angle, Vector3.up));
    }

    // angulo (en grados, visto desde arriba) de la mano respecto a la bisagra. false si no se puede calcular
    bool TryGetBearing(out float bearing)
    {
        bearing = 0f;

        Vector3 hand = interactorsSelecting[0].GetAttachTransform(this).position;
        Vector3 direction = Quaternion.Inverse(closedRotation) * (hand - pivot);

        // cuando el visor pierde el control la posicion llega invalida
        float distance = direction.sqrMagnitude;
        if (float.IsNaN(distance) || float.IsInfinity(distance)) return false;

        // con la mano justo encima de la bisagra el angulo salta para todo lado
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0004f) return false;

        bearing = Mathf.Atan2(-direction.z, direction.x) * Mathf.Rad2Deg;
        return true;
    }
}
