using MainServer;
using MainServer.exceptions;
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
        public Dictionary<string, List<string>> DataCenter = new Dictionary<string, List<string>>();
        
        // ServerList = <serverId, URL>
        public Dictionary<string, string> ServerList = new Dictionary<string, string>(); 
        
        // StorageSystem = <UniqueKey,value>
        public Dictionary<UniqueKey, string> StorageSystem = new Dictionary<UniqueKey, string>();

        // Storage System Lock
        private List<ObjectLock> StorageSystemLock = new List<ObjectLock>();

        // MyId stores the server id
        private readonly string ServerId;
        private readonly int MinDelay;
        private readonly int MaxDelay;

        public bool Freeze = false;
        public bool Crash = false;

        public MainServerService(string Id, int minDelay, int maxDelay)
        {
            ServerId = Id;
            MinDelay = minDelay;
            MaxDelay = maxDelay;
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
            return Task.FromResult(ReadObject(request));
        }
        public ReadResponse ReadObject(ReadRequest request)
        {
            WaitForStatement(Freeze);

            // Stays blocked until object gets unlocked
            WaitForStatement(IsObjectLocked(request.UniqueKey));

            SetDelay();
            lock (StorageSystem){
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
            return Task.FromResult(WriteObject(request, context));
        }
        public WriteResponse WriteObject(WriteRequest request, ServerCallContext context) {
            WaitForStatement(Freeze);
            SetDelay();
            Object obj = request.Object;
            UniqueKey uKey = obj.UniqueKey;
            string value = obj.Value;
            string url = context.Host;

            Console.WriteLine($"WRITE REQUEST FROM: {url}");

            // If the object was blocked and the write request was made by the master
            if (IsObjectLocked(uKey) && GetLockMasterServerUrl(uKey).Equals(url))
            {
                // 1st Write to storage System
                AddObjectToStorageSystem(obj);

                // 2nd Unlocks the object
                UnlockObject(uKey);

            } else
            {
                // Stays blocked until object gets unlocked
                WaitForStatement(IsObjectLocked(uKey));

                AddObjectToStorageSystem(obj);

                try {
                    SendLockObjectToAllServersOfPartition(uKey);
                    SendWriteToAllServersOfPartition(obj);
                } catch {
                    return new WriteResponse {
                        Ok = false
                    };
                }
            }
            return new WriteResponse
            {
                Ok = true
            };

        }

        public override Task<ListServerResponse> ListServer(ListServerRequest request, ServerCallContext context)
        {
            return Task.FromResult(ListServer(request));
        }
        public ListServerResponse ListServer(ListServerRequest request){
            WaitForStatement(Freeze);
            SetDelay();
            ListServerResponse listServerResponse = new ListServerResponse();
            if (request.ServerId == ServerId){
                lock(StorageSystem) lock(DataCenter){
                    foreach (var item in StorageSystem){
                        Object tmp = new Object();
                        var listObj = new ListServerObj();
                        tmp.UniqueKey = item.Key;
                        tmp.Value = item.Value;
                        string partId = tmp.UniqueKey.PartitionId;
                        listObj.Object = tmp;
                        if (DataCenter[partId][0] == ServerId){
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
                    try{
                        channel = GrpcChannel.ForAddress(url);
                        ServerService.ServerServiceClient server =
                            new ServerService.ServerServiceClient(channel);
                        listServerResponse = server.ListServer(request);
                    } catch {
                        Console.WriteLine($"Server {request.ServerId} is not available");
                    }
                }
            }
            return listServerResponse;
        }

        public override Task<ListEachGlobalResponse> ListEachGlobal(ListEachGlobalRequest request, ServerCallContext context)
        {
            return Task.FromResult(ListEachGlobal(request));
        }
        public ListEachGlobalResponse ListEachGlobal(ListEachGlobalRequest request){
            WaitForStatement(Freeze);
            SetDelay();
            var listEachGlobalResponse = new ListEachGlobalResponse();
            lock(StorageSystem){
                GlobalStructure gStruct = new GlobalStructure();
                foreach (var item in StorageSystem){
                    UniqueKey uKey = item.Key;
                    
                   gStruct.UniqueKeyList.Add(uKey);
                    
                }
                gStruct.ServerId = ServerId;
                listEachGlobalResponse.GlobalList.Add(gStruct);
            }
            return listEachGlobalResponse;
        }

        public override Task<ListGlobalResponse> ListGlobal(ListGlobalRequest request, ServerCallContext context)
        {
            return Task.FromResult(ListGlobal(request));
        }
        public ListGlobalResponse ListGlobal(ListGlobalRequest request){
            WaitForStatement(Freeze);
            SetDelay();
            var listGlobalResponse = new ListGlobalResponse();
            var listEachGlobalResponse = new ListEachGlobalResponse();
            Dictionary<string, string> tmpListServer = new Dictionary<string, string>(ServerList);
            tmpListServer.Remove(ServerId);

            lock(StorageSystem){
                GlobalStructure gStruct = new GlobalStructure();
                foreach (var item in StorageSystem){
                    UniqueKey uKey = item.Key;
                   gStruct.UniqueKeyList.Add(uKey);
                }
                gStruct.ServerId = ServerId;
                listGlobalResponse.GlobalList.Add(gStruct);
            }

            foreach (var item in tmpListServer) {
                string url = item.Value;
                try{
                    channel = GrpcChannel.ForAddress(url);
                    ServerService.ServerServiceClient server = new ServerService.ServerServiceClient(channel);
                    ListEachGlobalRequest lsRequest = new ListEachGlobalRequest{ ServerId = item.Key};
                    listEachGlobalResponse = server.ListEachGlobal(lsRequest);
                    foreach(var tmp in listEachGlobalResponse.GlobalList){
                        listGlobalResponse.GlobalList.Add(tmp);
                    }
                } catch {
                    Console.WriteLine($"Server {item.Key} is not available"); 
                }
            }

            return listGlobalResponse;
            
        }

        public override Task<LockObjectResponse> LockObject(LockObjectRequest request, ServerCallContext context)
        {
            return Task.FromResult(LockObject(request));
        }

        public LockObjectResponse LockObject(LockObjectRequest request)
        {
            lock (StorageSystemLock)
            {
                WaitForStatement(IsObjectLocked(request.Lock.UniqueKey));
                StorageSystemLock.Add(request.Lock);
                return new LockObjectResponse { Ok = true };
            }
        }

        private bool IsObjectLocked(UniqueKey key)
        {
            lock (StorageSystemLock)
            {
                foreach (ObjectLock oLock in StorageSystemLock)
                {
                    if (oLock.UniqueKey.ObjectId.Equals(key.ObjectId) && 
                        oLock.UniqueKey.PartitionId.Equals(key.PartitionId))
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        private string GetLockMasterServerUrl(UniqueKey key)
        {
            string serverId = "";
            string serverUrl = "";
            lock (StorageSystemLock)
            {
                foreach (ObjectLock oLock in StorageSystemLock)
                {
                    if (oLock.UniqueKey.ObjectId.Equals(key.ObjectId) &&
                        oLock.UniqueKey.PartitionId.Equals(key.PartitionId))
                    {
                        serverId = oLock.Master;
                        serverUrl = GetServerUrl(serverId);
                        break;
                    }
                }
                return serverUrl;
            }
        }

        public void PrintMappings()
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

        public void SetDelay()
        {
            Random rnd = new Random();
            int delay = rnd.Next(MinDelay, MaxDelay + 1);
            Thread.Sleep(delay);
        }

        private void WaitForStatement(bool statement)
        {
            while(statement) { Thread.Sleep(150); }
        }

        private void SendLockObjectToAllServersOfPartition(UniqueKey uKey)
        {
            lock (DataCenter)
            {
                string partitionId = uKey.PartitionId;
                List<string> servers = new List<string>(DataCenter[partitionId]); // Make a copy so that we dont remove from the DataCenter
                servers.Remove(ServerId);

                foreach (string serverId in servers)
                {
                    try
                    {
                        string url = ServerList[serverId];
                        channel = GrpcChannel.ForAddress(url);
                        ServerService.ServerServiceClient server = new ServerService.ServerServiceClient(channel);

                        LockObjectResponse response = server.LockObject(new LockObjectRequest
                        {
                            Lock = new ObjectLock { Master = ServerId, UniqueKey = uKey }
                        });

                        if (response.Ok)
                        {
                            Console.WriteLine($"Object {uKey.ObjectId} was locked on server {serverId}");
                        }
                        else
                        {
                            Console.WriteLine($"Failed to lock Object {uKey.ObjectId} on server {serverId}");
                        }
                    } catch
                    {
                        Console.WriteLine($"Failed to communicate with server {serverId}");
                        throw new FailedToSendRequestException($"Failed to communicate with server {serverId}"); 
                    }
                }

            }
        }

        private void SendWriteToAllServersOfPartition(Object obj)
        {
            lock (DataCenter)
            {
                string partitionId = obj.UniqueKey.PartitionId;
                List<string> servers = new List<string>(DataCenter[partitionId]); // Make a copy so that we dont remove from the DataCenter
                servers.Remove(ServerId);

                foreach (string serverId in servers)
                {
                    try
                    {
                        string url = ServerList[serverId];
                        channel = GrpcChannel.ForAddress(url);
                        ServerService.ServerServiceClient server = new ServerService.ServerServiceClient(channel);

                        WriteResponse response = server.Write(new WriteRequest
                        {
                            Object = obj
                        });

                        if (response.Ok)
                        {
                            Console.WriteLine($"Object {obj.UniqueKey.ObjectId} was written on server {serverId}");
                        }
                        else
                        {
                            Console.WriteLine($"Failed to write Object  {obj.UniqueKey.ObjectId} on server {serverId}");
                        }
                    }
                    catch
                    {
                        Console.WriteLine($"Failed to communicate with server {serverId}");
                        throw new FailedToSendRequestException($"Failed to communicate with server {serverId}");
                    }
                }
            }
        }

        private string GetServerUrl(string serverId)
        {
            lock (ServerList)
            {
                foreach (KeyValuePair<string, string> pair in ServerList)
                {
                    if (pair.Key.Equals(serverId))
                    {
                        return pair.Value;
                    }
                }
            }
            return "";
        }

        private void UnlockObject(UniqueKey uKey)
        {
            lock (StorageSystemLock)
            {
                foreach (ObjectLock oLock in StorageSystemLock)
                {
                    if (oLock.UniqueKey.ObjectId.Equals(uKey.ObjectId) &&
                        oLock.UniqueKey.PartitionId.Equals(uKey.PartitionId))
                    {
                        StorageSystemLock.Remove(oLock);
                        break;
                    }
                }
            }
        }

        private void AddObjectToStorageSystem(Object obj)
        {
            lock (StorageSystem) StorageSystem.Add(obj.UniqueKey, obj.Value);
        }
        class Program
        {
            
            public static void Main(string[] args)
            {
                if(args.Length == 4)
                {
                    // Reading from command line
                    Random Rnd = new Random();
                    string ServerId = args[0]; 
                    string ServerUrl = args[1];
                    int ServerPort = int.Parse(ServerUrl.Split(':')[2]);
                    int MinDelay = int.Parse(args[2]);
                    int MaxDelay = int.Parse(args[3]);
                    
                                
                    string hostname = (ServerUrl.Split(':')[1]).Substring(2);
                    string startupMessage;
                    ServerPort serverPort;         

                    serverPort = new ServerPort(hostname, ServerPort, ServerCredentials.Insecure);
                    startupMessage = "Server: " + ServerId + "\nListening on port: " + ServerPort;

                    MainServerService serviceServer = new MainServerService(ServerId, MinDelay, MaxDelay);
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

                    while (!serviceServer.Crash) { Thread.Sleep(50); }
                    Thread.Sleep(1000);

                } else {
                    Console.WriteLine("Received invalid arguments.");
                }
            }
        }
    }
}

