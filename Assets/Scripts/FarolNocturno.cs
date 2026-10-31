using UnityEngine;

// luz de un farol del lote: solo se enciende de noche, segun la hora de DayNightCycle
[RequireComponent(typeof(Light))]
public class FarolNocturno : MonoBehaviour
{
    // ciclo de dia y noche. si no se asigna se busca en la escena
    public DayNightCycle cycle;

    Light lamp;

    void Awake()
    {
        lamp = GetComponent<Light>();

        if (cycle == null)
            cycle = FindFirstObjectByType<DayNightCycle>();
    }

    void Update()
    {
        if (cycle != null)
            lamp.enabled = !cycle.IsDay;
    }
}
