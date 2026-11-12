using CampusCommons.StudyImmersive;
using UnityEditor;
using UnityEngine;

namespace CampusCommons.StudyImmersive.Editor
{
    public static class ConstructionBarrierPreview
    {
        [MenuItem("Campus Commons/Escape Room/Preview Construction Barrier")]
        public static void Preview()
        {
            var view = SceneView.lastActiveSceneView;
            Vector3 center = view ? view.pivot : Vector3.zero;
            center.y = 0;
            var barrier = ConstructionBarrier.Create(center, Quaternion.identity);
            barrier.gameObject.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            Selection.activeGameObject = barrier.gameObject;
            if (view) view.FrameSelected();
            Debug.Log("Temporary construction barrier preview; it will not be saved in the scene.");
        }
    }
}
