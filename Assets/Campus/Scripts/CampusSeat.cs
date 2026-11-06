using UnityEngine;
namespace CampusCommons
{
    public class CampusSeat : MonoBehaviour
    {
        public string room;
        [System.NonSerialized] public CampusPlayer occupant;
    }
}
