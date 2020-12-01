﻿using Grpc.Net.Client;
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
        private string replicationFactor;
        private Dictionary<string, string> serverMapping; // <server_id, url>
        private Dictionary<string, string> clientMapping; // <client_username, url>
        private Dictionary<string, List<string>> partitionsMapping; // <partition_id, server_id>
        public string scriptFilename { get; set; }

        public PMLogic() {
            serverMapping = new Dictionary<string, string>();
            clientMapping = new Dictionary<string, string>();
            partitionsMapping = new Dictionary<string, List<string>>();
            AppContext.SetSwitch(
            "System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
        }

        /// <summary>
        /// Method for executing all of the commands in a given script file
        /// </summary>
        public void RunScript()
        {
            List<string> commands = ReadFileLines(scriptFilename);

            commands.ForEach(command => ExecuteCommand(command));

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
            List<Task> tasks = new List<Task>();

            if (ContainsCommandIgnoreCase(command, "Wait")) 
            {
                SleepCommand(command);
            } else if (ContainsCommandIgnoreCase(command, "ReplicationFactor")) 
            {
                Task task = Task.Run(() => SetReplicationFactor(command));
                tasks.Add(task);
            } else if (ContainsCommandIgnoreCase(command, "Client")) {
                Task task = Task.Run(() => SendStartClientCommand(command));
                tasks.Add(task);
            }
            else if (ContainsCommandIgnoreCase(command, "Partition"))
            {
                Task task = Task.Run(() => CreatePartitionCommand(command));
                tasks.Add(task);
            }
            else if (ContainsCommandIgnoreCase(command, "Server"))
            {
                Task task = Task.Run(() => SendStartServerCommand(command));
                tasks.Add(task);
            }
            else if (ContainsCommandIgnoreCase(command, "Status"))
            {
                Task task = Task.Run(() => SendStatusCommand());
                tasks.Add(task);
            }
            else if (ContainsCommandIgnoreCase(command, "Freeze"))
            {
                Task task = Task.Run(() => SendFreezeCommand(command));
                tasks.Add(task);
            }
            else if (ContainsCommandIgnoreCase(command, "Unfreeze"))
            {
                Task task = Task.Run(() => SendUnfreezeCommand(command));
                tasks.Add(task);
            }
            else if (ContainsCommandIgnoreCase(command, "Crash"))
            {
                Task task = Task.Run(() => SendCrashCommand(command));
                tasks.Add(task);
            }
            else
            {
                throw new InvalidCommandException("The specified command is not valid. \nCommand: " + command);
            }

            if (tasks.Count > 0)
            {
                foreach (Task t in tasks)
                {
                    t.Wait();
                }

                // Rethrow exceptions thrown in Tasks
                if (_Exception != null)
                {
                    throw _Exception;
                }
            }
        }

        // ===== Commands =====

        private void SetReplicationFactor(string command)
        {
            string[] splittedCommand = command.Split(" ");
            if (splittedCommand.Length == 2)
            {
                lock (this) { replicationFactor = splittedCommand[1]; }
            } else
            {
                throw new InvalidCommandException("The specified command is not valid. \nCommand: " + command);
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
                    StartClientProcessReply reply = client.StartClientProcess(request);

                    if (reply.Ok)
                    {
                        lock (this) { clientMapping.TryAdd(splittedCommand[1], splittedCommand[2]); }
                        // Only add to mappings if node started correctly
                    } else
                    {
                        _Exception = new PCSNotOKException("PCS returned a NOT OK message.");
                    }
                }
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
                    StartServerProcessReply reply = client.StartServerProcess(request);

                    if (reply.Ok)
                    {
                        lock (this) { serverMapping.TryAdd(splittedCommand[1], splittedCommand[2]); }
                        // Only add to mappings if node started correctly
                    }
                    else
                    {
                        _Exception = new PCSNotOKException("PCS returned a NOT OK message.");
                    }
                }
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

                lock(this) { partitionsMapping.TryAdd(partitionId, replicas); }
            }
        }

        /// <summary>
        /// Method for sending a command to a node to obtain its Status
        /// </summary>
        /// <param name="command"></param>
        public void SendStatusCommand()
        {
            // TODO Send status, ignore reply for now, eventually show the status response...
            foreach (KeyValuePair<string, string> entry in serverMapping)
            {
                GetNodeStatusReply reply = SendStatusRequest(entry.Value);
            }

            foreach (KeyValuePair<string, string> entry in clientMapping)
            {
                GetNodeStatusReply reply = SendStatusRequest(entry.Value);
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
                string url = GetPCSUrlFromCommand(splittedCommand[1]);
                if (url != null)
                {
                    GrpcChannel channel = GrpcChannel.ForAddress(GetPCSUrlFromCommand(url));
                    PuppetService.PuppetServiceClient client = new PuppetService.PuppetServiceClient(channel);
                    ChangeServerStateRequest request = new ChangeServerStateRequest
                    {
                        State = ServerState.Crash
                    };

                    client.ChangeServerState(request);
                }
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
                string url = GetPCSUrlFromCommand(splittedCommand[1]);
                if (url != null)
                {
                    GrpcChannel channel = GrpcChannel.ForAddress(GetPCSUrlFromCommand(url));
                    PuppetService.PuppetServiceClient client = new PuppetService.PuppetServiceClient(channel);
                    ChangeServerStateRequest request = new ChangeServerStateRequest
                    {
                        State = ServerState.Freeze
                    };

                    client.ChangeServerState(request);
                }
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
                string url = GetPCSUrlFromCommand(splittedCommand[1]);
                if (url != null)
                {
                    GrpcChannel channel = GrpcChannel.ForAddress(GetPCSUrlFromCommand(url));
                    PuppetService.PuppetServiceClient client = new PuppetService.PuppetServiceClient(channel);
                    ChangeServerStateRequest request = new ChangeServerStateRequest
                    {
                        State = ServerState.Unfreeze
                    };

                    client.ChangeServerState(request);
                }
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
                    throw new InvalidCommandException("Wait command with invalid time.\nCommand: " + command);
                }
            }
            else
            {
                throw new InvalidCommandException("The specified command is not valid. \nCommand: " + command);
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
                foreach (KeyValuePair<string, string> entry in serverMapping)
                {
                    SendMapping(entry.Value);
                }

                // Then Send to Client
                foreach (KeyValuePair<string, string> entry in clientMapping)
                {
                    SendMapping(entry.Value);
                }
            }
            catch (Exception e)
            {
                throw new SendMappingsException("Failed to send mappings.\nCause: " + e.Message);
            }
        }


        // ===== Auxiliary methods ====
        private GetNodeStatusReply SendStatusRequest(string url)
        {
            GrpcChannel channel = GrpcChannel.ForAddress(url);
            PuppetService.PuppetServiceClient client = new PuppetService.PuppetServiceClient(channel);
            GetNodeStatusRequest request = new GetNodeStatusRequest();
            return client.GetStatus(request);
        }

        private void SendMapping(string url)
        {
            GrpcChannel channel = GrpcChannel.ForAddress(url);
            PuppetService.PuppetServiceClient client = new PuppetService.PuppetServiceClient(channel);
            SendMappingsReply reply = client.SendMappings(BuildMappingMessageRequest());
            
            if (!reply.Ok)
            {
                throw new SendMappingsException("Reply was NOT OK. Node: " + url);
                // TODO decide what to do when a node fails to get the mappings
            }
        }

        private SendMappingsRequest BuildMappingMessageRequest()
        {
            SendMappingsRequest request = new SendMappingsRequest
            {
                ReplicationFactor = replicationFactor
            };
            request.ClientMapping.AddRange(BuildClientMappingMessages());
            request.ServerMapping.AddRange(BuildServerMappingMessages());
            request.PartitionMapping.AddRange(BuildPartitionMappingMessages());

            return request;
        }

        private List<ServerMapping> BuildServerMappingMessages()
        {
            List<ServerMapping> servers = new List<ServerMapping>();

            foreach (KeyValuePair<string, string> entry in serverMapping)
            {
                servers.Add(new ServerMapping { ServerId = entry.Key, Url = entry.Value });
            }
            return servers;
        }

        private List<ClientMapping> BuildClientMappingMessages()
        {
            List<ClientMapping> clients = new List<ClientMapping>();

            foreach (KeyValuePair<string, string> entry in clientMapping)
            {
                clients.Add(new ClientMapping { Username = entry.Key, Url = entry.Value });
            }
            return clients;
        }

        private List<PartitionMapping> BuildPartitionMappingMessages()
        {
            List<PartitionMapping> partitions = new List<PartitionMapping>();

            foreach (KeyValuePair<string, List<string>> entry in partitionsMapping)
            {
                PartitionMapping partition = new PartitionMapping { PartitionId = entry.Key };
                partition.ServerId.AddRange(entry.Value);
                partitions.Add(partition);
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
            return serverMapping.GetValueOrDefault(serverId);
        }

        public List<string> GetServerIdsList()
        {
            List<string> servers = new List<string>();

            foreach (KeyValuePair<string, string> entry in serverMapping)
            {
                servers.Add(entry.Key);
            }

            return servers;
        }
    }
}