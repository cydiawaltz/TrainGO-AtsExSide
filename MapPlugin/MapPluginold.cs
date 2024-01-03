using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.IO.Pipes;
using AtsEx.PluginHost.Plugins;
using AtsEx.PluginHost.Native;
using AtsEx.PluginHost.Handles;

namespace AtsExCsTemplate.MapPlugin
{
    /// <summary>
    /// プラグインの本体
    /// </summary>
    [PluginType(PluginType.MapPlugin)]
    internal class MapPluginMain : AssemblyPluginBase
    {
        /// <summary>
        /// プラグインが読み込まれた時に呼ばれる
        /// 初期化を実装する
        /// </summary>
        /// <param name="builder"></param>
        private int life = 30;
        //持ち時間
        private float speed = VehicleState.Speed;
        private int atc = 100;
        private static readonly IPowerHandle power = HandleSet.Power;
        private int powerNotch = power.PowerNotchCount;
        //もしだめならこうする
        //public int PowerNotch = VehicleSpec.PowerNotches;
        public int BrakeNotch = HandleSet.Brake.ServiceBrakeNotchCount;

        

        public int PowerNotch { get => powerNotch; set => powerNotch = value; }

        ifpublic float Speed { get => speed; set => speed = value; }

        (life = 0)
        {
            PowerNotch = 0;
            BrakeNotch = HandleSet.Brake.EmergencyBrakeNotch;
            if(speed == 0)
            {
                ///シナリオを終了するかんじの処理（とnamedpipe)
            }
        }
        if(atc < speed && PowerNotch !== 0 && BrakeNotch == 0)
        {
            life -=2;
        }



        public MapPluginMain(PluginBuilder builder) : base(builder)
        {
            this.Native.BeaconPassed += BeaconPassed;
        }

        /// プラグインが解放されたときに呼ばれる
        /// 後処理を実装する
        public override void Dispose()
        {
            this.Native.BeaconPassed -= BeaconPassed;
        }

        /// シナリオ読み込み中に毎フレーム呼び出される
        public override TickResult Tick(TimeSpan elapsed)
        {
            return new MapPluginTickResult();
            
        }
        void BeaconPassed(BeaconPassedEventArgs e)
        {
            //e.Type 地上子の種類
            //e.Optional 
            //e.Distance 対象までの距離
            if(e.Type == 25){
                int atc = 75;
                }
            if(e.Type == 23){
                int atc = 65;
                }
            if(e.Type == 21){
                int atc = 55;
                }
            if(e.Type == 19){
                int atc = 45;
                }
            if(e.Type == 18){
                int atc = 40;
                }
            if(e.Type == 10){
                int atc = 0;
                }
        }

    }
}
