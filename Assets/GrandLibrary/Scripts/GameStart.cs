using UnityEngine;

namespace CampusCommons.GrandLibrary
{
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "CampusCommons.GrandLibrary", null, "LibraryBootstrap")]
    public class GameStart : MonoBehaviour
    {
        void Awake()
        {
            Application.targetFrameRate = 60;
            if (!FindAnyObjectByType<Player>()) Player.Create(transform);
        }
    }
}
