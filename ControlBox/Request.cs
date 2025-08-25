using Grpc.Net.Client;
using LibBox;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace ControlBox
{
    public class Request : IDisposable
    {
        GrpcChannel _channel;
        JsonRpc.JsonRpcClient _client;

        public Request(string ip, int port, bool noproxy = true) 
        {
            if (noproxy)
            {
                HttpClient.DefaultProxy = new WebProxy();
            }

            _channel = GrpcChannel.ForAddress($"http://{ip}:{port}", new GrpcChannelOptions()
            {
                MaxSendMessageSize = null,
                MaxReceiveMessageSize = null,
            });

            _client = new JsonRpc.JsonRpcClient(_channel);
        }


        public async Task<ReturnMessage?> Transfer(TransferMessage transferMessage)
        {
            return await CallService<ReturnMessage>(Constant.SystemService.Name, Constant.SystemService.Transfer, MessageHelper.ParamJson(transferMessage));
        }

        public async Task<ReturnMessage?> Install(InstallMessage installMessage)
        {
            return await CallService<ReturnMessage>(Constant.SystemService.Name, Constant.SystemService.Install, MessageHelper.ParamJson(installMessage));
        }

        public async Task<ReturnMessage?> Uninstall(UninstallMessage uninstallMessage)
        {
            return await CallService<ReturnMessage>(Constant.SystemService.Name, Constant.SystemService.Uninstall, MessageHelper.ParamJson(uninstallMessage));
        }

        public async Task<List<ServiceMetaData>?> List()
        {
            return await CallService<List<ServiceMetaData>>(Constant.SystemService.Name, Constant.SystemService.List, string.Empty);
        }

        public async Task<T?> CallService<T>(string serviceName, string functionName, string inputData)
        {
            var rpcMessage = await Call(serviceName, functionName, inputData);
            return JsonConvert.DeserializeObject<T>(rpcMessage?.Data ?? string.Empty);
        }

        public async Task<RpcMessage?> Call(string serviceName, string functionName, string inputData)
        {
            return await Call(new RpcMessage { Service = serviceName, Function = functionName, Data = inputData });
        }


        private async Task<RpcMessage?> Call(RpcMessage rpcMessage)
        {
            var json = await Call(JsonConvert.SerializeObject(rpcMessage));
            return JsonConvert.DeserializeObject<RpcMessage>(json);
        }

        private async Task<string> Call(string input)
        {
            var reply = await _client.CallAsync(new JsonRequest { Content = input });
            return reply.Content;
        }

        public void Dispose()
        {
            _channel.Dispose();
        }
    }
}
