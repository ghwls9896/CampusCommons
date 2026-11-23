using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CampusCommons.Quiz
{
    // All quiz graphics, including the controls, are on the existing wall board.
    internal sealed class QuizTheme : IDisposable
    {
        internal static readonly Color Chalk = new Color(.96f, .96f, .86f);
        internal static readonly Color Mint = new Color(.69f, .88f, .77f);
        internal static readonly Color Amber = new Color(1f, .84f, .48f);
        internal static readonly Color Slate = new Color(.075f, .16f, .145f);
        internal static readonly Color Control = new Color(.15f, .29f, .25f);
        internal static readonly Color Selected = new Color(.30f, .47f, .38f);
        internal const float PageWidth = 1020, PageHeight = 570;

        readonly Canvas canvas;
        readonly RectTransform page, content, body, rule;
        readonly VerticalLayoutGroup layout;
        readonly ScrollRect scroll;
        readonly CanvasGroup interaction;
        readonly Font font;
        readonly Text heading, footer;
        readonly Button leave;
        readonly Renderer[] oldSigns;
        readonly bool[] signVisibility;
        GameObject ownedEvents;
        bool acceptingInput;

        internal QuizTheme(Renderer surface, Camera view, Action stand)
        {
            var bounds = surface.bounds;
            var root = new GameObject("Quiz chalkboard", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            var rect = (RectTransform)root.transform;
            // The imported mesh has a distant pivot and a rotated, 100x parent.
            // Work in world bounds. Preserve metres when parenting it to that mesh.
            // Tools/build_grand_library.py builds a 9.5 x 2.4 m writing face
            // on the 10 x 2.9 m frame. The face projects 5 cm in front of it.
            var face = new Vector2(bounds.size.x - .5f, bounds.size.y - .5f);
            rect.position = new Vector3(bounds.center.x, bounds.center.y, bounds.min.z - .06f);
            float metresPerUnit = Mathf.Min(face.y / (PageHeight + 24), face.x / (PageWidth + 24));
            rect.sizeDelta = (face - Vector2.one * .008f) / metresPerUnit;
            rect.localScale = Vector3.one * metresPerUnit;
            rect.SetParent(surface.transform, true);
            canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = view;
            font = Resources.Load<Font>("KIT/NotoSansKR");
            var background = root.AddComponent<Image>();
            background.color = Slate;
            background.raycastTarget = false;
            // The original title meshes sit in front of the writing surface.
            var group = surface.transform.parent;
            oldSigns = new[] { Sign(group, "SIGN_Quiz_QUIZ  1  VS  1"), Sign(group, "SIGN_Quiz_READY  /  THINK  /  ANSWER") };
            signVisibility = new bool[oldSigns.Length];
            for (int i = 0; i < oldSigns.Length; i++)
            {
                if (!oldSigns[i]) continue;
                signVisibility[i] = oldSigns[i].enabled;
                oldSigns[i].enabled = false;
            }

            page = Child(rect, "Writing area");
            page.sizeDelta = new Vector2(PageWidth, PageHeight);
            interaction = page.gameObject.AddComponent<CanvasGroup>();
            heading = TextAt(page, "Board title", 32, Chalk, true);
            Place(heading.rectTransform, 16, 2, 760, 58);
            leave = MakeButton(page, "일어나기", stand, false);
            Place((RectTransform)leave.transform, PageWidth - 152, 8, 132, 42);
            rule = Child(page, "Chalk line");
            Place(rule, 16, 64, PageWidth - 36, 2);
            rule.gameObject.AddComponent<Image>().color = Mint;

            body = Child(page, "Board scroll area");
            Place(body, 16, 78, PageWidth - 34, PageHeight - 142);
            var hitSurface = body.gameObject.AddComponent<Image>();
            hitSurface.color = Color.clear;
            scroll = body.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 42;
            var clip = Child(body, "Viewport");
            Stretch(clip, 0, 0, 20, 0);
            clip.gameObject.AddComponent<RectMask2D>();
            content = Child(clip, "Board content");
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(.5f, 1);
            content.sizeDelta = Vector2.zero;
            layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.spacing = 9;
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = clip;
            scroll.content = content;
            var track = Child(body, "Scroll track");
            track.anchorMin = new Vector2(1, 0);
            track.anchorMax = Vector2.one;
            track.offsetMin = new Vector2(-12, 0);
            track.offsetMax = Vector2.zero;
            track.gameObject.AddComponent<Image>().color = Control;
            var handle = Child(track, "Handle");
            Stretch(handle);
            var thumb = handle.gameObject.AddComponent<Image>();
            thumb.color = Mint;
            var scrollbar = track.gameObject.AddComponent<Scrollbar>();
            scrollbar.targetGraphic = thumb;
            scrollbar.handleRect = handle;
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scroll.verticalScrollbar = scrollbar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            footer = TextAt(page, "Board status", 19, Amber);
            Place(footer.rectTransform, 16, PageHeight - 61, PageWidth - 32, 60);
        }

        static Renderer Sign(Transform parent, string name)
        {
            foreach (Transform child in parent)
                if (child.name == name) return child.GetComponent<Renderer>();
            return null;
        }

        internal void Frame(Camera view)
        {
            float halfHeight = Mathf.Max(PageHeight, PageWidth / Mathf.Max(.1f, view.aspect)) * .54f * page.lossyScale.y;
            float distance = halfHeight / Mathf.Tan(view.fieldOfView * Mathf.Deg2Rad * .5f);
            view.transform.SetPositionAndRotation(page.position - Vector3.forward * distance, Quaternion.identity);
        }

        internal void SetVisible(bool visible) => canvas.enabled = visible;

        internal void SetInteraction(bool active)
        {
            if (acceptingInput && !active && EventSystem.current)
            {
                var selected = EventSystem.current.currentSelectedGameObject;
                if (selected && selected.transform.IsChildOf(page)) EventSystem.current.SetSelectedGameObject(null);
            }
            acceptingInput = active;
            interaction.interactable = interaction.blocksRaycasts = active;
            if (ownedEvents) ownedEvents.SetActive(active);
            if (active && !EventSystem.current)
            {
                ownedEvents = new GameObject("Quiz board input", typeof(EventSystem), typeof(QuizBoardInput));
                var module = ownedEvents.AddComponent<StandaloneInputModule>();
                module.inputOverride = ownedEvents.GetComponent<QuizBoardInput>();
            }
        }

        internal void BeginPage(string title, bool seated)
        {
            float width = seated ? PageWidth : ((RectTransform)canvas.transform).rect.width * .93f;
            page.sizeDelta = new Vector2(width, PageHeight);
            heading.text = title;
            heading.fontSize = seated ? 32 : 57;
            heading.alignment = seated ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter;
            Place(heading.rectTransform, 16, 2, seated ? 760 : width - 32, seated ? 58 : 90);
            Place(rule, 16, seated ? 64 : 99, width - 36, 2);
            Place(body, 16, seated ? 78 : 119, width - 34, seated ? PageHeight - 142 : PageHeight - 196);
            Place(footer.rectTransform, 16, PageHeight - 65, width - 32, 62);
            footer.fontSize = seated ? 19 : 35;
            footer.alignment = seated ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter;
            layout.spacing = seated ? 9 : 21;
            layout.padding.top = seated ? 0 : 14;
            leave.gameObject.SetActive(seated);
            for (int i = content.childCount - 1; i >= 0; i--)
            {
                var previous = content.GetChild(i).gameObject;
                previous.SetActive(false);
                Object.Destroy(previous);
            }
            scroll.StopMovement();
            scroll.verticalNormalizedPosition = 1;
            content.anchoredPosition = Vector2.zero;
        }

        internal void Status(string value) { footer.text = value; }
        internal Text Line(string value, int size = 23, bool bold = false, bool accent = false)
        {
            var label = TextAt(content, "Chalk text", size, accent ? Mint : Chalk, bold);
            label.text = QuizText.Display(value);
            return label;
        }

        internal RectTransform Row()
        {
            var row = Child(content, "Controls");
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.spacing = 12;
            return row;
        }

        internal Button ActionButton(string value, Action action, RectTransform row = null, bool primary = false)
        {
            var button = MakeButton(row ? row : content, value, action, primary);
            var layout = button.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = 46;
            layout.preferredWidth = 0;
            layout.flexibleWidth = 1;
            // Wrapped choices must still be fully readable on the timed question.
            var wrap = button.gameObject.AddComponent<VerticalLayoutGroup>();
            wrap.padding = new RectOffset(12, 12, 5, 5);
            wrap.childControlWidth = wrap.childControlHeight = true;
            wrap.childForceExpandWidth = wrap.childForceExpandHeight = true;
            return button;
        }

        internal InputField Field(string name, string value, int maxLength, Action<string> changed,
            bool multiline = false, RectTransform row = null, bool numeric = false)
        {
            var rect = Child(row ? row : content, name);
            var background = rect.gameObject.AddComponent<Image>();
            background.color = new Color(.115f, .23f, .205f);
            var field = rect.gameObject.AddComponent<InputField>();
            field.targetGraphic = background;
            var entry = TextAt(rect, "Answer text", 23, Chalk);
            Stretch(entry.rectTransform, 12, 5, 12, 5);
            entry.supportRichText = false;
            entry.alignment = multiline ? TextAnchor.UpperLeft : TextAnchor.MiddleLeft;
            field.textComponent = entry;
            var hint = TextAt(rect, "Placeholder", 21, Mint);
            Stretch(hint.rectTransform, 12, 5, 12, 5);
            hint.text = name;
            field.placeholder = hint;
            field.lineType = multiline ? InputField.LineType.MultiLineNewline : InputField.LineType.SingleLine;
            if (numeric) field.contentType = InputField.ContentType.IntegerNumber;
            field.characterLimit = maxLength;
            field.customCaretColor = true;
            field.caretColor = Chalk;
            field.selectionColor = new Color(.54f, .81f, .7f, .5f);
            field.text = value;
            field.onValueChanged.AddListener(text => changed(text));
            var layout = rect.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = multiline ? 100 : 49;
            layout.flexibleWidth = 1;
            return field;
        }

        Button MakeButton(RectTransform parent, string value, Action action, bool primary)
        {
            var rect = Child(parent, value);
            var surface = rect.gameObject.AddComponent<Image>();
            surface.color = primary ? Selected : Control;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = surface;
            var colors = button.colors;
            colors.highlightedColor = new Color(.82f, 1f, .91f);
            colors.pressedColor = new Color(.8f, .8f, .8f);
            button.colors = colors;
            var label = TextAt(rect, "Label", 22, primary ? Amber : Chalk, true);
            Stretch(label.rectTransform, 10, 3, 10, 3);
            label.alignment = TextAnchor.MiddleCenter;
            label.text = QuizText.Display(value);
            button.onClick.AddListener(() => action());
            return button;
        }

        Text TextAt(RectTransform parent, string name, int size, Color color, bool bold = false)
        {
            var text = Child(parent, name).gameObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            text.color = color;
            text.supportRichText = false;
            text.raycastTarget = false;
            text.alignment = TextAnchor.MiddleLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        static RectTransform Child(RectTransform parent, string name)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false);
            return rect;
        }

        static void Place(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
        }

        static void Stretch(RectTransform rect, float left = 0, float top = 0, float right = 0, float bottom = 0)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        public void Dispose()
        {
            for (int i = 0; i < oldSigns.Length; i++)
                if (oldSigns[i]) oldSigns[i].enabled = signVisibility[i];
            SetInteraction(false);
            if (ownedEvents) { ownedEvents.SetActive(false); Object.Destroy(ownedEvents); }
            if (canvas) { canvas.gameObject.SetActive(false); Object.Destroy(canvas.gameObject); }
        }
    }
}
