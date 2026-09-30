using UnityEngine;

public class DayNightCycle : MonoBehaviour
{
    public Light sun;
    public Material skyboxMaterial;
    public float cycleDuration = 30f;

    // day color config
    public Color dayZenith = new Color(0.53f, 0.81f, 0.98f);
    public Color dayHorizon = new Color(0.73f, 0.89f, 1f);
    public float dayIntensity = 1f;

    // night color config
    public Color nightZenith = new Color(0.01f, 0.01f, 0.07f);
    public Color nightHorizon = new Color(0.01f, 0.01f, 0.05f);
    public float nightIntensity = 0.05f;

    private float timer;

    // true mientras el sol esta sobre el horizonte
    public bool IsDay { get; private set; }

    void Update()
    {
        timer = (timer + Time.deltaTime) % cycleDuration;
        ApplyCycle();
    }

    // salta al mediodia (sol en lo mas alto)
    public void SetNoon()
    {
        timer = cycleDuration * 0.25f;
        ApplyCycle();
    }

    // salta a la medianoche (sol en lo mas bajo)
    public void SetMidnight()
    {
        timer = cycleDuration * 0.75f;
        ApplyCycle();
    }

    // si es de dia pasa a medianoche, si es de noche pasa a mediodia
    public void ToggleDayNight()
    {
        if (IsDay)
            SetMidnight();
        else
            SetNoon();
    }

    void ApplyCycle()
    {
        // 0 a 1 a lo largo de todo el ciclo
        float cycle01 = timer / cycleDuration;

        // el sol da una vuelta completa en el ciclo
        float angle = cycle01 * 360f;
        sun.transform.rotation = Quaternion.Euler(angle, 0f, 0f);

        // 1 mediodia, -1 medianoche
        float sunHeight = Mathf.Sin(cycle01 * Mathf.PI * 2f);
        IsDay = sunHeight > 0f;

        // t = 0 cuando es de dia, 1 cuando es de noche
        float t = Mathf.Clamp01(1f - (sunHeight + 1f) / 2f * 2f);

        sun.intensity = Mathf.Lerp(dayIntensity, nightIntensity, t);

        if (skyboxMaterial != null)
        {
            skyboxMaterial.SetColor("_ZenithColor", Color.Lerp(dayZenith, nightZenith, t));
            skyboxMaterial.SetColor("_HorizonColor", Color.Lerp(dayHorizon, nightHorizon, t));
            skyboxMaterial.SetFloat("_AtmosphereThickness", Mathf.Lerp(0.5f, 1f, t));
            skyboxMaterial.SetFloat("_EnableStars", Mathf.Lerp(0f, 1f, t));
        }
    }
}