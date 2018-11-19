using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using LiteNetLib;

namespace netwerkTest
{
    class WaitPeer
    {
        public IPEndPoint InternalAddr { get; private set; }
        public IPEndPoint ExternalAddr { get; private set; }
        public DateTime RefreshTime { get; private set; }

        public void Refresh()
        {
            RefreshTime = DateTime.Now;
        }

        public WaitPeer(IPEndPoint internalAddr, IPEndPoint externalAddr)
        {
            Refresh();
            InternalAddr = internalAddr;
            ExternalAddr = externalAddr;
        }
    }

    class HolePunchServerTest : INatPunchListener
    {
        private const int ServerPort = 50010;
        private const string ConnectionKey = "test_key";
        private static readonly TimeSpan KickTime = new TimeSpan(0, 0, 10);

        private readonly Dictionary<string, WaitPeer> _waitingPeers = new Dictionary<string, WaitPeer>();
        private readonly Dictionary<string, WaitPeer> _hosts = new Dictionary<string, WaitPeer>();
        private readonly List<string> _peersToRemove = new List<string>();
        private NetManager _puncher;

        void INatPunchListener.OnNatIntroductionRequest(IPEndPoint localEndPoint, IPEndPoint remoteEndPoint,
            string token)
        {
            string tok = token;
            Console.WriteLine(tok);
            Console.WriteLine(tok.Length);
            if (tok.StartsWith("host"))
            {
                tok = tok.Substring(4, tok.Length-4);
                Console.WriteLine(tok);
                _hosts[tok] = new WaitPeer(localEndPoint, remoteEndPoint);
            }
            else
            {


                WaitPeer wpeer;
                if (_hosts.TryGetValue(token, out wpeer))
                {
                    Console.WriteLine("L " + localEndPoint + " r " + remoteEndPoint);

                    if (wpeer.InternalAddr.Equals(localEndPoint) &&
                        wpeer.ExternalAddr.Equals(remoteEndPoint))
                    {
                        wpeer.Refresh();
                        return;
                    }

                    Console.WriteLine("Wait peer found, sending introduction...");

                    //found in list - introduce client and host to eachother
                    Console.WriteLine(
                        "host - i({0}) e({1})\nclient - i({2}) e({3})",
                        wpeer.InternalAddr,
                        wpeer.ExternalAddr,
                        localEndPoint,
                        remoteEndPoint);

                    _puncher.NatPunchModule.NatIntroduce(
                        wpeer.InternalAddr, // host internal
                        wpeer.ExternalAddr, // host external
                        localEndPoint, // client internal
                        remoteEndPoint, // client external
                        token // request token
                    );

                    //Clear dictionary
                    _waitingPeers.Remove(token);
                }
                else
                {
                    Console.WriteLine("Wait peer created. i({0}) e({1})", localEndPoint, remoteEndPoint);
                    _waitingPeers[token] = new WaitPeer(localEndPoint, remoteEndPoint);
                }
            }
        }

        void INatPunchListener.OnNatIntroductionSuccess(IPEndPoint targetEndPoint, string token)
        {
            //Ignore we are server
        }

        public void Run()
        {
            Console.WriteLine("=== HolePunch Test ===");

            EventBasedNetListener netListener = new EventBasedNetListener();

            netListener.PeerConnectedEvent += peer =>
            {
                Console.WriteLine("PeerConnected: " + peer.EndPoint.ToString());
            };

            netListener.ConnectionRequestEvent += request => { request.AcceptIfKey(ConnectionKey); };

            netListener.PeerDisconnectedEvent += (peer, disconnectInfo) =>
            {
                Console.WriteLine("PeerDisconnected: " + disconnectInfo.Reason);
                if (disconnectInfo.AdditionalData.AvailableBytes > 0)
                {
                    Console.WriteLine("Disconnect data: " + disconnectInfo.AdditionalData.GetInt());
                }
            };


            _puncher = new NetManager(netListener);
            _puncher.Start(ServerPort);
            _puncher.NatPunchEnabled = true;
            _puncher.NatPunchModule.Init(this);

            // keep going until ESCAPE is pressed
            Console.WriteLine("Press ESC to quit");

            while (true)
            {
                if (Console.KeyAvailable)
                {
                    var key = Console.ReadKey(true).Key;
                    if (key == ConsoleKey.Escape)
                    {
                        break;
                    }

                    if (key == ConsoleKey.A)
                    {
                        Console.WriteLine("C1 stopped");
                    }
                }

                DateTime nowTime = DateTime.Now;

                _puncher.NatPunchModule.PollEvents();

                //check old peers
                foreach (var waitPeer in _waitingPeers)
                {
                    if (nowTime - waitPeer.Value.RefreshTime > KickTime)
                    {
                        _peersToRemove.Add(waitPeer.Key);
                    }
                }

                //remove
                for (int i = 0; i < _peersToRemove.Count; i++)
                {
                    Console.WriteLine("Kicking peer: " + _peersToRemove[i]);
                    _waitingPeers.Remove(_peersToRemove[i]);
                }

                _peersToRemove.Clear();

                Thread.Sleep(5);
            }

            _puncher.Stop();
        }
    }
}