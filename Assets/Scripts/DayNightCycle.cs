using UnityEngine;

public class DayNightCycle : MonoBehaviour
{
    public Light sun;
    public Material skyboxMaterial;
    public float cycleDuration = 30f;
    public bool automaticCycle;

    // day color config
    public Color dayZenith = new Color(0.53f, 0.81f, 0.98f);
    public Color dayHorizon = new Color(0.73f, 0.89f, 1f);
    public float dayIntensity = 1f;

    // night color config
    public Color nightZenith = new Color(0.01f, 0.01f, 0.07f);
    public Color nightHorizon = new Color(0.01f, 0.01f, 0.05f);
    public float nightIntensity = 0.05f;

    float cycle01 = 0.25f;
    Material runtimeSkybox;
    Material originalSkybox;
    Color originalAmbientSky, originalAmbientEquator, originalAmbientGround;
    float originalReflectionIntensity;

    public float Hour => Mathf.Repeat(cycle01 * 24f + 6f, 24f);

    // true mientras el sol esta sobre el horizonte
    public bool IsDay { get; private set; }

    void Awake()
    {
        if (sun == null)
            sun = GetComponent<Light>();
        originalSkybox = RenderSettings.skybox;
        originalAmbientSky = RenderSettings.ambientSkyColor;
        originalAmbientEquator = RenderSettings.ambientEquatorColor;
        originalAmbientGround = RenderSettings.ambientGroundColor;
        originalReflectionIntensity = RenderSettings.reflectionIntensity;
        Material source = skyboxMaterial != null ? skyboxMaterial : originalSkybox;
        if (source != null)
        {
            runtimeSkybox = new Material(source);
            RenderSettings.skybox = runtimeSkybox;
        }
        ApplyCycle();
    }

    void Update()
    {
        if (!automaticCycle) return;
        cycle01 = Mathf.Repeat(cycle01 + Time.deltaTime / Mathf.Max(1f, cycleDuration), 1f);
        ApplyCycle();
    }

    public void AdjustHours(float hours)
    {
        cycle01 = Mathf.Repeat(cycle01 + hours / 24f, 1f);
        ApplyCycle();
    }

    // salta al mediodia (sol en lo mas alto)
    public void SetNoon()
    {
        cycle01 = 0.25f;
        ApplyCycle();
    }

    // salta a la medianoche (sol en lo mas bajo)
    public void SetMidnight()
    {
        cycle01 = 0.75f;
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
        // el sol da una vuelta completa en el ciclo
        float angle = cycle01 * 360f;
        if (sun != null)
            sun.transform.rotation = Quaternion.Euler(angle, 0f, 0f);

        // 1 mediodia, -1 medianoche
        float sunHeight = Mathf.Sin(cycle01 * Mathf.PI * 2f);
        IsDay = sunHeight > 0f;

        // t = 0 cuando es de dia, 1 cuando es de noche
        float t = Mathf.Clamp01(1f - (sunHeight + 1f) / 2f * 2f);

        if (sun != null)
            sun.intensity = Mathf.Lerp(dayIntensity, nightIntensity, t);

        RenderSettings.ambientSkyColor = Color.Lerp(originalAmbientSky, nightZenith, t);
        RenderSettings.ambientEquatorColor = Color.Lerp(originalAmbientEquator, new Color(0.055f, 0.065f, 0.1f), t);
        RenderSettings.ambientGroundColor = Color.Lerp(originalAmbientGround, new Color(0.025f, 0.03f, 0.05f), t);
        RenderSettings.reflectionIntensity = Mathf.Lerp(originalReflectionIntensity, 0.15f, t);

        if (runtimeSkybox != null)
        {
            runtimeSkybox.SetColor("_ZenithColor", Color.Lerp(dayZenith, nightZenith, t));
            runtimeSkybox.SetColor("_HorizonColor", Color.Lerp(dayHorizon, nightHorizon, t));
            runtimeSkybox.SetFloat("_AtmosphereThickness", Mathf.Lerp(0.5f, 1f, t));
            runtimeSkybox.SetFloat("_EnableStars", t);
        }
    }

    void OnDestroy()
    {
        if (RenderSettings.skybox == runtimeSkybox)
            RenderSettings.skybox = originalSkybox;
        RenderSettings.ambientSkyColor = originalAmbientSky;
        RenderSettings.ambientEquatorColor = originalAmbientEquator;
        RenderSettings.ambientGroundColor = originalAmbientGround;
        RenderSettings.reflectionIntensity = originalReflectionIntensity;
        if (runtimeSkybox != null)
            Destroy(runtimeSkybox);
    }
}
