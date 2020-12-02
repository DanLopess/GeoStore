using Grpc.Net.Client;
using PuppetMasterGUI.exceptions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;


namespace PuppetMasterGUI
{
    public class PMLogic
    {
        private const string PCSPort = "10000";
        private static Exception _Exception = null;
        private string ReplicationFactor;
        private Dictionary<string, string> ServerMapping; // <server_id, url>
        private Dictionary<string, string> ClientMapping; // <client_username, url>
        private Dictionary<string, List<string>> PartitionMapping; // <partition_id, server_id>
        private Dictionary<string, string> CrashedNodes; // name/id , url
        private List<string> PCSList;
        public string ScriptFilename { get; set; }

        // LOCKS
        private readonly object RFLock = new object();
        private readonly object ExceptLock = new object();
        private readonly object ServerMapsLock = new object();
        private readonly object ClientMapsLock = new object();
        private readonly object PartMapsLock = new object();
        private readonly object CrashedNodesLock = new object();
        private readonly object PCSListLock = new object();


        public PMLogic() {
            PCSList = new List<string>();
            ServerMapping = new Dictionary<string, string>();
            ClientMapping = new Dictionary<string, string>();
            CrashedNodes = new Dictionary<string, string>();
            PartitionMapping = new Dictionary<string, List<string>>();
            AppContext.SetSwitch(
            "System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
        }

        /// <summary>
        /// Method for executing all of the commands in a given script file
        /// </summary>
        public async void RunScript()
        {
            List<string> commands = ReadFileLines(ScriptFilename);

            List<Task> tasks = new List<Task>();

            commands.ForEach(command =>
            {
                if (ContainsCommandIgnoreCase(command, "Wait"))
                    SleepCommand(command);
                else
                {
                    Task task = Task.Run(() => ExecuteCommand(command));
                    tasks.Add(task);
                }
            }
            );

            await Task.WhenAll(tasks);

            // Rethrow last exception thrown in Tasks
            if (_Exception != null)
            {
                throw _Exception;
            }

            SendMappingsToAll();            
        }

        /// <summary>
        /// Method for executing all possible commands in a given <c>script</c> file.
        /// Each command, apart from the wait command will execute in a new Thread.
        /// </summary>
        /// <seealso cref="RunScript"/>
        /// <param name="command"></param>
        public void ExecuteCommand(string command)
        {

            if (ContainsCommandIgnoreCase(command, "ReplicationFactor")) 
            {
                SetReplicationFactor(command);
            } 
            else if (ContainsCommandIgnoreCase(command, "Client")) 
            {
                SendStartClientCommand(command);
            }
            else if (ContainsCommandIgnoreCase(command, "Partition"))
            {
                CreatePartitionCommand(command);
            }
            else if (ContainsCommandIgnoreCase(command, "Server"))
            {
                SendStartServerCommand(command);
            }
            else if (ContainsCommandIgnoreCase(command, "Status"))
            {
                SendStatusCommand();
            }
            else if (ContainsCommandIgnoreCase(command, "Freeze"))
            {
                SendFreezeCommand(command);
            }
            else if (ContainsCommandIgnoreCase(command, "Unfreeze"))
            {
                SendUnfreezeCommand(command);
            }
            else if (ContainsCommandIgnoreCase(command, "Crash"))
            {
                SendCrashCommand(command);
            }
            else
            {
                lock (ExceptLock) 
                    _Exception = new InvalidCommandException("The specified command is not valid. \nCommand: " + command);
            }
        }

        // ===== Commands =====

        private void SetReplicationFactor(string command)
        {
            string[] splittedCommand = command.Split(" ");
            if (splittedCommand.Length == 2)
            {
                lock (RFLock) { ReplicationFactor = splittedCommand[1]; }
            } else
            {
                lock (ExceptLock)
                    _Exception = new InvalidCommandException("The specified command is not valid. \nCommand: " + command);
            }
        }

        /// <summary>
        /// Method for sending a command to a PCS terminal that will initiate a new Client process
        /// </summary>
        /// <param name="command"></param>
        private void SendStartClientCommand(string command)
        {
            string[] splittedCommand = command.Split(" ");
            
            if (splittedCommand.Length == 4)
            {
                string url = GetPCSUrlFromCommand(splittedCommand[2]);
                AddPCSToList(url);
                if (url != null)
                {
                    GrpcChannel channel = GrpcChannel.ForAddress(url);
                    PCSServerService.PCSServerServiceClient client = new PCSServerService.PCSServerServiceClient(channel);
                    StartClientProcessRequest request = new StartClientProcessRequest
                    {
                        Username = splittedCommand[1],
                        Url = splittedCommand[2],
                        ScriptFilename = splittedCommand[3]
                    };

                    try
                    {
                        StartClientProcessReply reply = client.StartClientProcess(request);
                        if (reply.Ok)
                        {
                            // Only add to mappings if node started correctly
                            lock (ClientMapsLock) { ClientMapping.TryAdd(splittedCommand[1], splittedCommand[2]); }
                        }
                        else
                        {
                            lock (ExceptLock)
                                _Exception = new PCSNotOKException("PCS returned a NOT OK message.");
                        }
                    } catch (Exception e)
                    {
                        // TODO node is down, do something abt it
                    }
                    
                }
            }
            else
            {
                lock (ExceptLock)
                    _Exception = new InvalidCommandException("The specified command is not valid. \nCommand: " + command);
            }
        }

        /// <summary>
        /// Method for sending a command to a PCS terminal that will initiate a new Server process
        /// </summary>
        /// <param name="command">Server s2 http://localhost:4000 100 300</param>
        private void SendStartServerCommand(string command)
        {
            string[] splittedCommand = command.Split(" ");
            if (splittedCommand.Length == 5)
            {
                string url = GetPCSUrlFromCommand(splittedCommand[2]);
                AddPCSToList(url);
                if (url != null) { 
                    GrpcChannel channel = GrpcChannel.ForAddress(url);
                    PCSServerService.PCSServerServiceClient client = new PCSServerService.PCSServerServiceClient(channel);
                    StartServerProcessRequest request = new StartServerProcessRequest
                    {
                        ServerId = splittedCommand[1],
                        Url = splittedCommand[2],
                        MinDelay = splittedCommand[3],
                        MaxDelay = splittedCommand[4]
                    };

                    try
                    {
                        StartServerProcessReply reply = client.StartServerProcess(request);
                        if (reply.Ok)
                        {
                            lock (ServerMapsLock) { ServerMapping.TryAdd(splittedCommand[1], splittedCommand[2]); }
                            // Only add to mappings if node started correctly
                        }
                        else
                        {
                            lock (ExceptLock)
                                _Exception = new PCSNotOKException("PCS returned a NOT OK message.");
                        }
                    } catch (Exception e)
                    {
                        // TODO PCS is down, do something about it
                    }
                }
            }
            else
            {
                lock (ExceptLock)
                    _Exception = new InvalidCommandException("The specified command is not valid. \nCommand: " + command);
            }
        }

        /// <summary>
        /// Method for creating a new partition
        /// </summary>
        /// <param name="command"></param>
        private void CreatePartitionCommand(string command)
        {
            string[] splittedCommand = command.Split(" ");
            if (splittedCommand.Length == int.Parse(splittedCommand[1]) + 3) // 3 corresponds to Partition + r + partition_id (1+1+1)
            {
                List<string> replicas = new List<string>();
                string partitionId = splittedCommand[2];
                for (int i = 3; i < splittedCommand.Length; i++)
                {
                    replicas.Add(splittedCommand[i]);
                }

                lock(PartMapsLock) { PartitionMapping.TryAdd(partitionId, replicas); }
            }
            else
            {
                lock (ExceptLock)
                    _Exception = new InvalidCommandException("The specified command is not valid. \nCommand: " + command);
            }
        }

        /// <summary>
        /// Method for sending a command to a node to obtain its Status
        /// </summary>
        /// <param name="command"></param>
        public void SendStatusCommand()
        {
            foreach (KeyValuePair<string, string> entry in ServerMapping)
            {
                SendStatusRequest(entry.Value);
            }

            foreach (KeyValuePair<string, string> entry in ClientMapping)
            {
                SendStatusRequest(entry.Value);
            }
        }

        /// <summary>
        /// Method for sending a command to a server process and terminate it
        /// </summary>
        /// <param name="command"></param>
        public void SendCrashCommand(string command)
        {
            string[] splittedCommand = command.Split(" ");
            if (splittedCommand.Length == 2)
            {
                string serverId = splittedCommand[1];
                string url = GetServerUrl(serverId);
                if (url != null)
                {
                    GrpcChannel channel = GrpcChannel.ForAddress(url);
                    PuppetService.PuppetServiceClient client = new PuppetService.PuppetServiceClient(channel);
                    ChangeServerStateRequest request = new ChangeServerStateRequest
                    {
                        State = ServerState.Crash
                    };

                    try
                    {
                        client.ChangeServerState(request);
                        DeleteServerFromMappings(serverId, url);
                    } catch (Exception e)
                    {
                        // If the node crashed, we definitely want to remove it from the mappings
                        DeleteServerFromMappings(serverId, url);
                    }
                }
            }
            else
            {
                lock (ExceptLock)
                    _Exception = new InvalidCommandException("The specified command is not valid. \nCommand: " + command);
            }
        }

        /// <summary>
        /// Method for sending a command to a server and freeze it (lock)
        /// </summary>
        /// <param name="command"></param>
        public void SendFreezeCommand(string command)
        {
            string[] splittedCommand = command.Split(" ");
            if (splittedCommand.Length == 2)
            {
                string serverId = splittedCommand[1];
                string url = GetServerUrl(serverId);
                if (url != null)
                {
                    GrpcChannel channel = GrpcChannel.ForAddress(url);
                    PuppetService.PuppetServiceClient client = new PuppetService.PuppetServiceClient(channel);
                    ChangeServerStateRequest request = new ChangeServerStateRequest
                    {
                        State = ServerState.Freeze
                    };

                    try
                    {
                        client.ChangeServerState(request);
                    }
                    catch (Exception e)
                    {
                        // If the node crashed, we definitely want to remove it from the mappings
                        DeleteServerFromMappings(serverId, url);
                    }
                }
            }
            else
            {
                lock (ExceptLock)
                    _Exception = new InvalidCommandException("The specified command is not valid. \nCommand: " + command);
            }
        }

        /// <summary>
        /// Method for sending a command to a server and unfreeze it (unlock)
        /// </summary>
        /// <param name="command"></param>
        public void SendUnfreezeCommand(string command)
        {
            string[] splittedCommand = command.Split(" ");
            if (splittedCommand.Length == 2)
            {
                string serverId = splittedCommand[1];
                string url = GetServerUrl(serverId);
                if (url != null)
                {
                    GrpcChannel channel = GrpcChannel.ForAddress(url);
                    PuppetService.PuppetServiceClient client = new PuppetService.PuppetServiceClient(channel);
                    ChangeServerStateRequest request = new ChangeServerStateRequest
                    {
                        State = ServerState.Unfreeze
                    };

                    try
                    {
                        client.ChangeServerState(request);
                    } catch (Exception e)
                    {
                        // If the node crashed, we definitely want to remove it from the mappings
                        DeleteServerFromMappings(serverId, url);
                    }
                }
            }
            else
            {
                lock (ExceptLock)
                    _Exception = new InvalidCommandException("The specified command is not valid. \nCommand: " + command);
            }
        }

        /// <summary>
        /// Method for executing the wait command
        /// </summary>
        /// <param name="command"></param>
        private void SleepCommand(string command)
        {
            string[] splittedCommand = command.Split(" ");
            if (splittedCommand.Length == 2)
            {
                try
                {
                    int milliseconds = int.Parse(splittedCommand[1]);
                    Thread.Sleep(milliseconds);
                } catch
                {
                    lock (ExceptLock)
                        _Exception = new InvalidCommandException("Wait command with invalid time.\nCommand: " + command);
                }
            }
            else
            {
                lock (ExceptLock)
                    _Exception = new InvalidCommandException("The specified command is not valid. \nCommand: " + command);
            }
        }

        /// <summary>
        /// Method for sending all of the nodes and partitions mappings to everyone
        /// </summary>
        /// <param name="command"></param>
        private void SendMappingsToAll()
        {
            try
            {
                // First Send to Server
                lock(ServerMapsLock)
                {
                    foreach (KeyValuePair<string, string> entry in ServerMapping)
                    {
                        SendMapping(entry.Value);
                    }
                }

                // Then Send to Client
                lock (ClientMapsLock)
                {
                    foreach (KeyValuePair<string, string> entry in ClientMapping)
                    {
                        SendMapping(entry.Value);
                    }
                }
            }
            catch (Exception e)
            {
                lock (ExceptLock)
                    _Exception = new SendMappingsException("Failed to send mappings.\nCause: " + e.Message);
            }
        }


        // ===== Auxiliary methods ====
        private void SendStatusRequest(string url)
        {
            GrpcChannel channel = GrpcChannel.ForAddress(url);
            PuppetService.PuppetServiceClient client = new PuppetService.PuppetServiceClient(channel);
            GetNodeStatusRequest request = new GetNodeStatusRequest();
            try
            {
                GetNodeStatusReply reply = client.GetStatus(request);
            }
            catch (Exception e)
            {
                // TODO node is down, do something about it 
            }
        }

        private void SendMapping(string url)
        {
            GrpcChannel channel = GrpcChannel.ForAddress(url);
            PuppetService.PuppetServiceClient client = new PuppetService.PuppetServiceClient(channel);

            try
            {
                SendMappingsReply reply = client.SendMappings(BuildMappingMessageRequest());
                if (!reply.Ok)
                {
                    lock (ExceptLock)
                        _Exception = new SendMappingsException("Reply was NOT OK. Node: " + url);
                    // TODO decide what to do when a node fails to get the mappings
                }
            } catch (Exception e)
            {
                // TODO node is down do something about 
            }
            
        }

        private SendMappingsRequest BuildMappingMessageRequest()
        {
            SendMappingsRequest request = new SendMappingsRequest
            {
                ReplicationFactor = ReplicationFactor
            };
            request.ClientMapping.AddRange(BuildClientMappingMessages());
            request.ServerMapping.AddRange(BuildServerMappingMessages());
            request.PartitionMapping.AddRange(BuildPartitionMappingMessages());

            return request;
        }

        private List<ServerMapping> BuildServerMappingMessages()
        {
            List<ServerMapping> servers = new List<ServerMapping>();

            lock(ServerMapsLock)
            {
                foreach (KeyValuePair<string, string> entry in ServerMapping)
                {
                    servers.Add(new ServerMapping { ServerId = entry.Key, Url = entry.Value });
                }
            }

            return servers;
        }

        private List<ClientMapping> BuildClientMappingMessages()
        {
            List<ClientMapping> clients = new List<ClientMapping>();

            lock(ClientMapsLock)
            {
                foreach (KeyValuePair<string, string> entry in ClientMapping)
                {
                    clients.Add(new ClientMapping { Username = entry.Key, Url = entry.Value });
                }
            }
            return clients;
        }

        private List<PartitionMapping> BuildPartitionMappingMessages()
        {
            List<PartitionMapping> partitions = new List<PartitionMapping>();

            lock(PartMapsLock)
            {
                foreach (KeyValuePair<string, List<string>> entry in PartitionMapping)
                {
                    PartitionMapping partition = new PartitionMapping { PartitionId = entry.Key };
                    partition.ServerId.AddRange(entry.Value);
                    partitions.Add(partition);
                }
            }
            return partitions;
        }

        private List<string> ReadFileLines(string filename)
        {
            List<string> lines = new List<string>();
            string line;

            System.IO.StreamReader file =
            new System.IO.StreamReader(filename);
            while ((line = file.ReadLine()) != null)
            {
                lines.Add(line);
            }

            file.Close();

            return lines;
        }

        private bool ContainsCommandIgnoreCase(string command, string expected)
        {
            return command.IndexOf(expected, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private string GetPCSUrlFromCommand(string url)
        {
            if (url == null) return null;

            string[] splittedURL = url.Split(":");
            
            if (splittedURL.Length.Equals(3)) 
                return splittedURL[0] + ':' + splittedURL[1] + ':' + PCSPort; // build http://localhost:10000 for example
            else 
                return null;
        }

        private string GetServerUrl(string serverId)
        {
            lock(ServerMapsLock)
                return ServerMapping.GetValueOrDefault(serverId);
        }

        public List<string> GetServerIdsList()
        {
            List<string> servers = new List<string>();

            lock(ServerMapsLock)
            {
                foreach (KeyValuePair<string, string> entry in ServerMapping)
                {
                    servers.Add(entry.Key);
                }
            }
            return servers;
        }

        private void DeleteServerFromMappings(string serverId, string serverUrl)
        {
            lock (ServerMapsLock) { ServerMapping.Remove(serverId); }
            lock (CrashedNodesLock) { CrashedNodes.TryAdd(serverId, serverUrl); }
            DeleteServerFromPartitions(serverId);
            SendMappingsToAll();
        }

        private void DeleteClientFromMappings(string clientId)
        {
            lock (ClientMapsLock) { ClientMapping.Remove(clientId); }
            SendMappingsToAll();
        }

        private void DeleteServerFromPartitions(string serverId)
        {
            lock (PartMapsLock)
            {
                foreach (KeyValuePair<string, List<string>> entry in PartitionMapping)
                {
                    List<string> servers = entry.Value;
                    int index = servers.FindIndex(server => server.Equals(serverId));

                    if (index != -1)
                        servers.RemoveAt(index);

                    // TODO create validation, if number of servers less than replication factor, start new server
                }
            }
        }

        private void AddPCSToList(string url)
        {
            lock (PCSListLock)
            {
                bool pcsInList = PCSList.Exists(pcs => pcs.Equals(url));

                if (!pcsInList)
                {
                    PCSList.Add(url);
                }
            }
        }
    }
}
