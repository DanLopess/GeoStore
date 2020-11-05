using MainServer;
using Grpc.Core;
using Grpc.Net.Client;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Data.SqlTypes;
using System.IO;
using System.Security;
using System.Threading;
using System.Threading.Tasks;

namespace MainServer
{
    // ServerService is the namespace defined in the protobuf
    // ServerServiceBase is the generated base implementation of the service
    public class MainServerService : ServerService.ServerServiceBase
    {
        private GrpcChannel channel;

        // DataCenter = <partionId, List<serverId>>
        private Dictionary<string, List<string>> DataCenter =
            new Dictionary<string, List<string>>();
        // ServerList = <serverId, URL>
        private Dictionary<string, string> ServerList =
            new Dictionary<string, string>(); 
        // StorageSystem = <UniqueKey,value>
        private Dictionary<UniqueKey, string> StorageSystem =
            new Dictionary<UniqueKey, string>();
        // MyId stores the server id
        private string MyId;

        public MainServerService(string Id){
            this.MyId = Id;
            
            Dictionary<string, List<string>> tmp = new Dictionary<string, List<string>>();
            List<string> tmpA = new List<string>();
            List<string> tmpB = new List<string>();
            tmpA.Add("Server-2");
            tmpA.Add("Server-3");
            tmpB.Add("Server-3");
            tmpB.Add("Server-2");
            tmp.Add("Part1", tmpA);
            tmp.Add("Part2", tmpB);
            this.DataCenter = tmp;

            Dictionary<string, string> tmp2 = new Dictionary<string, string>();
            tmp2.Add("Server-2", "http://localhost:10002");
            tmp2.Add("Server-3", "http://localhost:10003");
            this.ServerList = tmp2;
        }

        public override Task<ReadResponse> Read(ReadRequest request, ServerCallContext context)
        {
            return Task.FromResult(read(request));
        }
        public ReadResponse read(ReadRequest request)
        {
            if (StorageSystem.ContainsKey(request.UniqueKey)) {
                return new ReadResponse
                {
                    Value = StorageSystem[request.UniqueKey]
                };
            }
            else
            {
                return new ReadResponse
                {
                    Value = "N/A"
                };
            }
        }

        public override Task<WriteResponse> Write(WriteRequest request, ServerCallContext context)
        {
            return Task.FromResult(write(request));
        }
        public WriteResponse write(WriteRequest request) {
            UniqueKey uKey = request.Object.UniqueKey;
            string value = request.Object.Value;
            // TODO implement lock
            StorageSystem.Add(uKey, value);
            
            if (DataCenter[uKey.PartitionId][0] == MyId){
                List<string> OtherServer = new List<string>(DataCenter[uKey.PartitionId]); 
                OtherServer.RemoveAt(0);

                if (OtherServer.Count != 0){
                    foreach (var item in OtherServer){
                        string url = ServerList[item];
                        channel = GrpcChannel.ForAddress(url);
                        ServerService.ServerServiceClient server =
                            new ServerService.ServerServiceClient(channel);
                        server.WriteAsync(request);
                    }
                }
            }
            return new WriteResponse
            {
                Ok = true
            };

        }


        public override Task<ListServerResponse> ListServer(ListServerRequest request, ServerCallContext context)
        {
            return Task.FromResult(listServer(request));
        }
        public ListServerResponse listServer(ListServerRequest request){
                        
            ListServerResponse listServerResponse = new ListServerResponse();
            if (request.ServerId == MyId)
            {
                foreach (var item in StorageSystem)
                {
                    Object tmp = new Object();
                    var listObj = new ListServerObj();
                    tmp.UniqueKey = item.Key;
                    tmp.Value = item.Value;
                    string partId = tmp.UniqueKey.PartitionId;
                    listObj.Object = tmp;
                    if (DataCenter[partId][0] == MyId)
                    {
                        listObj.IsMaster = true;
                    }
                    else
                    {
                        listObj.IsMaster = false;
                    }
                    listServerResponse.ListServerObj.Add(listObj);

                }
            }
            else {
                string url = ServerList[request.ServerId];
                channel = GrpcChannel.ForAddress(url);
                ServerService.ServerServiceClient server =
                    new ServerService.ServerServiceClient(channel);
                listServerResponse = server.ListServer(request);
            }
            return listServerResponse;
        }


        public override Task<ListEachGlobalResponse> ListEachGlobal(ListEachGlobalRequest request, ServerCallContext context)
        {
            return Task.FromResult(listEachGlobal(request));
        }
        public ListEachGlobalResponse listEachGlobal(ListEachGlobalRequest request){
            var listEachGlobalResponse = new ListEachGlobalResponse();
            foreach (var item in StorageSystem)
            {
                UniqueKey uKey = item.Key;
                listEachGlobalResponse.UniqueKeyList.Add(uKey);
            }
            return listEachGlobalResponse;
        }


        public override Task<ListGlobalResponse> ListGlobal(ListGlobalRequest request, ServerCallContext context)
        {
            return Task.FromResult(listGlobal(request));
        }
        public ListGlobalResponse listGlobal(ListGlobalRequest request){
            var listGlobalResponse = new ListGlobalResponse();
            var listEachGlobalResponse = new ListEachGlobalResponse();
            Dictionary<string, string> tmpListServer = ServerList;
            tmpListServer.Remove(MyId);

            foreach (var item in StorageSystem){
                UniqueKey uKey = item.Key;
                listGlobalResponse.UniqueKeyList.Add(uKey);
            }

            foreach (var item in tmpListServer) {
                string url = item.Value;
                channel = GrpcChannel.ForAddress(url);
                ServerService.ServerServiceClient server = new ServerService.ServerServiceClient(channel);
                ListEachGlobalRequest lsRequest = new ListEachGlobalRequest{ ServerId = item.Key};

                listEachGlobalResponse = server.ListEachGlobal(lsRequest);
                foreach(var tmp in listEachGlobalResponse.UniqueKeyList){
                    listGlobalResponse.UniqueKeyList.Add(tmp);
                }
            }

            return listGlobalResponse;
            
        }

        class Program
        {
            
            public static void Main(string[] args)
            {
                /* Reading from command line
                Random rnd = new Random();
                string MyId = args[0]; 
                string MyUrl = args[2];
                int min_delay = int.Parse(args[3]);
                int max_delay = int.Parse(args[4]);
                int delay = rnd.Next(min_delay, max_delay + 1);
                */
                const string hostname = "localhost";
                string startupMessage;
                ServerPort serverPort;
                ServerPort pmPort;
                int serverId;

                Console.WriteLine("Insert an Id (2 or 3) for the Server");
                serverId = Convert.ToInt32(Console.ReadLine());
                int port = 10000 + serverId;

                //Just for testing
                string MyId = "Server-" + serverId.ToString();
                serverPort = new ServerPort(hostname, port, ServerCredentials.Insecure);
                startupMessage = "Server " + serverId + "listening on port " + port;


                Server server = new Server
                {
                    Services = { ServerService.BindService(new MainServerService(MyId)) },
                    Ports = { serverPort }
                };

                server.Start();

                Console.WriteLine(startupMessage);
                //Configuring HTTP for client connections in Register method
                AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

                while(true);
            }
        }
    }
}

