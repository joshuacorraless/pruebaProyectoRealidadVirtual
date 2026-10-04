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

    // posicion del control
    public Transform rightControllerTransform;

    // cabeza del jugador, para que el boton la mire
    public Transform head;

    // canvas del boton
    public GameObject toggleButton;

    public float spawnDistance = 0.75f;

    readonly InputAction timeStick = new InputAction(
        "TimeDial", InputActionType.Value, "<XRController>{RightHand}/primary2DAxis");
    readonly List<Behaviour> pausedLocomotion = new List<Behaviour>();
    DayNightCycle cycle;

    public bool IsOpen => toggleButton != null && toggleButton.activeSelf;

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
        timeStick.Enable();
    }

    void OnDisable()
    {
        primaryButtonAction.Disable();
        timeStick.Disable();
        CloseMenu();
    }

    void Update()
    {
        if (primaryButtonAction.WasPressedThisFrame())
        {
            ToggleMenu();
        }

        if (!IsOpen || cycle == null) return;
        float horizontal = timeStick.ReadValue<Vector2>().x;
        if (Mathf.Abs(horizontal) > 0.25f)
            cycle.AdjustHours(horizontal * 4f * Time.deltaTime);
    }

    public void ToggleMenu()
    {
        if (toggleButton == null) return;
        if (IsOpen)
        {
            CloseMenu();
            return;
        }

        MaterialChanger.CloseActiveMenu();

        Transform origin = head != null ? head : rightControllerTransform;
        if (origin == null) return;
        FindFirstObjectByType<WelcomeMenu>()?.CloseMenu();
        Vector3 forward = Vector3.ProjectOnPlane(origin.forward, Vector3.up).normalized;
        if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
        Vector3 position = origin.position + forward * Mathf.Max(0.75f, spawnDistance);
        toggleButton.transform.SetPositionAndRotation(position, Quaternion.LookRotation(forward, Vector3.up));
        toggleButton.SetActive(true);

        // El stick controla el dial mientras está abierto, sin mover ni teletransportar al jugador.
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
        timeStick.Dispose();
    }
}
