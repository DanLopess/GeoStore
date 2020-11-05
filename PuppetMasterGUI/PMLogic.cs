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

        /* Thread thread = new Thread(() => ExecuteCommand(command));
        thread.Start();*/

        public PMLogic() { }

        public void RunScript()
        {
            List<string> commands = ReadFileLines(scriptFilename);

            commands.ForEach(command => ExecuteCommand(command));

            try
            {
                SendMappingsToAll();
            } catch (Exception e)
            {
                throw new SendMappingsException("Failed to send mappings.\nCause: " + e.Message);
            }
            
        }

        public void ExecuteCommand(string command)
        {
            if (command.Contains("ReplicationFactor")) {
                lock(this) { SetReplicationFactor(command); }
            } else if (command.Contains("Client")) {
                //
            } else
            {
                throw new InvalidCommandException("The specified command is not valid. \nCommand: " + command);
            }
                
                
        }

        private void SetReplicationFactor(string command)
        {
            string[] splittedCommand = command.Split(" ");
            if (splittedCommand.Length == 2)
            {
                replicationFactor = splittedCommand[1];
            }
        }

        private void SendStartClientCommand()
        {
            // make request for creating client
            //  only add client to dictionary, if response = ok
        }

        private void SendStartServerCommand()
        {

        }

        private void SendStatusCommand()
        {

        }

        private void SendCrashCommand()
        {

        }

        private void SendFreezeCommand()
        {

        }
        private void SendUnfreezeCommand()
        {

        }

        private void SendMappingsToAll()
        {
            // First Send to Server
            foreach(KeyValuePair<string, string> entry in serverMapping)
            {
                SendMapping(entry.Value);
            }

            // Then Send to Client
            foreach (KeyValuePair<string, string> entry in serverMapping)
            {
                SendMapping(entry.Value);
            }
        }

        private void SendMapping(string url)
        {
            GrpcChannel channel = GrpcChannel.ForAddress(url);
            PuppetService.PuppetServiceClient client = new PuppetService.PuppetServiceClient(channel);
            client.SendMappings(BuildMappingMessageRequest());
        }

        private SendMappingsRequest BuildMappingMessageRequest()
        {
            SendMappingsRequest request = new SendMappingsRequest
            {
                ReplicationFactor = replicationFactor,
                //ServerMapping = 
            };

            return request;
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
    }
}
