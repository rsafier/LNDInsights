using Grpc.Core;
using LNDInsights.Outputs;
using Lnrpc;
using Routerrpc;
using ServiceStack.Text;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace LNDInsights.LND
{
    public static class LNDNodeConnection
    {
        private static Grpc.Core.Channel gRPCChannel;
        private static Lightning.LightningClient LightningClient;
        private static string LocalNodePubKey;
        private static string LocalAlias;
        public static void Start(string tlsCertFilePath, string macoroonFilePath)
        {
            // Due to updated ECDSA generated tls.cert we need to let gprc know that
            // we need to use that cipher suite otherwise there will be a handshake
            // error when we communicate with the lnd rpc server.
            System.Environment.SetEnvironmentVariable("GRPC_SSL_CIPHER_SUITES", "HIGH+ECDSA");
            string connectionString = Environment.GetEnvironmentVariable("AZURE_STORAGE_CONNECTION_STRING");

            var cert = System.IO.File.ReadAllText(tlsCertFilePath);
            var sslCreds = new SslCredentials(cert);

            byte[] macaroonBytes = System.IO.File.ReadAllBytes(macoroonFilePath);
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
            gRPCChannel = new Grpc.Core.Channel("localhost:10009", combinedCreds);

            LightningClient = new Lnrpc.Lightning.LightningClient(gRPCChannel);
            var nodeInfo = LightningClient.GetInfo(new GetInfoRequest());
            LocalNodePubKey = nodeInfo.IdentityPubkey;
            LocalAlias = nodeInfo.Alias;

        }

        public static async Task Stop()
        {   
            await gRPCChannel.ShutdownAsync();
        }

        public static async Task RunHTLCLoop()
        {
            var client = new Lnrpc.Lightning.LightningClient(gRPCChannel);
            var info = await client.GetInfoAsync(new GetInfoRequest());  //Get node info

            var htlcEventTask = Task.Run(async () =>
            {
                var routerClient = new Routerrpc.Router.RouterClient(gRPCChannel);
                using (var htlcEventStream = routerClient.SubscribeHtlcEvents(new SubscribeHtlcEventsRequest()))
                {
                    while (await htlcEventStream.ResponseStream.MoveNext())
                    {
                        var htlcEvent = htlcEventStream.ResponseStream.Current; 
                        var incomingNodeName = await GetFriendlyNodeNameFromChannel(htlcEvent.IncomingChannelId);
                        var outgoingNodeName = await GetFriendlyNodeNameFromChannel(htlcEvent.OutgoingChannelId);
                        await TelegramBot.SendTextMessage($"{incomingNodeName} -> {outgoingNodeName} \r\n" + htlcEvent.Dump());
                    }
                }
            });

            await htlcEventTask;
        }

        private static async Task<string> GetFriendlyNodeNameFromChannel(ulong channelId)
        {
            if (channelId == 0)
                return string.Empty;
            var channel = await LightningClient.GetChanInfoAsync(new ChanInfoRequest { ChanId = channelId });
            var node = await LightningClient.GetNodeInfoAsync(new NodeInfoRequest { PubKey = GetRemoteNodePubKeyFromChannel(channel) });
            return node.Node.Alias;
        }

        private static string GetRemoteNodePubKeyFromChannel(ChannelEdge edge)
        {
            return edge.Node1Pub == LocalNodePubKey ? edge.Node2Pub : edge.Node1Pub;
        }
    }
}
