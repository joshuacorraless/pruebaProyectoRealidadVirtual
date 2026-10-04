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
    // prefab del menu (Assets/Prefabs/RadialMenu)
    public RadialMenu radialMenuPrefab;

    // a quien se le cambia el material. additionalRenderers cambian junto con el (las piezas de un mueble)
    public Renderer targetRenderer;
    public Renderer[] additionalRenderers = new Renderer[0];

    // cual de los materiales del renderer se cambia
    public int materialSlot;

    // si el menu trae el boton del centro para volver al material con el que cargo la escena
    public bool allowRestoreOriginal;

    // materiales del menu: un boton por cada uno, de 6 en 6 (empieza arriba, sentido del reloj)
    public Material[] materials = new Material[6];

    // que tan lejos del jugador aparece el menu (metros)
    public float menuDistance = 1.5f;

    // el que tiene el menu abierto. solo puede haber uno a la vez
    static MaterialChanger activeChanger;

    XRBaseInteractable interactable;
    RadialMenu currentMenu;

    // stick y trigger de la mano que abrio el menu
    InputAction stickAction;
    InputAction triggerAction;
    bool stickCentered;

    int openedFrame;
    int closedFrame = -1;

    // controles cuyo rayo esta apuntando al objeto
    readonly List<XRBaseInputInteractor> pointingControllers = new List<XRBaseInputInteractor>();

    // lo que se apaga mientras el menu esta abierto, para que el stick no mueva al jugador
    readonly List<Behaviour> pausedLocomotion = new List<Behaviour>();

    // material que tenia cada renderer antes del primer cambio
    readonly Dictionary<Renderer, Material> originalMaterials = new Dictionary<Renderer, Material>();

    void Awake()
    {
        // la brocha tiene un XRSimpleInteractable y los muebles un XRGrabInteractable, sirve cualquiera
        interactable = GetComponent<XRBaseInteractable>();

        if (interactable == null)
        {
            enabled = false;
            return;
        }

        // solo los controles, la mirada no hace nada
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
            UpdatePage();
            return;
        }

        // si se acaba de cerrar, ese mismo trigger no lo vuelve a abrir. agarrado tampoco se abre
        if (closedFrame == Time.frameCount || interactable.isSelected) return;

        // trigger (Activate en las XRI Default Input Actions) del control que esta apuntando
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
        // en LateUpdate porque la UI marca primero el boton apuntado y despues el trigger lo confirma
        if (currentMenu != null && triggerAction.WasPressedThisFrame())
            ConfirmSelection();
    }

    // stick a la derecha: pagina siguiente. a la izquierda: la anterior
    void UpdatePage()
    {
        float horizontal = stickAction.ReadValue<Vector2>().x;

        // hay que soltar el stick entre una pagina y otra, si no pasaria una por frame
        if (Mathf.Abs(horizontal) < 0.3f)
        {
            stickCentered = true;
        }
        else if (stickCentered && Mathf.Abs(horizontal) > 0.6f)
        {
            stickCentered = false;
            currentMenu.ChangePage(horizontal > 0f ? 1 : -1);
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
        // el menu no se cierra aqui: queda abierto mientras el rayo va hacia los botones
        if (args.interactorObject is XRBaseInputInteractor controller)
            pointingControllers.Remove(controller);
    }

    void OnGrabbed(SelectEnterEventArgs args)
    {
        CloseMenu();
    }

    void OpenMenu(InteractorHandedness hand)
    {
        if (currentMenu != null || radialMenuPrefab == null) return;

        if (allowRestoreOriginal)
        {
            RememberOriginal(targetRenderer);
            foreach (Renderer renderer in additionalRenderers)
                RememberOriginal(renderer);
        }

        // cierra los otros menus que esten abiertos
        CloseActiveMenu();

        ButtonInputDetector dayNightMenu = FindFirstObjectByType<ButtonInputDetector>();
        if (dayNightMenu != null)
            dayNightMenu.CloseMenu();

        WelcomeMenu welcomeMenu = FindFirstObjectByType<WelcomeMenu>();
        if (welcomeMenu != null)
            welcomeMenu.CloseMenu();

        string handName = hand == InteractorHandedness.Left ? "LeftHand" : "RightHand";
        stickAction = new InputAction(
            "MenuStick", InputActionType.Value, "<XRController>{" + handName + "}/primary2DAxis");
        triggerAction = new InputAction(
            "MenuTrigger", InputActionType.Button, "<XRController>{" + handName + "}/triggerPressed");
        stickAction.Enable();
        triggerAction.Enable();
        stickCentered = false;

        activeChanger = this;
        openedFrame = Time.frameCount;

        Vector3 position = transform.position;
        Quaternion rotation = transform.rotation;
        float distance = menuDistance;

        if (Camera.main != null)
        {
            // enfrente del jugador y derecho, aunque este mirando hacia arriba o hacia abajo
            Transform head = Camera.main.transform;
            Vector3 forward = Vector3.ProjectOnPlane(head.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < 0.01f)
                forward = Vector3.forward;

            // si hay una pared antes, el menu se pone un poco antes de la pared
            RaycastHit hit;
            if (Physics.Raycast(head.position, forward, out hit, menuDistance,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                distance = Mathf.Max(0.025f, hit.distance - 0.08f);

            position = head.position + forward * distance;
            rotation = Quaternion.LookRotation(forward, Vector3.up);
        }

        currentMenu = Instantiate(radialMenuPrefab, position, rotation);

        // mas cerca se hace mas chico, para que se vea igual de grande
        currentMenu.transform.localScale *= distance / menuDistance;

        Material original = null;
        if (allowRestoreOriginal && targetRenderer != null)
            originalMaterials.TryGetValue(targetRenderer, out original);

        currentMenu.SpawnButtons(this, materials, original);

        // el stick pasa las paginas del menu, asi que no debe mover ni teletransportar al jugador
        foreach (ControllerInputActionManager manager in FindObjectsByType<ControllerInputActionManager>(FindObjectsSortMode.None))
            PauseLocomotion(manager);
        foreach (LocomotionProvider provider in FindObjectsByType<LocomotionProvider>(FindObjectsSortMode.None))
            PauseLocomotion(provider);
    }

    void PauseLocomotion(Behaviour component)
    {
        if (!component.enabled) return;

        pausedLocomotion.Add(component);
        component.enabled = false;
    }

    // lo llama el boton cuando el rayo entra
    public void SelectMaterial(int index)
    {
        if (currentMenu != null)
            currentMenu.Select(index);
    }

    // lo llama el boton cuando el rayo sale
    public void DeselectMaterial(int index)
    {
        if (currentMenu != null && currentMenu.SelectedIndex == index)
            currentMenu.Select(RadialMenu.None);
    }

    // trigger con el menu abierto: aplica el boton apuntado. si no apunta a ninguno solo cierra el menu
    public void ConfirmSelection()
    {
        // el trigger que abrio el menu no cuenta
        if (currentMenu == null || Time.frameCount <= openedFrame) return;

        int index = currentMenu.SelectedIndex;

        if (index == RadialMenu.RestoreOriginal)
        {
            foreach (KeyValuePair<Renderer, Material> original in originalMaterials)
                ApplyTo(original.Key, original.Value);
        }
        else if (index >= 0 && index < materials.Length && materials[index] != null)
        {
            ApplyTo(targetRenderer, materials[index]);
            foreach (Renderer renderer in additionalRenderers)
                ApplyTo(renderer, materials[index]);
        }

        CloseMenu();
    }

    // guarda el material del renderer la primera vez, antes de cambiarlo
    void RememberOriginal(Renderer renderer)
    {
        if (renderer == null || originalMaterials.ContainsKey(renderer)) return;

        Material[] slots = renderer.sharedMaterials;
        if (materialSlot >= 0 && materialSlot < slots.Length)
            originalMaterials.Add(renderer, slots[materialSlot]);
    }

    void ApplyTo(Renderer renderer, Material material)
    {
        if (renderer == null) return;

        Material[] slots = renderer.sharedMaterials;
        if (materialSlot < 0 || materialSlot >= slots.Length) return;

        slots[materialSlot] = material;
        renderer.sharedMaterials = slots;
    }

    public void CloseMenu()
    {
        if (currentMenu != null)
        {
            Destroy(currentMenu.gameObject);
            currentMenu = null;
            closedFrame = Time.frameCount;
        }

        if (stickAction != null)
        {
            stickAction.Dispose();
            triggerAction.Dispose();
            stickAction = null;
            triggerAction = null;
        }

        foreach (Behaviour component in pausedLocomotion)
        {
            if (component != null)
                component.enabled = true;
        }
        pausedLocomotion.Clear();

        if (activeChanger == this)
            activeChanger = null;
    }

    // cierra el menu de materiales que este abierto, sea de la pared o de un mueble
    public static void CloseActiveMenu()
    {
        if (activeChanger != null)
            activeChanger.CloseMenu();
    }
}
