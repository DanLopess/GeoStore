﻿using Grpc.Net.Client;
using Grpc.Core;
using PCS.services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Collections;
using System.Linq;

namespace PCS
{
    public class PCSServerLogic
    {
        private List<Process> clientsAndServers;
        private const string clientFilename = "client.exe";
        private const string serverFilename = "server.exe";


        public PCSServerLogic(){
            clientsAndServers = new List<Process>();
        }

        public void StartClient(StartClientProcessRequest request)
        {
            // Prepare the process to run
            ProcessStartInfo start = new ProcessStartInfo();

            List<string> arguments = new List<string> { request.Username, request.Url, request.ScriptFilename };
            string argumentString = string.Join(" ", arguments.Select(x => x.ToString()).ToArray());
            start.Arguments = argumentString;
            start.FileName = clientFilename;
            start.WindowStyle = ProcessWindowStyle.Normal;

            //start.CreateNoWindow = true;  === In case of needing to hide window

            // Run the external process
            using Process proc = Process.Start(start);
            clientsAndServers.Add(proc);
        }

        public void StartServer(StartServerProcessRequest request)
        {
            // Prepare the process to run
            ProcessStartInfo start = new ProcessStartInfo();

            List<string> arguments = new List<string> { request.ServerId.ToString(), 
                request.Url, request.MinDelay.ToString(), request.MaxDelay.ToString() };
            string argumentString = string.Join(" ", arguments.Select(x => x.ToString()).ToArray());
            start.Arguments = argumentString;
            start.FileName = serverFilename;
            start.WindowStyle = ProcessWindowStyle.Normal;

            //start.CreateNoWindow = true;  === In case of needing to hide window

            // Run the external process
            using Process proc = Process.Start(start);
            clientsAndServers.Add(proc);
        }

        public void TerminateAllProcesses()
        {
            clientsAndServers.ForEach(p =>
            {
                p.Kill();
                p.WaitForExit();
            });
        }
    }

    class Program
    {
        private const int PCSPort = 10000;
        private const string hostname = "localhost";

        static void Main(string[] args)
        {
            string startupMessage;
            ServerPort serverPort;

            serverPort = new ServerPort(hostname, PCSPort, ServerCredentials.Insecure);
            startupMessage = "Insecure ChatServer server listening on port " + PCSPort;

            PCSServerLogic logic = new PCSServerLogic();
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
