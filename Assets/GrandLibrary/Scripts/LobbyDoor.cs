using System.Collections;
using CampusCommons.StudyImmersive;
using UnityEngine;

namespace CampusCommons.GrandLibrary
{
    // A physical entrance uses the existing room destination without a visible portal.
    public sealed class LobbyDoor : MonoBehaviour
    {
        public string destinationLabel;
        public Transform hinge;
        public Portal room;
        public EscapeGame escape;
        public float interactionDistance = 2.25f;
        bool opening;
        float closeAt;
        public bool IsOpening => opening;

        public static LobbyDoor Nearest(Player player)
        {
            if (!player || player.currentSpace != Portal.Space.Library) return null;
            LobbyDoor result = null;
            float best = float.PositiveInfinity;
            foreach (var door in FindObjectsByType<LobbyDoor>())
            {
                Vector3 delta = player.transform.position - door.transform.position;
                float horizontal = new Vector2(delta.x, delta.z).magnitude;
                if (Mathf.Abs(delta.y) > 2f || horizontal > door.interactionDistance || horizontal >= best) continue;
                best = horizontal; result = door;
            }
            return result;
        }

        public bool TryEnter(Player player)
        {
            if (opening || !player || !player.enabled || player.currentSpace != Portal.Space.Library || Nearest(player) != this) return false;
            opening = true; closeAt = Time.time + 1.8f;
            StartCoroutine(Travel(player));
            return true;
        }

        IEnumerator Travel(Player player)
        {
            yield return new WaitForSeconds(.48f);
            if (player && player.enabled && player.currentSpace == Portal.Space.Library && Nearest(player) == this)
            {
                if (escape) escape.BeginSelection(player);
                else if (room) player.TryUsePortal(room);
            }
        }

        void Update()
        {
            if (opening && Time.time >= closeAt) opening = false;
            if (!hinge) return;
            float angle = Mathf.MoveTowardsAngle(hinge.localEulerAngles.y, opening ? -82f : 0f, 180f * Time.deltaTime);
            hinge.localRotation = Quaternion.Euler(0f, angle, 0f);
        }
    }
}
