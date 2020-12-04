using System;
using System.IO;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using Grpc.Core;
using Grpc.Net.Client;
using System.Threading.Tasks;


namespace Clients
{

    public class Client
    {
        private ServerService.ServerServiceClient client;
        private GrpcChannel channel;
        private string currentServer;
        private string username;
        private string myURL;
        private string[] lines;
        private static Exception _Exception = null;

        // ServerList = <serverId, URL>
        public Dictionary<string, string> ServerList = new Dictionary<string, string>();
        // DataCenter = <partitionId, List<serverId>>
        public Dictionary<string, List<string>> DataCenter = new Dictionary<string, List<string>>();
        //ClientList= <username,URL>
        public Dictionary<string, string> ClientList = new Dictionary<string, string>();

        // LOCKS
        private readonly object ExceptionLock = new object();
        private readonly object ServerListLock = new object();
        private readonly object DataCenterLock = new object();
        private readonly object ClientListLock = new object();
        private readonly object CurrentServerLock = new object();
        private readonly object ChannelLock = new object();
        private readonly object ClientLock = new object();

        public Client(string client_username, string client_URL, string script_file)
        {
            AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

            username = client_username;
            myURL = client_URL;
            lines = File.ReadAllLines(script_file);

            Console.WriteLine($"Client {username} started at {myURL}");
        }

        public void PrintMappings()
        {
            lock (ClientListLock)
            {
                foreach (KeyValuePair<string, string> entry in ClientList)
                {
                    Console.WriteLine($"Client {entry.Key} with URL: {entry.Value}");
                }
            }
            lock (DataCenterLock)
            {
                foreach (KeyValuePair<string, List<String>> entry in DataCenter)
                {
                    string servers = String.Join(", ", entry.Value.ToArray());
                    Console.WriteLine($"Partition {entry.Key} with Servers: {servers}");
                }
            }
        }

        public string GetServerId()
        {
            string id = "";
            lock (ServerListLock)
            {
                foreach (string key in ServerList.Keys)
                {
                    if (ServerList[key].Equals(currentServer))
                    {
                        id = key;
                    }
                }
            }
            return id;
        }


        public void ConnectToServer()
        {
            lock (ChannelLock)
            {
                this.channel = GrpcChannel.ForAddress(currentServer);
            }
            lock (ClientLock)
            {
                this.client = new ServerService.ServerServiceClient(this.channel);
            }

        }

        public void SetCurrentServer(string server)
        {
            lock (CurrentServerLock)
                this.currentServer = server;

        }

        //mode = 1 to check if there is a server with the given server_id available
        //mode = 2 to check if there is a server with the given URL available
        public Boolean ServerAvailable(string server, int mode)
        {
            if (mode == 1)
            {
                if (ServerList.ContainsKey(server))
                {
                    return true;
                }
                Console.WriteLine($"Server {server} is not available");
                return false;
            }

            else if (mode == 2)
            {
                if (ServerList.ContainsValue(server))
                {
                    return true;
                }
                Console.WriteLine($"Server {server} is not available");
                return false;
            }
            else
            {
                Console.WriteLine($"Cannot check if the server is running. Returning False ...");
                return false;
            }

        }

        public Boolean PartitionAvailable(string partitionId)
        {
            if (DataCenter.ContainsKey(partitionId))
            {
                return true;
            }
            Console.WriteLine($"Partition {partitionId} is not available");
            return false;
        }


        public void ParseInputFile()
        {
            String[] line = new String[lines.Length];
            List<Task> tasks = new List<Task>();

            int count = 0;
            if (lines.Length > 0 && ServerList.Count > 0)
            {
                foreach (var item in ServerList)
                {
                    string serverUrl = item.Value;
                    SetCurrentServer(serverUrl);
                    break; // only need to get the first element of dictionary
                }

                ConnectToServer();


                for (int i = 0; i < lines.Length; i++)
                {
                    line = lines[i].Split(' ');
                    if (line[0].Contains("Wait"))
                    {
                        Wait(int.Parse(line[1]));
                    }
                    else
                    {
                        if (line[0] == "begin-repeat")
                        {
                            count = BeginRepeat(i, int.Parse(line[1]));
                            i = i + count;
                            Console.WriteLine("end-repeat");
                        }
                        else
                        {

                            SwitchCase(line, -1);
                            /*
                            
                            Task task = Task.Run(() => SwitchCase(line, -1));
                            tasks.Add(task);
                            */
                        }

                    }
                }

                //Task.WaitAll(tasks.ToArray());

                // Rethrow last exception thrown in Tasks
                if (_Exception != null)
                {
                    //throw _Exception;
                    Console.WriteLine("Exception was thrown!!!");
                }
            }

        }

        public void SwitchCase(string[] line, int beginRepeat)
        {

            switch (line[0])
            {
                case "read":
                    Read(line[1], line[2], line[3], beginRepeat);
                    break;
                case "write":
                    CheckMaster(line[1], line[2], line[3], beginRepeat);
                    break;
                case "listServer":
                    ListServer(line[1], beginRepeat);
                    break;
                case "listGlobal":
                    ListGlobal(beginRepeat);
                    break;
            }
        }

        public string CheckReplace(string s, int n)
        {
            if (s.Contains("$i"))
            {
                s = s.Replace("$i", n.ToString());
            }
            return s;
        }
        public void Read(string partitionId, string objectId, string server_id, int beginRepeat)
        {
            if (PartitionAvailable(partitionId))
            {

                UniqueKey uniqueKey = new UniqueKey();
                uniqueKey.PartitionId = partitionId;
                uniqueKey.ObjectId = objectId;

                try
                {
                    ReadResponse response = client.Read(new ReadRequest
                    {
                        UniqueKey = uniqueKey,
                        ServerId = server_id
                    });

                    if (response.Value.Equals("N/A") && !server_id.Equals("-1") && ServerAvailable(server_id, 1))
                    {
                        Console.WriteLine("Current Server doesn't have the object. Changing Server ...");

                        SetCurrentServer(server_id);
                        ConnectToServer();

                        response = client.Read(new ReadRequest
                        {
                            UniqueKey = uniqueKey,
                            ServerId = server_id
                        });
                        Console.WriteLine("Response from the new server:");
                    }

                    if (beginRepeat != -1)
                    {
                        response.Value = CheckReplace(response.Value, beginRepeat);

                    }
                    Console.WriteLine(response);
                }
                catch
                {
                    Console.WriteLine($"Server {this.currentServer} is not available");
                }
            }
            return;
        }


        public void CheckMaster(string partitionId, string objectId, string value, int beginRepeat)
        {

            string server_id = GetServerId();

            lock (DataCenterLock)
            {
                if (PartitionAvailable(partitionId))
                {

                    if (!DataCenter[partitionId][0].Equals(server_id))
                    {

                        server_id = DataCenter[partitionId][0];
                        SetCurrentServer(ServerList[server_id]);
                        Console.WriteLine("Changing to the Master server for this partition ...");
                        ConnectToServer();
                    }
                    Write(partitionId, objectId, value, beginRepeat);
                }
                else
                {
                    return;
                }
            }

        }
        public void Write(string partitionId, string objectId, string value, int beginRepeat)
        {
            if (beginRepeat != -1)
            {
                partitionId = CheckReplace(partitionId, beginRepeat);
                objectId = CheckReplace(objectId, beginRepeat);
                value = CheckReplace(value, beginRepeat);

            }
            UniqueKey uniqueKey = new UniqueKey();
            uniqueKey.PartitionId = partitionId;
            uniqueKey.ObjectId = objectId;
            Object o = new Object();
            o.UniqueKey = uniqueKey;
            o.Value = value;

            try
            {
                WriteResponse response = client.Write(new WriteRequest
                {
                    Object = o

                });
                if (response.Ok)
                {
                    Console.WriteLine("Write completed!");
                }
                else
                {
                    Console.WriteLine("Error in write");
                }

            }
            catch
            {
                Console.WriteLine($"Server {this.currentServer} is not available");
                //lista de servidores ligados e escolher um de lá,ao receber mappings,limpar a lista
            }

        }
        public void ListServer(string server_id, int beginRepeat)
        {
            if (ServerAvailable(server_id,1))
            {
                try
                {
                    ListServerResponse response = client.ListServer(new ListServerRequest
                    {
                        ServerId = server_id
                    });
                    foreach (ListServerObj server in response.ListServerObj)
                    {
                        string output = $" partitionId {server.Object.UniqueKey.PartitionId} " +
                                        $"objectId {server.Object.UniqueKey.ObjectId} " +
                                        $"value {server.Object.Value}";
                        if (server.IsMaster)
                        {
                            output += $" Master replica for this object";
                        }

                        if (beginRepeat != -1)
                        {
                            output = CheckReplace(output, beginRepeat);

                        }
                        Console.WriteLine(output);
                    }
                }
                catch
                {
                    Console.WriteLine($"Server {this.currentServer} is not available");
                }
            }
            else
            {
                return;
            }
        }
        public void ListGlobal(int beginRepeat)
        {
            try
            {
                ListGlobalResponse response = client.ListGlobal(new ListGlobalRequest { });
                Console.WriteLine("----ListGlobal----");
                foreach (GlobalStructure globalStructure in response.GlobalList)
                {
                    string output = $"server {globalStructure.ServerId} with objects:\n";
                    foreach (UniqueKey uniqueKey in globalStructure.UniqueKeyList)
                    {
                        output += $"partitionId {uniqueKey.PartitionId} " +
                        $"objectId {uniqueKey.ObjectId}\n";

                    }

                    if (beginRepeat != -1)
                    {
                        if (output.Contains("$i"))
                        {
                            output = CheckReplace(output, beginRepeat);
                        }
                    }

                    Console.WriteLine(output);
                    output = "";
                }
                Console.WriteLine("--ListGlobalEnd--");
            }
            catch
            {
                Console.WriteLine($"Server {this.currentServer} is not available");
            }
        }
        public void Wait(int x)
        {
            Thread.Sleep(x);
        }

        public int BeginRepeat(int i, int x)
        {
            Console.WriteLine(lines[i].Split(' ')[0] + " " + lines[i].Split(' ')[1]);
            int count = 0;
            int aux = i;
            int run = 1;

            while (!lines[i].Split(' ')[0].Equals("end-repeat"))
            {
                if (lines[i + 1].Split(' ')[0].Equals("begin-repeat"))
                {
                    run = 0;
                }

                i = i + 1;
                count = count + 1;
            }
            int max = aux + count - 1;

            if (run == 1)
            {
                while (aux < max)
                {
                    for (int j = 1; j < x + 1; j++)
                    {
                        SwitchCase(lines[aux + 1].Split(' '), j);
                    }
                    aux = aux + 1;
                }
            }

            return count;
        }

        public void SetDataCenter(Dictionary<string, List<string>> DataCenter)
        {
            lock (DataCenter)
            {
                this.DataCenter = DataCenter;
            }
        }

        public void SetServerList(Dictionary<string, string> Servers)
        {
            lock (ServerList)
            {
                this.ServerList = Servers;
            }
        }

        public void SetClientList(Dictionary<string, string> Clients)
        {
            lock (ClientList)
            {
                this.ClientList = Clients;
            }
        }

        public static class Program
        {
            /// <summary>
            ///  The main entry point for the application.
            /// </summary>
            [STAThread]
            public static void Main(string[] args)
            {
                //run by the command line
                if (args.Length == 3)
                {
                    string username = args[0];
                    string URL = args[1];
                    string script = args[2];

                    string[] splittedURL = URL.Split(":");

                    if (splittedURL.Length == 3)
                    {
                        string hostname = splittedURL[1].Substring(2); // Obtain localhost from http://localhost
                        int port = int.Parse(splittedURL[2]);

                        ServerPort serverPort = new ServerPort(hostname, port, ServerCredentials.Insecure);
                        Client client = new Client(username, URL, script);

                        PuppetClient puppetClient = new PuppetClient(client);

                        Server server = new Server
                        {
                            Services = { PuppetService.BindService(puppetClient) },
                            Ports = { serverPort }
                        };

                        server.Start();


                        while (true)
                        {
                            if (puppetClient.hasReceivedMappings)
                            {
                                client.PrintMappings();

                                client.ParseInputFile();

                                Console.WriteLine("Input file executed.");

                                break;
                            }
                            Thread.Sleep(25);
                        }

                        while (true) { Thread.Sleep(1000); }  // Avoid consuming a lot of processing power                 

                    }
                    else
                    {
                        Console.WriteLine("Received invalid arguments.");
                        Console.ReadKey();
                    }

                }
                else
                {
                    Console.WriteLine("No arguments received.");
                    Console.ReadKey();
                }
            }
        }
    }
}

