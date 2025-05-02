namespace SpaceWar.Services;

using System.Diagnostics;
using GnsSharp;
using SpaceWar.Logic;
using SpaceWar.Models;

public sealed class LobbyUsers(ISteamLobbyServiceHandlers handlers)
{
    public const int MaxMembers = 250;

    ISteamLobbyServiceHandlers handlers = handlers;

    readonly Lock usersLock = new();
    readonly Dictionary<CSteamID, Peer> users = new(MaxMembers);

    public bool Ready
    {
        get
        {
            int numberOfPlayers = 0;

            lock (usersLock)
            {
                foreach (Peer user in users.Values)
                {
                    if (user.Mode == PlayerMode.Player)
                    {
                        if (!user.Ready)
                            return false;

                        ++numberOfPlayers;
                    }
                }
            }

            return numberOfPlayers >= 2;
        }
    }

    public void Clear()
    {
        lock (usersLock)
        {
            users.Clear();
        }
    }

    public void InitializeWithExistingUsers(CSteamID lobbyId)
    {
        Debug.Assert(lobbyId != CSteamID.Nil);

        var matchmaking = ISteamMatchmaking.User;
        var friends = ISteamFriends.User;

        Peer[] players = null;
        Peer[] spectators = null;

        lock (usersLock)
        {
            users.Clear();

            int usersCount = matchmaking.GetNumLobbyMembers(lobbyId);
            for (int i = 0; i < usersCount; ++i)
            {
                CSteamID peerId = matchmaking.GetLobbyMemberByIndex(lobbyId, i);
                AddUser(lobbyId, peerId);
            }

            GetPlayersAndSpectatorsArrayFromUsersDict(out players, out spectators);
        }

        handlers.OnUserListUpdated(players, spectators);
    }

    public void AddUser(CSteamID lobbyId, CSteamID userId)
    {
        lock (usersLock)
        {
            // Lock before creating a peer.
            // Otherwise, the peer info change might be lost when callback happens before adding new peer to `users`.
            Peer peer = CreatePeer(userId);

            users.Add(userId, peer);
        }

        Peer CreatePeer(CSteamID peerId)
        {
            var peer = new Peer()
            {
                PeerId = peerId,
                Username = ISteamFriends.User.GetFriendPersonaName(peerId),
                Mode = GetPeerMode(lobbyId, peerId),
                Ready = GetPeerReady(lobbyId, peerId),
            };

            if (peer.IsUsernameInvalid)
                peer.Username = peerId.ToString();

            return peer;
        }

        OnUserListUpdated();
    }

    public void RemoveUser(CSteamID userId)
    {
        lock (usersLock)
        {
            users.Remove(userId);
        }

        OnUserListUpdated();
    }

    public void UpdateUsername(CSteamID userId)
    {
        UpdateUser(userId, (Peer user) =>
        {
            user.Username = ISteamFriends.User.GetFriendPersonaName(user.PeerId);
        });
    }

    /// <remarks>
    /// This doesn't include username. Call <see cref="UpdateUsername"/> for that.
    /// </remarks>
    public void UpdateUserMetadata(CSteamID lobbyId, CSteamID userId)
    {
        // Update metadata of the user
        UpdateUser(userId, (Peer user) =>
        {
            user.Mode = GetPeerMode(lobbyId, userId);
            user.Ready = GetPeerReady(lobbyId, userId);
        });
    }

    public void GetPlayersAndSpectatorsArrayFromUsersDict(out Peer[] players, out Peer[] spectators)
    {
        lock (usersLock)
        {
            players = [.. users.Values.Where((peer) => peer.Mode == PlayerMode.Player)];
            spectators = [.. users.Values.Where((peer) => peer.Mode == PlayerMode.Spectator)];
        }
    }

    public void FillPlayersAndSpectatorsArrayWithSteamIds(out Peer[] players, out Peer[] spectators, CSteamID[] playerIds, CSteamID[] spectatorIds)
    {
        lock (usersLock)
        {
            players = new Peer[playerIds.Length];
            spectators = new Peer[spectatorIds.Length];

            for (int i = 0; i < playerIds.Length; ++i)
            {
                CSteamID id = playerIds[i];
                if (users.TryGetValue(id, out Peer user))
                    players[i] = user;
                else
                    players[i] = GetPeer(id);
            }

            for (int i = 0; i < spectatorIds.Length; ++i)
            {
                CSteamID id = spectatorIds[i];
                if (users.TryGetValue(id, out Peer user))
                    spectators[i] = user;
                else
                    spectators[i] = GetPeer(id);
            }
        }

        static Peer GetPeer(CSteamID id)
        {
            return new Peer() { PeerId = id, Username = ISteamFriends.User.GetFriendPersonaName(id), Mode = PlayerMode.Player, Ready = true };
        }
    }

    void UpdateUser(CSteamID userId, Action<Peer> action)
    {
        bool updated = false;

        lock (usersLock)
        {
            if (users.TryGetValue(userId, out Peer user))
            {
                action(user);
                updated = true;
            }
        }

        if (updated)
            OnUserListUpdated();
    }

    void OnUserListUpdated()
    {
        Peer[] players;
        Peer[] spectators;

        GetPlayersAndSpectatorsArrayFromUsersDict(out players, out spectators);

        handlers.OnUserListUpdated(players, spectators);
    }

    static PlayerMode GetPeerMode(CSteamID lobbyId, CSteamID peerId)
    {
        string raw = ISteamMatchmaking.User.GetLobbyMemberData(lobbyId, peerId, SteamLobbyConstants.SpectatorKey);

        return raw switch
        {
            "1" => PlayerMode.Spectator,
            "0" => PlayerMode.Player,
            _ => PlayerMode.Unknown,
        };
    }

    static bool GetPeerReady(CSteamID lobbyId, CSteamID peerId)
    {
        string raw = ISteamMatchmaking.User.GetLobbyMemberData(lobbyId, peerId, SteamLobbyConstants.ReadyKey);

        return raw switch
        {
            "1" => true,
            _ => false,
        };
    }
}
