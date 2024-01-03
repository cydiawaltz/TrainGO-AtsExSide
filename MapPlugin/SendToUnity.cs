using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO
using System.IO.Pipes
using AtsEx.PluginHost.Plugins;
using AtsEx.PluginHost.Native;
using AtsEx.PluginHost.Input;
using AtsEx.PluginHost.Handles;

namespace AtsExCsTemplate.MapPlugin
{
    [PluginType(PluginType.MapPlugin)]
    internal class SendToUnity : AssemblyPluginBase
    {
        private int PowerNotch = HandleSet.Power.PowerNotchCount;
        //private int PowerNotch = VehicleSpec.PowerNotches;
        private int BrakeNotch = HandleSet.Brake.ServiceBrakeNotchCount;

        if(KeyBase.IsPressed = true)
        {
            using (NamedPipeClientStream pipeClient = new NamedPipeClientStream(".", "ChangeNotch", PipeDirection.Out))
           {
            // パイプに接続
            pipeClient.Connect();

            // パイプにデータを書き込み
            using (StreamWriter writer = new StreamWriter(pipeClient))
            {
                writer.Write(PowerNotch+BrakeNotch);
            }
            }
        }

    }
}