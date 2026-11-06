using UnityEngine;

namespace CampusCommons.GrandLibrary
{
    [RequireComponent(typeof(BoxCollider))]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "CampusCommons.GrandLibrary", null, "LibraryPortal")]
    public class Portal : MonoBehaviour
    {
        public enum Space { Library, Mafia, Quiz, Escape }
        public Space destination;
        public Vector3 arrivalPosition;
        public float arrivalYaw;
        public string portalLabel;

        void Reset() { GetComponent<BoxCollider>().isTrigger = true; }

        void OnTriggerEnter(Collider other)
        {
            var player = other.GetComponent<Player>();
            if (player) player.TryUsePortal(this);
        }
        void OnTriggerStay(Collider other)
        {
            // If someone reaches the return portal during the arrival cooldown,
            // they can remain in it and travel as soon as the cooldown expires.
            var player = other.GetComponent<Player>();
            if (player) player.TryUsePortal(this);
        }
    }
}
