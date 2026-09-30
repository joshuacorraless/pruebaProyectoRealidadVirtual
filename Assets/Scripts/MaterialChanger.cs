using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

[RequireComponent(typeof(XRSimpleInteractable))]
public class MaterialChanger : MonoBehaviour
{
    // prefab del menu (Assets/Prefabs/RadialMenu)
    public RadialMenu radialMenuPrefab;

    // hijo "Collider": chico al inicio, se agranda mientras el menu esta abierto
    public Transform menuCollider;

    // escala del collider con el menu abierto. la malla mide 0.01, x95 = 0.95 m: cubre el circulo (radio 32.5)
    // mas medio boton (12.5) que sale por fuera, con un poco de margen.
    // el grosor (y) tiene que cubrir los botones para que la UI no le tape el collider al rayo
    public Vector3 openColliderScale = new Vector3(95f, 10f, 95f);

    // materiales fijos del menu: un boton por cada uno, en este orden (empieza arriba, sentido del reloj)
    public Material[] materials = new Material[6];

    XRSimpleInteractable interactable;
    RadialMenu currentMenu;
    Vector3 closedColliderScale;

    // controles cuyo rayo esta apuntando al collider
    readonly List<XRBaseInputInteractor> pointingControllers = new List<XRBaseInputInteractor>();

    void Awake()
    {
        interactable = GetComponent<XRSimpleInteractable>();

        // solo los controles, la mirada no hace nada
        interactable.allowGazeInteraction = false;

        if (menuCollider == null)
            menuCollider = transform.Find("Collider");

        if (menuCollider != null)
            closedColliderScale = menuCollider.localScale;
    }

    void OnEnable()
    {
        interactable.hoverEntered.AddListener(OnHoverEntered);
        interactable.hoverExited.AddListener(OnHoverExited);
    }

    void OnDisable()
    {
        interactable.hoverEntered.RemoveListener(OnHoverEntered);
        interactable.hoverExited.RemoveListener(OnHoverExited);

        pointingControllers.Clear();
        CloseMenu();
    }

    void Update()
    {
        if (currentMenu != null) return;

        // trigger (Activate en las XRI Default Input Actions) del control que esta apuntando
        foreach (XRBaseInputInteractor controller in pointingControllers)
        {
            if (controller.activateInput.ReadWasPerformedThisFrame())
            {
                OpenMenu();
                break;
            }
        }
    }

    void OnHoverEntered(HoverEnterEventArgs args)
    {
        if (args.interactorObject is XRGazeInteractor) return;

        if (args.interactorObject is XRBaseInputInteractor controller && !pointingControllers.Contains(controller))
            pointingControllers.Add(controller);
    }

    void OnHoverExited(HoverExitEventArgs args)
    {
        if (args.interactorObject is XRBaseInputInteractor controller)
            pointingControllers.Remove(controller);

        // ningun control apunta al menu: se cierra
        if (pointingControllers.Count == 0)
            CloseMenu();
    }

    void OpenMenu()
    {
        if (currentMenu != null || radialMenuPrefab == null) return;

        // mismo lugar y orientacion que el paint roller
        currentMenu = Instantiate(radialMenuPrefab, transform.position, transform.rotation);
        currentMenu.SpawnButtons(this, materials);

        // collider grande para que no se cierre al mover el rayo hacia los botones
        if (menuCollider != null)
            menuCollider.localScale = openColliderScale;
    }

    void CloseMenu()
    {
        if (currentMenu != null)
        {
            Destroy(currentMenu.gameObject);
            currentMenu = null;
        }

        if (menuCollider != null)
            menuCollider.localScale = closedColliderScale;
    }
}
