using Grpc.Core;
using Grpc.Net.Client;
using LibBox;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop.Implementation;
using Newtonsoft.Json;
using System;
using System.CodeDom.Compiler;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Text;
using System.Threading.Tasks;

namespace ControlBox
{
    public class ServiceExecutor
    {
        internal class FunctionInfo
        {
            private readonly Func<string, string> _func;
            private readonly Dictionary<string, object> _stats;
            private readonly ReadOnlyDictionary<string, object> _statsforread;
            private Stopwatch? _stopwatch;

            public FunctionInfo(Func<string, string> func)
            {
                _func = func;
                _stats = new Dictionary<string, object>();
                _statsforread = new ReadOnlyDictionary<string, object>(_stats);
            }

            public Func<string, string> Call => _func;
            public ReadOnlyDictionary<string, object> Stats => _statsforread;

            private T GetValue<T>(string key, T defaultValue)
            {
                if (!_stats.TryGetValue(key, out var value))
                {
                    return defaultValue;
                }
                return (T)value;
            }

            public void BeginCall()
            {
                var count = GetValue(Constant.StatNames.Count, 0);
                _stats[Constant.StatNames.Count] = ++count;
                _stopwatch = Stopwatch.StartNew();
            }

            public void EndCall()
            {
                _stopwatch?.Stop();
                var elapsed = _stopwatch?.Elapsed ?? TimeSpan.Zero;
                var total = GetValue(Constant.StatNames.Total, TimeSpan.Zero);
                _stats[Constant.StatNames.Total] = elapsed + total;
            }

            public void CallResult(Exception ex = null)
            {
                _stats[Constant.StatNames.Result] = ex == null ? "OK" : $"{ex}";
            }
        }

        internal class RegisterExecutorInfo
        {
            public string Name { get; set; }
            public IExecutor? Instance { get; set; }
            public Dictionary<string, FunctionInfo> Functions { get; set; }
        }

        internal readonly ConcurrentDictionary<string, RegisterExecutorInfo> _info = [];

        public void RegisterServiceExecutor(IExecutor executor)
        {
            var t = executor.GetType();
            var sattr = t.GetCustomAttribute<ServiceAttribute>();
            if (sattr == null)
                throw new Exception("ServiceAttribute is null");
            var name = sattr.Name;
            var entry = _info.GetOrAdd(name, new RegisterExecutorInfo() { Name = name, Instance = executor, Functions = [] });
                       
            var serviceFunctions = t.GetMethods().Where(m => m.GetCustomAttribute<ServiceFunctionAttribute>() != null);
            foreach (var serviceFunction in serviceFunctions)
            {
                var sfattr = serviceFunction.GetCustomAttribute<ServiceFunctionAttribute>();
                var aname = sfattr!.Name;
                entry.Functions.Add(aname, new FunctionInfo(serviceFunction.CreateDelegate<Func<string, string>>(executor)));
            }
        }

        public void UnregisterServiceExecutor(string name)
        {
            if (!_info.TryRemove(name, out var entry))
                return;
            entry.Functions.Clear();
            entry.Instance = null;
        }

        public RpcMessage Run(RpcMessage message)
        {
            var returnMessage = message;
            if (!_info.TryGetValue(message.Service, out RegisterExecutorInfo? svc))
            {
                returnMessage = MessageHelper.Error($"{message.Service} was not found");
            }
            else
            {
                if (!svc.Functions.TryGetValue(message.Function, out var svcFunc))
                {
                    returnMessage = MessageHelper.Error($"{message.Service}.{message.Function} was not found");
                }
                else
                {
                    svcFunc.BeginCall();
                    try
                    {
                        var result = svcFunc.Call(message.Data);
                        returnMessage = new RpcMessage { Data = result };
                    }
                    catch (Exception ex)
                    {
                        returnMessage = MessageHelper.Error($"{message.Service}.{message.Function} occurred exception {ex}");
                    }
                    svcFunc.EndCall();
                }
            }

            returnMessage.Service = message.Service;
            returnMessage.Function = message.Function;
            return returnMessage;
        }
    }

    public class JsonRpcService : JsonRpc.JsonRpcBase
    {
        
        private ServiceExecutor _executor;

        public JsonRpcService(ServiceExecutor executor)
        {
            _executor = executor;
        }

        public override Task<JsonReply> Call(JsonRequest request, ServerCallContext context)
        {
            var reply = new JsonReply();
            var message = JsonConvert.DeserializeObject<RpcMessage>(request.Content);
            if (message == null) 
            {
                return Task.FromResult(ErrorReply("message is null"));
            }

            var replyMessage = _executor.Run(message);

            return Task.FromResult(Reply(replyMessage));
        }

        private JsonReply ErrorReply(string errorMessage)
        {
            return Reply(MessageHelper.Error(errorMessage));
        }

        private JsonReply Reply(RpcMessage message)
        {
            return new JsonReply() { Content = JsonConvert.SerializeObject(message) };
        }
    }
    public record TransferMessage
    {
        public required string Filename { get; set; }
        public required string Type { get; set; }
        public required string Content { get; set; }
    }

    public record InstallMessage
    {
        public required string MainDll { get; set; }
        public required string[] Filenames { get; set; }
    }

    public class InputMetaData
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public string ElementType { get; set; }
    }

    public class FunctionMetaData
    {
        public string Name { get; set; }
        public List<InputMetaData> Inputs { get; set; }
        public Dictionary<string, object> Properties { get; set; }
    }

    public class ServiceMetaData
    {
        public string Name { get; set; }
        public List<FunctionMetaData> Functions { get; set; }
    }

    [Service(Constant.SystemService.Name)]
    public class DefaultServiceExecutor : IExecutor
    {
        [ServiceFunction(nameof(Constant.SystemService.Transfer), InputType = typeof(TransferMessage))]
        public string Transfer(string input)
        {
            var transfer = JsonConvert.DeserializeObject<TransferMessage>(input);
            if (transfer == null)
            {
                return MessageHelper.ErrorJson("input is null");
            }

            var filepath = Path.Combine("temp", transfer.Filename);

            if (string.Compare(transfer.Type, "base64", true) == 0)
            {
                File.WriteAllBytes(filepath, Convert.FromBase64String(transfer.Content));
            }
            else
            {
                File.WriteAllText(filepath, transfer.Content);
            }

            return MessageHelper.SuccessJson("Transfer OK");
        }

        [ServiceFunction(nameof(Constant.SystemService.Install), InputType = typeof(InstallMessage))]
        public string Install (string input)
        {
            var install = JsonConvert.DeserializeObject<InstallMessage>(input);
            if (install == null)
            {
                return MessageHelper.ErrorJson("input is null");
            }

            foreach (var file in install.Filenames)
            {
                var filepath = Path.Combine("temp", file);
                if (!File.Exists(filepath))
                    return MessageHelper.ErrorJson($"file {file} was not found");
            }
            var mainpath = Path.Combine("extra", install.MainDll); 
            if (!File.Exists(mainpath))
                return MessageHelper.ErrorJson($"maindll {mainpath} was not found");

            foreach (var file in install.Filenames)
            {
                var fromfilepath = Path.Combine("temp", file);
                var tofilepath = Path.Combine("extra", file);
                File.Move(fromfilepath, tofilepath, true);
            }

            try
            {
                Box.LoadAddin(install);
            }
            catch (Exception ex)
            {
                return MessageHelper.ErrorJson($"Exception: {ex}");
            }

            return MessageHelper.SuccessJson("Install OK");
        }

        [ServiceFunction(Constant.SystemService.Uninstall, InputType = typeof(InstallMessage))]
        public string Uninstall(string input)
        {
            var install = JsonConvert.DeserializeObject<InstallMessage>(input);
            if (install == null)
            {
                return MessageHelper.ErrorJson("input is null");
            }

            Box.UnloadAddin(install);

            foreach (var file in install.Filenames)
            {
                var filepath = Path.Combine("extra", file);
                if (!File.Exists(filepath))
                    continue;
                File.Delete(filepath);
            }

            return MessageHelper.SuccessJson("Uninstall OK");
        }

        [ServiceFunction(Constant.SystemService.List)]
        public string List(string input)
        {
            List<ServiceMetaData> serviceMetaDatas = new List<ServiceMetaData>();
            var se = Box.Services.GetService<ServiceExecutor>();
            foreach (var pair in se._info)
            {
                var execInfo = pair.Value;
                var smd = new ServiceMetaData() { Name = execInfo.Name, Functions = [] };
                foreach (var fpair in execInfo.Functions)
                {
                    var fInfo = fpair.Value;
                    var fmd = new FunctionMetaData() { Name = fpair.Key, Inputs = [], Properties = [] };
                    var sfa = fInfo.Call.Method.GetCustomAttribute<ServiceFunctionAttribute>();
                    foreach (var property in sfa?.InputType?.GetProperties() ?? [])
                    {
                        fmd.Inputs.Add(new InputMetaData() {
                            Name = property.Name,
                            Type = property.PropertyType.IsArray ? "Array" : property.PropertyType.Name,
                            ElementType = (property.PropertyType.IsArray ? property.PropertyType.GetElementType()?.Name : null) ?? string.Empty
                        });
                    }
                    foreach (var spair in fInfo.Stats)
                    {
                        fmd.Properties.Add(spair.Key, spair.Value);
                    }
                    smd.Functions.Add(fmd);
                }
                serviceMetaDatas.Add(smd);
            }
            return JsonConvert.SerializeObject(serviceMetaDatas);
        }
    }

    public class AddinManager
    {
        private const string AddinConfig = "addins.json";
        private readonly List<InstallMessage> _addins = [];
        private readonly ConcurrentDictionary<string, AssemblyLoadContext> _assemblyLoadContexts = [];
        private readonly string baseDir;

        public AddinManager() 
        {
            baseDir = Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location) ?? "";
            Load(); 
        }

        public void Add(InstallMessage addin)
        {
            lock(_addins)
            {
                _addins.Add(addin);
                Save();
            }
            Load(addin.MainDll);
        }

        public void Remove(InstallMessage addin)
        {
            Unload(addin.MainDll);
            lock (_addins)
            {
                _addins.Remove(addin);
                Save();
            }
        }

        private void Load()
        {
            if (!File.Exists(AddinConfig))
                return;
            var json = File.ReadAllText(AddinConfig);
            var list = JsonConvert.DeserializeObject<List<InstallMessage>>(json);
            _addins.Clear();
            if (list != null)
            {
                _addins.AddRange(list);
            }
            foreach (var addin in _addins)
            {
                Load(addin.MainDll);
            }
        }

        private void Load(string dllname)
        {
            var mainpath = Path.Combine(baseDir, "extra", dllname);
            try
            {
                var ctx = new AssemblyLoadContext(mainpath, true);
                _assemblyLoadContexts.TryAdd(mainpath, ctx);
                ctx.Resolving += (assemblyLoadContext, assemblyName) =>
                {
                    var dependencyPath = Path.Combine(baseDir, "extra", $"{assemblyName.Name}.dll");
                    return assemblyLoadContext.LoadFromAssemblyPath(dependencyPath);
                };
                var assembly = ctx.LoadFromAssemblyPath(mainpath);
                var allExecutors = assembly.GetExportedTypes().Where(type => type.IsAssignableTo(typeof(IExecutor)) && type.IsDefined(typeof(ServiceAttribute), false));
                foreach (var executorType in allExecutors)
                {
                    var executor = Activator.CreateInstance(executorType);
                    Box.Register(executor);
                }
            }
            catch
            {
                try
                {
                    Box.Unregister(mainpath);
                    if (_assemblyLoadContexts.TryRemove(mainpath, out var ctx))
                    {
                        ctx.Unload();
                    }
                }
                catch { }
                throw;
            }
        }

        private void Unload(string dllname)
        {
            var mainpath = Path.Combine("extra", dllname);
            Box.Unregister(mainpath);
            if (_assemblyLoadContexts.TryRemove(mainpath, out var ctx))
            {
                ctx.Unload();
            }
        }

        private void Save()
        {
            var json = JsonConvert.SerializeObject(_addins, Formatting.Indented);
            File.WriteAllText(AddinConfig, json);
        }
    }

    public static class Box
    {
        private static WebApplication? webApplication;
        private static ServiceExecutor? serviceExecutor;
        private static AddinManager? addinManager;

        public static IServiceProvider? Services => webApplication?.Services;

        public static void Register(object? service)
        {
            var executor = service as IExecutor;
            if (executor == null)
                return;
            serviceExecutor!.RegisterServiceExecutor(executor);
        }

        public static void Unregister(string name)
        {
            serviceExecutor!.UnregisterServiceExecutor(name);
        }

        public static void LoadAddin(InstallMessage addin)
        {
            addinManager?.Add(addin);
        }

        public static void UnloadAddin(InstallMessage addin)
        {
            addinManager?.Remove(addin);
        }


        public static void Start()
        {
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.ConfigureKestrel(options =>
            {
                options.Listen(IPAddress.Any, 5001, listenOptions =>
                {
                    listenOptions.Protocols = HttpProtocols.Http2;
                });
            });

            serviceExecutor = new ServiceExecutor();
            serviceExecutor.RegisterServiceExecutor(new DefaultServiceExecutor());
            builder.Services.AddSingleton(serviceExecutor);

            addinManager = new AddinManager();
            builder.Services.AddSingleton(addinManager);

            builder.Services.AddGrpc(options =>
            {
                options.MaxReceiveMessageSize = null;
                options.MaxSendMessageSize = null;
            });
            webApplication = builder.Build();
            webApplication.MapGrpcService<JsonRpcService>();
            webApplication.RunAsync();
        }

        public static void Stop() 
        {
            if (webApplication is null)
                return;

            webApplication.StopAsync();
        }
    }
}
