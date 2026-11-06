using UnityEngine;

namespace CampusCommons.LibraryCharacter
{
    /// <summary>Keeps the flat sprite upright while it faces the local player's camera.</summary>
    [DisallowMultipleComponent]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "CampusCommons.LibraryCharacter", null, "FacePlayerCamera")]
    public sealed class FaceCamera : MonoBehaviour
    {
        void LateUpdate()
        {
            var camera=Camera.main;
            if(camera) Face(camera.transform);
        }

        public void Face(Transform cameraTransform)
        {
            if(!cameraTransform) return;
            Vector3 away=transform.position-cameraTransform.position;
            away.y=0;
            if(away.sqrMagnitude<.0001f)
            {
                away=cameraTransform.forward;away.y=0;
                if(away.sqrMagnitude<.0001f) return;
            }
            // Unity sprites face local -Z. Point +Z away from the camera so
            // the picture reads correctly rather than mirrored.
            transform.rotation=Quaternion.LookRotation(away.normalized,Vector3.up);
        }
    }
}
