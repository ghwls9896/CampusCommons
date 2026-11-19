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
        Transform model;
        GameObject avatarPrefab;
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
                avatarPrefab = Resources.Load<GameObject>("TeamAvatar/LibraryAvatar");
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
            if (controller.enabled && lastSampleTime >= 0 && now > lastSampleTime)
            {
                double speed = Vector2.Distance(planar, lastSamplePosition) / (now - lastSampleTime);
                if (speed > .1f)
                    motion = speed > (local.walkSpeed + local.runSpeed) * .5f ? "run" : "walk";
            }
            lastSampleTime = now;
            lastSamplePosition = planar;
            return new LobbyMove {
                x = position.x, y = position.y, z = position.z,
                yaw = Mathf.Repeat(model.eulerAngles.y, 360f), motion = motion
            };
        }

        public void Apply(string selfId, LobbyPeer[] peers)
        {
            departed.Clear();
            departed.AddRange(remotes.Keys);
            foreach (var peer in peers)
            {
                if (peer == null || string.IsNullOrEmpty(peer.id) || peer.id == selfId) continue;
                // JsonUtility can turn JSON null into a pose object with default field values.
                if (peer.pose == null || string.IsNullOrEmpty(peer.pose.motion)) continue;
                departed.Remove(peer.id);
                if (!remotes.TryGetValue(peer.id, out var remote))
                {
                    remote = new RemoteAvatar(model, avatarPrefab, peer);
                    remotes.Add(peer.id, remote);
                }
                else remote.SetTarget(peer);
            }
            foreach (var id in departed)
            {
                remotes[id].Remove();
                remotes.Remove(id);
            }
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
            readonly Transform head;
            public string Nickname { get; private set; }
            public int Level { get; private set; }
            public Vector3 NameAnchor => head ? head.position + Vector3.up * .29f : root.position + Vector3.up * 1.89f;

            public RemoteAvatar(Transform localVisual, GameObject prefab, LobbyPeer peer)
            {
                root = new GameObject("Lobby participant").transform;
                if (prefab)
                {
                    var avatar = Object.Instantiate(prefab, root, false);
                    animation = root.gameObject.AddComponent<LibraryAvatarMotion>();
                    animation.Initialize(avatar);
                    foreach (var animator in avatar.GetComponentsInChildren<Animator>())
                    {
                        if (!animator.isActiveAndEnabled || !animator.avatar || !animator.avatar.isValid || !animator.isHuman) continue;
                        head = animator.GetBoneTransform(HumanBodyBones.Head);
                        if (head) break;
                    }
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
                SetTarget(peer);
                root.SetPositionAndRotation(target, rotation);
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

            public void SetTarget(LobbyPeer peer)
            {
                Nickname = peer.nickname;
                Level = peer.level;
                target = new Vector3(peer.pose.x, peer.pose.y, peer.pose.z);
                rotation = Quaternion.Euler(0, peer.pose.yaw, 0);
                motion = peer.pose.motion;
            }

            public void Animate(float blend)
            {
                root.SetPositionAndRotation(Vector3.Lerp(root.position, target, blend),
                    Quaternion.Slerp(root.rotation, rotation, blend));
                bool moving = motion != "idle";
                bool running = motion == "run";
                if (animation) animation.UpdateMovement(moving, running, false);
                else if (leftLeg && rightLeg && leftArm && rightArm)
                {
                    float swing = moving ? Mathf.Sin(Time.unscaledTime * (running ? 12f : 8f)) * 25f : 0f;
                    leftLeg.localRotation = rightArm.localRotation = Quaternion.Euler(swing, 0f, 0f);
                    rightLeg.localRotation = leftArm.localRotation = Quaternion.Euler(-swing, 0f, 0f);
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
