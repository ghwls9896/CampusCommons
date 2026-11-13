using System;
using System.Collections.Generic;
using CampusCommons.LibraryCharacter;
using UnityEngine;

namespace CampusCommons.StudyImmersive
{
    // Restrict the school's walking graph to the corridor centerlines. This keeps
    // the cleaner out of classrooms while retaining dynamic obstacle handling.
    sealed class EscapeCleanerNavigation
    {
        readonly SchoolGrid map;
        readonly EscapeWorld world;
        readonly Door[] doors;
        readonly int[] queue, parent, seen, visits;
        readonly byte[] clearance;
        readonly List<int> route = new List<int>();
        readonly int[] offsets;
        int stamp, cursor, segmentEnd, joinCell;
        bool joining;
        int blockedFrom = -1, blockedTo = -1;
        float blockedUntil;
        public Vector3 Target { get; private set; }

        public EscapeCleanerNavigation(SchoolGrid map, EscapeWorld world, Door[] doors)
        {
            this.map = map; this.world = world; this.doors = doors ?? Array.Empty<Door>();
            int count = map.links.Length;
            queue = new int[count]; parent = new int[count]; seen = new int[count];
            visits = new int[count]; clearance = new byte[count * 4];
            offsets = new[] { map.width, 1, -map.width, -1 };
        }

        static bool Between(float n, float a, float b) => n >= a - .05f && n <= b + .05f;
        static bool Near(float a, float b) => Mathf.Abs(a - b) < .05f;
        static bool OnCorridor(Vector3 p)
        {
            return (Near(p.x, 472f) && Between(p.z, -41f, 41f)) ||
                   (Near(p.z, -41f) && Between(p.x, 472f, 492.5f)) ||
                   (Near(p.x, 492.5f) && Between(p.z, -41f, 8.5f)) ||
                   (Near(p.z, -18.5f) && Between(p.x, 492.5f, 527.5f)) ||
                   (Near(p.z, 8.5f) && Between(p.x, 485f, 527.5f)) ||
                   (Near(p.x, 485f) && Between(p.z, 8.5f, 41f)) ||
                   (Near(p.x, 500f) && Between(p.z, 8.5f, 41f)) ||
                   (Near(p.z, 41f) && Between(p.x, 472f, 527.5f)) ||
                   (Near(p.x, 527.5f) && Between(p.z, -46f, 41f));
        }

        bool StaticClear(int from, int to, int direction)
        {
            int key = from * 4 + direction;
            if (clearance[key] != 0) return clearance[key] == 2;
            Vector3 a = map.Position(from), b = map.Position(to);
            Vector3 delta = b - a;
            bool clear = true;
            bool backfaces = Physics.queriesHitBackfaces;
            try
            {
                Physics.queriesHitBackfaces = true;
                foreach (var hit in Physics.SphereCastAll(a + Vector3.up * .58f, .46f,
                             delta.normalized, delta.magnitude, Physics.DefaultRaycastLayers,
                             QueryTriggerInteraction.Ignore))
                {
                    if (!hit.collider || hit.collider.GetComponentInParent<CharacterController>() ||
                        hit.collider.GetComponentInParent<Door>() ||
                        hit.collider.GetComponentInParent<ConstructionBarrier>()) continue;
                    clear = false; break;
                }
            }
            finally { Physics.queriesHitBackfaces = backfaces; }
            clearance[key] = (byte)(clear ? 2 : 1);
            clearance[to * 4 + ((direction + 2) % 4)] = clearance[key];
            return clear;
        }

        bool HasTemporaryObstacles()
        {
            if (world && world.ActiveBarrierCount > 0) return true;
            foreach (var door in doors) if (door && door.IsLocked) return true;
            return false;
        }

        public bool Plan(Vector3 position)
        {
            int start = map.NearestCell(position);
            if (start >= 0 && !OnCorridor(map.Position(start)))
            {
                float bestDistance = 9f;
                start = -1;
                for (int i = 0; i < map.links.Length; i++)
                {
                    if (map.links[i] == 0) continue;
                    Vector3 point = map.Position(i);
                    if (!OnCorridor(point)) continue;
                    float distance = (new Vector2(point.x, point.z) - new Vector2(position.x, position.z)).sqrMagnitude;
                    if (distance < bestDistance) { bestDistance = distance; start = i; }
                }
            }
            if (start < 0) return false;
            if (stamp == int.MaxValue) { Array.Clear(seen, 0, seen.Length); stamp = 0; }
            stamp++;
            int head = 0, tail = 0, goal = -1, best = int.MaxValue, ties = 0;
            bool temporary = HasTemporaryObstacles();
            queue[tail++] = start; seen[start] = stamp; parent[start] = -1;
            while (head < tail)
            {
                int current = queue[head++];
                // Avoid repeatedly selecting the cell the robot already occupies.
                if (current != start)
                {
                    int count = visits[current];
                    if (count < best) { best = count; goal = current; ties = 1; }
                    else if (count == best && UnityEngine.Random.Range(0, ++ties) == 0) goal = current;
                }
                int x = current % map.width, z = current / map.width;
                for (int d = 0; d < 4; d++)
                {
                    if ((map.links[current] & (1 << d)) == 0 ||
                        (d == 0 && z + 1 >= map.height) || (d == 1 && x + 1 >= map.width) ||
                        (d == 2 && z == 0) || (d == 3 && x == 0)) continue;
                    int next = current + offsets[d];
                    if (seen[next] == stamp) continue;
                    if (!OnCorridor(map.Position(next))) continue;
                    if (Time.time < blockedUntil && ((current == blockedFrom && next == blockedTo) ||
                        (current == blockedTo && next == blockedFrom))) continue;
                    if (temporary && world && world.IsEdgeBlocked(map.Position(current), map.Position(next))) continue;
                    if (!StaticClear(current, next, d)) continue;
                    seen[next] = stamp; parent[next] = current; queue[tail++] = next;
                }
            }
            if (goal < 0) { route.Clear(); return false; }
            route.Clear();
            for (int at = goal; at != start; at = parent[at]) route.Add(at);
            route.Reverse(); cursor = 0;
            // Complete a straight run to the next junction or end instead of
            // turning around at an arbitrary half-metre grid cell.
            int last = route[route.Count - 1];
            int previous = route.Count > 1 ? route[route.Count - 2] : start;
            int forward = last - previous;
            while (true)
            {
                int x = last % map.width, z = last / map.width, choices = 0, forwardDir = -1;
                for (int d = 0; d < 4; d++)
                {
                    if ((map.links[last] & (1 << d)) == 0 ||
                        (d == 0 && z + 1 >= map.height) || (d == 1 && x + 1 >= map.width) ||
                        (d == 2 && z == 0) || (d == 3 && x == 0)) continue;
                    if (!OnCorridor(map.Position(last + offsets[d]))) continue;
                    choices++;
                    if (offsets[d] == forward) forwardDir = d;
                }
                if (choices != 2 || forwardDir < 0) break;
                int next = last + forward;
                if (Time.time < blockedUntil && ((last == blockedFrom && next == blockedTo) ||
                    (last == blockedTo && next == blockedFrom))) break;
                if ((temporary && world && world.IsEdgeBlocked(map.Position(last), map.Position(next))) ||
                    !StaticClear(last, next, forwardDir)) break;
                route.Add(next);
                last = next;
            }
            Vector3 startPoint = map.Position(start);
            if ((new Vector2(position.x, position.z) - new Vector2(startPoint.x, startPoint.z)).sqrMagnitude > .01f)
            {
                joinCell = start;
                joining = true;
                Target = new Vector3(startPoint.x, .08f, startPoint.z);
                return true;
            }
            joining = false;
            return SetNextSegment(start);
        }

        bool SetNextSegment(int from)
        {
            if (cursor >= route.Count) return false;
            int direction = route[cursor] - from;
            segmentEnd = cursor;
            while (segmentEnd + 1 < route.Count && route[segmentEnd + 1] - route[segmentEnd] == direction)
                segmentEnd++;
            Target = map.Position(route[segmentEnd]);
            Target = new Vector3(Target.x, .08f, Target.z);
            return true;
        }

        public bool Arrive(Vector3 position)
        {
            if (joining) { joining = false; return SetNextSegment(joinCell); }
            for (int i = cursor; i <= segmentEnd; i++) if (visits[route[i]] < int.MaxValue) visits[route[i]]++;
            int from = route[segmentEnd];
            cursor = segmentEnd + 1;
            return cursor < route.Count ? SetNextSegment(from) : Plan(position);
        }

        public bool ReplanBlocked(Vector3 position, Vector3 forward)
        {
            int from = map.NearestCell(position);
            int to = map.NearestCell(position + forward * map.step);
            if (from >= 0 && to >= 0 && from != to)
            {
                blockedFrom = from; blockedTo = to; blockedUntil = Time.time + 8f;
            }
            return Plan(position);
        }
    }
}
