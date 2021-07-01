using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Grpc.Core;
using Lnrpc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Routerrpc;
using ServiceStack.Text; 


namespace LNDInsights
{
    class Program
    {
        async static Task Main(string[] args)
        {
        
            

            if (args.Length < 2)
            {
                Console.WriteLine("Valid usage: LNDInsights <path to tls.cert> <path to macaroon>");
                return;
            }
             
            // Due to updated ECDSA generated tls.cert we need to let gprc know that
            // we need to use that cipher suite otherwise there will be a handshake
            // error when we communicate with the lnd rpc server.
            System.Environment.SetEnvironmentVariable("GRPC_SSL_CIPHER_SUITES", "HIGH+ECDSA");
            string connectionString = Environment.GetEnvironmentVariable("AZURE_STORAGE_CONNECTION_STRING");

            var cert = File.ReadAllText(args[0]);
            var sslCreds = new SslCredentials(cert);

            byte[] macaroonBytes = File.ReadAllBytes(args[1]);
            var macaroon = BitConverter.ToString(macaroonBytes).Replace("-", ""); // hex format stripped of "-" chars
            

            // combine the cert credentials and the macaroon auth credentials using interceptors
            // so every call is properly encrypted and authenticated
            Task AddMacaroon(AuthInterceptorContext context, Metadata metadata)
            {
                metadata.Add(new Metadata.Entry("macaroon", macaroon));
                return Task.CompletedTask;
            }
            var macaroonInterceptor = new AsyncAuthInterceptor(AddMacaroon);
            var combinedCreds = ChannelCredentials.Create(sslCreds, CallCredentials.FromInterceptor(macaroonInterceptor));

            // finally pass in the combined credentials when creating a channel
            var channel = new Grpc.Core.Channel("localhost:10009", combinedCreds);
            //var client = new Lnrpc.Lightning.LightningClient(channel);
           // var info = await client.GetInfoAsync(new GetInfoRequest());  //Get node info

            var client2 = new Routerrpc.Router.RouterClient(channel);

            using (var call2 = client2.SubscribeHtlcEvents(new SubscribeHtlcEventsRequest()))
            {
                while (await call2.ResponseStream.MoveNext())
                {
                    Console.Write(call2.ToString());
                }
            }

            //using (var call = client.SubscribeChannelBackups(new ChannelBackupSubscription()))
            //{
            //    while (await call.ResponseStream.MoveNext())
            //    {

            //    }
            //}

            await channel.ShutdownAsync();
        }
    }

}
