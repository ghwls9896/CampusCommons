using UnityEngine;

namespace CampusCommons.GrandLibrary
{
    public class LibraryBootstrap : MonoBehaviour
    {
        void Awake()
        {
            Application.targetFrameRate = 60;
            if (!FindFirstObjectByType<LibraryPlayer>()) LibraryPlayer.Create();
        }
    }
}
