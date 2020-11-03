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
    // ChatServerService is the namespace defined in the protobuf
    // ChatServerServiceBase is the generated base implementation of the service
    public class MainServerService : ServerService.ServerServiceBase
    {
        private GrpcChannel channel;
        private Dictionary<string, ClientService.ClientServiceClient> clientMap =
            new Dictionary<string, ClientService.ClientServiceClient>();

        // DataCenter = <partionId, List<serverId>>
        private Dictionary<int, List<int>> DataCenter =
            new Dictionary<int, List<int>>();
        // ServerList = <serverId, URL>
        private Dictionary<int, string> ServerList =
            new Dictionary<int, string>(); 
        // StorageSystem = <UniqueKey,value>
        private Dictionary<UniqueKey, string> StorageSystem =
            new Dictionary<UniqueKey, string>();
        // MyId stores the server id
        private int MyId;

        public MainServerService(int Id){
            this.MyId = Id;
        }

        public override Task<ClientRegisterReply> Register(
            ClientRegisterRequest request, ServerCallContext context)
        {
            Console.WriteLine("Deadline: " + context.Deadline);
            Console.WriteLine("Host: " + context.Host);
            Console.WriteLine("Method: " + context.Method);
            Console.WriteLine("Peer: " + context.Peer);
            return Task.FromResult(Reg(request));
        }
        public ClientRegisterReply Reg(ClientRegisterRequest request)
        {
            channel = GrpcChannel.ForAddress(request.Url);
            ClientService.ClientServiceClient client =
                new ClientService.ClientServiceClient(channel);
            lock (this)
            {
                clientMap.Add(request.Nick, client);
            }
            Console.WriteLine($"Registered client {request.Nick} with URL {request.Url}");
            ClientRegisterReply reply = new ClientRegisterReply();
            lock (this)
            {
                foreach (string nick in clientMap.Keys)
                {
                    reply.Users.Add(new User { Nick = nick });
                }
            }
            return reply;
        }


        public override Task<BcastMsgReply> BcastMsg(BcastMsgRequest request, ServerCallContext context)
        {
            return Task.FromResult(Bcast(request));
        }
        public BcastMsgReply Bcast(BcastMsgRequest request)
        {
            // random wait to simulate slow msg broadcast: Thread.Sleep(5000);
            Console.WriteLine("msg arrived. lazy server waiting for server admin to press key.");
            Console.ReadKey();
            lock (this)
            {
                foreach (string nick in clientMap.Keys)
                {
                    if (nick != request.Nick)
                    {
                        try
                        {
                            clientMap[nick].RecvMsg(new RecvMsgRequest
                            {
                                Msg = request.Nick + ": " + request.Msg
                            });
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e.Message);
                            clientMap.Remove(nick);
                        }
                    }
                }
            }
            Console.WriteLine($"Broadcast message {request.Msg} from {request.Nick}");
            return new BcastMsgReply
            {
                Ok = true
            };
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
                List<int> OtherServer = DataCenter[uKey.PartitionId];
                OtherServer.RemoveAt(0);
                if (OtherServer.Count != 0){
                    foreach (var item in OtherServer){
                        string url = ServerList[item];
                        channel = GrpcChannel.ForAddress(url);
                        ServerService.ServerServiceClient server =
                            new ServerService.ServerServiceClient(channel);
                        server.Write(request);
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
            
            Object tmp = new Object();
            var listServerResponse = new ListServerResponse();
            var listObj = new ListServerObj();
            
            foreach (var item in StorageSystem){
                tmp.UniqueKey = item.Key;
                tmp.Value = item.Value;
                int partId = item.Key.PartitionId;
                listObj.Object = tmp;

                if (DataCenter[partId][0] == MyId){
                    listObj.IsMaster = true;
                }
                else{
                    listObj.IsMaster = false;
                }

                listServerResponse.ListServerObj.Add(listObj);
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
            Dictionary<int, string> tmpListServer = ServerList;
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
                
                const string hostname = "localhost";
                string startupMessage;
                ServerPort serverPort;
                int serverId;

                Console.WriteLine("Insert an Id for the Server");
                serverId = Convert.ToInt32(Console.ReadLine());
                int port = 10000 + serverId;

                serverPort = new ServerPort(hostname, port, ServerCredentials.Insecure);
                startupMessage = "Insecure ChatServer server listening on port " + port;


                Server server = new Server
                {
                    Services = { ServerService.BindService(new MainServerService(serverId)) },
                    Ports = { serverPort }
                };

                server.Start();

                Console.WriteLine(startupMessage);
                //Configuring HTTP for client connections in Register method
                AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

                Console.WriteLine("Press any key to stop the PCS");
                Console.ReadKey();
                server.ShutdownAsync().Wait();
            }
        }
    }
}

