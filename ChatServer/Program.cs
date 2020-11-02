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

namespace chatServer
{
    // ChatServerService is the namespace defined in the protobuf
    // ChatServerServiceBase is the generated base implementation of the service
    public class ServerService : ChatServerService.ChatServerServiceBase
    {
        private GrpcChannel channel;
        private Dictionary<string, ChatClientService.ChatClientServiceClient> clientMap =
            new Dictionary<string, ChatClientService.ChatClientServiceClient>();

        // DataCenter = <partionId, List<serverId>>
        public Dictionary<int, List<int>> DataCenter =
            new Dictionary<int, List<int>>();
        // ServerList = <serverId, URL>
        public Dictionary<int, string> ServerList =
            new Dictionary<int, string>(); 
        // StorageSystem = <UniqueKey,value>
        public Dictionary<UniqueKey, string> StorageSystem =
            new Dictionary<UniqueKey, string>();
        // MyId stores the server id
        public int MyId;

        public ServerService(){
        }

        public override Task<ChatClientRegisterReply> Register(
            ChatClientRegisterRequest request, ServerCallContext context)
        {
            Console.WriteLine("Deadline: " + context.Deadline);
            Console.WriteLine("Host: " + context.Host);
            Console.WriteLine("Method: " + context.Method);
            Console.WriteLine("Peer: " + context.Peer);
            return Task.FromResult(Reg(request));
        }
        public ChatClientRegisterReply Reg(ChatClientRegisterRequest request)
        {
            channel = GrpcChannel.ForAddress(request.Url);
            ChatClientService.ChatClientServiceClient client =
                new ChatClientService.ChatClientServiceClient(channel);
            lock (this)
            {
                clientMap.Add(request.Nick, client);
            }
            Console.WriteLine($"Registered client {request.Nick} with URL {request.Url}");
            ChatClientRegisterReply reply = new ChatClientRegisterReply();
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

            // TODO implement send to other servers
            if (DataCenter[uKey.PartitionId][0] == MyId){
                List<int> OtherServer = DataCenter[uKey.PartitionId];
                foreach (var item in OtherServer)
                {
                    AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
                    string url = ServerList[item];
                    channel = GrpcChannel.ForAddress(url);
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


        public override Task<ListGlobalResponse> ListGlobal(ListGlobalRequest request, ServerCallContext context)
        {
            return Task.FromResult(listGlobal(request));
        }
        public ListGlobalResponse listGlobal(ListGlobalRequest request){
            
            var listGlobalResponse = new ListGlobalResponse();

            foreach (var item in StorageSystem){
                UniqueKey uKey = item.Key;
                listGlobalResponse.UniqueKeyList.Add(uKey);
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
                
                serverId = Convert.ToInt32(Console.ReadLine());
                Console.WriteLine(serverId);
                int port = 1000 + serverId;

                serverPort = new ServerPort(hostname, port, ServerCredentials.Insecure);
                startupMessage = "Insecure ChatServer server listening on port " + port;


                Server server = new Server
                {
                    Services = { ChatServerService.BindService(new ServerService()) },
                    Ports = { serverPort }
                };

                server.Start();

                Console.WriteLine(startupMessage);
                //Configuring HTTP for client connections in Register method
                AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
                while (true) ;
            }
        }
    }
}

