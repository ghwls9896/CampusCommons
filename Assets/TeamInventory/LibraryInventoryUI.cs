
using UnityEngine;

using CampusCommons.GrandLibrary;


[DefaultExecutionOrder(-50)]
public class LibraryInventoryUI : MonoBehaviour
{
    public bool IsOpen { get; private set; }
    public int SelectedHotbar { get; private set; }

    private const int Columns = 5;
    private const int Rows = 6;
    private const int SlotCount = Columns * Rows;
    private const int HotbarStart = 25;

    private readonly KeyCode[] hotkeys =
    {
        KeyCode.Alpha1,
        KeyCode.Alpha2,
        KeyCode.Alpha3,
        KeyCode.Alpha4,
        KeyCode.Alpha5
    };

    private readonly string[] items = new string[SlotCount];

    private Player player;
    private LibraryAvatarPreview preview;
    public bool CanUseInput => isActiveAndEnabled && player != null && player.CanUseInventory;
    public bool OwnsInput => IsOpen && CanUseInput;

    void Awake() { player = GetComponent<Player>(); }

    public void SetOpen(bool open)
    {
        IsOpen = open && CanUseInput;
        if (preview == null) preview = GetComponent<LibraryAvatarPreview>();
        if (preview != null) preview.SetInventoryVisible(IsOpen);
    }

    void Update()
    {
        if (!CanUseInput) { SetOpen(false); return; }
        if (Input.GetKeyDown(KeyCode.I)) SetOpen(!IsOpen);
        if (IsOpen && (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.C))) SetOpen(false);
        for (int i = 0; i < hotkeys.Length; i++)
            if (Input.GetKeyDown(hotkeys[i])) SelectHotbar(i);
    }

    void LateUpdate() { if (!CanUseInput) SetOpen(false); }
    void OnDisable() { SetOpen(false); }

    public void SelectHotbar(int index)
    {
        if (!CanUseInput || index < 0 || index >= 5)
            return;

        SelectedHotbar = index;

        Debug.Log("���� ���� ����: " + (index + 1));
    }

    void OnGUI()
    {
        if (!CanUseInput) return;
        DrawHotbar();

        if (IsOpen)
        {
            DrawInventory();
        }
    }


    void DrawInventory()
    {
        // �κ��丮 ��ü ũ��
        float panelWidth = 290f;
        float panelHeight = 400f;

        // ȭ�� ������ ��ܿ� ��ġ
        float px = Mathf.Max(
            8f,
            Screen.width - panelWidth - 18f
        );
        float py = 18f;


        // ���� ĳ���� �̸����� ����
        Rect characterRect = new Rect(
            px + 12f,
            py + 30f,
            140f,
            90f
        );

        // ������ ��� ����
        GUI.Box(
            new Rect(px + 160, py + 30, 118, 90),
            "EQUIPMENT"
        );

        GUI.Box(characterRect, "CHARACTER");

        // ���� Ŀ���͸���¡ �̸����� ��������
        
        if (preview == null) preview = GetComponent<LibraryAvatarPreview>();
        Texture avatarTexture = preview != null ? preview.PreviewTexture : null;

// 미리보기 표시
if (avatarTexture != null)
{
    Rect imageRect = new Rect(
        px + 16f,
        py + 48f,
        132f,
        68f
    );

    GUI.DrawTexture(
        imageRect,
        avatarTexture,
        ScaleMode.ScaleToFit
    );
}
else
{
    GUI.Label(
        new Rect(px + 22f, py + 70f, 120f, 25f),
        "Preview unavailable"
    );
}



        // �κ��丮 ���� ũ�� ���
        float slot = 39f;
        float gap = 4f;

        float gridWidth = Columns * slot
                        + (Columns - 1) * gap;

        float gridX = px + (panelWidth - gridWidth) / 2f;
        float gridY = py + 129f;

        // 5�� x 6��
        for (int i = 0; i < SlotCount; i++)
        {
            int row = i / Columns;
            int col = i % Columns;

            Rect rect = new Rect(
                gridX + col * (slot + gap),
                gridY + row * (slot + gap),
                slot,
                slot
            );

            bool isHotbar = i >= HotbarStart;
            int hotbarIndex = i - HotbarStart;

            Color previousColor = GUI.backgroundColor;

            // ������ ���� ���� ����� ǥ��
            if (isHotbar && hotbarIndex == SelectedHotbar)
            {
                GUI.backgroundColor =
                    new Color(1f, 0.8f, 0.3f);
            }

            string text = items[i] ?? "";

            if (isHotbar)
            {
                text = (hotbarIndex + 1) + "\n" + text;
            }

            if (GUI.Button(rect, text))
            {
                if (isHotbar)
                {
                    SelectHotbar(hotbarIndex);
                }
            }

            GUI.backgroundColor = previousColor;
        }
    }



    void DrawHotbar()
    {
        // ���� 48���� 36���� ���
        float slot = 36f;
        float gap = 4f;

        float width = slot * 5 + gap * 4;

        // ȭ�� �ϴ� �߾�
        float startX = (Screen.width - width) / 2f;
        float startY = Screen.height - slot - 12f;

        // ���� ���� ���
        GUI.Box(
            new Rect(
                startX - 6,
                startY - 6,
                width + 12,
                slot + 12
            ),
            ""
        );

        // ���� ���� 1~5
        for (int i = 0; i < 5; i++)
        {
            Rect rect = new Rect(
                startX + i * (slot + gap),
                startY,
                slot,
                slot
            );

            Color previousColor = GUI.backgroundColor;

            if (i == SelectedHotbar)
            {
                GUI.backgroundColor =
                    new Color(1f, 0.8f, 0.3f);
            }

            string label = (i + 1).ToString();

            if (GUI.Button(rect, label))
            {
                SelectHotbar(i);
            }

            GUI.backgroundColor = previousColor;
        }
    }

}
