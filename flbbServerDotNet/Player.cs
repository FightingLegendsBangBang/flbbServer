using System;
using LiteNetLib;
using LiteNetLib.Utils;

namespace flbbServerDotNet
{
    public class Player
    {
        public NetPeer peer;
        public int playerId;
        public bool isHost;
        public string playerName;
        public int characterId;
        public float colorR;
        public float colorG;
        public float colorB;


        public Player(NetPeer peer, int playerId, bool isHost, string playerName, int characterId, float colorR,
            float colorG, float colorB)
        {
            this.peer = peer;
            this.playerId = playerId;
            this.isHost = isHost;
            this.playerName = playerName;
            this.characterId = characterId;
            this.colorR = colorR;
            this.colorG = colorG;
            this.colorB = colorB;
        }

        public void SendNewPlayerData(NetDataWriter writer)
        {
            writer.Put((ushort) 2);
            writer.Put(peer.Id);
            writer.Put(playerId);
            writer.Put(playerName);
            writer.Put(isHost);
            writer.Put(characterId);
            writer.Put(colorR);
            writer.Put(colorG);
            writer.Put(colorB);
        }

        public void ReadPlayerUpdate(NetDataReader reader)
        {
            isHost = reader.GetBool();
            playerName = reader.GetString();
            characterId = reader.GetInt();
            colorR = reader.GetFloat();
            colorG = reader.GetFloat();
            colorB = reader.GetFloat();
        }

        public void SendNewPlayerUpdate(NetDataWriter writer)
        {
            writer.Put((ushort) 4);
            writer.Put(playerId);
            writer.Put(isHost);
            writer.Put(playerName);
            writer.Put(characterId);
            writer.Put(colorR);
            writer.Put(colorG);
            writer.Put(colorB);
        }
    }
}