using UnityEngine;
namespace CampusCommons
{
    public class CampusBootstrap : MonoBehaviour
    {
        void Awake()
        {
            if (!GameObject.Find(CampusWorld.RootName)) CampusWorld.Build();
            Application.targetFrameRate = 60;
            if (!FindFirstObjectByType<CampusPlayer>()) CampusPlayer.Create();
        }
    }
}
