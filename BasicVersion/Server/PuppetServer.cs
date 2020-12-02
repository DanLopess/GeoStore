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
        private List<ServerMapping> serverMappings;
        private List<ClientMapping> clientMappings;
        private List<PartitionMapping> partitionMappings;
        private MainServerService server;

        public PuppetServer(MainServerService server)
        {
            this.server = server;
        }

        public override Task<SendMappingsReply> SendMappings(SendMappingsRequest request, ServerCallContext context)
        {
            return Task.FromResult(sendMappings(request));
        }
        public SendMappingsReply sendMappings(SendMappingsRequest request)
        {
            server.SetDelay();
            Console.WriteLine("\nReceived Mappings:\n");
            serverMappings = request.ServerMapping.ToList();
            partitionMappings = request.PartitionMapping.ToList();

            server.SetDataCenter(getDataCenter());
            server.SetServerList(getServerList());
            server.printMappings();

            return new SendMappingsReply { Ok = true };
        }

        public override Task<GetNodeStatusReply> GetStatus(GetNodeStatusRequest request, ServerCallContext context)
        {
            return Task.FromResult(getStatus());
        }
        public GetNodeStatusReply getStatus()
        {
            server.SetDelay();
            if (server.freeze){
                Console.WriteLine("Server Status: Freeze");
            }
            else if (!server.freeze){
                Console.WriteLine("Server Status: Unfreeze");
            }
            
            return new GetNodeStatusReply { Ok = true, Response = "Server running" };
        }

        public override Task<ChangeServerStateReply> ChangeServerState(ChangeServerStateRequest request, ServerCallContext context)
        {
            return Task.FromResult(changeServerState(request));
        }
        public ChangeServerStateReply changeServerState(ChangeServerStateRequest request)
        {
            server.SetDelay();
            Console.WriteLine(request.State);
            if (request.State == ServerState.Freeze){
                server.freeze = true;
            }
            else if (request.State == ServerState.Unfreeze){
                server.freeze = false;
            }
            else if (request.State == ServerState.Crash){
                server.crash = true;
            }
            return new ChangeServerStateReply { Ok = true };
        }

        public Dictionary<string, string> getServerList()
        {
            Dictionary<string, string> ServerList = new Dictionary<string, string>();
            for (int i = 0; i < serverMappings.Count; i++)
            {
                ServerList[serverMappings[i].ServerId] = serverMappings[i].Url;
            }
            return ServerList;
        }

        public Dictionary<string, List<string>> getDataCenter()
        {
            Dictionary<string, List<string>> DataCenter = new Dictionary<string, List<string>>();
            for (int i = 0; i < partitionMappings.Count; i++)
            {
                DataCenter[partitionMappings[i].PartitionId] = partitionMappings[i].ServerId.ToList();
            }
            return DataCenter;
        }

        public Dictionary<string, string> getClientList()
        {
            Dictionary<string, string> ClientList = new Dictionary<string, string>();
            for (int i = 0; i < clientMappings.Count; i++)
            {
                ClientList[clientMappings[i].Username] = clientMappings[i].Url;
            }
            return ClientList;
        }
    }
}
