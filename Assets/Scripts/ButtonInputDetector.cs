using UnityEngine;
using UnityEngine.InputSystem;

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

    // que tan enfrente del control aparece el boton (metros)
    public float spawnDistance = 0.25f;

    void Start()
    {
        if (head == null && Camera.main != null)
            head = Camera.main.transform;

        if (toggleButton != null)
            toggleButton.SetActive(false);
    }

    void OnEnable()
    {
        primaryButtonAction.Enable();
    }

    void OnDisable()
    {
        primaryButtonAction.Disable();
    }

    void Update()
    {
        if (primaryButtonAction.WasPressedThisFrame())
        {
            OnButtonDown();
        }
    }

    void OnButtonDown()
    {
        if (toggleButton == null) return;

        bool activating = !toggleButton.activeSelf;

        if (activating)
        {
            // enfrente del control, no encima, para que el rayo lo pueda tocar
            Vector3 position = rightControllerTransform.position + rightControllerTransform.forward * spawnDistance;
            toggleButton.transform.position = position;

            // el frente de un canvas mira hacia -Z, asi que su forward apunta lejos de la cabeza
            if (head != null)
                toggleButton.transform.rotation = Quaternion.LookRotation(position - head.position, Vector3.up);
            else
                toggleButton.transform.rotation = rightControllerTransform.rotation;
        }

        toggleButton.SetActive(activating);
    }
}
