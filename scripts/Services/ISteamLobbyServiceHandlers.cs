namespace SpaceWar.Services;

using SpaceWar.Models;

public interface ISteamLobbyServiceHandlers
{
    void OnUserListUpdated(Peer[] players, Peer[] spectators);

    void OnStartGameMsgReceived();
}
