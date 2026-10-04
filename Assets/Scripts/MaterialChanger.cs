using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;
using UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets;

public class MaterialChanger : MonoBehaviour
{
    public RadialMenu radialMenuPrefab;
    public Renderer targetRenderer;
    public Renderer[] additionalRenderers = new Renderer[0];
    public string menuTitle = "Acabados de pared";
    public bool allowRestoreOriginal;
    [Min(0)] public int materialSlot;
    public Material[] materials = new Material[6];

    public static bool IsAnyMenuOpen => activeChanger != null;
    public bool CanRestoreOriginal => allowRestoreOriginal && originalMaterials.Count > 0;
    static MaterialChanger activeChanger;

    public static void CloseActiveMenu()
    {
        if (activeChanger != null) activeChanger.CloseMenu();
    }

    XRBaseInteractable interactable;
    RadialMenu currentMenu;
    InputAction navigateAction;
    InputAction applyAction;
    InputAction nextPageAction;
    int openedFrame;
    int lastActionFrame = -1;
    int closedFrame = -1;
    readonly List<XRBaseInputInteractor> pointingControllers = new List<XRBaseInputInteractor>();
    readonly List<Behaviour> pausedLocomotion = new List<Behaviour>();
    readonly Dictionary<Renderer, Material> originalMaterials = new Dictionary<Renderer, Material>();

    void Awake()
    {
        // La brocha usa XRSimpleInteractable; los muebles reutilizan su XRGrabInteractable.
        interactable = GetComponent<XRBaseInteractable>();
        if (interactable == null)
        {
            enabled = false;
            return;
        }
        interactable.allowGazeInteraction = false;
    }

    void OnEnable()
    {
        interactable.hoverEntered.AddListener(OnHoverEntered);
        interactable.hoverExited.AddListener(OnHoverExited);
        interactable.selectEntered.AddListener(OnGrabbed);
    }

    void OnDisable()
    {
        if (interactable != null)
        {
            interactable.hoverEntered.RemoveListener(OnHoverEntered);
            interactable.hoverExited.RemoveListener(OnHoverExited);
            interactable.selectEntered.RemoveListener(OnGrabbed);
        }
        pointingControllers.Clear();
        CloseMenu();
    }

    void Update()
    {
        if (currentMenu != null)
        {
            if (nextPageAction.WasPressedThisFrame())
                currentMenu.ChangePage(1);
            Vector2 stick = navigateAction.ReadValue<Vector2>();
            if (stick.sqrMagnitude > 0.3f)
                currentMenu.SelectDirection(stick);
            return;
        }

        if (closedFrame == Time.frameCount || interactable.isSelected) return;
        foreach (XRBaseInputInteractor controller in pointingControllers)
        {
            if (controller != null && controller.activateInput.ReadWasPerformedThisFrame())
            {
                OpenMenu(controller.handedness);
                break;
            }
        }
    }

    void LateUpdate()
    {
        // La UI actualiza primero el boton apuntado; el trigger confirma despues.
        if (currentMenu != null && Time.frameCount > openedFrame && applyAction.WasPressedThisFrame())
            ConfirmSelection();
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
        // El menu permanece abierto al pasar de la brocha a sus botones.
    }

    void OnGrabbed(SelectEnterEventArgs args) => CloseMenu();

    public void OpenMenu() => OpenMenu(InteractorHandedness.Right);

    void OpenMenu(InteractorHandedness hand)
    {
        if (currentMenu != null || radialMenuPrefab == null) return;
        if (allowRestoreOriginal)
        {
            RememberOriginal(targetRenderer);
            foreach (Renderer renderer in additionalRenderers) RememberOriginal(renderer);
        }
        CloseActiveMenu();
        FindFirstObjectByType<ButtonInputDetector>()?.CloseMenu();
        FindFirstObjectByType<WelcomeMenu>()?.CloseMenu();

        string handName = hand == InteractorHandedness.Left ? "LeftHand" : "RightHand";
        navigateAction = new InputAction("Material Navigate", InputActionType.Value,
            "<XRController>{" + handName + "}/primary2DAxis");
        applyAction = new InputAction("Material Apply", InputActionType.Button,
            "<XRController>{" + handName + "}/triggerPressed");
        nextPageAction = new InputAction("Material Next Page", InputActionType.Button,
            "<XRController>{" + handName + "}/primary2DAxisClick");
        navigateAction.Enable();
        applyAction.Enable();
        nextPageAction.Enable();

        activeChanger = this;
        openedFrame = Time.frameCount;
        Vector3 position = transform.position;
        Quaternion rotation = transform.rotation;
        float menuDistance = 1.5f;
        if (Camera.main != null)
        {
            // Los acabados se leen de frente y el panel queda fijo hasta cerrarlo.
            var head = Camera.main.transform;
            var forward = Vector3.ProjectOnPlane(head.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
            if (Physics.Raycast(head.position, forward, out var hit, menuDistance,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                menuDistance = Mathf.Max(0.025f, hit.distance - 0.08f);
            position = head.position + forward * menuDistance;
            rotation = Quaternion.LookRotation(forward, Vector3.up);
        }
        currentMenu = Instantiate(radialMenuPrefab, position, rotation);
        // Conserva el tamaño aparente al acercar el panel para evitar una pared.
        currentMenu.transform.localScale *= menuDistance / 1.5f;
        currentMenu.SpawnButtons(this, materials);

        foreach (var manager in FindObjectsByType<ControllerInputActionManager>(FindObjectsSortMode.None))
            Pause(manager);
        foreach (var provider in FindObjectsByType<LocomotionProvider>(FindObjectsSortMode.None))
            Pause(provider);
    }

    void Pause(Behaviour behaviour)
    {
        if (!behaviour.enabled) return;
        pausedLocomotion.Add(behaviour);
        behaviour.enabled = false;
    }

    public void SelectMaterial(int index)
    {
        if (currentMenu != null) currentMenu.Select(index);
    }

    public void ConfirmSelection()
    {
        if (currentMenu != null) ApplyMaterial(currentMenu.SelectedIndex);
    }

    public void ApplyMaterial(int index)
    {
        if (currentMenu == null || Time.frameCount <= openedFrame || lastActionFrame == Time.frameCount) return;
        lastActionFrame = Time.frameCount;
        if (index == RadialMenu.PreviousPage || index == RadialMenu.NextPage)
        {
            currentMenu.ChangePage(index == RadialMenu.NextPage ? 1 : -1);
            return;
        }
        if (index == RadialMenu.Close)
        {
            CloseMenu();
            return;
        }
        if (index == RadialMenu.RestoreOriginal)
        {
            if (!CanRestoreOriginal) return;
            foreach (var original in originalMaterials) ApplyTo(original.Key, original.Value);
            CloseMenu();
            return;
        }
        if (index < 0 || index >= materials.Length || materials[index] == null) return;

        ApplyTo(targetRenderer, materials[index]);
        foreach (Renderer renderer in additionalRenderers)
            ApplyTo(renderer, materials[index]);
        CloseMenu();
    }

    void RememberOriginal(Renderer renderer)
    {
        // Capture each surface before its first change, and keep it across menu openings.
        if (renderer == null || originalMaterials.ContainsKey(renderer)) return;
        var slots = renderer.sharedMaterials;
        if (materialSlot >= 0 && materialSlot < slots.Length)
            originalMaterials.Add(renderer, slots[materialSlot]);
    }

    void ApplyTo(Renderer renderer, Material material)
    {
        if (renderer == null) return;
        var slots = renderer.sharedMaterials;
        if (materialSlot < 0 || materialSlot >= slots.Length) return;
        slots[materialSlot] = material;
        renderer.sharedMaterials = slots;
    }

    public void CloseMenu()
    {
        if (currentMenu != null) Destroy(currentMenu.gameObject);
        currentMenu = null;
        navigateAction?.Dispose();
        applyAction?.Dispose();
        nextPageAction?.Dispose();
        navigateAction = null;
        applyAction = null;
        nextPageAction = null;
        foreach (Behaviour behaviour in pausedLocomotion)
            if (behaviour != null) behaviour.enabled = true;
        pausedLocomotion.Clear();
        if (activeChanger == this) activeChanger = null;
        closedFrame = Time.frameCount;
    }
}
