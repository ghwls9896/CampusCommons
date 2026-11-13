using UnityEngine;

namespace CampusCommons.StudyImmersive
{
    /// <summary>Movement sounds for the local player while an escape round is active.</summary>
    public sealed class EscapePlayerFootsteps : MonoBehaviour
    {
        EscapeGame game;
        Transform player;
        CharacterController body;
        AudioSource source;
        AudioClip walk, run;
        Vector3 lastPosition;

        public static void Create(EscapeGame owner, Transform target, Transform parent)
        {
            if (!owner || !target || !parent) return;
            var controller = target.GetComponent<CharacterController>();
            if (!controller) return;
            var root = new GameObject("Escape player footsteps");
            root.transform.SetParent(parent, false);
            var footsteps = root.AddComponent<EscapePlayerFootsteps>();
            footsteps.game = owner;
            footsteps.player = target;
            footsteps.body = controller;
            footsteps.lastPosition = target.position;
            footsteps.walk = Resources.Load<AudioClip>("EscapeSounds/PlayerWalk");
            footsteps.run = Resources.Load<AudioClip>("EscapeSounds/PlayerRun");
            footsteps.source = root.AddComponent<AudioSource>();
            footsteps.source.playOnAwake = false;
            footsteps.source.loop = true;
            footsteps.source.spatialBlend = 0f;
            footsteps.source.volume = 1f;
        }

        void LateUpdate()
        {
            if (!game || !player || !body || !source) return;
            Vector3 displacement = player.position - lastPosition;
            lastPosition = player.position;
            displacement.y = 0f;
            bool blocked = !game.IsRunning || game.IsEnded || game.IsAnswering || game.IsExitConfirmOpen ||
                (game.World && (game.World.BlocksMovement || game.World.IsCleanerPushing));
            float dt = Time.deltaTime;
            if (blocked || !body.enabled || !body.isGrounded || dt <= 0f || displacement.sqrMagnitude < .000001f)
            {
                StopSound();
                return;
            }
            float actualSpeed = displacement.magnitude / dt;
            float boost = game.World && game.World.HasSpeedBoost ? 1.35f : 1f;
            float walkingSpeed = 3.6f * game.TimeAttackSpeed * boost;
            AudioClip desired = actualSpeed > walkingSpeed * 1.15f ? run : walk;
            if (!desired) { StopSound(); return; }
            if (source.clip != desired || !source.isPlaying)
            {
                source.Stop();
                source.clip = desired;
                source.Play();
            }
        }
        void StopSound() { if (source && source.isPlaying) source.Stop(); }
        void OnDisable() { StopSound(); }
    }
}
