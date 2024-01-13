using System;
using System.IO.MemoryMappedFiles;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using AtsEx.PluginHost.Native;
using AtsEx.PluginHost.Plugins;
using BveTypes.ClassWrappers;

namespace syokyu//初級
{
    /// プラグインの本体
    [PluginType(PluginType.MapPlugin)]
    internal class MapPluginMain : AssemblyPluginBase
    {
        //時速をUnityへ送信
        public float speed;
        MemoryMappedViewAccessor speedtounity;
        //現在位置
        public double NowLocation;
        MemoryMappedViewAccessor nowlocatounity;
        //次駅のindex
        int index;
        MemoryMappedViewAccessor indextounity;
        //次駅の位置
        public double NeXTLocation;
        MemoryMappedViewAccessor nextstatounity;
        //BeaconType
        int BeaconType;
        MemoryMappedViewAccessor beacontounity;
        //力行ノッチ
        int Power;
        MemoryMappedViewAccessor powertounity;
        //ブレーキノッチ
        int Brake;
        MemoryMappedViewAccessor braketounity;
        //lifeの値を受信
        int life;
        MemoryMappedViewAccessor lifefromunity;
        //通過/停車を判定
        bool pass;
        MemoryMappedViewAccessor passornot;
        //到着時刻
        int arrival;//ミリ秒で送信するので、Unity側でTimeSpan fromMilliSec = TimeSpan.FromMilliseconds(milliSec);としてくれ俺
        MemoryMappedViewAccessor arrivaltounity;
        //通過時刻
        int past;
        MemoryMappedViewAccessor arrivaltounity;
        public MapPluginMain(PluginBuilder builder) : base(builder)
        {
            //スピード
            speed = Native.VehicleState.Speed;
            MemoryMappedFile a = MemoryMappedFile.CreateNew("speed", 4096);
            speedtounity = a.CreateViewAccessor();
            //現在位置
            NowLocation = Native.VehicleState.Speed;
            MemoryMappedFile b = MemoryMappedFile.CreateNew("NowLocation", 4096);
            nowlocatounity = b.CreateViewAccessor();
            //index
            index = BveHacker.Scenario.Route.Stations.CurrentIndex;
            MemoryMappedFile c = MemoryMappedFile.CreateNew("Index", 4096);
            indextounity = c.CreateViewAccessor();
            //次駅位置
            NeXTLocation = BveHacker.Scenario.Route.Stations[index].Location;
            MemoryMappedFile d = MemoryMappedFile.CreateNew("NextLocation", 4096);
            nextstatounity = d.CreateViewAccessor();
            //BeaconType
            MemoryMappedFile e = MemoryMappedFile.CreateNew("Beacon", 4096);
            beacontounity= e.CreateViewAccessor();
            Native.BeaconPassed += new BeaconPassedEventHandler(BeaconPassed);
            //Power
            Power = Native.Handles.Power.Notch;
            MemoryMappedFile f = MemoryMappedFile.CreateNew("Power", 4096);
            powertounity = f.CreateViewAccessor();
            //Brake
            Brake = Native.Handles.Brake.Notch;
            MemoryMappedFile g = MemoryMappedFile.CreateNew("Brake", 4096);
            braketounity= g.CreateViewAccessor();
            //通過到着の判定
            pass = //判定する文章
            MemoryMappedFile h = MemoryMappedFile.CreateNew("passornot", 4096);
            passornot= h.CreateViewAccessor();
            /*
            0=停車
            1-通過
            */
            //到着時刻
            arrival = station.ArrivalTimeMilliseconds;
            MemoryMappedFile i = MemoryMappedFile.CreateNew("arrival", 4096);
            arrivaltounity= i.CreateViewAccessor();
            //持ち時間を受
            MemoryMappedFile lifetimefromunity = MemoryMappedFile.OpenExisting("life");
            lifefromunity = lifetime.CreateViewAccessor();
        }

        /// プラグインが解放されたときに呼ばれる
        /// 後処理を実装する
        public override void Dispose()
        {
        }

        /// シナリオ読み込み中に毎フレーム呼び出される
        /// <param name="elapsed">前回フレームからの経過時間</param>
        public override TickResult Tick(TimeSpan elapsed)
        {
            speedtounity.Write(0,speed);//スピードをUnityへ常時送信する
            nowlocatounity.Write(0,NowLocation);//現在位置
            indextounity.Write(0,index);//次駅インデックス
            nextstatounity.Write(0,NeXTLocation);//次駅位置
            powertounity.Write(0,Power);//力行ノッチ
            braketounity.Write(0,Brake);//ブレーキノッチ
            if(pass = falese)
            {
                arrivaltounity.Write(0,arrival);//到着時刻（ミリ秒）
                passornot.Write(0,0);//停車
            }
            else{
                
                passornot.Write(0,1);
            }
            life = lifefromunity.ReadInt32(0);//lifeのあたいを受信
            if(life == 0)
            {
                brakeNotch = Native.Handles.Brake.EmergencyBrakeNotch;
                //Unity側では持ち時間が0になったら音を鳴らしたりする（本家参照）
                if (speed == 0)
                {
                    Thread.Sleep(3000);//停止してから３秒待つ
                    //Unity側で終了しろ
                    //終了する処理を実装
                }
            }
            return new VehiclePluginTickResult();
        }
        public void BeaconPassed(BeaconPassedEventArgs e)
        {
            BeaconType =e.Type;
            beacontounity.Write(0,BeaconType);
        }
    }
}