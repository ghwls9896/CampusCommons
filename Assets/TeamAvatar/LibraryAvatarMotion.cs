#if CAMPUS_SUNBOX_AVATAR

using UnityEngine;
using Sunbox.Avatars;

namespace CampusCommons.GrandLibrary
{
    public class LibraryAvatarMotion : MonoBehaviour
    {
        private AvatarCustomization avatar;

        public void Initialize(GameObject avatarVisual)
        {
            avatar = avatarVisual.GetComponentInChildren
                <AvatarCustomization>(true);

            if (avatar == null)
            {
                Debug.LogError("AvatarCustomization을 찾지 못했습니다.");
                return;
            }

            RuntimeAnimatorController controller =
                Resources.Load<RuntimeAnimatorController>(
                    "TeamAvatar/LibraryMovement");

            if (controller == null)
            {
                Debug.LogError("LibraryMovement 컨트롤러를 찾지 못했습니다.");
                return;
            }

            // 남성과 여성 모두 같은 애니메이션 컨트롤러 사용
            avatar.AnimatorController = controller;
        }

        public void UpdateMovement(bool moving, bool running, bool sitting)
        {
            if (avatar == null) return;

            Animator animator = avatar.Animator;
            if (animator == null) return;

            animator.applyRootMotion = false;

            // 기존 Player.cs는 이동 방향으로 몸을 회전시키므로
            // 전진 걷기 애니메이션을 기본으로 사용

            if (sitting)
            {
                // 앉는 동안 걷기 애니메이션을 즉시 정지
                animator.SetFloat("MoveX", 0f);
                animator.SetFloat("MoveY", 0f);
            }
            else
            {
                animator.SetFloat(
                    "MoveX", 0f, 0.12f, Time.deltaTime);

                animator.SetFloat(
                    "MoveY",
                    moving ? 1f : 0f,
                    0.12f, Time.deltaTime);
            }


            // 착석 여부 전달
            animator.SetBool("IsSitting", sitting);

            // 별도 달리기 클립 추가 전까지 재생 속도로 구분
            animator.speed = moving && running && !sitting
                ? 1.5f : 1f;
        }

        public void Wave()
        {
            if (avatar == null) return;

            Animator animator = avatar.Animator;

            if (animator != null)
                animator.SetTrigger("Wave");
        }
    }
}
#endif
