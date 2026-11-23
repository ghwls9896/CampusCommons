using System;
using System.Threading;
using System.Threading.Tasks;
using CampusCommons.Multiplayer;

namespace CampusCommons.Account
{
    public sealed partial class AccountSession
    {
        [Serializable]
        sealed class AvatarUpdate
        {
            public AvatarUserMetadata data;
        }

        LobbyAppearance pendingAvatar;
        Task avatarSave;

        internal LobbyAppearance SavedAvatarAppearance => User?.user_metadata?.campus_avatar_v1;
        internal bool AvatarSavePending => pendingAvatar != null;
        internal bool AvatarSaveFailed { get; private set; }

        internal void QueueAvatarAppearance(LobbyAppearance appearance)
        {
            if (!IsAuthenticated || appearance == null || !appearance.HasSlots) return;
            pendingAvatar = appearance;
            _ = FlushAvatarAppearanceAsync();
        }

        internal Task FlushAvatarAppearanceAsync()
        {
            if (!IsAuthenticated || !AvatarSavePending) return Task.CompletedTask;
            if (avatarSave != null && !avatarSave.IsCompleted) return avatarSave;
            AvatarSaveFailed = false;
            return avatarSave = SaveAvatarAppearanceAsync(generation, lifetime.Token);
        }

        async Task SaveAvatarAppearanceAsync(int expected, CancellationToken ct)
        {
            try
            {
                while (pendingAvatar != null)
                {
                    // Coalesce quick changes and keep requests in order for this session.
                    await Task.Delay(350, ct);
                    await EnsureFreshAsync(ct);
                    Current(expected, ct);
                    var selection = pendingAvatar;
                    var update = new AvatarUpdate { data = new AvatarUserMetadata { campus_avatar_v1 = selection } };
                    var saved = await transport.SendAsync<AuthUser>("PUT", "/auth/v1/user", update, access, ct);
                    Current(expected, ct);
                    if (saved?.id != UserId || !selection.Matches(saved.user_metadata?.campus_avatar_v1))
                        throw new AccountException(502, "invalid_avatar_response");
                    User.user_metadata = saved.user_metadata;
                    if (ReferenceEquals(selection, pendingAvatar)) pendingAvatar = null;
                }
            }
            catch (OperationCanceledException) { }
            catch (AccountException)
            {
                if (expected == generation) AvatarSaveFailed = true;
            }
        }

        void ClearAvatarSave()
        {
            pendingAvatar = null;
            avatarSave = null;
            AvatarSaveFailed = false;
        }
    }
}
