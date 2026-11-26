using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace CampusCommons.Webcam
{
    sealed class StudyWindowPicker : IDisposable
    {
        [Serializable] sealed class WindowChoice
        {
            public string id, title, name;
            public int pid;
        }

        [Serializable] sealed class Selection
        {
            public string id;
            public int pid;
        }

        [Serializable] sealed class Reply
        {
            public bool ok, permitted;
            public WindowChoice[] windows;
            public string error;
        }

        readonly MonoBehaviour owner;
        readonly LocalStudyProcess local;
        UnityWebRequest request;
        WindowChoice[] windows = Array.Empty<WindowChoice>();
        Vector2 scroll;
        string selected, notice;
        bool open, needsPermission;

        public StudyWindowPicker(MonoBehaviour owner, LocalStudyProcess local)
        {
            this.owner = owner;
            this.local = local;
        }

        public void Draw(GUIStyle button, GUIStyle label)
        {
            float width = Mathf.Min(180, Screen.width - 24);
            if (GUI.Button(new Rect(12, Screen.height - 48, width, 36),
                selected == null ? "공부 창 선택" : "공부 창 변경", button))
            {
                open = !open;
                if (open && request == null) Send("windows");
            }
            if (!open) return;

            width = Mathf.Min(388, Screen.width - 24);
            float height = Mathf.Min(335, Screen.height - 80);
            GUILayout.BeginArea(new Rect(12, Screen.height - height - 56, width, height), GUI.skin.box);
            GUILayout.Label("선택한 창이 활성일 때만 화면을 확인합니다.", label);
            if (selected != null) GUILayout.Label("선택한 앱 " + selected, label);
            if (request != null) GUILayout.Label("공부 창 확인 중", label);
            else if (!string.IsNullOrEmpty(notice)) GUILayout.Label(notice, label);

            bool enabled = GUI.enabled;
            GUI.enabled = enabled && request == null;
            GUILayout.BeginHorizontal();
            if (needsPermission)
            {
                if (GUILayout.Button("화면 기록 허용", button)) Send("permission");
            }
            else if (GUILayout.Button("목록 새로 고침", button)) Send("windows");
            if (selected != null && GUILayout.Button("선택 해제", button)) Send("clear");
            GUILayout.EndHorizontal();
            if (!needsPermission)
            {
                scroll = GUILayout.BeginScrollView(scroll);
                foreach (WindowChoice window in windows)
                {
                    string display = string.IsNullOrWhiteSpace(window.title) ? window.name : window.title;
                    if (GUILayout.Button(display, button)) Send("select", window);
                }
                GUILayout.EndScrollView();
            }
            GUI.enabled = enabled;
            GUILayout.EndArea();
        }

        void Send(string command, WindowChoice window = null)
        {
            if (request != null) return;
            byte[] body = window == null ? Array.Empty<byte>() :
                Encoding.UTF8.GetBytes(JsonUtility.ToJson(new Selection { id = window.id, pid = window.pid }));
            request = new UnityWebRequest(local.Origin + "/api/study/" + command, "POST")
            {
                uploadHandler = new UploadHandlerRaw(body),
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = command == "permission" ? 18 : 6
            };
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Origin", local.Origin);
            request.SetRequestHeader("X-Session-Token", local.Token);
            owner.StartCoroutine(Read(command, window));
        }

        IEnumerator Read(string command, WindowChoice window)
        {
            yield return request.SendWebRequest();
            Reply reply = null;
            try { reply = JsonUtility.FromJson<Reply>(request.downloadHandler.text); }
            catch (ArgumentException) { }
            request.Dispose();
            request = null;
            if (reply == null || !reply.ok)
            {
                notice = reply?.error ?? "창을 확인하지 못했습니다. 다시 시도해 주세요.";
                yield break;
            }
            notice = null;
            if (command == "select")
            {
                selected = window.name;
                windows = Array.Empty<WindowChoice>();
                open = false;
            }
            else if (command == "clear") selected = null;
            else if (command == "permission")
            {
                needsPermission = !reply.permitted;
                if (reply.permitted) Send("windows");
                else notice = "시스템 설정에서 화면 기록을 허용한 뒤 다시 시도해 주세요.";
            }
            else
            {
                needsPermission = !reply.permitted;
                windows = reply.windows ?? Array.Empty<WindowChoice>();
                if (needsPermission) notice = "공부 창을 보려면 화면 기록 권한이 필요합니다.";
                else if (windows.Length == 0) notice = "확인할 공부 창을 먼저 열어 주세요.";
            }
        }

        public void Dispose()
        {
            if (request == null) return;
            request.Abort();
            request.Dispose();
            request = null;
        }
    }
}
