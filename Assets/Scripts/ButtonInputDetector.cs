using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;
using UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets;

public class ButtonInputDetector : MonoBehaviour
{
    // boton A del control derecho (funciona con el simulador y con el visor real)
    public InputAction primaryButtonAction = new InputAction(
        "PrimaryButton", InputActionType.Button, "<XRController>{RightHand}/primaryButton");

    // stick del control derecho: con el menu abierto adelanta o atrasa la hora
    InputAction timeStickAction = new InputAction(
        "TimeStick", InputActionType.Value, "<XRController>{RightHand}/primary2DAxis");

    // posicion del control
    public Transform rightControllerTransform;

    // cabeza del jugador, para que el boton aparezca enfrente
    public Transform head;

    // canvas del boton
    public GameObject toggleButton;

    // que tan enfrente del jugador aparece el boton (metros)
    public float spawnDistance = 0.75f;

    // horas que avanza por segundo con el stick a fondo
    public float hoursPerSecond = 4f;

    DayNightCycle cycle;

    // lo que se apaga mientras el menu esta abierto, para que el stick no mueva al jugador
    readonly List<Behaviour> pausedLocomotion = new List<Behaviour>();

    public bool IsOpen
    {
        get { return toggleButton != null && toggleButton.activeSelf; }
    }

    void Start()
    {
        if (head == null && Camera.main != null)
            head = Camera.main.transform;

        cycle = FindFirstObjectByType<DayNightCycle>();

        if (toggleButton != null)
            toggleButton.SetActive(false);
    }

    void OnEnable()
    {
        primaryButtonAction.Enable();
        timeStickAction.Enable();
    }

    void OnDisable()
    {
        primaryButtonAction.Disable();
        timeStickAction.Disable();
        CloseMenu();
    }

    void Update()
    {
        if (primaryButtonAction.WasPressedThisFrame())
        {
            ToggleMenu();
        }

        if (!IsOpen || cycle == null) return;

        // stick a la derecha adelanta la hora, a la izquierda la atrasa
        float horizontal = timeStickAction.ReadValue<Vector2>().x;
        if (Mathf.Abs(horizontal) > 0.25f)
            cycle.AdjustHours(horizontal * hoursPerSecond * Time.deltaTime);
    }

    public void ToggleMenu()
    {
        if (toggleButton == null) return;

        if (IsOpen)
        {
            CloseMenu();
            return;
        }

        Transform origin = head != null ? head : rightControllerTransform;
        if (origin == null) return;

        // cierra los otros menus que esten abiertos
        MaterialChanger.CloseActiveMenu();

        WelcomeMenu welcomeMenu = FindFirstObjectByType<WelcomeMenu>();
        if (welcomeMenu != null)
            welcomeMenu.CloseMenu();

        // enfrente del jugador y derecho, aunque este mirando hacia arriba o hacia abajo
        Vector3 forward = Vector3.ProjectOnPlane(origin.forward, Vector3.up).normalized;
        if (forward.sqrMagnitude < 0.01f)
            forward = Vector3.forward;

        // el frente de un canvas mira hacia -Z, asi que su forward apunta lejos de la cabeza
        toggleButton.transform.position = origin.position + forward * spawnDistance;
        toggleButton.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
        toggleButton.SetActive(true);

        // el stick cambia la hora, asi que no debe mover ni teletransportar al jugador
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

    public void CloseMenu()
    {
        if (toggleButton != null)
            toggleButton.SetActive(false);

        foreach (Behaviour component in pausedLocomotion)
        {
            if (component != null)
                component.enabled = true;
        }
        pausedLocomotion.Clear();
    }

    void OnDestroy()
    {
        primaryButtonAction.Dispose();
        timeStickAction.Dispose();
    }
}
