using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Drawing;
using System.Linq;
using System.Threading;
using Grpc.Core;
using Grpc.Net.Client;
using System.Threading.Tasks;

namespace MainServer
{
    public class PuppetServer : PuppetService.PuppetServiceBase
    {
        private List<ServerMapping> ServerMappings;
        private List<ClientMapping> ClientMappings;
        private List<PartitionMapping> PartitionMappings;
        private MainServerService Server;

        public PuppetServer(MainServerService server)
        {
            this.Server = server;
        }

        public override Task<SendMappingsReply> SendMappings(SendMappingsRequest request, ServerCallContext context)
        {
            return Task.FromResult(sendMappings(request));
        }
        public SendMappingsReply sendMappings(SendMappingsRequest request)
        {
            Server.SetDelay();
            Console.WriteLine("\nReceived Mappings:\n");
            ServerMappings = request.ServerMapping.ToList();
            PartitionMappings = request.PartitionMapping.ToList();

            Server.SetDataCenter(getDataCenter());
            Server.SetServerList(getServerList());
            Server.PrintMappings();

            return new SendMappingsReply { Ok = true };
        }

        public override Task<GetNodeStatusReply> GetStatus(GetNodeStatusRequest request, ServerCallContext context)
        {
            return Task.FromResult(getStatus());
        }
        public GetNodeStatusReply getStatus()
        {
            Server.SetDelay();
            Console.WriteLine("============ STATUS ==========\n");
            if (Server.Freeze){
                Console.WriteLine("Server Status: Freeze");
            }
            else if (!Server.Freeze){
                Console.WriteLine("Server Status: Normal");
            }
            Console.WriteLine("Mappings:\n");
            Server.PrintMappings();
            Console.WriteLine("==============================");

            return new GetNodeStatusReply { Ok = true, Response = "Server running" };
        }

        public override Task<ChangeServerStateReply> ChangeServerState(ChangeServerStateRequest request, ServerCallContext context)
        {
            return Task.FromResult(changeServerState(request));
        }
        public ChangeServerStateReply changeServerState(ChangeServerStateRequest request)
        {
            Server.SetDelay();
            Console.WriteLine("Changed server state to: " + request.State);
            if (request.State == ServerState.Freeze){
                Server.Freeze = true;
            }
            else if (request.State == ServerState.Unfreeze){
                Server.Freeze = false;
            }
            else if (request.State == ServerState.Crash){
                Server.Crash = true;
            }
            return new ChangeServerStateReply { Ok = true };
        }

        public Dictionary<string, string> getServerList()
        {
            Dictionary<string, string> ServerList = new Dictionary<string, string>();
            for (int i = 0; i < ServerMappings.Count; i++)
            {
                ServerList[ServerMappings[i].ServerId] = ServerMappings[i].Url;
            }
            return ServerList;
        }

        public Dictionary<string, List<string>> getDataCenter()
        {
            Dictionary<string, List<string>> DataCenter = new Dictionary<string, List<string>>();
            for (int i = 0; i < PartitionMappings.Count; i++)
            {
                DataCenter[PartitionMappings[i].PartitionId] = PartitionMappings[i].ServerId.ToList();
            }
            return DataCenter;
        }

        public Dictionary<string, string> getClientList()
        {
            Dictionary<string, string> ClientList = new Dictionary<string, string>();
            for (int i = 0; i < ClientMappings.Count; i++)
            {
                ClientList[ClientMappings[i].Username] = ClientMappings[i].Url;
            }
            return ClientList;
        }
    }
}
