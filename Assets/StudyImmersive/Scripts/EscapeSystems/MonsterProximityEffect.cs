using CampusCommons.LibraryCharacter;
using UnityEngine;

namespace CampusCommons.StudyImmersive
{
    // A small, reversible effect; it does not change the player's stored view angles.
    [DefaultExecutionOrder(1000)]
    public sealed class MonsterProximityEffect : MonoBehaviour
    {
        const float FarDistance = 18f, NearDistance = 2f;
        EscapeGame game;
        Monster monster;
        Transform player;
        Camera view;
        AudioSource heartbeat;
        Texture2D vignette, grain;
        float intensity;
        bool cameraAdjusted;
        Quaternion originalRotation, adjustedRotation;

        public static MonsterProximityEffect Create(EscapeGame game, Monster monster, Transform player, Camera view)
        {
            var root = new GameObject("Monster proximity effects");
            var effect = root.AddComponent<MonsterProximityEffect>();
            effect.game = game; effect.monster = monster; effect.player = player; effect.view = view;
            effect.Initialize();
            return effect;
        }

        void Initialize()
        {
            heartbeat = gameObject.AddComponent<AudioSource>();
            heartbeat.playOnAwake = false; heartbeat.loop = true; heartbeat.spatialBlend = 0f;
            heartbeat.clip = Resources.Load<AudioClip>("EscapeSounds/Night/Heartbeat");
            heartbeat.volume = 0f;
            vignette = new Texture2D(128, 128, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[128 * 128];
            for (int y = 0; y < 128; y++) for (int x = 0; x < 128; x++)
            {
                float dx = (x - 63.5f) / 63.5f, dy = (y - 63.5f) / 63.5f;
                float edge = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.42f, 1.25f, Mathf.Sqrt(dx * dx + dy * dy)));
                pixels[y * 128 + x] = new Color(0, 0, 0, edge);
            }
            vignette.SetPixels32(pixels); vignette.Apply();
            grain = new Texture2D(64, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Point };
            pixels = new Color32[64 * 64];
            uint seed = 0x12345678;
            for (int i = 0; i < pixels.Length; i++)
            {
                seed = seed * 1664525u + 1013904223u;
                byte shade = (byte)(seed >> 24);
                pixels[i] = new Color32(shade, shade, shade, 255);
            }
            grain.SetPixels32(pixels); grain.Apply();
        }

        void Update()
        {
            bool active = game && game.IsRunning && !game.IsEnded && !game.IsAnswering && !game.IsExitConfirmOpen &&
                monster && monster.IsAwakened && player;
            float target = active ? Mathf.InverseLerp(FarDistance, NearDistance, Vector3.Distance(player.position, monster.transform.position)) : 0f;
            intensity = Mathf.MoveTowards(intensity, target, Time.deltaTime * (target > intensity ? 1.5f : .45f));
            if (!heartbeat || !heartbeat.clip) return;
            heartbeat.volume = intensity * .55f;
            heartbeat.pitch = Mathf.Lerp(.85f, 1.3f, intensity);
            if (intensity > .001f && !heartbeat.isPlaying) heartbeat.Play();
            else if (intensity <= .001f && heartbeat.isPlaying) heartbeat.Stop();
        }

        void LateUpdate()
        {
            RestoreCamera();
            if (!view || !game || !game.IsRunning || game.IsEnded || game.IsAnswering || game.IsExitConfirmOpen) return;
            float shake = ShakeDegrees(intensity);
            if (shake <= 0f) return;
            originalRotation = view.transform.rotation;
            float x = (Mathf.PerlinNoise(Time.unscaledTime * 7f, 1f) - .5f) * 2f * shake;
            float y = (Mathf.PerlinNoise(1f, Time.unscaledTime * 7f) - .5f) * 2f * shake;
            adjustedRotation = originalRotation * Quaternion.Euler(x, y, 0f);
            view.transform.rotation = adjustedRotation;
            cameraAdjusted = true;
        }

        void RestoreCamera()
        {
            if (cameraAdjusted && view && Quaternion.Angle(view.transform.rotation, adjustedRotation) < .001f)
                view.transform.rotation = originalRotation;
            cameraAdjusted = false;
        }

        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint || !game || !game.IsRunning || game.IsEnded) return;
            var previousMatrix = GUI.matrix; var previousColor = GUI.color; int previousDepth = GUI.depth;
            GUI.matrix = Matrix4x4.identity; GUI.depth = 100;
            GUI.color = new Color(1, 1, 1, intensity * .58f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), vignette);
            float frame = Mathf.Floor(Time.unscaledTime * 10f);
            GUI.color = new Color(1, 1, 1, NoiseOpacity(intensity));
            GUI.DrawTextureWithTexCoords(new Rect(0, 0, Screen.width, Screen.height), grain,
                new Rect((frame * .137f) % 1f, (frame * .271f) % 1f, Screen.width / 128f, Screen.height / 128f));
            GUI.matrix = previousMatrix; GUI.color = previousColor; GUI.depth = previousDepth;
        }

        public void Shutdown()
        {
            RestoreCamera();
            if (heartbeat) heartbeat.Stop();
            if (vignette) Destroy(vignette);
            if (grain) Destroy(grain);
            vignette = null; grain = null;
        }
        static float NoiseOpacity(float level) => Mathf.Lerp(.018f, .18f, level);
        static float ShakeDegrees(float level) => Mathf.InverseLerp(.08f, 1f, level) * 1.15f;
        void OnDestroy() { Shutdown(); }
    }
}
