using UnityEngine;

namespace CampusCommons.StudyImmersive
{
    // Plays once all notebooks have been collected, continuing until it ends.
    public sealed class FinalNotebookMusic : MonoBehaviour
    {
        const float FadeSeconds = 12f;
        const float MaximumVolume = .65f;
        EscapeGame game;
        AudioSource source;
        bool started, paused;

        public static void Create(EscapeGame owner, Transform parent)
        {
            var root = new GameObject("Final notebook music");
            root.transform.SetParent(parent, false);
            var music = root.AddComponent<FinalNotebookMusic>();
            music.game = owner;
            music.source = root.AddComponent<AudioSource>();
            music.source.playOnAwake = false;
            music.source.spatialBlend = 0f;
            music.source.loop = true;
            music.source.volume = 0f;
            music.source.clip = Resources.Load<AudioClip>("EscapeSounds/FinalChase");
        }

        void Update()
        {
            if (!game || !source) return;
            if (!game.IsRunning || game.IsEnded)
            {
                source.Stop(); source.volume = 0f; started = false; paused = false;
                return;
            }
            if (!started && game.ReadyToExit)
            {
                if (!source.clip) return;
                started = true; source.volume = 0f; source.Play();
            }
            if (!started) return;
            if (game.IsExitConfirmOpen)
            {
                if (!paused) { source.Pause(); paused = true; }
                return;
            }
            if (paused) { source.UnPause(); paused = false; }
            source.volume = Mathf.MoveTowards(source.volume, MaximumVolume, MaximumVolume * Time.deltaTime / FadeSeconds);
        }
    }
}
