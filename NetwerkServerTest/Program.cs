using System;
using System.Collections.Generic;
using System.Threading;
using LiteNetLib;
using LiteNetLib.Utils;

namespace NetwerkServerTest
{
    internal class Program
    {
        public static Dictionary<int, Player> Players = new Dictionary<int, Player>();

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
            writer.Put(peer == server.FirstPeer);
            peer.Send(writer, DeliveryMethod.ReliableOrdered);
            writer.Reset();
            foreach (var player in Players)
            {
                writer.Reset();
                writer.Put((ushort) 2);
                writer.Put(player.Key);
                writer.Put(player.Value.playerName);
                writer.Put(player.Value.isHost);
                writer.Put(player.Value.posX);
                writer.Put(player.Value.posY);
                writer.Put(player.Value.posZ);
                peer.Send(writer, DeliveryMethod.ReliableOrdered);
            }
        }

        private static void OnListenerOnPeerDisconnectedEvent(NetPeer peer, DisconnectInfo info)
        {
            Console.WriteLine("peer disconnected: " + peer.EndPoint);

            Players.Remove(peer.Id);

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
                    string pName = dataReader.GetString();
                    bool isHost = dataReader.GetBool();
                    Players.Add(fromPeer.Id,
                        new Player(fromPeer,
                            isHost,
                            pName,
                            0,
                            0,
                            0
                        ));
                    writer.Put((ushort) 2);
                    writer.Put(fromPeer.Id);
                    writer.Put(pName);
                    writer.Put(isHost);
                    writer.Put(0);
                    writer.Put(0);
                    writer.Put(0);
                    SendOthers(fromPeer, writer, DeliveryMethod.ReliableOrdered);
                    Console.WriteLine("registering player " + pName);

                    break;
                case 101: //player update
                    writer.Put((ushort) 101);
                    Players[fromPeer.Id].ReadPlayerData(dataReader);
                    Players[fromPeer.Id].WritePlayerData(writer);
                    SendOthers(fromPeer, writer, DeliveryMethod.Unreliable);
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