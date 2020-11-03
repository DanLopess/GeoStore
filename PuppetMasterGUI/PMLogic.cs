using System;
using System.Collections.Generic;
using System.Text;

namespace PuppetMasterGUI
{
    public class PMLogic
    {
        // add here mapping for servers and partitions and clients
        private int replicationFactor;
        private Dictionary<String, String> serverMapping; // <server_id, url>
        private Dictionary<String, String> clientMapping; // <client_username, url>
        private Dictionary<String, List<String>> partitionsMapping; // <partition_id, server_id>
        public string scriptFilename { get; set; }
        
        
        public PMLogic() { }

        public void RunScript()
        {
            
        }

        public void SendStatusCommand()
        {

        }

        public void SendCrashCommand()
        {

        }

        public void SendFreezeCommand()
        {

        }
        public void SendUnfreezeCommand()
        {

        }

        // maybe add method for sending mappings the first time, to synchronize everything?
    }
}
