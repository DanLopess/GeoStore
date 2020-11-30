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
        public Dictionary<string, List<string>> DataCenter =
            new Dictionary<string, List<string>>();
        // ServerList = <serverId, URL>
        public Dictionary<string, string> ServerList =
            new Dictionary<string, string>(); 
        // StorageSystem = <UniqueKey,value>
        public Dictionary<UniqueKey, string> StorageSystem =
            new Dictionary<UniqueKey, string>();
        // MyId stores the server id
        private string MyId;

        public Boolean freeze = false;
        public Boolean crash = false;

        public MainServerService(string Id){
            this.MyId = Id;
            /*
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
            */
        }

        public void SetDataCenter(Dictionary<string, List<string>> DataCenter){
            lock(DataCenter){
                this.DataCenter = DataCenter;
            }
        }

        public void SetServerList(Dictionary<string, string> Servers){
            lock (ServerList){
                this.ServerList = Servers;
            }
        }

        public override Task<ReadResponse> Read(ReadRequest request, ServerCallContext context)
        {
            return Task.FromResult(read(request));
        }
        public ReadResponse read(ReadRequest request)
        {
            lock(StorageSystem){
                if (StorageSystem.ContainsKey(request.UniqueKey)) {
                    return new ReadResponse
                    {
                        Value = StorageSystem[request.UniqueKey]
                    };
                }else{
                    return new ReadResponse
                    {
                        Value = "N/A"
                    };
                }
            }
        }

        public override Task<WriteResponse> Write(WriteRequest request, ServerCallContext context)
        {
            return Task.FromResult(write(request));
        }
        public WriteResponse write(WriteRequest request) {
            while (freeze) ;
            UniqueKey uKey = request.Object.UniqueKey;
            string value = request.Object.Value;

            lock(StorageSystem){
                StorageSystem.Add(uKey, value);
                lock(DataCenter){
                    if (DataCenter[uKey.PartitionId][0] == MyId){
                        List<string> OtherServer = new List<string>(DataCenter[uKey.PartitionId]); 
                        OtherServer.RemoveAt(0);

                        if (OtherServer.Count != 0){
                            lock(ServerList){
                                foreach (var item in OtherServer){
                                    string url = ServerList[item];
                                    channel = GrpcChannel.ForAddress(url);
                                    ServerService.ServerServiceClient server =
                                        new ServerService.ServerServiceClient(channel);
                                    server.WriteAsync(request);
                                }
                            }
                        }
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
            if (request.ServerId == MyId){
                lock(StorageSystem) lock(DataCenter){
                    foreach (var item in StorageSystem){
                        Object tmp = new Object();
                        var listObj = new ListServerObj();
                        tmp.UniqueKey = item.Key;
                        tmp.Value = item.Value;
                        string partId = tmp.UniqueKey.PartitionId;
                        listObj.Object = tmp;
                        if (DataCenter[partId][0] == MyId){
                            listObj.IsMaster = true;
                        } else {
                            listObj.IsMaster = false;
                        }
                        listServerResponse.ListServerObj.Add(listObj);

                    }
                }
            } else {
                lock(ServerList){
                    string url = ServerList[request.ServerId];
                    channel = GrpcChannel.ForAddress(url);
                    ServerService.ServerServiceClient server =
                        new ServerService.ServerServiceClient(channel);
                    listServerResponse = server.ListServer(request);
                }
            }
            return listServerResponse;
        }

        public override Task<ListEachGlobalResponse> ListEachGlobal(ListEachGlobalRequest request, ServerCallContext context)
        {
            return Task.FromResult(listEachGlobal(request));
        }
        public ListEachGlobalResponse listEachGlobal(ListEachGlobalRequest request){
            var listEachGlobalResponse = new ListEachGlobalResponse();
            lock(StorageSystem){
                //GlobalStructure gStruct = new GlobalStructure();
                foreach (var item in StorageSystem){
                    UniqueKey uKey = item.Key;
                    //Trocar
                    //gStruct.UniqueKeyList.Add(uKey);
                    listEachGlobalResponse.UniqueKeyList.Add(uKey);
                }
                //listEachGlobalResponse.globalList.Add(gStruct);
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
            Dictionary<string, string> tmpListServer = new Dictionary<string, string>(ServerList);
            tmpListServer.Remove(MyId);

            lock(StorageSystem){
                //GlobalStructure gStruct = new GlobalStructure();
                foreach (var item in StorageSystem){
                    UniqueKey uKey = item.Key;
                    //Trocar
                    //gStruct.UniqueKeyList.Add(uKey);
                    listGlobalResponse.UniqueKeyList.Add(uKey);
                }
                //listGlobalResponse.globalList.Add(gStruct);
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
        public void printMappings()
        {
            foreach (KeyValuePair<string, string> entry in ServerList)
            {
                Console.WriteLine($"Server {entry.Key} with URL: {entry.Value}");
            }
            foreach (KeyValuePair<string, List<String>> entry in DataCenter)
            {
                string servers = String.Join(", ", entry.Value.ToArray());
                Console.WriteLine($"Partition {entry.Key} with Servers: {servers}");
            }
        }

        class Program
        {
            
            public static void Main(string[] args)
            {
                if(args.Length == 4)
                {
                    // Reading from command line
                    Random rnd = new Random();
                    string ServerId = args[0]; 
                    string ServerUrl = args[1];
                    int ServerPort = int.Parse(ServerUrl.Split(':')[2]);
                    int min_delay = int.Parse(args[2]);
                    int max_delay = int.Parse(args[3]);
                    int delay = rnd.Next(min_delay, max_delay + 1);
                                
                    string hostname = (ServerUrl.Split(':')[1]).Substring(2);
                    string startupMessage;
                    ServerPort serverPort;         

                    serverPort = new ServerPort(hostname, ServerPort, ServerCredentials.Insecure);
                    startupMessage = "Server: " + ServerId + "\nListening on port: " + ServerPort;

                    MainServerService serviceServer = new MainServerService(ServerId);
                    PuppetServer puppetServer = new PuppetServer(serviceServer);

                    Server server = new Server
                    {
                        Services = { ServerService.BindService(serviceServer),
                                     PuppetService.BindService(puppetServer)
                                   },
                        Ports = { serverPort }
                    };
                    server.Start();

                    Console.WriteLine(startupMessage);
                    //Configuring HTTP for client connections in Register method
                    AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

                    while (true)
                    {
                        if (puppetServer.hasReceivedMappings)
                        {
                            serviceServer.printMappings();

                            Console.WriteLine("Mappings received");
                            Console.WriteLine("Input file executed.");

                            break;
                        }
                    }

                    while (!serviceServer.crash) { }
                } else {
                    Console.WriteLine("Received invalid arguments.");
                }
            }
        }
    }
}

