#if CAMPUS_SUNBOX_AVATAR

using UnityEngine;

namespace CampusCommons.GrandLibrary
{
    public class LibraryAvatarPreview : MonoBehaviour
    {
        private Transform avatarVisual;
        private LibraryAvatarCustomizerUI avatarUI;

        private Camera previewCamera;
        private RenderTexture previewTexture;

        private float rotationAngle = 0f;

        public void Initialize(
            GameObject avatar,
            LibraryAvatarCustomizerUI ui)
        {
            avatarVisual = avatar.transform;
            avatarUI = ui;
        }

        private void CreatePreviewCamera()
        {
            previewTexture = new RenderTexture(512, 768, 16);
            previewTexture.name = "AvatarPreviewTexture";
            previewTexture.Create();

            GameObject cameraObject =
                new GameObject("Avatar Preview Camera");

            previewCamera =
                cameraObject.AddComponent<Camera>();

            previewCamera.targetTexture = previewTexture;

            // 캐릭터만 촬영
            previewCamera.cullingMask = 1 << 2;

            previewCamera.clearFlags =
                CameraClearFlags.SolidColor;

            previewCamera.backgroundColor =
                new Color(0.12f, 0.14f, 0.17f);

            // 전신을 촬영하는 카메라
            previewCamera.orthographic = true;
            previewCamera.orthographicSize = 1.2f;

            previewCamera.nearClipPlane = 0.05f;
            previewCamera.farClipPlane = 10f;
        }

        private void SetPreviewLayer(Transform target)
        {
            // 플레이어가 이미 사용 중인 Ignore Raycast 레이어
            target.gameObject.layer = 2;

            foreach (Transform child in target)
            {
                SetPreviewLayer(child);
            }
        }

        private void LateUpdate()
        {
            if (avatarUI == null)
                return;

            if (!avatarUI.IsOpen)
            {
                if (previewCamera != null)
                    previewCamera.enabled = false;

                return;
            }

            // 1인칭에서는 모델이 숨겨질 수 있으므로
            // 커스터마이징 중에는 3인칭으로 전환
            Player player = GetComponent<Player>();

            if (player != null && player.IsFirstPerson)
                player.SetFirstPerson(false);

            if (avatarVisual == null ||
                !avatarVisual.gameObject.activeInHierarchy)
                return;

            if (previewCamera == null)
                CreatePreviewCamera();

            // 헤어나 의상 변경으로 새로 생긴 메시도 포함
            SetPreviewLayer(avatarVisual);

            Vector3 targetPosition =
                avatarVisual.position + Vector3.up * 1.0f;

            Vector3 direction =
                Quaternion.AngleAxis(
                    rotationAngle, Vector3.up) *
                avatarVisual.forward;

            previewCamera.transform.position =
                targetPosition + direction * 3f;

            previewCamera.transform.LookAt(targetPosition);

            previewCamera.enabled = true;
        }

        private void OnGUI()
        {
            if (avatarUI == null ||
                !avatarUI.IsOpen ||
                previewTexture == null)
                return;

            // 기존 오른쪽 커스터마이징 메뉴의 시작 위치
            float menuWidth =
                Mathf.Min(360f, Screen.width - 20f);

            float menuX =
                Mathf.Max(10f, Screen.width - menuWidth - 10f);

            // 왼쪽에 공간이 부족하면 미리보기 숨김
            if (menuX < 160f)
                return;

            Rect previewRect = new Rect(
                10f,
                110f,
                menuX - 20f,
                Mathf.Max(190f, Screen.height - 130f)
            );

            GUI.Box(previewRect, "내 캐릭터");

            Rect imageRect = new Rect(
                previewRect.x + 8f,
                previewRect.y + 25f,
                previewRect.width - 16f,
                previewRect.height - 68f
            );

            GUI.DrawTexture(
                imageRect,
                previewTexture,
                ScaleMode.ScaleToFit
            );

            float buttonY = previewRect.yMax - 35f;
            float buttonWidth = (previewRect.width - 24f) / 2f;

            if (GUI.Button(
                new Rect(
                    previewRect.x + 8f,
                    buttonY,
                    buttonWidth,
                    26f),
                "왼쪽 회전"))
            {
                rotationAngle -= 45f;
            }

            if (GUI.Button(
                new Rect(
                    previewRect.x + 16f + buttonWidth,
                    buttonY,
                    buttonWidth,
                    26f),
                "오른쪽 회전"))
            {
                rotationAngle += 45f;
            }
        }

        private void OnDestroy()
        {
            if (previewCamera != null)
            {
                previewCamera.targetTexture = null;
                Destroy(previewCamera.gameObject);
            }

            if (previewTexture != null)
            {
                previewTexture.Release();
                Destroy(previewTexture);
            }
        }
    }
}
#else
using UnityEngine;
namespace CampusCommons.GrandLibrary
{
    public class LibraryAvatarPreview : MonoBehaviour
    {
        public RenderTexture PreviewTexture => null;
        public void Initialize(GameObject avatar, LibraryAvatarCustomizerUI ui) { }
    }
}
#endif
