using LibBox;
using Newtonsoft.Json;
using System.Diagnostics;

namespace OpenCmd
{
    [Service("Utils")]
    public class Utils : IExecutor
    {
        [ServiceFunction(nameof(OpenCmd))]
        public string OpenCmd(string input)
        {
            var process = Process.Start(new ProcessStartInfo()
            {
                FileName = "cmd.exe",
                UseShellExecute = true,
            });
            return MessageHelper.SuccessJson("OK");
        }
    }
}
