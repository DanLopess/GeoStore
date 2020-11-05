using Grpc.Net.Client;
using PuppetMasterGUI.exceptions;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace PuppetMasterGUI
{
    public class PMLogic
    {
        private string replicationFactor;
        private Dictionary<string, string> serverMapping; // <server_id, url>
        private Dictionary<string, string> clientMapping; // <client_username, url>
        private Dictionary<string, List<string>> partitionsMapping; // <partition_id, server_id>
        public string scriptFilename { get; set; }

        // TODO LIST
        // 1 - Decide when to send the mappings...
        // 2 - Should I send the mappings to everyone everytime i add a new server/partition/client?
        // 3 - Should I run the script and send the mappings afterwards?
        // 4 - If i choose the 3rd , what happens to the freeze and unfreeze and crash? What if a server crashed?

        public PMLogic() { }

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
            if (ContainsCommandIgnoreCase(command, "Wait")) {
                SleepCommand(command);
            } else if (ContainsCommandIgnoreCase(command, "ReplicationFactor")) {
                Thread thread = new Thread(() => SetReplicationFactor(command));
                thread.Start();
            } else if (ContainsCommandIgnoreCase(command, "Client")) {
                Thread thread = new Thread(() => SendStartClientCommand(command));
                thread.Start();
            }
            else if (ContainsCommandIgnoreCase(command, "Partition"))
            {
                Thread thread = new Thread(() => CreatePartitionCommand(command));
                thread.Start();
            }
            else if (ContainsCommandIgnoreCase(command, "Server"))
            {
                Thread thread = new Thread(() => SendStartServerCommand(command));
                thread.Start();
            }
            else if (ContainsCommandIgnoreCase(command, "Status"))
            {
                Thread thread = new Thread(() => SendStatusCommand(command));
                thread.Start();
            }
            else if (ContainsCommandIgnoreCase(command, "Freeze"))
            {
                Thread thread = new Thread(() => SendFreezeCommand(command));
                thread.Start();
            }
            else if (ContainsCommandIgnoreCase(command, "Unfreeze"))
            {
                Thread thread = new Thread(() => SendUnfreezeCommand(command));
                thread.Start();
            }
            else if (ContainsCommandIgnoreCase(command, "Crash"))
            {
                Thread thread = new Thread(() => SendCrashCommand(command));
                thread.Start();
            }
            else
            {
                throw new InvalidCommandException("The specified command is not valid. \nCommand: " + command);
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
            // make request for creating client
            //  only add client to dictionary, if response = ok
        }

        /// <summary>
        /// Method for sending a command to a PCS terminal that will initiate a new Server process
        /// </summary>
        /// <param name="command"></param>
        private void SendStartServerCommand(string command)
        {

        }

        /// <summary>
        /// Method for sending a command to a node to obtain its Status
        /// </summary>
        /// <param name="command"></param>
        private void SendStatusCommand(string command)
        {

        }

        /// <summary>
        /// Method for sending a command to a server process and terminate it
        /// </summary>
        /// <param name="command"></param>
        private void SendCrashCommand(string command)
        {

        }

        /// <summary>
        /// Method for sending a command to a server and freeze it (lock)
        /// </summary>
        /// <param name="command"></param>
        private void SendFreezeCommand(string command)
        {

        }

        /// <summary>
        /// Method for sending a command to a server and unfreeze it (unlock)
        /// </summary>
        /// <param name="command"></param>
        private void SendUnfreezeCommand(string command)
        {

        }

        /// <summary>
        /// Method for creating a new partition
        /// </summary>
        /// <param name="command"></param>
        private void CreatePartitionCommand(string command)
        {

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
                foreach (KeyValuePair<string, string> entry in serverMapping)
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
    }
}
