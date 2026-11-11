#if CAMPUS_SUNBOX_AVATAR

using UnityEngine;
using Sunbox.Avatars;

namespace CampusCommons.GrandLibrary
{
    public class LibraryAvatarSitPose : MonoBehaviour
    {
        [Header("앉기 자세 각도")]
        public float thighAngle = -75f;
        public float kneeAngle = 75f;



        [Header("의자 위치 보정")]
        public Vector3 sitOffset = new Vector3(0f, -0.24f, -0.05f);


        private Transform avatarVisualTransform;
        private Vector3 originalAvatarPosition;


        private AvatarCustomization avatar;
        private bool sitting;

        private Transform currentBase;
        private Transform leftThigh, rightThigh;
        private Transform leftKnee, rightKnee;

        private Quaternion leftThighBase, rightThighBase;
        private Quaternion leftKneeBase, rightKneeBase;
        private bool hasBasePose;

        public void Initialize(GameObject avatarVisual)
        {
            avatarVisualTransform = avatarVisual.transform;
            originalAvatarPosition = avatarVisualTransform.localPosition;
            avatar = avatarVisual.GetComponentInChildren
                <AvatarCustomization>(true);

            if (avatar == null)
                Debug.LogError("앉기 자세: AvatarCustomization을 찾지 못했습니다.");
        }


        public void SetSitting(bool value)
        {
            if (sitting != value)
            {
                sitting = value;

                // 앉기 또는 일어서기 상태가 바뀌면
                // 이전에 저장한 다리 자세를 다시 계산
                hasBasePose = false;
            }
        }


        private Transform FindBone(Transform root, string boneName)
        {
            foreach (Transform bone in
                root.GetComponentsInChildren<Transform>(true))
            {
                if (bone.name == boneName)
                    return bone;
            }
            return null;
        }

        private void ConnectBones()
        {
            Transform baseTransform = avatar.CurrentBase.transform;

            if (baseTransform == currentBase)
                return;

            currentBase = baseTransform;
            hasBasePose = false;

            leftThigh = FindBone(currentBase, "UpperLeg_L");
            rightThigh = FindBone(currentBase, "UpperLeg_R");
            leftKnee = FindBone(currentBase, "LowerLeg_L");
            rightKnee = FindBone(currentBase, "LowerLeg_R");

            if (!leftThigh || !rightThigh ||
                !leftKnee || !rightKnee)
            {
                Debug.LogError(
                    "앉기 자세: 다리 뼈대를 찾지 못했습니다.");
            }
        }

        private void RememberBasePose()
        {
            leftThighBase = leftThigh.localRotation;
            rightThighBase = rightThigh.localRotation;
            leftKneeBase = leftKnee.localRotation;
            rightKneeBase = rightKnee.localRotation;

            hasBasePose = true;
        }

        private void LateUpdate()
        {

            // 앉으면 캐릭터 외형만 의자 위치에 맞게 보정
            if (avatarVisualTransform != null)
            {
                Vector3 target = originalAvatarPosition +
                    (sitting ? sitOffset : Vector3.zero);

                avatarVisualTransform.localPosition = Vector3.Lerp(
                    avatarVisualTransform.localPosition,
                    target,
                    Mathf.Clamp01(Time.deltaTime * 10f)
                );
            }

            if (avatar == null || avatar.CurrentBase == null)
                return;

            ConnectBones();

            if (!leftThigh || !rightThigh ||
                !leftKnee || !rightKnee)
                return;

            // 평소에는 Animator가 만든 자세를 보존
            if (!sitting)
            {
                RememberBasePose();
                return;
            }

            if (!currentBase.gameObject.activeInHierarchy)
                return;

            if (!hasBasePose)
                RememberBasePose();

            // 기존 캐릭터의 다리 회전 방식 재현
            leftThigh.localRotation = leftThighBase *
                Quaternion.Euler(thighAngle, 0f, 0f);

            rightThigh.localRotation = rightThighBase *
                Quaternion.Euler(thighAngle, 0f, 0f);

            leftKnee.localRotation = leftKneeBase *
                Quaternion.Euler(kneeAngle, 0f, 0f);

            rightKnee.localRotation = rightKneeBase *
                Quaternion.Euler(kneeAngle, 0f, 0f);
        }
    }
}
#else
using UnityEngine;
namespace CampusCommons.GrandLibrary
{
    public class LibraryAvatarSitPose : MonoBehaviour
    {
        public Vector3 sitOffset = new Vector3(0f, -0.24f, -0.05f);
        public void Initialize(GameObject avatar) { }
        public void SetSitting(bool sitting) { }
    }
}
#endif
