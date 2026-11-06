using UnityEngine;

namespace CampusCommons.GrandLibrary
{
    /// <summary>Keeps the depth-tested sign material bound to the current dynamic font atlas.</summary>
    public sealed class WorldTextFontAtlas : MonoBehaviour
    {
        public Font font;
        public Material depthMaterial;

        void OnEnable()
        {
            Font.textureRebuilt += RefreshAfterRebuild;
            Refresh();
        }
        void Start() { Refresh(); }
        void OnDisable() { Font.textureRebuilt -= RefreshAfterRebuild; }
        void RefreshAfterRebuild(Font rebuilt) { if (rebuilt == font) Refresh(); }
        void Refresh()
        {
            if (font && font.material && depthMaterial)
                depthMaterial.mainTexture = font.material.mainTexture;
        }
    }
}
