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
            CreateRuntimeSky();
            if (sun) { originalSunIntensity = sun.intensity; originalSunRotation = sun.transform.rotation; originalSunColor = sun.color; }
            originalLights = new float[interiorLights == null ? 0 : interiorLights.Length];
            for (int i = 0; i < originalLights.Length; i++) if (interiorLights[i]) originalLights[i] = interiorLights[i].intensity;
        }
        void OnEnable()
        {
            if (originalSky && !runtimeSky) { CreateRuntimeSky(); RefreshNow(); }
        }
        void CreateRuntimeSky()
        {
            if (!originalSky) return;
            var skyShader = Resources.Load<Shader>("CampusTimeSkybox");
            var cubemap = originalSky.HasProperty("_Tex") ? originalSky.GetTexture("_Tex") : null;
            if (skyShader && cubemap)
            {
                runtimeSky = new Material(skyShader) { name = "Local time sky (runtime)" };
                runtimeSky.SetTexture("_Tex", cubemap);
            }
            else runtimeSky = new Material(originalSky) { name = "Local time sky (runtime)" };
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
            // A 24-hour arc: east at 06:00, high in the southern sky at noon,
            // west at 18:00, and below the horizon overnight.
            float angle = (hour - 6f) * Mathf.PI / 12f;
            float height = Mathf.Sin(angle);
            Vector3 sunDirection = new Vector3(Mathf.Cos(angle), height * 1.73205f, -Mathf.Abs(height)).normalized;
            if (runtimeSky)
            {
                if (runtimeSky.HasProperty("_Exposure")) runtimeSky.SetFloat("_Exposure", Mathf.Lerp(.11f, 1f, day));
                if (runtimeSky.HasProperty("_Tint")) runtimeSky.SetColor("_Tint", Color.Lerp(new Color(.25f,.32f,.48f, .5f), new Color(.5f,.5f,.5f,.5f), day));
                if (runtimeSky.HasProperty("_SunDirection")) runtimeSky.SetVector("_SunDirection", sunDirection);
                if (runtimeSky.HasProperty("_SunStrength")) runtimeSky.SetFloat("_SunStrength", Mathf.SmoothStep(0, 1, Mathf.InverseLerp(-.03f, .04f, sunDirection.y)));
                RenderSettings.skybox = runtimeSky;
            }
            RenderSettings.ambientIntensity = originalAmbient * Mathf.Lerp(.42f, 1f, day);
            if (sun)
            {
                sun.intensity = originalSunIntensity * day;
                sun.transform.rotation = Quaternion.LookRotation(-sunDirection, Vector3.up);
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
