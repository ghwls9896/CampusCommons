using System;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CampusCommons.Account;
using UnityEngine;

namespace CampusCommons.Multiplayer
{
    [Serializable]
    internal sealed class LobbyMove
    {
        public string type = "move";
        public int seq;
        public float x, y, z, yaw;
        public string motion;

        public bool ChangedSince(LobbyMove previous)
        {
            return previous == null || motion != previous.motion ||
                Mathf.Abs(x - previous.x) > .001f || Mathf.Abs(y - previous.y) > .001f ||
                Mathf.Abs(z - previous.z) > .001f || Mathf.Abs(Mathf.DeltaAngle(yaw, previous.yaw)) > .25f;
        }
    }

    // Unity fills these public fields from the #45 wire format.
#pragma warning disable 0649
    [Serializable]
    internal sealed class LobbyPeer
    {
        public string id, nickname;
        public int slot, level;
        public LobbyMove pose;
        public LobbyAppearance appearance;
    }

    [Serializable]
    internal sealed class LobbyFrame
    {
        public string type, self_id;
        public long revision;
        public int appearance_version;
        public LobbyPeer[] players;
    }
#pragma warning restore 0649

    // Each visit owns its own socket, cancellation source and send sequence.
    internal sealed class LobbyChannel
    {
        readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        ClientWebSocket socket;
        Task pendingSend = Task.CompletedTask;
        bool stopped;
        int nextSequence;
        LobbyMove last;
        LobbyAppearance lastAppearance;
        public bool SupportsAppearance { get; private set; }

        public void Stop()
        {
            if (stopped) return;
            stopped = true;
            lifetime.Cancel();
            socket?.Abort();
        }

        // LateUpdate invokes this at most ten times per second. A slow send drops
        // intermediate poses, so it cannot build a queue of stale positions.
        public void Publish(LobbyMove pose, LobbyAppearance appearance)
        {
            if (!pendingSend.IsCompleted || socket?.State != WebSocketState.Open) return;
            string style = null, movement = null;
            if (SupportsAppearance && appearance != null && !appearance.Matches(lastAppearance))
            {
                style = JsonUtility.ToJson(appearance);
                lastAppearance = appearance;
            }
            if (pose.ChangedSince(last))
            {
                pose.seq = nextSequence++;
                movement = JsonUtility.ToJson(pose);
                last = pose;
            }
            if (style != null || movement != null) pendingSend = SendAsync(style, movement);
        }

        async Task SendAsync(string style, string movement)
        {
            try
            {
                if (style != null) await SendText(style);
                if (movement != null) await SendText(movement);
            }
            catch (Exception)
            {
                if (!lifetime.IsCancellationRequested) Stop();
            }
        }

        Task SendText(string json) => socket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(json)),
            WebSocketMessageType.Text, true, lifetime.Token);

        public async Task<int> RunAsync(AccountSession session, string user, Action<LobbyFrame> apply)
        {
            // A sleeping service may take a minute to start. CancelAfter is disabled
            // after admission since an unchanged room legitimately sends no snapshots.
            lifetime.CancelAfter(TimeSpan.FromSeconds(90));
            try
            {
                socket = await session.OpenLobbyAsync(user, lifetime.Token);
                var buffer = new byte[8192];
                while (!lifetime.IsCancellationRequested)
                {
                    int length = 0;
                    WebSocketReceiveResult part;
                    do
                    {
                        if (length == buffer.Length) throw new InvalidDataException();
                        part = await socket.ReceiveAsync(new ArraySegment<byte>(buffer, length, buffer.Length - length), lifetime.Token);
                        if (part.MessageType == WebSocketMessageType.Close)
                        {
                            if (part.CloseStatusDescription == "token_expired" && (int?)part.CloseStatus == 4401)
                                return 1001; // Reopen with the current account session (including refreshed bearer).
                            return (int?)part.CloseStatus ?? 1006;
                        }
                        if (part.MessageType != WebSocketMessageType.Text) throw new InvalidDataException();
                        length += part.Count;
                    } while (!part.EndOfMessage);

                    lifetime.Token.ThrowIfCancellationRequested();
                    var frame = JsonUtility.FromJson<LobbyFrame>(Encoding.UTF8.GetString(buffer, 0, length));
                    if (frame?.type == "welcome") SupportsAppearance = frame.appearance_version == 1;
                    apply(frame);
                    if (frame.type == "welcome") lifetime.CancelAfter(Timeout.Infinite);
                }
            }
            catch (AccountException e) when (e.Status == 401 || e.Status == 403)
            {
                return e.Code == "profile_not_ready" ? 4403 : 4401;
            }
            catch (InvalidDataException) { return 1008; }
            catch (ArgumentException) { return 1008; } // Malformed JSON.
            catch (OperationCanceledException) { }
            catch (WebSocketException) { }
            catch (Exception e)
            {
                // Network backends can report different exception types. Never log
                // request data, player identities, or exception messages containing headers.
                Debug.LogWarning("로비 연결 오류 (" + e.GetType().Name + ")");
            }
            finally
            {
                Stop();
                await pendingSend;
                socket?.Dispose();
                socket = null;
                lifetime.Dispose();
            }
            return 1006;
        }
    }
}
