using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Grpc.Core;
using Lnrpc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Routerrpc;
using ServiceStack;
using ServiceStack.Text;
using LNDInsights.Outputs;
using LNDInsights.LND;

namespace LNDInsights
{
    class Program
    {
      
        async static Task Main(string[] args)
        {

            if (args.Length < 3)
            {
                Console.WriteLine("Valid usage: LNDInsights <path to tls.cert> <path to macaroon> <telegram API key>");
                return;
            }
            await TelegramBot.Start(args[2]);
            LNDNodeConnection.Start(args[0], args[1]);
            var htlcLoop = LNDNodeConnection.RunHTLCLoop();
            Console.WriteLine("Press ANY key to stop process");
            Console.ReadKey();
            
            await LNDNodeConnection.Stop();
            await TelegramBot.Stop();
            
        }

        private static object GetNodeName(ulong incomingChannelId)
        {
            throw new NotImplementedException();
        }
    }

}
