namespace SpaceWar.Services;

using System.Diagnostics;
using System.Threading.Tasks;
using Backdash;
using Backdash.Gns;
using GnsSharp;
using SpaceWar.Logic;
using SpaceWar.Models;

public sealed class SteamLobbyService(ISteamLobbyServiceHandlers handlers) : IDisposable
{
    readonly LobbyUsers users = new(handlers);
    readonly ISteamLobbyServiceHandlers handlers = handlers;

    CSteamID lobbyId = CSteamID.Nil;
    public CSteamID LobbyId
    {
        get => Volatile.Read(ref lobbyId.Id);
        set => Volatile.Write(ref lobbyId.Id, value.Id);
    }

    ~SteamLobbyService() => Dispose();

    public void Dispose()
    {
        LeaveLobby();
        GC.SuppressFinalize(this);
    }

    public async Task CreateLobby()
    {
        try
        {
            SubscribeLobbyEvents();

            CallTask<LobbyCreated_t> task = ISteamMatchmaking.User.CreateLobby(ELobbyType.Public, LobbyUsers.MaxMembers);
            if (task == null)
                throw new Exception("ISteamMatchmaking.CreateLobby returned null task");

            LobbyCreated_t? lobbyCreated = await task;
            if (!lobbyCreated.HasValue)
                throw new Exception("ISteamMatchmaking.CreateLobby returned null result");
            if (lobbyCreated.Value.Result != EResult.OK)
                throw new Exception($"ISteamMatchmaking.CreateLobby failed with {lobbyCreated.Value.Result}");

            LobbyId = lobbyCreated.Value.SteamIDLobby;
            GlobalConfig.Instance.LobbySteamId = lobbyCreated.Value.SteamIDLobby;

            InitializeLobbyData(LobbyId);
            InitializeLobbyMemberData(LobbyId);
            users.InitializeWithExistingUsers(LobbyId);

            GC.ReRegisterForFinalize(this);
        }
        catch (Exception ex)
        {
            Log.Error(ex, ex.Message);
            Dispose();
        }
    }

    public async Task EnterLobby(CSteamID lobbySteamId)
    {
        try
        {
            SubscribeLobbyEvents();

            CallTask<LobbyEnter_t> task = ISteamMatchmaking.User.JoinLobby(lobbySteamId);
            if (task == null)
                throw new Exception("ISteamMatchmaking.JoinLobby returned null task");

            LobbyEnter_t? lobbyEnter = await task;
            if (!lobbyEnter.HasValue)
                throw new Exception("ISteamMatchmaking.JoinLobby returned null result");
            if (lobbyEnter.Value.ChatRoomEnterResponse != EChatRoomEnterResponse.Success)
                throw new Exception($"ISteamMatchmaking.JoinLobby failed with {lobbyEnter.Value.ChatRoomEnterResponse}");

            LobbyId = lobbyEnter.Value.SteamIDLobby;
            GlobalConfig.Instance.LobbySteamId = lobbyEnter.Value.SteamIDLobby;

            FetchLobbyData(LobbyId);
            InitializeLobbyMemberData(LobbyId);
            users.InitializeWithExistingUsers(LobbyId);

            GC.ReRegisterForFinalize(this);
        }
        catch (Exception ex)
        {
            Log.Error(ex, ex.Message);
            Dispose();
        }
    }

    public void LeaveLobby()
    {
        if (LobbyId == CSteamID.Nil)
            return;

        ISteamMatchmaking.User.LeaveLobby(LobbyId);
        LobbyId = CSteamID.Nil;

        UnsubscribeLobbyEvents();
    }

    public void SetLocalPlayerReady()
    {
        var matchmaking = ISteamMatchmaking.User;

        matchmaking.SetLobbyMemberData(LobbyId, SteamLobbyConstants.ReadyKey, "1");
    }

    void OnUserJoined(CSteamID userId)
    {
        users.AddUser(lobbyId, userId);
    }

    void OnUserLeft(CSteamID userId)
    {
        users.RemoveUser(userId);
        BroadcastStartGameMsgIfRequired();
    }

    void OnLobbyDataUpdate(ref LobbyDataUpdate_t update)
    {
        if (update.SteamIDLobby != LobbyId)
            return;

        if (!update.Success)
        {
            Log.Warning("Failed to update lobby metadata.");
            return;
        }

        if (update.SteamIDMember != update.SteamIDLobby)
            users.UpdateUserMetadata(LobbyId, update.SteamIDMember);

        BroadcastStartGameMsgIfRequired();
    }

    void OnLobbyChatMsg(ref LobbyChatMsg_t msg)
    {
        if (msg.SteamIDLobby != LobbyId)
            return;

        if (((EChatEntryType)msg.ChatEntryType) != EChatEntryType.ChatMsg)
            return;

        var matchmaking = ISteamMatchmaking.User;

        if (msg.SteamIDUser != matchmaking.GetLobbyOwner(LobbyId))
        {
            Log.Warning($"Non lobby owner steamid:{msg.SteamIDUser} sent a LobbyChatMsg_t, which is forbidden for this sample");
            return;
        }

        // Header (3 bytes) + number of players (1 byte) + number of spectators (1 byte) + steam id of each peer
        var payload = new byte[3 + 2 + (8 * LobbyUsers.MaxMembers)];

        int bytesReceived = matchmaking.GetLobbyChatEntry(msg.SteamIDLobby, (int)msg.ChatID, payload);

        if (DeserializeStartGameMsgToGlobalConfig(payload, bytesReceived))
            handlers.OnStartGameMsgReceived();

        bool DeserializeStartGameMsgToGlobalConfig(byte[] payload, int payloadLength)
        {
            try
            {
                using MemoryStream memoryStream = new(payload, 0, payloadLength);
                using BinaryReader reader = new(memoryStream);

                if (reader.ReadByte() != SteamLobbyConstants.LobbyChatMsgMagic1 ||
                    reader.ReadByte() != SteamLobbyConstants.LobbyChatMsgMagic2)
                    throw new Exception("Invalid magic string");

                if (((LobbyChatMsgKind)reader.ReadByte()) != LobbyChatMsgKind.StartGame)
                    throw new Exception("Invalid LobbyChatMsgKind");

                byte playersCount = reader.ReadByte();
                if (playersCount < 2 || playersCount > LobbyUsers.MaxMembers)
                    throw new Exception($"Invalid players count: {playersCount}");

                var playerSteamIds = new CSteamID[playersCount];
                for (int i = 0; i < playerSteamIds.Length; ++i)
                    playerSteamIds[i] = reader.ReadUInt64();

                byte spectatorsCount = reader.ReadByte();
                if (spectatorsCount > LobbyUsers.MaxMembers)
                    throw new Exception($"Invalid players count: {playersCount}");

                var spectatorSteamIds = new CSteamID[spectatorsCount];
                for (int i = 0; i < spectatorSteamIds.Length; ++i)
                    spectatorSteamIds[i] = reader.ReadUInt64();

                var config = GlobalConfig.Instance;

                List<NetcodePlayer> netcodePlayers = new(playersCount + spectatorsCount);

                switch (GlobalConfig.Instance.Mode)
                {
                    case PlayerMode.Player:
                        {
                            int localPlayerIndex = -1;

                            for (int i = 0; i < playersCount; ++i)
                            {
                                var playerId = playerSteamIds[i];
                                SteamNetworkingIdentity playerIdentity = default;
                                playerIdentity.SetSteamID(playerId);

                                if (playerId == config.UserSteamId)
                                {
                                    netcodePlayers.Add(NetcodePlayer.CreateLocal());
                                    localPlayerIndex = i;
                                }
                                else
                                {
                                    netcodePlayers.Add(NetcodePlayer.CreateRemote(
                                      new SteamEndPoint(playerIdentity, GlobalConfig.Instance.LocalPort)));
                                }
                            }

                            if (localPlayerIndex == -1)
                                throw new Exception("No local player id exists in the list");

                            // Local player is responsible for spectators whose index is multiple of local player's index
                            for (int i = localPlayerIndex; i < spectatorsCount; i += playersCount)
                            {
                                var spectatorId = spectatorSteamIds[i];
                                SteamNetworkingIdentity spectatorIdentity = default;
                                spectatorIdentity.SetSteamID(spectatorId);

                                netcodePlayers.Add(NetcodePlayer.CreateSpectator(new SteamEndPoint(spectatorIdentity, GlobalConfig.Instance.LocalPort)));
                            }

                            break;
                        }

                    case PlayerMode.Spectator:
                        {
                            int localSpectatorIndex = Array.IndexOf(spectatorSteamIds, config.UserSteamId);
                            if (localSpectatorIndex == -1)
                                throw new Exception("No local spectator id exists in the list");

                            int hostPlayerIndex = localSpectatorIndex % playersCount;

                            SteamNetworkingIdentity hostIdentity = default;
                            hostIdentity.SetSteamID(playerSteamIds[hostPlayerIndex]);
                            config.SpectateHost = new SteamEndPoint(hostIdentity, GlobalConfig.Instance.LocalPort);

                            break;
                        }

                    default:
                        throw new Exception($"Invalid local PlayerMode.{config.Mode}");
                }

                Peer[] players;
                Peer[] spectators;

                users.FillPlayersAndSpectatorsArrayWithSteamIds(out players, out spectators, playerSteamIds, spectatorSteamIds);

                config.LobbyInfo ??= new() { Name = config.LobbyName };
                config.LobbyInfo.Players = players;
                config.LobbyInfo.Spectators = spectators;

                config.MatchPlayers = netcodePlayers.AsReadOnly();
            }
            catch (Exception ex)
            {
                Log.Warning($"Lobby owner sent an invalid LobbyChatMsg_t: {ex}");
                return false;
            }

            return true;
        }
    }

    void OnLobbyChatUpdate(ref LobbyChatUpdate_t update)
    {
        if (update.SteamIDLobby != LobbyId)
            return;

        if (update.ChatMemberStateChange.HasFlag(EChatMemberStateChange.Entered))
            OnUserJoined(update.SteamIDUserChanged);
        else
            OnUserLeft(update.SteamIDUserChanged);
    }

    void OnPersonaStateChange(ref PersonaStateChange_t change)
    {
        // Skip if not a username change
        if ((change.ChangeFlags & (EPersonaChange.Name | EPersonaChange.NameFirstSet | EPersonaChange.Nickname)) == 0)
            return;

        users.UpdateUsername(change.SteamID);
    }

    /// <summary>
    /// Broadcast the "start game" message to all players and spectators,
    /// if the local user is the lobby owner and all the players are set to ready.
    /// </summary>
    void BroadcastStartGameMsgIfRequired()
    {
        var config = GlobalConfig.Instance;
        var matchmaking = ISteamMatchmaking.User;

        // Skip if the local user is not the lobby owner
        if (config.UserSteamId != matchmaking.GetLobbyOwner(lobbyId))
            return;

        // Skip if not all players are ready
        if (!users.Ready)
            return;

        // Broadcast "start game" message
        byte[] payload = SerializeStartGameMsg();
        matchmaking.SendLobbyChatMsg(lobbyId, payload);

        byte[] SerializeStartGameMsg()
        {
            users.GetPlayersAndSpectatorsArrayFromUsersDict(out Peer[] players, out Peer[] spectators);

            Debug.Assert(players.Length <= byte.MaxValue);
            Debug.Assert(spectators.Length <= byte.MaxValue);

            // Header (3 bytes) + number of players (1 byte) + number of spectators (1 byte) + steam id of each peer
            var payload = new byte[3 + 2 + (8 * (players.Length + spectators.Length))];

            using MemoryStream memoryStream = new(payload);
            using BinaryWriter writer = new(memoryStream);

            writer.Write(SteamLobbyConstants.LobbyChatMsgMagic1);
            writer.Write(SteamLobbyConstants.LobbyChatMsgMagic2);
            writer.Write((byte)LobbyChatMsgKind.StartGame);

            writer.Write((byte)players.Length);
            foreach (var player in players)
                writer.Write(player.PeerId);

            writer.Write((byte)spectators.Length);
            foreach (var spectator in spectators)
                writer.Write(spectator.PeerId);

            writer.Flush();

            return payload;
        }
    }

    void SubscribeLobbyEvents()
    {
        var matchmaking = ISteamMatchmaking.User;
        var friends = ISteamFriends.User;

        matchmaking.LobbyChatUpdate += OnLobbyChatUpdate;
        friends.PersonaStateChange += OnPersonaStateChange;
        matchmaking.LobbyDataUpdate += OnLobbyDataUpdate;
        matchmaking.LobbyChatMsg += OnLobbyChatMsg;
    }

    void UnsubscribeLobbyEvents()
    {
        var matchmaking = ISteamMatchmaking.User;
        var friends = ISteamFriends.User;

        matchmaking.LobbyChatMsg -= OnLobbyChatMsg;
        matchmaking.LobbyDataUpdate -= OnLobbyDataUpdate;
        friends.PersonaStateChange -= OnPersonaStateChange;
        matchmaking.LobbyChatUpdate -= OnLobbyChatUpdate;
    }

    static void InitializeLobbyData(CSteamID lobbyId)
    {
        var matchmaking = ISteamMatchmaking.User;
        var config = GlobalConfig.Instance;

        matchmaking.SetLobbyData(lobbyId, SteamLobbyConstants.GameTitleKey, SteamLobbyConstants.GameTitleValue);
        matchmaking.SetLobbyData(lobbyId, SteamLobbyConstants.LobbyNameKey, config.LobbyName);
    }

    static void FetchLobbyData(CSteamID lobbyId)
    {
        var matchmaking = ISteamMatchmaking.User;
        var config = GlobalConfig.Instance;

        config.LobbyName = matchmaking.GetLobbyData(lobbyId, SteamLobbyConstants.LobbyNameKey);
    }

    static void InitializeLobbyMemberData(CSteamID lobbyId)
    {
        var matchmaking = ISteamMatchmaking.User;
        var config = GlobalConfig.Instance;

        Debug.Assert(config.Mode != PlayerMode.Unknown);

        matchmaking.SetLobbyMemberData(lobbyId, SteamLobbyConstants.SpectatorKey, config.Mode == PlayerMode.Spectator ? "1" : "0");
        matchmaking.SetLobbyMemberData(lobbyId, SteamLobbyConstants.ReadyKey, "0");
    }
}
