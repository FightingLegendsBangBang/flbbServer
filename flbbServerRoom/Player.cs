using System;
using LiteNetLib;
using LiteNetLib.Utils;

namespace flbbServer
{
    public class Player
    {
        public NetPeer peer;
        public int playerId;
        public bool isHost;
        public string playerName;

        public Player(NetPeer peer, int playerId, bool isHost, string playerName)
        {
            this.peer = peer;
            this.playerId = playerId;
            this.isHost = isHost;
            this.playerName = playerName;
        }

        public void SendNewPlayerData(NetDataWriter writer)
        {
            writer.Put((ushort) 2);
            writer.Put(peer.Id);
            writer.Put(playerId);
            writer.Put(playerName);
            writer.Put(isHost);
        }
    }
}