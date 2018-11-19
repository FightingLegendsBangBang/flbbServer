using System;
using System.Collections.Generic;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading;
using LiteNetLib;
using LiteNetLib.Ntp;
using LiteNetLib.Utils;

namespace netwerkClientTest
{
    public class ClientTest
    {
        public void Run()
        {
            EventBasedNetListener listener = new EventBasedNetListener();
            NetManager client = new NetManager(listener);
            client.Start();
            client.Connect("localhost" /* host ip or name */, 9050 /* port */,
                "SomeConnectionKey" /* text key or NetDataWriter */);

            listener.NetworkReceiveEvent += (fromPeer, dataReader, deliveryMethod) =>
            {
                //Console.WriteLine("We got: {0}", dataReader.GetString(100 /* max length of string */));
                dataReader.Recycle();
            };


            bool quit = false;
            Random rand = new Random();

            while (!quit)
            {
                if (Console.KeyAvailable)
                {
                    var key = Console.ReadKey(true).Key;
                    switch (key)
                    {
                        case ConsoleKey.A:
                            Console.WriteLine(client.PeersCount);
                            var msg = "cl sending message: " + rand.Next(1000, 9999);
                            Console.WriteLine(msg);

                            NetDataWriter writer = new NetDataWriter(); // Create writer class
                            writer.Put((ushort) 1);
                            writer.Put(msg); // Put some string
                            client.FirstPeer.Send(writer, DeliveryMethod.ReliableOrdered); // Send with reliability


                            break;
                        case ConsoleKey.P:
                            Console.WriteLine(client.FirstPeer.Ping);
                            break;
                        case ConsoleKey.Q:
                            quit = true;
                            break;
                    }
                }


                NetDataWriter writer2 = new NetDataWriter();
                writer2.Put((ushort) 101);
                writer2.Put(40f);
                writer2.Put(20f);
                writer2.Put(50f);
                client.FirstPeer.Send(writer2, DeliveryMethod.Unreliable);
                
                client.PollEvents();
                Thread.Sleep(15);
            }

            client.Stop();
        }
    }
}