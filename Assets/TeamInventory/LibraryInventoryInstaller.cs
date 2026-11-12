
using UnityEngine;
using CampusCommons.GrandLibrary;

public class LibraryInventoryInstaller : MonoBehaviour
{
    private static LibraryInventoryInstaller instance;

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateInstaller()
    {
        if (instance != null)
            return;

        GameObject go =
            new GameObject("LibraryInventoryInstaller");

        DontDestroyOnLoad(go);

        instance =
            go.AddComponent<LibraryInventoryInstaller>();
    }

    private void Update()
    {
        Player player = FindFirstObjectByType<Player>();

        if (player == null)
            return;

        // 플레이어에게 인벤토리가 없을 때만 추가
        if (player.GetComponent<LibraryInventoryUI>() == null)
        {
            player.gameObject.AddComponent<LibraryInventoryUI>();
        }

        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }
}
