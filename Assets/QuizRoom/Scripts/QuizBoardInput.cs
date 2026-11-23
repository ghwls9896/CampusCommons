using UnityEngine;
using UnityEngine.EventSystems;

namespace CampusCommons.Quiz
{
    // The project maps mouse axes only. Supply uGUI navigation without changing shared input settings.
    internal sealed class QuizBoardInput : BaseInput
    {
        public override float GetAxisRaw(string axis)
        {
            if (axis == "Horizontal") return (Input.GetKey(KeyCode.RightArrow) ? 1 : 0) - (Input.GetKey(KeyCode.LeftArrow) ? 1 : 0);
            if (axis == "Vertical") return (Input.GetKey(KeyCode.UpArrow) ? 1 : 0) - (Input.GetKey(KeyCode.DownArrow) ? 1 : 0);
            return 0;
        }

        public override bool GetButtonDown(string button)
        {
            if (button == "Submit") return Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
            return button == "Cancel" && Input.GetKeyDown(KeyCode.Escape);
        }
    }
}
