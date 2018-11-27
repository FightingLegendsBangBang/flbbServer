using System;
using System.Collections.Generic;
using System.Threading;
using LiteNetLib;
using LiteNetLib.Utils;

namespace flbbServerDotNet
{
    class Program
    {
        public static Dictionary<int, Player> Players = new Dictionary<int, Player>();
        public static Dictionary<int, NetworkObject> NetworkObjects = new Dictionary<int, NetworkObject>();
        private static EventBasedNetListener listener;
        private static NetManager server;

        private static Random rand = new Random();

        private static bool quit = false;

        public static void Main(string[] args)
        {
            Console.WriteLine("starting server");

            listener = new EventBasedNetListener();
            server = new NetManager(listener);

            server.Start(9050);

            listener.ConnectionRequestEvent += OnListenerOnConnectionRequestEvent;
            listener.PeerDisconnectedEvent += OnListenerOnPeerDisconnectedEvent;
            listener.PeerConnectedEvent += OnListenerOnPeerConnectedEvent;
            listener.NetworkReceiveEvent += OnListenerOnNetworkReceiveEvent;

            Console.WriteLine(Environment.ProcessorCount);

            while (!quit)
            {
                server.PollEvents();
                Thread.Sleep(20);
            }

            server.Stop();
        }

        private static void OnListenerOnPeerConnectedEvent(NetPeer peer)
        {
            Console.WriteLine("We got connection: {0}", peer.EndPoint);
            NetDataWriter writer = new NetDataWriter();
            writer.Put((ushort) 1);
            writer.Put(peer.Id);

            int newPlayerId;
            do
            {
                newPlayerId = rand.Next(1000000, 9999999);
            } while (Players.ContainsKey(newPlayerId));

            writer.Put(newPlayerId);
            writer.Put(Players.Count == 0);
            peer.Send(writer, DeliveryMethod.ReliableOrdered);
            writer.Reset();
            foreach (var player in Players)
            {
                writer.Reset();

                player.Value.SendNewPlayerData(writer);
                peer.Send(writer, DeliveryMethod.ReliableOrdered);
            }

            foreach (var networkObject in NetworkObjects)
            {
                writer.Reset();
                networkObject.Value.SendObjectData(writer);
                peer.Send(writer, DeliveryMethod.ReliableOrdered);
            }
        }

        private static void OnListenerOnPeerDisconnectedEvent(NetPeer peer, DisconnectInfo info)
        {
            Console.WriteLine("peer disconnected: " + peer.EndPoint);

            List<int> playersToRemove = new List<int>();
            List<int> objectsToRemove = new List<int>();
            foreach (var player in Players)
            {
                if (player.Value.peer == peer)
                {
                    playersToRemove.Add(player.Key);
                    foreach (var networkObject in NetworkObjects)
                    {
                        if (networkObject.Value.playerId == player.Key)
                        {
                            objectsToRemove.Add(networkObject.Key);
                        }
                    }
                }
            }

            foreach (var i in playersToRemove)
            {
                Players.Remove(i);
            }

            foreach (var i in objectsToRemove)
            {
                NetworkObjects.Remove(i);
            }


            NetDataWriter writer = new NetDataWriter();
            writer.Put((ushort) 3);
            writer.Put(peer.Id);
            SendOthers(peer, writer, DeliveryMethod.ReliableOrdered);
        }

        private static void OnListenerOnConnectionRequestEvent(ConnectionRequest request)
        {
            if (server.PeersCount < 1000 /* max connections */)
                request.AcceptIfKey("SomeConnectionKey");
            else
                request.Reject();
        }

        private static void OnListenerOnNetworkReceiveEvent(NetPeer fromPeer, NetPacketReader dataReader,
            DeliveryMethod deliveryMethod)
        {
            ushort msgid = dataReader.GetUShort();

            NetDataWriter writer = new NetDataWriter();

            switch (msgid)
            {
                case 1:
                    var playerId = dataReader.GetInt();
                    var pName = dataReader.GetString();
                    var isHost = dataReader.GetBool();
                    var charId = dataReader.GetInt();
                    var pColorR = dataReader.GetFloat();
                    var pColorG = dataReader.GetFloat();
                    var pColorB = dataReader.GetFloat();
                    var player = new Player(fromPeer, playerId, isHost, pName, charId, pColorR, pColorG, pColorB);
                    Players.Add(playerId, player);
                    player.SendNewPlayerData(writer);
                    SendOthers(fromPeer, writer, DeliveryMethod.ReliableOrdered);
                    Console.WriteLine("registering player " + pName + " pid " + playerId);

                    break;
                case 4:
                    var playerUpdateId = dataReader.GetInt();
                    Players[playerUpdateId].ReadPlayerUpdate(dataReader);
                    Players[playerUpdateId].SendNewPlayerUpdate(writer);
                    server.SendToAll(writer, DeliveryMethod.ReliableOrdered);
                    break;
                case 101: //create networkObject

                    int objectId;

                    do
                    {
                        objectId = rand.Next(1000000, 9999999);
                    } while (NetworkObjects.ContainsKey(objectId));

                    var netObj = new NetworkObject(
                        fromPeer,
                        dataReader.GetInt(),
                        objectId,
                        dataReader.GetInt(),
                        dataReader.GetFloat(),
                        dataReader.GetFloat(),
                        dataReader.GetFloat(),
                        dataReader.GetFloat(),
                        dataReader.GetFloat(),
                        dataReader.GetFloat(),
                        dataReader.GetFloat());
                    NetworkObjects.Add(objectId, netObj);

                    netObj.SendObjectData(writer);
                    server.SendToAll(writer, DeliveryMethod.ReliableOrdered);
                    break;

                case 102:
                    var objectToDelete = dataReader.GetInt();
                    NetworkObjects.Remove(objectToDelete);

                    writer.Put((ushort) 102);
                    writer.Put(objectToDelete);
                    server.SendToAll(writer, DeliveryMethod.ReliableOrdered);
                    break;
                case 103:
                    var objectToUpdate = dataReader.GetInt();
                    if (NetworkObjects.ContainsKey(objectToUpdate))
                    {
                        NetworkObjects[objectToUpdate].ReadData(dataReader);
                        NetworkObjects[objectToUpdate].WriteData(writer);
                        SendOthers(fromPeer, writer, DeliveryMethod.Unreliable);
                    }

                    break;
                case 201:
                    byte[] data = new byte[dataReader.UserDataSize];
                    Array.Copy(dataReader.RawData, dataReader.UserDataOffset, data, 0, dataReader.UserDataSize);
                    var target = dataReader.GetByte();
                    var rpcName = dataReader.GetString();
                    var rpcObjectId = dataReader.GetInt();
                    switch (target)
                    {
                        case 0:
                            NetworkObjects[rpcObjectId].peer.Send(data, DeliveryMethod.ReliableUnordered);
                            break;
                        case 1:
                            SendOthers(NetworkObjects[rpcObjectId].peer, data, DeliveryMethod.ReliableUnordered);
                            break;
                        case 2:
                            server.SendToAll(data, DeliveryMethod.ReliableUnordered);
                            break;
                    }

                    break;

                case 301:
                    var levelId = dataReader.GetInt();
                    writer.Put((ushort) 301);
                    writer.Put(levelId);

                    server.SendToAll(writer, DeliveryMethod.ReliableOrdered);
                    break;
            }

            dataReader.Recycle();
        }

        private static void SendOthers(NetPeer cpeer, NetDataWriter writer, DeliveryMethod dm)
        {
            foreach (NetPeer netPeer in server.ConnectedPeerList)
            {
                if (cpeer == netPeer) continue;

                netPeer.Send(writer, dm);
            }
        }

        private static void SendOthers(NetPeer cpeer, byte[] data, DeliveryMethod dm)
        {
            foreach (NetPeer netPeer in server.ConnectedPeerList)
            {
                if (cpeer == netPeer) continue;

                netPeer.Send(data, dm);
            }
        }
    }
}