using Grpc.Core;
using Grpc.Net.Client;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PCS.services
{
    class ServerService : PCSServerService.PCSServerServiceBase
    {
        private GrpcChannel channel;
        private PCSServerLogic logic;

        public ServerService(PCSServerLogic serverLogic) {
            this.logic = serverLogic;
        }

        // ==== Overrides ====
        public override Task<StartClientProcessReply> StartClientProcess(
            StartClientProcessRequest request, ServerCallContext context)
        {
            return Task.FromResult(StartClient(request));
        }

        public override Task<StartServerProcessReply> StartServerProcess(
        StartServerProcessRequest request, ServerCallContext context)
        {
            return Task.FromResult(StartServer(request));
        }

        // ==== Implementation ====
        public StartClientProcessReply StartClient(StartClientProcessRequest request)
        {
            channel = GrpcChannel.ForAddress(request.Url);

            try
            {
                lock (this)
                {
                    logic.StartClient(request);
                }
            } catch (Exception e)
             {
                Console.WriteLine("Some error ocurred while starting client process: {}", e);
                return new StartClientProcessReply
                {
                    Ok = false
                };
            }
            
            return new StartClientProcessReply
            {
                Ok = true
            };
        }

        public StartServerProcessReply StartServer(StartServerProcessRequest request)
        {
            channel = GrpcChannel.ForAddress(request.Url);

            try
            {
                lock (this)
                {
                    logic.StartServer(request);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Some error ocurred while starting server process: {}", e);
                return new StartServerProcessReply
                {
                    Ok = false
                };
            }

            return new StartServerProcessReply
            {
                Ok = true
            };
        }
    }
}





