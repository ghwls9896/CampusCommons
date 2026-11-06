using System;
using UnityEngine;

namespace CampusCommons.GrandLibrary
{
    // The lighting follows the device's local clock, without requiring a server.
    public class LocalTimeLighting : MonoBehaviour
    {
        public Light sun;
        public Light[] interiorLights;
        [Tooltip("Use the computer's local clock. Turn off to preview a chosen hour.")]
        public bool useLocalClock = true;
        [Range(0, 24)] public float previewHour = 12;
        public float refreshSeconds = 5;

        Material originalSky, runtimeSky;
        float originalAmbient, originalSunIntensity;
        Quaternion originalSunRotation;
        Color originalSunColor;
        float[] originalLights;
        float nextUpdate;
        bool suspended;
        Player player;

        void Awake()
        {
            originalSky = RenderSettings.skybox;
            originalAmbient = RenderSettings.ambientIntensity;
            if (originalSky) runtimeSky = new Material(originalSky) { name = "Local time sky (runtime)" };
            if (sun) { originalSunIntensity = sun.intensity; originalSunRotation = sun.transform.rotation; originalSunColor = sun.color; }
            originalLights = new float[interiorLights == null ? 0 : interiorLights.Length];
            for (int i = 0; i < originalLights.Length; i++) if (interiorLights[i]) originalLights[i] = interiorLights[i].intensity;
        }
        void Start() { RefreshNow(); }
        void Update()
        {
            if (Time.unscaledTime < nextUpdate) return;
            nextUpdate = Time.unscaledTime + Mathf.Max(.25f, refreshSeconds);
            RefreshNow();
        }
        public void RefreshNow()
        {
            if (!player) player = FindAnyObjectByType<Player>();
            bool school = player && player.currentSpace == Portal.Space.Escape;
            if (school)
            {
                if (!suspended) RestoreOriginal();
                suspended = true;
                return;
            }
            suspended = false;
            float hour = useLocalClock ? (float)DateTime.Now.TimeOfDay.TotalHours : previewHour;
            ApplyHour(hour);
        }
        public void ApplyHour(float hour)
        {
            hour = Mathf.Repeat(hour, 24f);
            // Dawn 05:30-07:30; dusk 17:00-19:00, with smooth transitions.
            float day = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(5.5f, 7.5f, hour)) *
                        (1f - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(17f, 19f, hour)));
            if (runtimeSky)
            {
                if (runtimeSky.HasProperty("_Exposure")) runtimeSky.SetFloat("_Exposure", Mathf.Lerp(.11f, 1f, day));
                if (runtimeSky.HasProperty("_Tint")) runtimeSky.SetColor("_Tint", Color.Lerp(new Color(.25f,.32f,.48f, .5f), new Color(.5f,.5f,.5f,.5f), day));
                RenderSettings.skybox = runtimeSky;
            }
            RenderSettings.ambientIntensity = originalAmbient * Mathf.Lerp(.42f, 1f, day);
            if (sun)
            {
                sun.intensity = originalSunIntensity * day;
                float elevation = Mathf.Lerp(8f, 58f, Mathf.Sin(Mathf.Clamp01((hour - 6f) / 12f) * Mathf.PI));
                sun.transform.rotation = Quaternion.Euler(elevation, Mathf.Lerp(-90f, 90f, Mathf.Clamp01((hour - 6f) / 12f)), 0f);
                sun.color = Color.Lerp(new Color(1f,.55f,.29f), originalSunColor, day);
            }
            for (int i = 0; i < originalLights.Length; i++)
                if (interiorLights[i]) interiorLights[i].intensity = originalLights[i] * Mathf.Lerp(1.65f, .72f, day);
        }
        void RestoreOriginal()
        {
            RenderSettings.skybox = originalSky;
            RenderSettings.ambientIntensity = originalAmbient;
            if (sun) { sun.intensity = originalSunIntensity; sun.transform.rotation = originalSunRotation; sun.color = originalSunColor; }
            if (originalLights != null) for (int i = 0; i < originalLights.Length; i++) if (interiorLights[i]) interiorLights[i].intensity = originalLights[i];
        }
        void OnDisable()
        {
            RestoreOriginal();
            if (runtimeSky) { Destroy(runtimeSky); runtimeSky = null; }
        }
    }
}
