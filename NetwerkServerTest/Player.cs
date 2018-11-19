using System;
using LiteNetLib;
using LiteNetLib.Utils;

namespace NetwerkServerTest
{
    public class Player
    {
        public NetPeer peer;
        public bool isHost;
        public string playerName;
        public float posX = 0;
        public float posY = 0;
        public float posZ = 0;

        public Player(NetPeer peer, bool isHost, string playerName, float posX, float posY, float posZ)
        {
            this.peer = peer;
            this.isHost = isHost;
            this.playerName = playerName;
            this.posX = posX;
            this.posY = posY;
            this.posZ = posZ;
        }

        public void ReadPlayerData(NetDataReader reader)
        {
            var i = reader.GetInt();
            posX = reader.GetFloat();
            posY = reader.GetFloat();
            posZ = reader.GetFloat();
        }

        public void WritePlayerData(NetDataWriter writer)
        {
            writer.Put(peer.Id);
            writer.Put(posX);
            writer.Put(posY);
            writer.Put(posZ);
        }
    }
}