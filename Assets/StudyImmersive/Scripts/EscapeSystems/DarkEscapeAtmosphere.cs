using CampusCommons.LibraryCharacter;
using UnityEngine;

namespace CampusCommons.StudyImmersive
{
    /// <summary>Audio and alternate monster artwork used only in the two dark modes.</summary>
    public sealed class DarkEscapeAtmosphere : MonoBehaviour
    {
        const string ResourceFolder = "EscapeSounds/Night/";
        const float AmbientVolume = .22f;
        const float PursuitVolume = .65f;
        const float PursuitNearDistance = 3f;
        const float PursuitStopDistance = 40f;
        const float PursuitRestartDistance = 38f;
        const float FadePerSecond = .5f;
        const float StingerRearmSeconds = 10f;
        const float SightingRetrySeconds = .5f;
        const float CryMinInterval = 45f;
        const float CryMaxInterval = 90f;

        EscapeGame game;
        Monster monster;
        Transform player;
        AudioSource ambience, pursuit, cry, stinger;
        AudioClip cryClip, stingerClip;
        SpriteRenderer birdRenderer;
        Sprite originalBird, nightBird;
        bool pursuitStarted, pursuitPlaying;
        bool previousLook, sawAway, stopped, paused;
        bool stingerArmed = true;
        float nextCry, nextSightingAttempt, stingerUnseenSeconds;

        public AudioClip JumpScare { get; private set; }
        public Texture2D JumpScareImage { get; private set; }

        public static DarkEscapeAtmosphere Create(EscapeGame owner, Monster bird, Transform target)
        {
            var root = new GameObject("Dark escape atmosphere");
            var effect = root.AddComponent<DarkEscapeAtmosphere>();
            effect.game = owner;
            effect.monster = bird;
            effect.player = target;
            effect.Initialize();
            return effect;
        }

        AudioSource Source(string name, bool spatial)
        {
            var item = new GameObject(name);
            item.transform.SetParent(transform, false);
            var source = item.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.dopplerLevel = 0f;
            source.spatialBlend = spatial ? 1f : 0f;
            if (spatial)
            {
                source.minDistance = 1f;
                source.maxDistance = 500f;
                source.rolloffMode = AudioRolloffMode.Custom;
                source.SetCustomCurve(AudioSourceCurveType.CustomRolloff, AnimationCurve.Linear(0f, 1f, 1f, .65f));
            }
            return source;
        }

        void Initialize()
        {
            ambience = Source("Dark ambient", false);
            ambience.clip = Resources.Load<AudioClip>(ResourceFolder + "Ambient");
            ambience.loop = true;
            ambience.volume = AmbientVolume;
            if (ambience.clip) ambience.Play();

            pursuit = Source("Distance pursuit music", false);
            pursuit.clip = Resources.Load<AudioClip>(ResourceFolder + "PursuitMusic");
            pursuit.loop = true;
            pursuit.volume = 0f;

            cry = Source("Distant bird cry", true);
            cryClip = Resources.Load<AudioClip>(ResourceFolder + "DistantCry");
            cry.volume = .9f;

            stinger = Source("Monster sighting stinger", true);
            stingerClip = Resources.Load<AudioClip>(ResourceFolder + "Stinger");
            stinger.volume = .8f;

            JumpScare = Resources.Load<AudioClip>(ResourceFolder + "Jumpscare");
            JumpScareImage = Resources.Load<Texture2D>(ResourceFolder + "NightJumpscare");
            nextCry = Time.time + Random.Range(CryMinInterval, CryMaxInterval);
            ApplyNightBirdSprite();
        }

        void ApplyNightBirdSprite()
        {
            if (!monster || !monster.picture) return;
            birdRenderer = monster.picture.GetComponentInChildren<SpriteRenderer>(true);
            var texture = Resources.Load<Texture2D>(ResourceFolder + "NightScholarBird");
            if (!birdRenderer || !birdRenderer.sprite || !texture) return;

            originalBird = birdRenderer.sprite;
            var pivot = new Vector2(originalBird.pivot.x / originalBird.rect.width,
                originalBird.pivot.y / originalBird.rect.height);
            nightBird = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                pivot, originalBird.pixelsPerUnit);
            nightBird.name = "Night scholar bird";
            birdRenderer.sprite = nightBird;
        }

        void Update()
        {
            if (!game || !game.IsRunning || game.IsEnded)
            {
                StopAudio();
                return;
            }
            if (monster)
            {
                cry.transform.position = monster.transform.position + Vector3.up * 1.2f;
                stinger.transform.position = cry.transform.position;
            }
            if (game.IsExitConfirmOpen)
            {
                SetPaused(true);
                return;
            }
            SetPaused(false);

            if (monster && monster.IsChasing && !pursuitStarted) pursuitStarted = true;
            if (pursuitStarted && player && monster)
            {
                UpdatePursuit(Vector3.Distance(player.position, monster.transform.position), Time.deltaTime);
                CheckSighting();
            }
            if (cryClip && cry && Time.time >= nextCry)
            {
                if (!cry.isPlaying) cry.PlayOneShot(cryClip);
                nextCry = Time.time + Random.Range(CryMinInterval, CryMaxInterval);
            }
        }

        // Beyond 40 m fade out and stop. Once inside 38 m, replay from the beginning.
        void UpdatePursuit(float distance, float dt)
        {
            if (!pursuit || !pursuit.clip) return;
            if (!pursuitPlaying && distance < PursuitRestartDistance)
            {
                pursuit.Stop();
                pursuit.time = 0f;
                pursuit.Play();
                pursuitPlaying = true;
            }
            if (!pursuitPlaying) return;

            float near = Mathf.InverseLerp(PursuitStopDistance, PursuitNearDistance, distance);
            float targetVolume = distance >= PursuitStopDistance ? 0f : PursuitVolume * Mathf.SmoothStep(0f, 1f, near);
            pursuit.volume = Mathf.MoveTowards(pursuit.volume, targetVolume, dt * FadePerSecond);
            if (distance >= PursuitStopDistance && pursuit.volume <= 0f)
            {
                pursuit.Stop();
                pursuitPlaying = false;
            }
        }

        void SetPaused(bool value)
        {
            if (paused == value) return;
            paused = value;
            foreach (var source in new[] { ambience, pursuit, cry, stinger })
            {
                if (!source) continue;
                if (value) source.Pause();
                else source.UnPause();
            }
        }

        void CheckSighting()
        {
            if (game.IsAnswering) return;
            UpdateSighting(monster.IsLookingAtPlayer, Time.deltaTime);
        }

        void UpdateSighting(bool looking, float dt)
        {
            if (!looking)
            {
                sawAway = true;
                previousLook = false;
                if (!stingerArmed)
                {
                    stingerUnseenSeconds += dt;
                    if (stingerUnseenSeconds >= StingerRearmSeconds) stingerArmed = true;
                }
                return;
            }
            stingerUnseenSeconds = 0f;
            if (!previousLook && sawAway && stingerArmed && stinger && stingerClip)
            {
                if (Time.time < nextSightingAttempt) return;
                nextSightingAttempt = Time.time + SightingRetrySeconds;
                if (!monster.PursueVisiblePlayer()) return;
                stinger.PlayOneShot(stingerClip);
                stingerArmed = false;
                sawAway = false;
            }
            previousLook = true;
        }

        void StopAudio()
        {
            if (stopped) return;
            stopped = true;
            foreach (var source in new[] { ambience, pursuit, cry, stinger })
                if (source) source.Stop();
        }

        public void Shutdown()
        {
            StopAudio();
            if (birdRenderer && originalBird) birdRenderer.sprite = originalBird;
            if (nightBird)
            {
                Destroy(nightBird);
                nightBird = null;
            }
        }

        void OnDestroy() { Shutdown(); }
    }
}
