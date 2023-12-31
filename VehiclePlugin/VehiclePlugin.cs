﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO
using System.IO.Pipes
using AtsEx.PluginHost.Plugins;

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
        private int havingtime = 30;
        private int speed = locationManager.SpeedMeterPerSecond;
        private int atc = 100;

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
            if(havingtime == 0)
            {
                int atsPowerNotch = 0;
                int atsBrakeNotch = handleset.Brake.MaxServiceBrakeNotch;
                if(speed == 0)
                {
                    ///シナリオを終了するかんじの処理（とnamedpipe)
                }
            }
            if(atc < speed && !atsPowerNotch == 0 && atsBrakeNotch == 0)
            {
                havingtime -=2;
            }
            
        }
        private void BeaconPassed(BeaconPassedEventArgs atc)
        {
            //atc.Type 地上子の種類
            //atc.Optional 
            //atc.Distance 対象までの距離
            if(atc.Type == 25){
                atc.Value = 75;
                }
            if(atc.Type == 23){
                int atc = 65;
                }
            if(atc.Type == 21){
                int atc = 55;
                }
            if(atc.Type == 19){
                int atc = 45;
                }
            if(atc.Type == 18){
                int atc = 40;
                }
            if(atc.Type == 10){
                int atc = 0;
                }
        }

    }
}
