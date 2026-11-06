using CampusCommons.StudyImmersive;
using UnityEngine;

namespace CampusCommons.LibraryCharacter
{
    public sealed class Sheet : MonoBehaviour
    {
        public EscapeGame game;
        public void Collect(){if(game)game.OpenSheet(gameObject);}
    }
}
