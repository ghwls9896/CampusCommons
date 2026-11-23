using System.Collections.Generic;
using CampusCommons.GrandLibrary;
using UnityEngine;

namespace CampusCommons.Multiplayer
{
    internal sealed class LobbyAvatars
    {
        // Player.Create places the local controller at this library entrance.
        static readonly Vector2 Entrance = new Vector2(0f, -9.3f);
        readonly Dictionary<string, RemoteAvatar> remotes = new Dictionary<string, RemoteAvatar>();
        readonly List<string> departed = new List<string>(8);
        Player local;
        CharacterController controller;
        Seat[] seats;
        Portal.Space lastSampleSpace;
        Transform model;
        GameObject avatarPrefab;
        LobbyAvatarStyle localStyle;
        bool leftEntrance;
        Vector2 lastSamplePosition;
        double lastSampleTime = -1;

        public bool Bind(Player player)
        {
            if (!player) return false;
            if (local != player || !model)
            {
                model = player.transform.Find("Student character");
                if (!model) return false;
                local = player;
                controller = player.GetComponent<CharacterController>();
                seats = Object.FindObjectsByType<Seat>(FindObjectsSortMode.None);
                avatarPrefab = Resources.Load<GameObject>("TeamAvatar/LibraryAvatar");
                localStyle = new LobbyAvatarStyle(model);
                leftEntrance = false;
                lastSampleTime = -1;
            }
            Vector3 position = player.transform.position;
            if ((new Vector2(position.x, position.z) - Entrance).sqrMagnitude > .0004f)
                leftEntrance = true;
            return controller != null;
        }

        public void PlaceAtEntry(int slot)
        {
            if (slot < 0 || slot >= 8 || leftEntrance || !local || !controller || !controller.enabled)
                return;
            var here = local.transform.position;
            var offset = new Vector2(here.x, here.z) - Entrance;
            if (offset.sqrMagnitude > .0004f || Mathf.Abs(here.y) > .3f) return;
            // Two columns ahead of the entrance keep the normal orbit camera and doorways clear.
            float x = slot % 2 == 0 ? -1.05f : 1.05f;
            float z = .9f + (slot / 2) * .9f;
            controller.Move(new Vector3(x - offset.x, 0, z - offset.y));
            leftEntrance = true;
            lastSampleTime = -1;
        }

        public LobbyMove Sample()
        {
            Vector3 position = local.transform.position;
            var planar = new Vector2(position.x, position.z);
            double now = Time.timeAsDouble;
            string motion = "idle";
            if (controller.enabled && local.currentSpace == lastSampleSpace && lastSampleTime >= 0 && now > lastSampleTime)
            {
                double speed = Vector2.Distance(planar, lastSamplePosition) / (now - lastSampleTime);
                if (speed > .1f)
                    motion = speed > (local.walkSpeed + local.runSpeed) * .5f ? "run" : "walk";
            }
            lastSampleSpace = local.currentSpace;
            lastSampleTime = now;
            lastSamplePosition = planar;
            return new LobbyMove {
                x = position.x, y = position.y, z = position.z,
                yaw = Mathf.Repeat(model.eulerAngles.y, 360f), motion = motion
            };
        }

        public LobbyAppearance SampleAppearance() => localStyle?.Capture();

        // The current library, mafia, quiz and escape maps are centered at world X 0, 100, 200 and 500.
        // Older servers accept only world coordinates and idle/walk/run.
        static bool InSpace(float x, Portal.Space space)
        {
            return space == Portal.Space.Library ? Mathf.Abs(x) < 50f :
                space == Portal.Space.Quiz && Mathf.Abs(x - 200f) < 50f;
        }

        bool IsSeated(LobbyMove pose)
        {
            if (pose.motion != "idle") return false;
            var anchor = new Vector3(pose.x, pose.y + .72f, pose.z); // Player.TrySit aligns here.
            foreach (var seat in seats)
                if (seat && seat.gameObject.scene == local.gameObject.scene &&
                    (seat.transform.position - anchor).sqrMagnitude < .0001f) return true;
            return false;
        }

        public int Apply(string selfId, LobbyPeer[] peers)
        {
            departed.Clear();
            departed.AddRange(remotes.Keys);
            foreach (var peer in peers)
            {
                if (peer == null || string.IsNullOrEmpty(peer.id) || peer.id == selfId) continue;
                // JsonUtility can turn JSON null into a pose object with default field values.
                if (peer.pose == null || string.IsNullOrEmpty(peer.pose.motion) || !InSpace(peer.pose.x, local.currentSpace)) continue;
                departed.Remove(peer.id);
                if (!remotes.TryGetValue(peer.id, out var remote))
                {
                    remote = new RemoteAvatar(model, avatarPrefab, peer, IsSeated(peer.pose));
                    remotes.Add(peer.id, remote);
                }
                else remote.SetTarget(peer, IsSeated(peer.pose));
            }
            foreach (var id in departed)
            {
                remotes[id].Remove();
                remotes.Remove(id);
            }
            return remotes.Count;
        }

        public void Tick()
        {
            float blend = 1f - Mathf.Exp(-16f * Time.unscaledDeltaTime);
            foreach (var remote in remotes.Values) remote.Animate(blend);
        }

        public void DrawVisibleNameplates(Camera camera, System.Action<Vector2, string, int> draw)
        {
            if (!camera || !camera.isActiveAndEnabled) return;
            foreach (var remote in remotes.Values)
            {
                var p = camera.WorldToScreenPoint(remote.NameAnchor);
                if (p.z > camera.nearClipPlane && p.x >= 0 && p.x <= Screen.width && p.y >= 0 && p.y <= Screen.height)
                    draw(new Vector2(p.x, Screen.height - p.y), remote.Nickname, remote.Level);
            }
        }

        public void Clear()
        {
            foreach (var remote in remotes.Values) remote.Remove();
            remotes.Clear();
            departed.Clear();
            lastSampleTime = -1;
        }

        sealed class RemoteAvatar
        {
            readonly Transform root;
            readonly LibraryAvatarMotion animation;
            readonly Transform leftLeg, rightLeg, leftArm, rightArm;
            Vector3 target;
            Quaternion rotation;
            string motion;
            bool seated;
            readonly Transform visual;
            readonly LobbyAvatarStyle style;
            LobbyAppearance appliedAppearance;
            Animator rig;
            Transform[] seatBones;
            Quaternion[] standingPose;
            readonly Vector3 standingOffset;
            Transform head;
            public string Nickname { get; private set; }
            public int Level { get; private set; }
            public Vector3 NameAnchor => head ? head.position + Vector3.up * .29f : root.position + Vector3.up * 1.89f;

            public RemoteAvatar(Transform localVisual, GameObject prefab, LobbyPeer peer, bool sitting)
            {
                root = new GameObject("Lobby participant").transform;
                if (prefab)
                {
                    var avatar = Object.Instantiate(prefab, root, false);
                    visual = avatar.transform;
                    standingOffset = visual.localPosition;
                    animation = root.gameObject.AddComponent<LibraryAvatarMotion>();
                    animation.Initialize(avatar);
                    style = new LobbyAvatarStyle(visual);
                    RefreshRig();
                }
                else
                {
                    // Only copy meshes and joints. Local controls, colliders, and cameras stay local.
                    CopyMeshes(localVisual, root);
                    head = root.Find("Head");
                    leftLeg = root.Find("Left leg");
                    rightLeg = root.Find("Right leg");
                    leftArm = root.Find("Left arm");
                    rightArm = root.Find("Right arm");
                }
                SetTarget(peer, sitting);
                root.SetPositionAndRotation(target, rotation);
            }

            void RefreshRig()
            {
                Animator active = null;
                foreach (var animator in visual.GetComponentsInChildren<Animator>())
                    if (animator.isActiveAndEnabled && animator.avatar && animator.avatar.isValid && animator.isHuman)
                    {
                        active = animator;
                        break;
                    }
                if (rig == active) return;
                if (seated) SetSeatPose(false);
                rig = active;
                head = null;
                seatBones = null;
                standingPose = null;
                if (!rig) return;
                head = rig.GetBoneTransform(HumanBodyBones.Head);
                seatBones = new[] {
                    rig.GetBoneTransform(HumanBodyBones.LeftUpperLeg), rig.GetBoneTransform(HumanBodyBones.RightUpperLeg),
                    rig.GetBoneTransform(HumanBodyBones.LeftLowerLeg), rig.GetBoneTransform(HumanBodyBones.RightLowerLeg)
                };
                standingPose = new Quaternion[seatBones.Length];
                if (seated) SetSeatPose(true);
            }

            static void CopyMeshes(Transform source, Transform destination)
            {
                foreach (Transform part in source)
                {
                    var joint = new GameObject(part.name).transform;
                    joint.SetParent(destination, false);
                    joint.localPosition = part.localPosition;
                    joint.localRotation = part.localRotation;
                    joint.localScale = part.localScale;
                    if (part.TryGetComponent<MeshFilter>(out var mesh) &&
                        part.TryGetComponent<MeshRenderer>(out var renderer))
                    {
                        joint.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh.sharedMesh;
                        joint.gameObject.AddComponent<MeshRenderer>().sharedMaterials = renderer.sharedMaterials;
                    }
                    CopyMeshes(part, joint);
                }
            }

            public void SetTarget(LobbyPeer peer, bool sitting)
            {
                Nickname = peer.nickname;
                Level = peer.level;
                target = new Vector3(peer.pose.x, peer.pose.y, peer.pose.z);
                rotation = Quaternion.Euler(0, peer.pose.yaw, 0);
                motion = peer.pose.motion;
                if (style != null && peer.appearance != null && !peer.appearance.Matches(appliedAppearance) &&
                    style.Apply(peer.appearance))
                {
                    appliedAppearance = peer.appearance;
                    RefreshRig();
                }
                if (seated == sitting) return;
                seated = sitting;
                if (visual) visual.localPosition = standingOffset + (seated ? new Vector3(0f, -.24f, -.05f) : Vector3.zero);
                SetSeatPose(seated);
            }

            void SetSeatPose(bool remember)
            {
                if (seatBones == null) return;
                for (int i = 0; i < seatBones.Length; i++)
                {
                    if (!seatBones[i]) continue;
                    if (remember) standingPose[i] = seatBones[i].localRotation;
                    else seatBones[i].localRotation = standingPose[i];
                }
            }

            public void Animate(float blend)
            {
                root.SetPositionAndRotation(Vector3.Lerp(root.position, target, blend),
                    Quaternion.Slerp(root.rotation, rotation, blend));
                bool moving = motion != "idle";
                bool running = motion == "run";
                if (animation)
                {
                    animation.UpdateMovement(moving, running, seated);
                    if (seated && seatBones != null)
                        for (int i = 0; i < seatBones.Length; i++)
                            if (seatBones[i]) seatBones[i].localRotation = standingPose[i] * Quaternion.Euler(i < 2 ? -75f : 75f, 0, 0);
                }
                else if (leftLeg && rightLeg && leftArm && rightArm)
                {
                    float swing = moving ? Mathf.Sin(Time.unscaledTime * (running ? 12f : 8f)) * 25f : 0f;
                    leftLeg.localRotation = Quaternion.Euler(seated ? -75f : swing, 0f, 0f);
                    rightLeg.localRotation = Quaternion.Euler(seated ? -75f : -swing, 0f, 0f);
                    rightArm.localRotation = Quaternion.Euler(swing, 0f, 0f);
                    leftArm.localRotation = Quaternion.Euler(-swing, 0f, 0f);
                }
            }

            public void Remove()
            {
                if (!root) return;
                root.gameObject.SetActive(false);
                Object.Destroy(root.gameObject);
            }
        }
    }
}
