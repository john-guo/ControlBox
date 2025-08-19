using Newtonsoft.Json;

namespace LibBox
{
    public sealed class Constant
    {
        public sealed class SystemService
        {
            public const string Name = "_";
            public const string Transfer = nameof(Transfer);
            public const string Install = nameof(Install);
            public const string Uninstall = nameof(Uninstall);
            public const string List = nameof(List);
        }

        public sealed class StatNames
        {
            public const string Count = "Count";
            public const string Total = "Total";
            public const string Result = "Result";
        }
    }

    public class RpcMessage
    {
        public string Service { get; set; }
        public string Function { get; set; }
        public string Data { get; set; }
    }

    public record ReturnMessage
    {
        public required string Type { get; set; }
        public required string Result { get; set; }
    }

    public static class MessageHelper
    {
        public static RpcMessage Error(string errorMessage)
        {
            return new RpcMessage { Data = ErrorJson(errorMessage) };
        }

        public static string ErrorJson(string errorMessage)
        {
            return JsonConvert.SerializeObject(new ReturnMessage { Type = "Error", Result = errorMessage });
        }

        public static string SuccessJson(string result)
        {
            return JsonConvert.SerializeObject(new ReturnMessage { Type = "Success", Result = result });
        }

        public static string ParamJson(object data)
        {
            return JsonConvert.SerializeObject(data);
        }

        public static string ServiceCall(string service, string function, string data)
        {
            return JsonConvert.SerializeObject(new RpcMessage { Service = service, Function = function, Data = data });
        }

        public static string SystemCall(string function, string data)
        {
            return ServiceCall(Constant.SystemService.Name, function, data);
        }
    }

    public interface IExecutor { }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class ServiceAttribute : Attribute
    {
        public string Name { get; set; }

        public ServiceAttribute(string name)
        {
            Name = name;
        }
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public class ServiceFunctionAttribute : Attribute
    {
        public string Name { get; set; }
        public Type? InputType { get; set; }

        public ServiceFunctionAttribute(string name)
        {
            Name = name;
        }
    }

}
