using System.Collections.Generic;
using CampusCommons.LibraryCharacter;
using UnityEngine;

namespace CampusCommons.StudyImmersive
{
    /// <summary>Continuously patrols connected corridors and pushes players along each run.</summary>
    public sealed class EscapeCorridorCleaner : MonoBehaviour
    {
        // Endpoints stop near the actual corridor walls, leaving room for a pushed player.
        static readonly Vector3 SpawnPoint = new Vector3(527.5f, .08f, -27f);
        static readonly Vector3[] CorridorNodes = {
            new Vector3(527.5f, .08f, -46f),
            new Vector3(527.5f, .08f, -18.5f),
            new Vector3(527.5f, .08f, 8.5f),
            new Vector3(527.5f, .08f, 41f),
            new Vector3(492.5f, .08f, -18.5f),
            new Vector3(485f, .08f, 8.5f),
            new Vector3(472f, .08f, 41f),
            new Vector3(472f, .08f, -41f)
        };
        static readonly int[][] Neighbors = {
            new[] { 1 }, new[] { 0, 2, 4 }, new[] { 1, 3, 5 },
            new[] { 2, 6 }, new[] { 1 }, new[] { 2 },
            new[] { 3, 7 }, new[] { 6 }
        };
        const float InitialSpeed = 2.42f;
        const float SlowStartSeconds = 5f;
        const float AccelerationSeconds = 2.5f;
        const float MaximumSpeed = 28.6f;
        const float TurnDegreesPerSecond = 17.5f;
        const float CatchDistance = 1.2f;
        // Cover the corridor from edge to edge; ClearTo still rejects actors
        // separated from the cleaner by a wall or closed door.
        const float CatchHalfWidth = 2f;

        EscapeGame game;
        EscapeWorld world;
        CharacterController robotBody, playerBody;
        readonly List<CharacterController> participants = new List<CharacterController>();
        readonly List<CharacterController> carriedNpcs = new List<CharacterController>();
        readonly List<CollisionPair> temporaryCollisionIgnores = new List<CollisionPair>();
        // Count departures per directed corridor so each junction eventually explores every branch.
        readonly int[,] corridorVisits = new int[CorridorNodes.Length, CorridorNodes.Length];
        // A blocked trip must not count as a completed visit.
        bool currentTripCounted;
        struct CollisionPair { public CharacterController a, b; }
        Transform player, visual;
        SpriteRenderer spriteRenderer;
        Sprite[] views;
        Door[] doors;
        EscapeCleanerNavigation navigation;
        bool navigationReady;
        float navigationRetry;
        AudioSource motionAudio, turningAudio;
        AudioClip turnSound, accelerationSound;
        const float MotorFadeSeconds = .8f;
        float motorFadeRemaining, motorFadeStartVolume;
        bool motorWasMoving;
        Vector3 lastAudioPosition;
        float speed, activeSeconds, accelerationTime, blockedTime, catchCooldown;
        int previousNode, targetNode;
        bool pushing, turning;
        Vector3 direction, facingDirection;
        public bool IsPushing => pushing;

        public static EscapeCorridorCleaner Create(EscapeGame owner, EscapeWorld ownerWorld, Transform target, Door[] roomDoors)
        {
            var atlas = Resources.Load<Texture2D>("EscapePixels/CleaningRobot8");
            if (!atlas || !owner || !ownerWorld || !target) return null;
            var targetBody = target.GetComponent<CharacterController>();
            if (!targetBody) return null;
            var root = new GameObject("Corridor cleaning robot");
            root.transform.SetParent(ownerWorld.transform, true);
            root.transform.position = SpawnPoint;
            var cleaner = root.AddComponent<EscapeCorridorCleaner>();
            cleaner.game = owner;
            cleaner.world = ownerWorld;
            cleaner.player = target;
            cleaner.playerBody = targetBody;
            cleaner.doors = roomDoors;
            cleaner.previousNode = 0;
            cleaner.corridorVisits[0, 1]++;
            cleaner.currentTripCounted = true;
            cleaner.SetTarget(1);
            cleaner.facingDirection = cleaner.direction;
            cleaner.speed = InitialSpeed;
            cleaner.turnSound = Resources.Load<AudioClip>("EscapeSounds/CleanerTurn");
            cleaner.accelerationSound = Resources.Load<AudioClip>("EscapeSounds/CleanerAcceleration");
            cleaner.lastAudioPosition = root.transform.position;
            cleaner.motionAudio = CreateRobotAudio(root);
            cleaner.turningAudio = CreateRobotAudio(root);
            cleaner.robotBody = root.AddComponent<CharacterController>();
            cleaner.robotBody.height = 1.1f;
            cleaner.robotBody.radius = .5f;
            cleaner.robotBody.center = Vector3.up * .55f;
            cleaner.robotBody.stepOffset = .2f;
            foreach (var other in FindObjectsByType<CharacterController>(FindObjectsSortMode.None))
                if (other != cleaner.robotBody) Physics.IgnoreCollision(cleaner.robotBody, other, true);
            var visualObject = new GameObject("Eight direction sprite");
            visualObject.transform.SetParent(root.transform, false);
            cleaner.visual = visualObject.transform;
            cleaner.visual.localScale = Vector3.one * 1.35f;
            cleaner.spriteRenderer = visualObject.AddComponent<SpriteRenderer>();
            cleaner.views = new Sprite[8];
            float w = atlas.width / 4f, h = atlas.height / 2f;
            for (int i = 0; i < cleaner.views.Length; i++)
            {
                int column = i % 4, row = i / 4;
                cleaner.views[i] = Sprite.Create(atlas, new Rect(column * w, (1 - row) * h, w, h),
                    new Vector2(.5f, .14f), w / 1.65f);
            }
            cleaner.UpdateView();
            if (owner.schoolGrid && owner.schoolGrid.links != null)
            {
                cleaner.navigation = new EscapeCleanerNavigation(owner.schoolGrid, ownerWorld, roomDoors);
                cleaner.navigationReady = cleaner.navigation.Plan(root.transform.position);
                if (cleaner.navigationReady)
                {
                    cleaner.SetNavigationDirection();
                    cleaner.facingDirection = cleaner.direction;
                }
            }
            return cleaner;
        }

        static AudioSource CreateRobotAudio(GameObject root)
        {
            var audio = root.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.loop = true;
            audio.spatialBlend = 1f;
            audio.rolloffMode = AudioRolloffMode.Linear;
            audio.minDistance = 2f;
            audio.maxDistance = 20f;
            audio.dopplerLevel = 0f;
            return audio;
        }

        void Update()
        {
            if (!game || !world || !player || !playerBody || !robotBody) return;
            if (!game.IsRunning || game.IsEnded) { Release(); return; }
            UpdateView();
            if (game.IsExitConfirmOpen || game.IsAnswering) return;
            // A professor encounter freezes the player, but must not freeze the
            // cleaner after it releases the group at a wall.
            bool professorEncounter = world.BlocksMovement;
            if (professorEncounter && (pushing || carriedNpcs.Count > 0)) Release();
            catchCooldown = Mathf.Max(0f, catchCooldown - Time.deltaTime);
            if (navigation != null && !navigationReady)
            {
                navigationRetry -= Time.deltaTime;
                if (navigationRetry > 0f) return;
                navigationRetry = 1f;
                navigationReady = navigation.Plan(transform.position);
                if (!navigationReady) return;
                SetNavigationDirection();
                turning = true;
            }
            activeSeconds += Time.deltaTime;
            if (turning)
            {
                Quaternion current = Quaternion.LookRotation(facingDirection, Vector3.up);
                Quaternion target = Quaternion.LookRotation(direction, Vector3.up);
                facingDirection = Quaternion.RotateTowards(current, target,
                    TurnDegreesPerSecond * Time.deltaTime) * Vector3.forward;
                if (Vector3.Angle(facingDirection, direction) < .1f)
                {
                    facingDirection = direction;
                    turning = false;
                }
                UpdateView();
                return;
            }
            if (WaitForDoor())
            {
                ResetAcceleration();
                return;
            }
            UpdateAcceleration(Time.deltaTime);
            float distance = Mathf.Min(speed * game.TimeAttackSpeed * Time.deltaTime,
                Mathf.Max(0f, Vector3.Dot((navigation != null ? navigation.Target : CorridorNodes[targetNode]) - transform.position, direction)));
            if (!professorEncounter && catchCooldown <= 0f && !pushing && CanCatch(playerBody, distance))
            {
                if (robotBody.enabled) Physics.IgnoreCollision(robotBody, playerBody, true);
                pushing = true;
                foreach (var npc in carriedNpcs) IgnoreWhileCarried(playerBody, npc);
            }
            if (!professorEncounter && catchCooldown <= 0f) CaptureNpcs(distance);
            // Center everyone before advancing so a captured actor does not snag on
            // lockers along the edge of a corridor. CharacterController.Move keeps
            // the sideways correction subject to the same collision rules as travel.
            bool aligned = true, alignmentProgress = false;
            if (pushing) AlignCarried(playerBody, ref aligned, ref alignmentProgress);
            foreach (var npc in carriedNpcs) AlignCarried(npc, ref aligned, ref alignmentProgress);
            Vector3 robotBefore = transform.position;
            robotBody.Move(direction * distance + Vector3.down * (2f * Time.deltaTime));
            float robotMoved = Mathf.Max(0f, Vector3.Dot(transform.position - robotBefore, direction));
            bool actorBlocked = false;
            if (pushing)
            {
                float playerMoved = MoveCarried(playerBody, robotMoved);
                actorBlocked |= robotMoved > .001f && playerMoved < robotMoved * .35f;
            }
            for (int i = 0; i < carriedNpcs.Count; i++)
            {
                float npcMoved = MoveCarried(carriedNpcs[i], robotMoved);
                actorBlocked |= robotMoved > .001f && npcMoved < robotMoved * .35f;
            }
            // A character touching a wall must not hold the cleaner back. Leave
            // the group safely in place and keep driving towards the route target.
            if (actorBlocked || (!aligned && !alignmentProgress))
            {
                Release();
                catchCooldown = 2f;
            }
            blockedTime = robotMoved < distance * .35f ? blockedTime + Time.deltaTime : 0f;
            // Reverse visibly if a locked door or temporary barrier blocks the corridor.
            if (blockedTime > .7f) { ReverseBlockedRoute(); return; }
            Vector3 remaining = (navigation != null ? navigation.Target : CorridorNodes[targetNode]) - transform.position;
            remaining.y = 0f;
            if (Vector3.Dot(remaining, direction) < .05f)
            {
                // A collision can slide the controller sideways. Do not mark a
                // segment visited until it actually reaches the grid waypoint.
                if (navigation != null && remaining.sqrMagnitude > .04f)
                {
                    SetNavigationDirection();
                    turning = Vector3.Angle(facingDirection, direction) > .1f;
                    if (turning) ResetAcceleration();
                    return;
                }
                ArriveAtNode();
            }
        }

        void LateUpdate()
        {
            if (!motionAudio || !turningAudio || !game) return;
            Vector3 moved = transform.position - lastAudioPosition;
            lastAudioPosition = transform.position;
            moved.y = 0f;
            bool paused = !game.IsRunning || game.IsEnded || game.IsAnswering || game.IsExitConfirmOpen;
            if (paused)
            {
                motionAudio.Stop();
                turningAudio.Stop();
                motorWasMoving = false;
                motorFadeRemaining = 0f;
                return;
            }

            if (turning && turnSound)
            {
                turningAudio.volume = .18f;
                if (turningAudio.clip != turnSound || !turningAudio.isPlaying)
                {
                    turningAudio.Stop();
                    turningAudio.clip = turnSound;
                    turningAudio.Play();
                }
            }
            else if (turningAudio.isPlaying) turningAudio.Stop();

            bool moving = !turning && moved.sqrMagnitude > .000001f && accelerationSound;
            if (moving)
            {
                float speedFactor = Mathf.InverseLerp(InitialSpeed, MaximumSpeed, speed);
                motionAudio.volume = Mathf.Lerp(.13f, .20f, speedFactor);
                motionAudio.pitch = Mathf.Lerp(.95f, 1.15f, speedFactor);
                motorWasMoving = true;
                motorFadeRemaining = 0f;
                if (motionAudio.clip != accelerationSound || !motionAudio.isPlaying)
                {
                    motionAudio.Stop();
                    motionAudio.clip = accelerationSound;
                    motionAudio.Play();
                }
            }
            else if (motionAudio.isPlaying)
            {
                if (motorWasMoving)
                {
                    motorFadeStartVolume = motionAudio.volume;
                    motorFadeRemaining = MotorFadeSeconds;
                    motorWasMoving = false;
                }
                motorFadeRemaining = Mathf.Max(0f, motorFadeRemaining - Time.deltaTime);
                motionAudio.volume = motorFadeStartVolume * (motorFadeRemaining / MotorFadeSeconds);
                if (motorFadeRemaining <= 0f) motionAudio.Stop();
            }
        }
        void OnDisable()
        {
            if (motionAudio) motionAudio.Stop();
            if (turningAudio) turningAudio.Stop();
            Release();
        }

        void UpdateAcceleration(float deltaTime)
        {
            if (activeSeconds <= SlowStartSeconds) return;
            accelerationTime += deltaTime;
            float progress = Mathf.Clamp01(accelerationTime / AccelerationSeconds);
            speed = Mathf.Lerp(InitialSpeed, MaximumSpeed, progress * progress);
        }
        void ResetAcceleration()
        {
            accelerationTime = 0f;
            speed = InitialSpeed;
        }

        bool WaitForDoor()
        {
            if (doors == null) return false;
            float lookAhead = 3.5f + speed * game.TimeAttackSpeed * Time.deltaTime;
            bool wait = false;
            foreach (var door in doors)
            {
                if (!door || door.IsLocked || door.IsFullyOpen) continue;
                Vector3 normal = door.transform.forward;
                normal.y = 0;
                if (normal.sqrMagnitude < .01f || Mathf.Abs(Vector3.Dot(normal.normalized, direction)) < .7f) continue;
                Vector3 offset = door.DoorwayCenter - transform.position;
                float ahead = Vector3.Dot(offset, direction);
                float side = Mathf.Abs(Vector3.Dot(offset, Vector3.Cross(Vector3.up, direction)));
                if (ahead < -.2f || ahead > lookAhead || side > door.DoorwayWidth * .5f + .4f) continue;
                door.OpenForMonster();
                // Stop early enough that neither the robot nor the player touches the swinging leaf.
                wait = true;
            }
            return wait;
        }

        public void RegisterParticipants(CharacterController professor, CharacterController worker, CharacterController monster)
        {
            participants.Clear();
            foreach (var actor in new[] { professor, worker, monster })
                if (actor && !participants.Contains(actor)) participants.Add(actor);
        }
        public bool IsCarrying(CharacterController actor) => actor && carriedNpcs.Contains(actor);

        void CaptureNpcs(float travel)
        {
            foreach (var actor in participants)
            {
                if (!actor || !actor.enabled || !actor.gameObject.activeInHierarchy || carriedNpcs.Contains(actor)) continue;
                if (!CanCatch(actor, travel)) continue;
                if (robotBody.enabled) Physics.IgnoreCollision(robotBody, actor, true);
                if (pushing) IgnoreWhileCarried(playerBody, actor);
                foreach (var other in carriedNpcs) IgnoreWhileCarried(other, actor);
                carriedNpcs.Add(actor);
            }
        }
        bool CanCatch(CharacterController actor, float travel)
        {
            if (!actor || !actor.enabled || !actor.gameObject.activeInHierarchy) return false;
            Vector3 offset = actor.transform.position - transform.position;
            if (Mathf.Abs(offset.y) > 1.5f) return false;
            float ahead = Vector3.Dot(offset, direction);
            float side = Mathf.Abs(Vector3.Dot(offset, Vector3.Cross(Vector3.up, direction)));
            return ahead >= -.5f && ahead <= CatchDistance + travel && side <= CatchHalfWidth && ClearTo(actor);
        }
        bool ClearTo(CharacterController actor)
        {
            Vector3 from = transform.position + Vector3.up * .8f;
            Vector3 to = actor.transform.position + Vector3.up * .8f;
            bool oldBackfaces = Physics.queriesHitBackfaces;
            try
            {
                Physics.queriesHitBackfaces = true;
                return !HasObstruction(from, to) && !HasObstruction(to, from);
            }
            finally { Physics.queriesHitBackfaces = oldBackfaces; }
        }
        bool HasObstruction(Vector3 from, Vector3 to)
        {
            Vector3 delta = to - from;
            if (delta.sqrMagnitude < .0001f) return false;
            foreach (var hit in Physics.RaycastAll(from, delta.normalized, delta.magnitude,
                         Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                if (hit.collider && !hit.collider.GetComponentInParent<CharacterController>() &&
                    !hit.collider.transform.IsChildOf(transform)) return true;
            return false;
        }
        float MoveCarried(CharacterController actor, float distance)
        {
            if (!actor || !actor.enabled || !actor.gameObject.activeInHierarchy) return 0f;
            Vector3 before = actor.transform.position;
            actor.Move(direction * distance + Vector3.down * (2f * Time.deltaTime));
            return Mathf.Max(0f, Vector3.Dot(actor.transform.position - before, direction));
        }
        void AlignCarried(CharacterController actor, ref bool aligned, ref bool progress)
        {
            if (!actor || !actor.enabled || !actor.gameObject.activeInHierarchy) return;
            Vector3 side = Vector3.Cross(Vector3.up, direction);
            float offset = Vector3.Dot(actor.transform.position - transform.position, side);
            if (Mathf.Abs(offset) <= .04f) return;
            Vector3 before = actor.transform.position;
            actor.Move(side * Mathf.Clamp(-offset, -5f * Time.deltaTime, 5f * Time.deltaTime));
            float remaining = Mathf.Abs(Vector3.Dot(actor.transform.position - transform.position, side));
            if (remaining > .04f) aligned = false;
            if (Mathf.Abs(offset) - remaining > .0001f) progress = true;
        }
        void IgnoreWhileCarried(CharacterController a, CharacterController b)
        {
            if (!a || !b || !a.enabled || !b.enabled || Physics.GetIgnoreCollision(a, b)) return;
            Physics.IgnoreCollision(a, b, true);
            temporaryCollisionIgnores.Add(new CollisionPair { a = a, b = b });
        }
        void SetTarget(int node)
        {
            targetNode = node;
            Vector3 toward = CorridorNodes[node] - transform.position;
            toward.y = 0;
            if (toward.sqrMagnitude > .001f) direction = toward.normalized;
        }
        void SetNavigationDirection()
        {
            Vector3 toward = navigation.Target - transform.position;
            toward.y = 0f;
            if (toward.sqrMagnitude > .001f) direction = toward.normalized;
        }
        int ChooseNextNode(int arrived, int from)
        {
            var choices = Neighbors[arrived];
            int next = -1, fewestVisits = int.MaxValue, ties = 0;
            foreach (int candidate in choices)
            {
                // Do not immediately double back when another corridor is available.
                if (choices.Length > 1 && candidate == from) continue;
                int visits = corridorVisits[arrived, candidate];
                if (visits < fewestVisits)
                {
                    fewestVisits = visits;
                    next = candidate;
                    ties = 1;
                }
                else if (visits == fewestVisits && Random.Range(0, ++ties) == 0)
                    next = candidate;
            }
            if (next < 0) next = from;
            corridorVisits[arrived, next]++;
            return next;
        }
        void ArriveAtNode()
        {
            if (navigation != null)
            {
                Vector3 oldNavigationDirection = direction;
                navigationReady = navigation.Arrive(transform.position);
                if (!navigationReady) { Release(); ResetAcceleration(); return; }
                SetNavigationDirection();
                if (Vector3.Dot(oldNavigationDirection, direction) < .99f)
                {
                    Release(); ResetAcceleration(); turning = true;
                }
                else facingDirection = direction;
                blockedTime = 0f;
                return;
            }
            int arrived = targetNode;
            int next = ChooseNextNode(arrived, previousNode);
            currentTripCounted = true;
            Vector3 oldDirection = direction;
            previousNode = arrived;
            SetTarget(next);
            if (Vector3.Dot(oldDirection, direction) < .99f)
            {
                Release();
                ResetAcceleration();
                turning = true;
            }
            blockedTime = 0f;
        }
        void ReverseBlockedRoute()
        {
            Release();
            if (navigation != null)
            {
                navigationReady = navigation.ReplanBlocked(transform.position, direction);
                if (navigationReady) { SetNavigationDirection(); turning = true; }
                blockedTime = 0f;
                ResetAcceleration();
                return;
            }
            if (currentTripCounted)
            {
                corridorVisits[previousNode, targetNode]--;
                currentTripCounted = false;
            }
            int oldTarget = targetNode;
            SetTarget(previousNode);
            previousNode = oldTarget;
            blockedTime = 0f;
            ResetAcceleration();
            turning = true;
        }
        public void Release()
        {
            pushing = false;
            foreach (var pair in temporaryCollisionIgnores)
                if (pair.a && pair.b && pair.a.enabled && pair.b.enabled) Physics.IgnoreCollision(pair.a, pair.b, false);
            temporaryCollisionIgnores.Clear();
            foreach (var actor in carriedNpcs) if (world && actor) world.CleanerReleased(actor);
            carriedNpcs.Clear();
        }
        void OnDestroy()
        {
            Release();
            if (views != null) foreach (var sprite in views) if (sprite) Destroy(sprite);
        }

        // Atlas order: front, front-right, right, back-right, back, back-left, left, front-left.
        public static int ViewIndex(Vector3 forward, Vector3 towardViewer)
        {
            forward.y = 0; towardViewer.y = 0;
            if (forward.sqrMagnitude < .001f || towardViewer.sqrMagnitude < .001f) return 0;
            // The atlas shows the robot's front at image left for a viewer on
            // its right; reverse the relative angle to match the rendered view.
            float angle = Mathf.Repeat(Vector3.SignedAngle(towardViewer, forward, Vector3.up), 360f);
            return Mathf.RoundToInt(angle / 45f) % 8;
        }
        void UpdateView()
        {
            var camera = Camera.main;
            if (!camera || !spriteRenderer || views == null) return;
            Vector3 towardCamera = camera.transform.position - transform.position;
            spriteRenderer.sprite = views[ViewIndex(facingDirection, towardCamera)];
            towardCamera.y = 0;
            if (towardCamera.sqrMagnitude > .001f) visual.rotation = Quaternion.LookRotation(-towardCamera);
        }
    }
}
