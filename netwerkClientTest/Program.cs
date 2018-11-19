using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using LiteNetLib;
using LiteNetLib.Ntp;

namespace netwerkClientTest
{
    internal class Program
    {
        public static void Main(string[] args)
        {
            new ClientTest().Run();
        }
    }
}