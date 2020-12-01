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

namespace Clients
{
    public class PuppetClient : PuppetService.PuppetServiceBase
    {
        List<ServerMapping> serverMappings;
        List<ClientMapping> clientMappings;
        List<PartitionMapping> partitionMappings;

        public PuppetClient()
        {

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


            return new SendMappingsReply { Ok = true };
        }



        public override Task<GetNodeStatusReply> GetStatus(GetNodeStatusRequest request, ServerCallContext context)
        {
            return Task.FromResult(getStatus());
        }
        public GetNodeStatusReply getStatus()
        {
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
