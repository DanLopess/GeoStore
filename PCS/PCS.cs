using Grpc.Net.Client;
using Grpc.Core;
using PCS.services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Collections;
using System.Linq;
using System.IO;

namespace PCS
{
    public class PCSServerLogic
    {
        private List<Process> clientsAndServers;
        private string clientFilename = "client.exe";
        private string serverFilename = "server.exe";


        public PCSServerLogic(){
            clientsAndServers = new List<Process>();
        }

        public StartClientProcessReply StartClient(StartClientProcessRequest request)
        {
            if (File.Exists(serverFilename) && File.Exists(request.ScriptFilename))
            {
                // Prepare the process to run
                ProcessStartInfo start = new ProcessStartInfo();

                List<string> arguments = new List<string> { request.Username, request.Url, request.ScriptFilename };
                string argumentString = string.Join(" ", arguments.Select(x => x.ToString()).ToArray());
                start.Arguments = argumentString;
                start.FileName = clientFilename;
                start.WindowStyle = ProcessWindowStyle.Normal;
                start.UseShellExecute = true;
                start.CreateNoWindow = false;

                //start.CreateNoWindow = true;  === In case of needing to hide window

                // Run the external process
                using Process proc = Process.Start(start);
                clientsAndServers.Add(proc);
                Console.WriteLine($"Started new client with Username:{request.Username} and URL:{request.Url}");

                return new StartClientProcessReply
                {
                    Ok = true
                };
            }
            {
                Console.WriteLine("Could not run new client process.");
                return new StartClientProcessReply
                {
                    Ok = false
                };
            }
        }

        public StartServerProcessReply StartServer(StartServerProcessRequest request)
        {
            if (File.Exists(serverFilename))
            {
                // Prepare the process to run
                ProcessStartInfo start = new ProcessStartInfo();

                List<string> arguments = new List<string> { request.ServerId,
                request.Url, request.MinDelay, request.MaxDelay };
                string argumentString = String.Join(" ", arguments);
                start.Arguments = argumentString;
                start.FileName = serverFilename;
                start.WindowStyle = ProcessWindowStyle.Normal;
                start.UseShellExecute = true;
                start.CreateNoWindow = false;

                //start.CreateNoWindow = true;  === In case of needing to hide window

                // Run the external process
                using Process proc = Process.Start(start);
                clientsAndServers.Add(proc);

                Console.WriteLine($"Started new server with serverId: {request.ServerId} and URL: {request.Url}");

                return new StartServerProcessReply
                {
                    Ok = true
                };
            } else
            {
                Console.WriteLine("Could not run new server process.");
                return new StartServerProcessReply
                {
                    Ok = false
                };
            }
        }

        public void TerminateAllProcesses()
        {
            clientsAndServers.ForEach(p =>
            {
                p.Kill();
                p.WaitForExit();
            });
        }

        public void SetServerFilename(string filename) { this.serverFilename = filename; }
        public void SetClientFilename(string filename) { this.clientFilename = filename; }
    }

    class Program
    {
        private const int PCSPort = 10000;
        private const string hostname = "localhost";

        static void Main(string[] args)
        {
            string startupMessage;
            ServerPort serverPort;

            Console.Write("Enter server executable path: ");
            string serverPath = Console.ReadLine();
            Console.Write("Enter client executable path: ");
            string clientPath = Console.ReadLine();

            serverPort = new ServerPort(hostname, PCSPort, ServerCredentials.Insecure);
            startupMessage = "Insecure server listening on port " + PCSPort;

            PCSServerLogic logic = new PCSServerLogic();
            logic.SetClientFilename(clientPath);
            logic.SetServerFilename(serverPath);

            ServerService serverService = new ServerService(logic);

            Server server = new Server
            {
                Services = { PCSServerService.BindService(serverService) },
                Ports = { serverPort }
            };

            server.Start();

            Console.WriteLine(startupMessage);

            //Configuring HTTP for client connections in Register method
            AppContext.SetSwitch(
            "System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
            
            Console.WriteLine("Press any key to stop the PCS");
            Console.ReadKey();

            server.ShutdownAsync().Wait();
            logic.TerminateAllProcesses();
        }
    }
}
