using System;
using UnityEngine;
using CampusCommons.GrandLibrary;
using CampusCommons.TeamProgression;

/// <summary>
/// 3 x 4 inventory. One reward is visible for each authenticated level Lv.5-15.
/// Right-click an owned slot to equip/unequip. The bottom hotbar and old
/// EQUIPMENT box are intentionally hidden; avatar preview is enlarged.
/// </summary>
[DefaultExecutionOrder(-50)]
public class LibraryInventoryUI : MonoBehaviour
{
    public bool IsOpen { get; private set; }

    // Kept solely for team InventoryIntegrationChecks compatibility.
    // It is no longer bound to keyboard keys and is never drawn on screen.
    public int SelectedHotbar { get; private set; }

    private const int Columns = 3;
    private const int Rows = 4;
    private const int SlotCount = Columns * Rows;

    private Player player;
    private LibraryAvatarPreview preview;
    private GUIStyle itemStyle;
    private GUIStyle equippedStyle;
    private GUIStyle hintStyle;
    private GUIStyle levelBadgeStyle;

    // Each reward image is imported as a separate Sprite (Single), not a 4x3 sprite sheet.
    // Asset directory: Assets/Resources/TeamProgression/RewardIcons/
    private const string IconResourceDirectory = "TeamProgression/RewardIcons";
    private static readonly string[] IconNames =
    {
        "lv05_magic_circle", "lv06_magic_bracelet", "lv07_teal_particles",
        "lv08_afterimage", "lv09_floating_crystals", "lv10_mascot_cape",
        "lv11_cape_emblem", "lv12_star_sigils", "lv13_golden_particles",
        "lv14_small_wings", "lv15_final_wings"
    };

    private readonly Sprite[] rewardIcons =
        new Sprite[CosmeticEquipmentState.RewardCount];
    private bool iconsLoaded;

    public bool CanUseInput =>
        isActiveAndEnabled && player != null && player.CanUseInventory;

    public bool OwnsInput => IsOpen && CanUseInput;

    private void Awake()
    {
        player = GetComponent<Player>();
        preview = GetComponent<LibraryAvatarPreview>();
    }

    public void SetOpen(bool open)
    {
        IsOpen = open && CanUseInput;
        if (preview == null)
            preview = GetComponent<LibraryAvatarPreview>();
        if (preview != null)
            preview.SetInventoryVisible(IsOpen);
    }

    private void Update()
    {
        if (!CanUseInput)
        {
            if (IsOpen) SetOpen(false);
            return;
        }

        if (Input.GetKeyDown(KeyCode.I)) SetOpen(!IsOpen);

        if (IsOpen && (Input.GetKeyDown(KeyCode.Escape) ||
                       Input.GetKeyDown(KeyCode.C)))
            SetOpen(false);
    }

    private void LateUpdate()
    {
        if (IsOpen && !CanUseInput) SetOpen(false);
    }

    private void OnDisable()
    {
        SetOpen(false);
    }

    // Compatibility API: existing editor tests still invoke this method.
    public void SelectHotbar(int index)
    {
        if (!CanUseInput || index < 0 || index >= 5) return;
        SelectedHotbar = index;
    }

    private void EnsureRewardIcons()
    {
        if (iconsLoaded) return;
        iconsLoaded = true;

        // Loading once from Resources avoids per-frame allocations in OnGUI.
        // OrdinalIgnoreCase helps if a filename differs in capitalization.
        Sprite[] sprites = Resources.LoadAll<Sprite>(IconResourceDirectory);
        foreach (Sprite sprite in sprites)
        {
            if (sprite == null) continue;
            for (int i = 0; i < IconNames.Length; i++)
            {
                if (string.Equals(sprite.name, IconNames[i],
                    StringComparison.OrdinalIgnoreCase))
                {
                    rewardIcons[i] = sprite;
                    break;
                }
            }
        }

        int missing = 0;
        for (int i = 0; i < rewardIcons.Length; i++)
        {
            if (rewardIcons[i] == null) missing++;
        }
        if (missing != 0)
        {
            Debug.LogWarning(
                "[CampusCommons] " + missing + " level-reward icons are missing. " +
                "Check Sprite (2D and UI) / Single, names, and Assets/Resources/" +
                IconResourceDirectory + "/. Missing icons fall back to text.");
        }
    }

    // Handles an individually sliced Sprite, preserving its UV crop and alpha.
    private static void DrawRewardIcon(Rect rect, Sprite sprite)
    {
        if (sprite == null || sprite.texture == null) return;

        Texture2D texture = sprite.texture;
        Rect r = sprite.rect;
        Rect uv = new Rect(r.x / texture.width, r.y / texture.height,
            r.width / texture.width, r.height / texture.height);
        GUI.DrawTextureWithTexCoords(rect, texture, uv, true);
    }

    private void EnsureStyles()
    {
        if (itemStyle != null) return;

        itemStyle = new GUIStyle(GUI.skin.label);
        itemStyle.alignment = TextAnchor.MiddleCenter;
        itemStyle.wordWrap = true;
        itemStyle.fontSize = 11;
        itemStyle.normal.textColor = Color.white;

        equippedStyle = new GUIStyle(itemStyle);
        equippedStyle.fontSize = 10;
        equippedStyle.normal.textColor = new Color(0.62f, 1f, 0.7f);

        hintStyle = new GUIStyle(GUI.skin.label);
        hintStyle.fontSize = 11;
        hintStyle.alignment = TextAnchor.MiddleLeft;
        hintStyle.normal.textColor = new Color(0.88f, 0.88f, 0.91f);
        hintStyle.wordWrap = true;

        levelBadgeStyle = new GUIStyle(GUI.skin.label);
        levelBadgeStyle.alignment = TextAnchor.UpperLeft;
        levelBadgeStyle.fontSize = 10;
        levelBadgeStyle.fontStyle = FontStyle.Bold;
        levelBadgeStyle.normal.textColor = new Color(1f, 0.93f, 0.62f);
    }

    private void OnGUI()
    {
        if (!CanUseInput || !IsOpen) return;
        EnsureStyles();
        EnsureRewardIcons();
        DrawInventory();
    }

    private void DrawInventory()
    {
        const float margin = 14f;
        float panelWidth = Mathf.Min(402f, Screen.width - margin * 2f);
        float panelHeight = Mathf.Min(720f, Screen.height - margin * 2f);
        if (panelWidth < 190f || panelHeight < 300f) return;

        Rect panel = new Rect(Screen.width - margin - panelWidth,
            margin, panelWidth, panelHeight);
        GUI.Box(panel, "INVENTORY  |  I / ESC 닫기");

        const float gap = 8f;
        float slot = Mathf.Clamp((panelHeight - 145f) / 7.1f, 36f, 68f);
        float gridWidth = Columns * slot + (Columns - 1) * gap;
        float gridHeight = Rows * slot + (Rows - 1) * gap;
        float gridX = panel.x + (panel.width - gridWidth) * 0.5f;
        float gridY = panel.yMax - gridHeight - 17f;
        float bagTitleY = gridY - 51f;

        Rect avatarFrame = new Rect(panel.x + 12f, panel.y + 32f,
            panel.width - 24f, bagTitleY - (panel.y + 32f) - 8f);
        GUI.Box(avatarFrame, "MY CHARACTER");

        if (preview == null) preview = GetComponent<LibraryAvatarPreview>();
        Texture avatarTexture = preview != null ? preview.PreviewTexture : null;
        Rect portrait = new Rect(avatarFrame.x + 8f, avatarFrame.y + 23f,
            avatarFrame.width - 16f, Mathf.Max(1f, avatarFrame.height - 29f));
        if (avatarTexture != null)
            GUI.DrawTexture(portrait, avatarTexture, ScaleMode.ScaleToFit);
        else
            GUI.Label(portrait, "캐릭터 미리보기 대기 중", itemStyle);

        CosmeticEquipmentState state = CosmeticEquipmentState.Instance;
        int owned = state != null ? state.OwnedCount : 0;
        bool isPreview = state != null && state.IsEditorPreview;
        GUI.Label(new Rect(panel.x + 15f, bagTitleY,
            panel.width - 30f, 22f),
            "COSMETICS   " + owned + " / " + SlotCount +
            (isPreview ? "   [EDITOR PREVIEW]" : ""));
        GUI.Label(new Rect(panel.x + 15f, bagTitleY + 21f,
            panel.width - 30f, 20f),
            state != null ? state.Feedback : "아이템 우클릭: 장착 / 장착 해제",
            hintStyle);

        Event e = Event.current;
        for (int i = 0; i < SlotCount; i++)
        {
            int row = i / Columns;
            int column = i % Columns;
            Rect cell = new Rect(gridX + column * (slot + gap),
                gridY + row * (slot + gap), slot, slot);

            int rewardLevel = CosmeticEquipmentState.FirstRewardLevel + i;
            bool hasItem = state != null && state.OwnsReward(rewardLevel);
            bool equipped = hasItem && state.Equipped(rewardLevel);

            Color oldBackground = GUI.backgroundColor;
            if (equipped)
                GUI.backgroundColor = new Color(0.34f, 0.62f, 0.46f);
            GUI.Box(cell, "");
            GUI.backgroundColor = oldBackground;

            if (hasItem)
            {
                string name = CosmeticEquipmentState.RewardName(rewardLevel);
                Sprite icon = i < rewardIcons.Length ? rewardIcons[i] : null;
                if (icon != null)
                {
                    // Use the whole square so the gold icon border stays visible.
                    Rect iconRect = new Rect(cell.x + 2f, cell.y + 2f,
                        cell.width - 4f, cell.height - 4f);
                    DrawRewardIcon(iconRect, icon);
                    GUI.Label(new Rect(cell.x + 5f, cell.y + 3f,
                        cell.width - 10f, 18f), "Lv." + rewardLevel, levelBadgeStyle);
                }
                else
                {
                    // Graceful fallback if an icon is not imported correctly.
                    GUI.Label(new Rect(cell.x + 3f, cell.y + 5f,
                        cell.width - 6f, cell.height - 21f),
                        "Lv." + rewardLevel + "\n" + name, itemStyle);
                }

                if (equipped)
                {
                    GUI.Label(new Rect(cell.x + 3f, cell.yMax - 20f,
                        cell.width - 6f, 18f), "장착 중", equippedStyle);
                }

                if (cell.Contains(e.mousePosition) &&
                    e.type == EventType.MouseDown && e.button == 1)
                {
                    // Keep the existing inventory equip/unequip logic untouched.
                    state.ToggleReward(rewardLevel);
                    e.Use();
                }
            }
        }

    }
}
