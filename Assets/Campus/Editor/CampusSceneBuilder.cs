using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CampusCommons.Editor
{
    [InitializeOnLoad]
    public static class CampusSceneBuilder
    {
        const string ScenePath = "Assets/Campus/Scenes/CampusCommons.unity";
        const string MaterialPath = "Assets/Campus/Materials";
        static CampusSceneBuilder()
        {
            EditorApplication.delayCall += FirstOpen;
        }
        static void FirstOpen()
        {
            // Never overwrite a scene that the user has already generated or edited.
            if (File.Exists(ScenePath) || EditorApplication.isPlayingOrWillChangePlaymode) return;
            // On a fresh project only: preserve any unrelated unsaved scene work.
            for (int i=0;i<SceneManager.sceneCount;i++)
                if (SceneManager.GetSceneAt(i).isDirty) return;
            Generate();
        }
        [MenuItem("Campus/Create editable campus scene")]
        public static void CreateScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (File.Exists(ScenePath) && !EditorUtility.DisplayDialog("Regenerate campus?",
                "This replaces CampusCommons.unity with the original layout. Save your custom scene under another name to keep your edits.", "Regenerate", "Cancel")) return;
            Generate();
        }
        // Also callable with Unity -batchmode -executeMethod CampusCommons.Editor.CampusSceneBuilder.Generate.
        public static void Generate()
        {
            Directory.CreateDirectory("Assets/Campus/Scenes");
            Directory.CreateDirectory(MaterialPath);
            Directory.CreateDirectory("Assets/Campus/Prefabs");
            AssetDatabase.Refresh();
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var root=CampusWorld.Build();
            new GameObject("Campus startup").AddComponent<CampusBootstrap>();
            // Persist procedural materials so scene references survive closing the editor.
            var persisted=new Dictionary<Material,Material>();
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>())
            {
                var source=renderer.sharedMaterial;
                if (!source || AssetDatabase.Contains(source)) continue;
                if (!persisted.TryGetValue(source,out Material material))
                {
                    string path=MaterialPath+"/"+source.name+".mat";
                    material=AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (material) EditorUtility.CopySerialized(source,material);
                    else { material=source; AssetDatabase.CreateAsset(material,path); }
                    persisted.Add(source,material);
                }
                renderer.sharedMaterial=material;
            }
            // A reusable environment prefab plus individually editable objects in the scene.
            PrefabUtility.SaveAsPrefabAsset(root,"Assets/Campus/Prefabs/CampusEnvironment.prefab");
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene,ScenePath);
            EditorBuildSettings.scenes=new[] {new EditorBuildSettingsScene(ScenePath,true)};
            AssetDatabase.SaveAssets();
            if (SceneView.lastActiveSceneView)
                SceneView.lastActiveSceneView.LookAt(new Vector3(0,0,7),Quaternion.Euler(60,0,0),38);
            Debug.Log("Campus scene saved. Press Play: WASD move, RMB look, E sit, R wave, M map. This is a local prototype.");
        }
    }
}
