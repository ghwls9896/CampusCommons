#if !CAMPUS_SUNBOX_AVATAR
using UnityEngine;

namespace CampusCommons.GrandLibrary
{
    // The player keeps its original fallback model until Sunbox is installed.
    public class LibraryAvatarMotion : MonoBehaviour
    {
        public void Initialize(GameObject visual) { }
        public void UpdateMovement(bool moving, bool running, bool sitting) { }
        public void Wave() { }
    }
    public class LibraryAvatarCustomizerUI : MonoBehaviour
    {
        public bool IsOpen => false;
        public void Initialize(GameObject visual) { }
        public void Toggle() { }
        public void Close() { }
    }
    public class LibraryAvatarSitPose : MonoBehaviour
    {
        public void Initialize(GameObject visual) { }
        public void SetSitting(bool sitting) { }
    }
    public class LibraryAvatarPreview : MonoBehaviour
    {
        public void Initialize(GameObject visual, LibraryAvatarCustomizerUI ui) { }
    }
}
#endif
