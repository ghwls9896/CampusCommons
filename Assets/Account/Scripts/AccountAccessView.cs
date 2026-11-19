using System;
using System.Threading.Tasks;
using UnityEngine;
using Page = CampusCommons.Account.AccountGate.Screen;

namespace CampusCommons.Account
{
    public sealed partial class AccountView
    {
        static readonly Color Cream = new Color(.97f, .94f, .85f);
        static readonly Color Gold = new Color(.91f, .75f, .48f);
        static readonly Color Muted = new Color(.73f, .79f, .75f);
        static readonly Color Ink = new Color(.035f, .095f, .094f);
        Texture2D libraryArt, shade, bookMark;
        Font displayFace;
        GUIStyle titleFace, headingFace, copyFace, smallFace, entryFace, tabFace, actionFace;
        bool requestingEntry;
        Vector2 accessScroll;
        float formX, formWidth, rowY;

        GUIStyle Face(Font font, int size, Color color, FontStyle weight = FontStyle.Normal)
        {
            var style = new GUIStyle { font = font, fontSize = size, fontStyle = weight,
                alignment = TextAnchor.MiddleLeft, richText = false };
            style.normal.textColor = color;
            return style;
        }

        void PrepareAccessArt()
        {
            if (titleFace != null) return;
            libraryArt = Resources.Load<Texture2D>("TwilightLibrary");
            displayFace = Font.CreateDynamicFontFromOSFont(new[] { "Baskerville", "Georgia", "Times New Roman" }, 92);
            var korean = heading.font;
            titleFace = Face(displayFace ? displayFace : korean, 92, Cream);
            headingFace = Face(korean, 38, Cream, FontStyle.Bold);
            copyFace = Face(korean, 16, Cream);
            copyFace.wordWrap = true;
            smallFace = Face(korean, 15, Muted);
            tabFace = Face(korean, 17, Cream, FontStyle.Bold);
            tabFace.alignment = TextAnchor.MiddleCenter;
            actionFace = new GUIStyle(tabFace);
            actionFace.normal.textColor = Ink;
            entryFace = new GUIStyle(GUI.skin.textField) {
                font = korean, fontSize = 19, alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(15, 15, 2, 2), border = new RectOffset()
            };
            foreach (var state in new[] { entryFace.normal, entryFace.hover, entryFace.active, entryFace.focused }) {
                state.background = null;
                state.textColor = Cream;
            }
            shade = new Texture2D(1, 128, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < 128; y++)
                shade.SetPixel(0, y, new Color(.012f, .048f, .049f, .94f * Mathf.Pow(1 - y / 127f, 1.65f)));
            shade.Apply(false, true);
            bookMark = CreateBookMark();
        }

        void ReleaseAccessArt()
        {
            if (shade) Destroy(shade);
            if (bookMark) Destroy(bookMark);
            if (displayFace) Destroy(displayFace);
        }

        static void Paint(Rect rect, Color color)
        {
            if (Event.current.type != EventType.Repaint) return;
            GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, color, 0, 0);
        }

        static Texture2D CreateBookMark()
        {
            // Rasterize the brass outline once. Its local coordinates stay fixed inside scroll groups.
            const int size = 96;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            void Line(float ax, float ay, float bx, float by)
            {
                var a = new Vector2(48 + 2*ax, 48 - 2*ay);
                var b = new Vector2(48 + 2*bx, 48 - 2*by);
                var along = b - a;
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++) {
                    var delta = new Vector2(x + .5f, y + .5f) - a;
                    float u = Mathf.Clamp01(Vector2.Dot(delta, along) / along.sqrMagnitude);
                    byte alpha = (byte)(255 * Mathf.Clamp01(2 - (delta - u * along).magnitude));
                    int index = y * size + x;
                    pixels[index] = new Color32(255, 255, 255, Math.Max(alpha, pixels[index].a));
                }
            }
            for (int side = -1; side <= 1; side += 2) {
                Line(0, 0, 14 * side, -6); Line(14 * side, -6, 14 * side, 6);
                Line(14 * side, 6, 0, 12); Line(18 * side, -2, 18 * side, 9);
                Line(18 * side, 9, 0, 16);
            }
            Line(0, 0, 0, 12); Line(0, -13, 0, -6); Line(-3, -9.5f, 3, -9.5f);
            texture.SetPixels32(pixels);
            texture.Apply(true, true);
            return texture;
        }

        void Book(float x, float y, float scale)
        {
            float radius = 24 * scale;
            GUI.DrawTexture(new Rect(x-radius, y-radius, radius*2, radius*2), bookMark,
                ScaleMode.StretchToFill, true, 0, Gold, 0, 0);
        }

        void DrawSignIn()
        {
            PrepareAccessArt();
            var original = GUI.matrix;
            bool wasEnabled = GUI.enabled;
            // Work in design units even on Retina. Narrow windows stack the artwork above the form.
            float scale = Mathf.Max(.25f, Mathf.Min(Screen.height / 800f, Screen.width / 510f));
            float w = Screen.width / scale, h = Screen.height / scale;
            bool wide = w / h >= 1.22f;
            GUI.matrix = original * Matrix4x4.Scale(new Vector3(scale, scale, 1));
            try {
                float side = Mathf.Clamp(w * .365f, 430, 640);
                float top = wide ? 0 : Mathf.Min(h * .25f, 260);
                float edge = wide ? w - side : 0;
                Rect art = wide ? new Rect(0, 0, edge, h) : new Rect(0, 0, w, top);
                DrawLibrary(art, wide);
                Rect panelRect = new Rect(edge, top, w - edge, h - top);
                Paint(panelRect, Ink);
                if (wide) Paint(new Rect(edge, 0, 2, h), Gold);
                else Paint(new Rect(0, top, w, 2), Gold);
                DrawAccessPanel(panelRect);
            }
            finally { GUI.matrix = original; GUI.enabled = wasEnabled; }
        }

        void DrawLibrary(Rect r, bool wide)
        {
            Paint(r, Ink);
            if (libraryArt) GUI.DrawTexture(r, libraryArt, ScaleMode.ScaleAndCrop);
            GUI.DrawTexture(r, shade);
            float t = Time.unscaledTime;
            for (int i = 0; i < 28; i++) {
                float x = Mathf.Repeat(i * .618034f + .012f * Mathf.Sin(t * .17f + i), 1) * r.width;
                float y = Mathf.Repeat(i * .413f - t * (.007f + i % 3 * .001f), 1) * r.height;
                float size = i % 4 == 0 ? 3 : 1.7f;
                var light = new Color(1, .82f, .45f, .32f + .25f * Mathf.Sin(i + t * .85f));
                Paint(new Rect(x, y, size, size), light);
            }
            float inset = wide ? Mathf.Clamp(r.width * .065f, 36, 90) : 28;
            if (wide) {
                Book(inset + 20, 46, 1.1f);
                titleFace.fontSize = Mathf.RoundToInt(Mathf.Clamp(r.width * .115f, 65, 110));
                float textHeight = titleFace.fontSize * 1.2f;
                float y = r.yMax - textHeight * 2 - 92;
                GUI.Label(new Rect(inset, y, r.width - 2 * inset, textHeight), "Campus", titleFace);
                GUI.Label(new Rect(inset, y + textHeight * .80f, r.width - 2 * inset, textHeight), "Commons", titleFace);
                Paint(new Rect(inset + 3, r.yMax - 64, 38, 2), Gold);
                GUI.Label(new Rect(inset + 54, r.yMax - 82, 200, 38), "황혼의 대도서관", copyFace);
            } else {
                titleFace.fontSize = Mathf.RoundToInt(Mathf.Min(50, r.width / 9));
                GUI.Label(new Rect(inset, r.yMax - 82, r.width - inset * 2, 68), "Campus Commons", titleFace);
            }
        }

        static bool IsLogin(Page p) => p == Page.Start || p == Page.Login;

        string AccessTitle(Page p)
        {
            if (requestingEntry || p == Page.LoadingProfile) return "대도서관 입장";
            switch (p) {
                case Page.Signup: return "계정 만들기";
                case Page.SignupCode: return "이메일 인증";
                case Page.RecoveryEmail: return "비밀번호 찾기";
                case Page.RecoveryCode: return "재설정 코드 확인";
                case Page.NewPassword: return "새 비밀번호";
                default: return "캠퍼스 입장";
            }
        }

        void DrawAccessPanel(Rect panelRect)
        {
            var page = gate.Page;
            bool entering = requestingEntry || page == Page.LoadingProfile;
            bool tabs = !entering && (IsLogin(page) || page == Page.Signup);
            float width = Mathf.Min(406, panelRect.width - 76);
            float rows = entering ? (requestingEntry || gate.Busy ? 270 : 340) : (page == Page.Signup ? 550 : 492);
            float top = Mathf.Max(24, (panelRect.height - rows) * .5f);
            // The content can scroll when an OS window is unusually short or an error wraps.
            Rect window = new Rect(panelRect.x + (panelRect.width - width) / 2 - 9,
                panelRect.y + top, width + 28, panelRect.height - top - 16);
            string message = !gate.Configured ? "계정 연결 설정이 필요합니다. 프로젝트의 계정 설정을 확인해 주세요." : gate.Message;
            float messageHeight = string.IsNullOrEmpty(message) ? 0 : copyFace.CalcHeight(new GUIContent(message), width);
            accessScroll = GUI.BeginScrollView(window, accessScroll, new Rect(0, 0, width + 9, rows + messageHeight), false, false);
            try {
                formX = 9; formWidth = width; rowY = 1;
                Book(formX + 18, rowY + 15, .81f);
                Paint(new Rect(formX + 48, rowY + 16, formWidth - 48, 1), new Color(.68f, .57f, .36f, .55f));
                rowY += 43;
                GUI.Label(Row(57), AccessTitle(page), headingFace);
                rowY += 12;
                if (entering) DrawEntryProgress();
                else {
                    GUI.enabled = GUI.enabled && !gate.Busy && gate.Configured;
                    if (tabs) DrawTabs(page);
                    DrawFields(page);
                }
                GUI.enabled = true;
                if (!string.IsNullOrEmpty(message)) GUI.Label(Row(messageHeight + 8), message, copyFace);
            } finally { GUI.EndScrollView(); }
        }

        Rect Row(float height)
        {
            var rect = new Rect(formX, rowY, formWidth, height);
            rowY += height;
            return rect;
        }

        void Navigate(Page page)
        {
            GUI.FocusControl(null);
            accessScroll = Vector2.zero;
            password = confirm = code = "";
            gate.OpenAccessPage(page);
        }

        void DrawTabs(Page page)
        {
            var row = Row(43);
            float half = row.width / 2;
            Paint(new Rect(row.x, row.yMax - 1, row.width, 1), new Color(.25f, .35f, .32f));
            for (int i = 0; i < 2; i++) {
                bool login = i == 0, active = login == IsLogin(page);
                var hit = new Rect(row.x + half * i, row.y, half, row.height);
                if (active) Paint(new Rect(hit.x, hit.yMax - 3, half, 3), Gold);
                tabFace.normal.textColor = active ? Gold : Muted;
                GUI.Label(hit, login ? "로그인" : "회원가입", tabFace);
                if (GUI.Button(hit, GUIContent.none, GUIStyle.none) && !active)
                    Navigate(login ? Page.Login : Page.Signup);
            }
            rowY += 17;
        }

        string AccessField(string caption, string value, bool secret = false)
        {
            GUI.Label(Row(27), caption, smallFace);
            var box = Row(46);
            GUI.SetNextControlName(caption);
            bool focused = GUI.GetNameOfFocusedControl() == caption;
            Paint(box, focused ? new Color(.105f, .20f, .19f) : new Color(.065f, .15f, .145f));
            Paint(new Rect(box.x, box.yMax - 1.5f, box.width, 1.5f), focused ? Gold : new Color(.30f, .42f, .38f));
            string result = secret ? GUI.PasswordField(box, value, '●', 128, entryFace) : GUI.TextField(box, value, 254, entryFace);
            rowY += 10;
            return result;
        }

        bool Submit(string title)
        {
            rowY += 5;
            Rect hit = Row(49);
            bool hover = hit.Contains(Event.current.mousePosition) && GUI.enabled;
            Color brass = hover ? new Color(1, .87f, .60f) : Gold;
            Paint(hit, brass);
            Paint(new Rect(hit.x, hit.y, hit.width, 1.5f), Cream);
            GUI.Label(hit, title, actionFace);
            rowY += 13;
            var ev = Event.current;
            bool enter = GUI.enabled && ev.type == EventType.KeyDown && (ev.keyCode == KeyCode.Return || ev.keyCode == KeyCode.KeypadEnter) && string.IsNullOrEmpty(UnityEngine.Input.compositionString);
            if (enter) ev.Use();
            return GUI.Button(hit, GUIContent.none, GUIStyle.none) || enter;
        }

        bool Link(Rect hit, string title, bool centered = false)
        {
            smallFace.alignment = centered ? TextAnchor.MiddleCenter : TextAnchor.MiddleLeft;
            smallFace.normal.textColor = hit.Contains(Event.current.mousePosition) ? Gold : Muted;
            GUI.Label(hit, title, smallFace);
            smallFace.normal.textColor = Muted;
            smallFace.alignment = TextAnchor.MiddleLeft;
            return GUI.Button(hit, GUIContent.none, GUIStyle.none);
        }

        void BeginEntry(Func<Task> action)
        {
            Run(async () => {
                requestingEntry = true;
                accessScroll = Vector2.zero;
                try { await action(); }
                finally { requestingEntry = false; }
            });
        }

        void DrawFields(Page p)
        {
            if (p != Page.NewPassword) email = AccessField("이메일", email);
            if (IsLogin(p) || p == Page.Signup || p == Page.NewPassword)
                password = AccessField(IsLogin(p) ? "비밀번호" : "비밀번호  ·  12~128자", password, true);
            if (p == Page.Signup || p == Page.NewPassword) confirm = AccessField("비밀번호 확인", confirm, true);
            if (p == Page.RecoveryCode || p == Page.SignupCode) code = AccessField("메일로 받은 확인 코드", code);

            switch (p) {
                case Page.Start: case Page.Login:
                    if (Submit("로그인하고 입장")) { string e = email, pass = password; BeginEntry(() => gate.LoginAsync(e, pass)); }
                    if (Link(Row(33), "비밀번호 찾기", true)) Navigate(Page.RecoveryEmail);
                    break;
                case Page.Signup:
                    if (Submit("가입하고 인증 코드 받기")) PasswordAction(pass => gate.SignupAsync(email, pass));
                    break;
                case Page.SignupCode:
                    if (Submit("인증하고 입장")) { string e = email, c = code; BeginEntry(() => gate.VerifySignupAsync(e, c)); }
                    if (Link(Row(31), "인증 코드 다시 받기")) Run(() => gate.ResendSignupCodeAsync(email));
                    break;
                case Page.RecoveryEmail:
                    if (Submit("재설정 코드 받기")) Run(() => gate.RecoverAsync(email));
                    break;
                case Page.RecoveryCode:
                    if (Submit("코드 확인")) Run(() => gate.VerifyRecoveryAsync(email, code));
                    if (Link(Row(31), "재설정 코드 다시 받기")) Run(() => gate.RecoverAsync(email));
                    break;
                case Page.NewPassword:
                    if (Submit("비밀번호 변경")) PasswordAction(pass => gate.ResetPasswordAsync(pass));
                    break;
            }
            if (!IsLogin(p) && p != Page.Signup && Link(Row(32), "로그인으로 돌아가기")) {
                gate.Session.Clear(); Navigate(Page.Login);
            }
            if (gate.Busy) GUI.Label(Row(30), "요청을 처리하고 있습니다…", smallFace);
        }

        void DrawEntryProgress()
        {
            bool profile = gate.Page == Page.LoadingProfile;
            bool loading = requestingEntry || gate.Busy;
            GUI.Label(Row(44), !loading ? "입장 준비가 중단되었습니다." :
                profile ? "프로필을 불러오고 있습니다…" : "계정을 확인하고 있습니다…", copyFace);
            var track = Row(6);
            Paint(track, new Color(.15f, .25f, .23f));
            float half = track.width * .5f;
            if (profile) Paint(new Rect(track.x, track.y, half, 6), Gold);
            if (loading) {
                float x = (profile ? half : 0) + Mathf.PingPong(Time.unscaledTime * 95, half - 40);
                Paint(new Rect(track.x + x, track.y, 40, 6), Cream);
            }
            rowY += 8;
            var captions = Row(32);
            GUI.Label(captions, "계정 인증", smallFace);
            GUI.Label(new Rect(captions.x + half, captions.y, half, captions.height), "프로필 연결", smallFace);
            if (!loading) {
                if (Submit("다시 연결")) BeginEntry(gate.LoadProfileAndEnterAsync);
                if (Link(Row(32), "다른 계정으로 로그인")) Run(gate.LogoutAsync);
            }
        }
    }
}
