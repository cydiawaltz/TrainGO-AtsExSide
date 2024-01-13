using System;
using System.IO.MemoryMappedFiles;
using System.Threading;
using AtsEx.PluginHost.Native;
using AtsEx.PluginHost.Plugins;
using BveTypes.ClassWrappers;
using AtsEx.PluginHost.Plugins.Extensions;

namespace syokyu//初級
{
    /// プラグインの本体
    [PluginType(PluginType.Extension)]
    internal class ExtensionMain : AssemblyPluginBase,IExtension
    {
        private Station station;
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
        MemoryMappedViewAccessor pasttounity;
        //現在時刻
        int now;
        MemoryMappedViewAccessor nowtounity;
        public ExtensionMain(PluginBuilder builder) : base(builder)
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
            pass = station.Pass;
            MemoryMappedFile h = MemoryMappedFile.CreateNew("passornot", 4096);
            passornot= h.CreateViewAccessor();
            /*
            0=停車
            1-通過
            */
            //到着時刻
            arrival = station.ArrivalTimeMilliseconds;
            MemoryMappedFile i = MemoryMappedFile.CreateNew("arrival", 86400000);
            arrivaltounity = i.CreateViewAccessor();
            //通貨時刻
            past = station.DepertureTimeMilliseconds;
            MemoryMappedFile j = MemoryMappedFile.CreateNew("past", 86400000);
            arrivaltounity= j.CreateViewAccessor();
            //現在時刻
            now = BveHacker.Scenario.TimeManager.TimeMilliseconds;
            MemoryMappedFile k = MemoryMappedFile.CreateNew("now", 86400000);
            arrivaltounity= k.CreateViewAccessor();
            //持ち時間を受
            MemoryMappedFile l = MemoryMappedFile.OpenExisting("life");
            lifefromunity = l.CreateViewAccessor();
        }
        public override void Dispose()
        {
            Native.BeaconPassed -= BeaconPassed;
            speedtounity.Dispose(); 
            nowlocatounity.Dispose();
            indextounity.Dispose();
            nextstatounity.Dispose();
            powertounity.Dispose();
            braketounity.Dispose();
            arrivaltounity.Dispose();
            pasttounity.Dispose();
            nowtounity.Dispose();
            beacontounity.Dispose();
            passornot.Dispose();
            lifefromunity.Dispose();
            
        }
        public override TickResult Tick(TimeSpan elapsed)
        {
            speedtounity.Write(0,speed);//スピードをUnityへ常時送信する
            nowlocatounity.Write(0,NowLocation);//現在位置
            indextounity.Write(0,index);//次駅インデックス
            nextstatounity.Write(0,NeXTLocation);//次駅位置
            powertounity.Write(0,Power);//力行ノッチ
            braketounity.Write(0,Brake);//ブレーキノッチ
            if(pass == false)
            {
                arrivaltounity.Write(0,arrival);//到着時刻（ミリ秒）
                passornot.Write(0,0);//停車
            }
            else
            {
                pasttounity.Write(0,past);//通貨時刻（ミリ秒）
                passornot.Write(0,1);//通過
            }
            life = lifefromunity.ReadInt32(0);//lifeのあたいを受信
            nowtounity.Write(0,now);
            if(life == 0)
            {
                Brake = Native.Handles.Brake.EmergencyBrakeNotch;
                //Unity側では持ち時間が0になったら音を鳴らしたりする（本家参照）
                if (speed == 0)
                {
                    Thread.Sleep(3000);//停止してから３秒待つ
                    //Unity側で終了しろ
                    //終了する処理を実装
                }
            }
            return new ExtensionTickResult();
        }
        public void BeaconPassed(BeaconPassedEventArgs e)
        {
            BeaconType =e.Type;
            beacontounity.Write(0,BeaconType);
        }
    }
}