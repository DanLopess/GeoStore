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

        // ServerList = <serverId, URL>
        public Dictionary<string, string> ServerList = new Dictionary<string, string>();
        // DataCenter = <partitionId, List<serverId>>
        public Dictionary<string, List<string>> DataCenter = new Dictionary<string, List<string>>();
        //ClientList= <username,URL>
        public Dictionary<string, string> ClientList = new Dictionary<string, string>();

        public Client(string client_username, string client_URL, string script_file)
        {
            AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

            username = client_username;
            myURL = client_URL;
            lines = File.ReadAllLines(script_file);

            Console.WriteLine($"Client {username} started at {myURL}");
        }

        // DELETE THIS
        public void PrintMappings()
        {
            foreach (KeyValuePair<string, string> entry in ClientList)
            {
                Console.WriteLine($"Client {entry.Key} with URL: {entry.Value}");
            }
            foreach (KeyValuePair<string, List<String>> entry in DataCenter)
            {
                string servers = String.Join(", ", entry.Value.ToArray());
                Console.WriteLine($"Partition {entry.Key} with Servers: {servers}");
            }
        }

        public string GetServerId()
        {
            string id = "";
            foreach (string key in ServerList.Keys)
            {
                if (ServerList[key].Equals(currentServer))
                {
                    id = key;
                }
            }
            return id;
        }


        public void ConnectToServer()
        {
            this.channel = GrpcChannel.ForAddress(currentServer);
            this.client = new ServerService.ServerServiceClient(this.channel);
        }

        public void SetCurrentServer(string server)
        {
            this.currentServer = server;
        }

        public void ParseInputFile()
        {
            String[] line = new String[lines.Length];
            Thread[] array = new Thread[lines.Length];

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

                    if (line[0] == "begin-repeat")
                    {
                        count = BeginRepeat(i, int.Parse(line[1]));
                        i = i + count;
                        Console.WriteLine("end-repeat");

                    }
                    else { SwitchCase(line, -1); }

                    /*
                   array[i] = new Thread(() => switchCase(line, i));
                   array[i].Start();
                   */
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
                case "wait":
                    Wait(int.Parse(line[1]));
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
        public ReadResponse Read(string partitionId, string objectId, string server_id, int beginRepeat)
        {
            UniqueKey uniqueKey = new UniqueKey();
            uniqueKey.PartitionId = partitionId;
            uniqueKey.ObjectId = objectId;

            ReadResponse response = client.Read(new ReadRequest
            {
                UniqueKey = uniqueKey,
                ServerId = server_id
            });

            if (response.Value.Equals("N/A") && !server_id.Equals("-1"))
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

            return response;
        }

        
        public void CheckMaster(string partitionId, string objectId, string value, int beginRepeat)
        {
            string server_id = GetServerId();

            lock (DataCenter)
            {
                if (!DataCenter[partitionId][0].Equals(server_id))
                {
                    server_id = DataCenter[partitionId][0];
                    SetCurrentServer(ServerList[server_id]);
                    ConnectToServer();
                }
            }
            Write(partitionId, objectId, value, beginRepeat);
        }
        public WriteResponse Write(string partitionId, string objectId, string value, int beginRepeat)
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

            return response;

        }
        public void ListServer(string server_id, int beginRepeat)
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
        public void ListGlobal(int beginRepeat)
        {

            ListGlobalResponse response = client.ListGlobal(new ListGlobalRequest { });
            foreach (UniqueKey server in response.UniqueKeyList)
            {

                string output = $" partitionId {server.PartitionId} " +
                                $"objectId {server.ObjectId} ";

                if (beginRepeat != -1)
                {
                    if (output.Contains("$i"))
                    {
                        output = CheckReplace(output, beginRepeat);
                    }
                }
                Console.WriteLine(output);
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
                    for (int j = 1; j < x+1; j++)
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
            lock(DataCenter)
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

                                Console.WriteLine("Mappings received");

                                client.ParseInputFile();

                                Console.WriteLine("Input file executed.");

                                break;
                            }
                            Thread.Sleep(25); 
                        }

                        while (true){ Thread.Sleep(1000); }  // Avoid consuming a lot of processing power                 

                    } else
                    {
                        Console.WriteLine("Received invalid arguments.");
                        Console.ReadKey();
                    }
                    
                } else
                {
                    Console.WriteLine("No arguments received.");
                    Console.ReadKey();
                }
            }
        }
    }
}

    