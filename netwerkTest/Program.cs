using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using LiteNetLib;
using LiteNetLib.Ntp;

namespace netwerkTest
{
    
    internal class Program
    {
       

        
        public static void Main(string[] args)
        {
            NtpRequest ntpRequest = null;
            ntpRequest = NtpRequest.Create("pool.ntp.org", ntpPacket =>
            {
                ntpRequest.Close();
                if (ntpPacket != null)
                    Console.WriteLine("[MAIN] NTP time test offset: " + ntpPacket.CorrectionOffset);
                else
                    Console.WriteLine("[MAIN] NTP time error");
            });
            ntpRequest.Send();
            
            
            new HolePunchServerTest().Run();
            
            Console.WriteLine("hello world");
            Console.ReadKey();
        }
    }
}