using System;
using System.Collections.Generic;
using System.Threading;
using LiteNetLib;
using LiteNetLib.Utils;

namespace flbbServer
{
    internal class Program
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

            while (!quit)
            {
                server.PollEvents();
                Thread.Sleep(10);
            }

            server.Stop();
        }

        private static void OnListenerOnPeerConnectedEvent(NetPeer peer)
        {
            Console.WriteLine("We got connection: {0}", peer.EndPoint);
            NetDataWriter writer = new NetDataWriter();
            writer.Put((ushort) 1);
            writer.Put(peer.Id);
            writer.Put(rand.Next(1000000, 9999999));
            writer.Put(peer == server.FirstPeer);
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
                    int playerId = dataReader.GetInt();
                    string pName = dataReader.GetString();
                    bool isHost = dataReader.GetBool();
                    var player = new Player(fromPeer, playerId, isHost, pName);
                    Players.Add(playerId, player);
                    player.SendNewPlayerData(writer);
                    SendOthers(fromPeer, writer, DeliveryMethod.ReliableOrdered);
                    Console.WriteLine("registering player " + pName + " pid " + playerId);

                    break;
                case 101: //create networkObject
                    var objectType = dataReader.GetInt();
                    var objectId = rand.Next(1000000, 9999999);
                    var oplayerId = dataReader.GetInt();
                    var oposX = dataReader.GetFloat();
                    var oposY = dataReader.GetFloat();
                    var oposZ = dataReader.GetFloat();
                    var orotX = dataReader.GetFloat();
                    var orotY = dataReader.GetFloat();
                    var orotZ = dataReader.GetFloat();
                    var orotW = dataReader.GetFloat();

                    var netObj = new NetworkObject(
                        fromPeer,
                        oplayerId,
                        objectId,
                        objectType,
                        oposX,
                        oposY,
                        oposZ,
                        orotX,
                        orotY,
                        orotZ,
                        orotW);
                    NetworkObjects.Add(objectId, netObj);

                    netObj.SendObjectData(writer);
                    server.SendToAll(writer, DeliveryMethod.ReliableOrdered);
                    break;

                case 102:
                    var dobjectId = dataReader.GetInt();
                    NetworkObjects.Remove(dobjectId);

                    writer.Put((ushort) 102);
                    writer.Put(dobjectId);
                    server.SendToAll(writer, DeliveryMethod.ReliableOrdered);
                    break;
                case 103:
                    var cobjectId = dataReader.GetInt();
                    if (NetworkObjects.ContainsKey(cobjectId))
                    {
                        NetworkObjects[cobjectId].ReadData(dataReader);
                        NetworkObjects[cobjectId].WriteData(writer);
                        SendOthers(fromPeer, writer, DeliveryMethod.Unreliable);
                    }

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
    }
}