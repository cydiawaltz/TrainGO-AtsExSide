using System;
using System.Text;
using System.Threading;
using System.IO.Pipes;
using AtsEx.PluginHost.Plugins;
using AtsEx.PluginHost.Native;
using BveTypes.ClassWrappers;
using System.IO.MemoryMappedFiles;
using AtsEx.PluginHost.Handles;
using AtsEx.PluginHost.Input;
using static System.Collections.Specialized.BitVector32;


namespace elementary
{
    /// プラグインの本体
    [PluginType(PluginType.MapPlugin)]
    internal class MapPluginMain : AssemblyPluginBase
    {
        /// プラグインが読み込まれた時に呼ばれる
        /// 初期化を実装する
        public MapPluginMain(PluginBuilder builder, VehicleState vehicleState, VehicleSpec vehicleSpec, BeaconPassedEventArgs beaconPassedEventArgs, AtsEx.PluginHost.Handles.HandleSet handleSet, TimeSpan time, AtsEx.PluginHost.Input.KeyBase keyBase, Scenario scenario, TimeManager timeManager, Station station) : base(builder)
        {
        }

        /// プラグインが解放されたときに呼ばれる
        /// 後処理を実装する
        public override void Dispose()
        {
        }

        /// シナリオ読み込み中に毎フレーム呼び出される
        /// <param name="elapsed">前回フレームからの経過時間</param>
        public override TickResult Tick(TimeSpan elapsed)
            //TimeSpan elapsed ,PluginBuilder builder, VehicleState vehicleState, VehicleSpec vehicleSpec, BeaconPassedEventArgs beaconPassedEventArgs, AtsEx.PluginHost.Handles.HandleSet handleSet, TimeSpan time, AtsEx.PluginHost.Input.KeyBase keyBase, Scenario scenario, TimeManager timeManager, Station station)
        {

            MemoryMappedFile share_mem = MemoryMappedFile.CreateNew("SendToUnity", 1024);
            MemoryMappedViewAccessor accessor = share_mem.CreateViewAccessor();
            //残り距離を共有メモリで共有
            MemoryMappedFile sharemem = MemoryMappedFile.CreateNew("PowerNotch", 1024);
            MemoryMappedViewAccessor power = sharemem.CreateViewAccessor();
            //力行ノッチ
            MemoryMappedFile share = MemoryMappedFile.CreateNew("BrakeNotch", 1024);
            MemoryMappedViewAccessor brake = share.CreateViewAccessor();
            //ブレーキノッチ
            //許容範囲

            //持ち時間
            int life = 30;
            //速度
            float speed = Native.VehicleState.Speed;
            //力行ハンドルの数値
            int PowerNotch = Native.Handles.Power.Notch;
            //ブレーキハンドルの数値
            int BrakeNotch = Native.Handles.Brake.Notch;
            //atc信号（ここでは適当に100を代入）
            int atc = 100;
            //namedPipeの宣言
            NamedPipeServerStream pipeServer;
            pipeServer = new NamedPipeServerStream("SendToUnity", PipeDirection.Out);
            //最初に時刻をUnityへ送るるる
            TimeSpan now = Native.VehicleState.Time;
            string SendNow = now.ToString();
            byte[] mesnow = Encoding.UTF8.GetBytes(SendNow);
            pipeServer.Write(mesnow, 0, mesnow.Length);
            //次駅到着時刻をUnityへ送る
            TimeSpan arrival = StationList.Station.ArrivalTime;//停車時に呼び出す
            TimeSpan past = Station.DepertureTime;//通過時に呼び出す
            string SendArrival = arrival.ToString();//string停車
            string sendPast = arrival.ToString();//string通過
                                                 //内部演算用にミリ秒でもカウント
            int millinow = Scenario.TimeManager.TimeMilliseconds;//ミリ秒の現在時刻
            int milliarrival = Station.ArrivalTimeMilliseconds;//ミリ秒の到着時刻
            int milliDeperture = station.DepertureTimeMilliseconds;//ミリ秒の通過時刻
            //停車時刻・通過時刻を送信
            bool Pass = station.Pass;//停車・通過の判
            if (Pass == false)//次駅を停車するとき
            {
                byte[] mesArrival = Encoding.UTF8.GetBytes(SendArrival);
                pipeServer.Write(mesArrival, 0, mesArrival.Length);
            }
            else//通過時
            {
                byte[] mespast = Encoding.UTF8.GetBytes(sendPast);
                pipeServer.Write(mespast, 0, mespast.Length);
            }

            //ノッチ数に変更があったらUnityに送信
            //bool Press = keyBase.IsPressed;//なんかエラー吐くのでこのようにしてみた
            //if (Press == true)
            //{
            //    string power = PowerNotch.ToString();//なんかエラー吐くので分けた
            //    string brake = BrakeNotch.ToString();
            //    byte[] mesnotch = Encoding.UTF8.GetBytes("Notch" + power + brake);//P4B5なら45とでる予定　Unity側では10の時は..のように実装よろしく
            //    pipeServer.Write(mesnotch, 0, mesnotch.Length);
            //}
            //旧処理(namedpipe)
            power.Write(0, PowerNotch);
            brake.Write(0, BrakeNotch);
            //新処理（メモリ共有）

            //地上子をatc信号に代入
            switch (BeaconPassedEventArgs.Type)
            {
                //ATC信号0
                case 10:
                    atc = 0;
                    break;
                //ATC信号40
                case 18:
                    atc = 40;
                    break;
                //ATC45
                case 19:
                    atc = 45;
                    break;
                //ATC55
                case 21:
                    atc = 55;
                    break;
                //ATC65
                case 23:
                    atc = 65;
                    break;
                //ATC75
                case 25:
                    atc = 75;
                    break;
                default://そうでないときはクソめんどいのでATC80
                    atc = 80;
                    break;
            }
            //ここから持ち時間の減点処理
            //前方予告無視減点（級によって変更）
            if (atc < speed && BrakeNotch == 0)
            {
                life -= 2;//持ち時間の減点処理
                string lifemes = "Life" + life.ToString();//持ち時間をstringに変換
                byte[] mesByte = Encoding.UTF8.GetBytes(lifemes);//byteに変換
                pipeServer.Write(mesByte, 0, mesByte.Length);//Unityへ送信
            }
            //遅れの減点
            if (Pass == false)//次駅停車
            {
                if (milliarrival - millinow > 5000)//Final形式で５秒以上遅れたらまとめて減点
                {
                    life -= 5;//５点減点
                    string late = "late" + life.ToString();
                    byte[] meslate = Encoding.UTF8.GetBytes(late);
                }
            }
            else//次駅通過
            {
                if (milliDeperture - millinow > 5000)
                {
                    life -= 5;//５点減点
                    string late = "late" + life.ToString();
                    byte[] meslate = Encoding.UTF8.GetBytes(late);
                }
                if (System.Math.Abs(milliarrival - millinow) < 1000)//絶対値処理（定通）
                {
                    life += 3;//３点ボーナス
                    string teituu = "Teituu" + life.ToString();
                    byte[] mesteituu = Encoding.UTF8.GetBytes(teituu);
                    pipeServer.Write(mesteituu, 0, mesteituu.Length);//Unityには持ち時間5の時「teituu5」と表示、teituuの部分
                }
            }
            //持ち時間が無くなったときの処理
            if (life == 0)
            {
                //非常ブレーキで減速
                BrakeNotch = AtsEx.PluginHost.HandleSet.Brake.EmergencyBrakeNotch;
                //Unity側では持ち時間が0になったら音を鳴らしたりする（本家参照）
                if (speed == 0)
                {
                    Thread.Sleep(3000);//停止してから３秒待つ
                    string EndGame = "end";
                    byte[] end = Encoding.UTF8.GetBytes(EndGame);
                    pipeServer.Write(end, 0, end.Length);//Unityへ終了処理を送信
                    Thread.Sleep(1000);//１秒待ってから終了
                }
            }


            //以下ひたすらインデックスと対応させるただのデスゲーム
            //自列車位置
            double Location = vehicleState.Location;//ここを残り距離にしませう
            //ここからテンプラ
            double H23 = scenario.Route.Stations[0].Location;//北千住停留場
            double H22 = scenario.Route.Stations[1].Location;//北千住
            double H21 = scenario.Route.Stations[2].Location;//南千住
            double H20 = scenario.Route.Stations[3].Location;//三ノ輪
            double H19 = scenario.Route.Stations[4].Location;//入谷
            double H18 = scenario.Route.Stations[5].Location;//上野
            double H17 = scenario.Route.Stations[6].Location;//仲御徒町
            double H16 = scenario.Route.Stations[7].Location;//秋葉原
            double H15 = scenario.Route.Stations[8].Location;//小伝馬町
            double H14 = scenario.Route.Stations[9].Location;//人形町
            double H13 = scenario.Route.Stations[10].Location;//茅場町
            double H12 = scenario.Route.Stations[11].Location;//八丁堀
            double H11 = scenario.Route.Stations[12].Location;//築地
            double H10 = scenario.Route.Stations[13].Location;//東銀座
            double H09 = scenario.Route.Stations[14].Location;//銀座
            double H08 = scenario.Route.Stations[15].Location;//日比谷
            double H07 = scenario.Route.Stations[16].Location;//霞が関
            double H06 = scenario.Route.Stations[17].Location;//虎ノ門ヒルズ
            double H05 = scenario.Route.Stations[18].Location;//神谷町
            double H04 = scenario.Route.Stations[19].Location;//六本木
            double H03 = scenario.Route.Stations[20].Location;//広尾
            double H02 = scenario.Route.Stations[21].Location;//恵比寿
            double H01 = scenario.Route.Stations[22].Location;//中目黒
            double H00 = scenario.Route.Stations[23].Location;//中目黒留置線（ここまで実装できるかは不明）
            accessor.Write(0, Location);//現在位置を送信

            //実装したい機能
            //現在時刻・到着時刻>>OK
            //持ち時間（遅れでFinal式）>>OK
            //持ち時間（ATC作動時Bをいれてないと予告無視） >>OK
            //定通（ピッタなら加点）>>OK
            //残り距離
            //定通ポイント（独自地上子？）
            //定速ポイント（同）
            //ノッチ数変更通知 >>OK

            return new VehiclePluginTickResult();
        }

        public override TickResult Tick(TimeSpan elapsed)
        {
            throw new NotImplementedException();
        }
    }
}    

