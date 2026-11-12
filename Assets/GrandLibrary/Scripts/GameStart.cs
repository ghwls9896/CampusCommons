using UnityEngine;

namespace CampusCommons.GrandLibrary
{
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "CampusCommons.GrandLibrary", null, "LibraryBootstrap")]
    public class GameStart : MonoBehaviour
    {
        void Awake()
        {
            Application.targetFrameRate = 60;
            var player = FindAnyObjectByType<Player>();
            if (!player) player = Player.Create(transform);
            if (!player.GetComponent<CampusCommons.Quiz.QuizRoom>())
                player.gameObject.AddComponent<CampusCommons.Quiz.QuizRoom>();
        }
    }
}
