using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Grpc.Core;
using System.Threading.Tasks;

namespace Clients
{
    public class PuppetClient : PuppetService.PuppetServiceBase
    {
        private List<ServerMapping> serverMappings;
        private List<ClientMapping> clientMappings;
        private List<PartitionMapping> partitionMappings;
        private Client client;
        public bool hasReceivedMappings { get; set; }
        public PuppetClient(Client client)
        {
            hasReceivedMappings = false;
            this.client = client;
        }

        public override Task<SendMappingsReply> SendMappings(SendMappingsRequest request, ServerCallContext context)
        {
            return Task.FromResult(sendMappings(request));
        }
        public SendMappingsReply sendMappings(SendMappingsRequest request)
        {

            this.serverMappings = request.ServerMapping.ToList();
            this.clientMappings = request.ClientMapping.ToList();
            this.partitionMappings = request.PartitionMapping.ToList();

            client.SetDataCenter(getDataCenter());
            client.SetClientList(getClientList());
            client.SetServerList(getServerList());

            this.hasReceivedMappings = true;
            return new SendMappingsReply { Ok = true };
            //TODO-when receiving new mappings,clear current server
        }

        public override Task<GetNodeStatusReply> GetStatus(GetNodeStatusRequest request, ServerCallContext context)
        {
            return Task.FromResult(getStatus());
        }
        public GetNodeStatusReply getStatus()
        {
            Console.WriteLine("============ STATUS ==========\nClient Status: Normal");
            Console.WriteLine("Mappings:\n");
            client.PrintMappings();
            Console.WriteLine("==============================");
            return new GetNodeStatusReply { Ok = true, Response = "Client running" };
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
