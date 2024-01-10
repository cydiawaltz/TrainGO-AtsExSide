using System;
using System.IO.MemoryMappedFiles;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using AtsEx.PluginHost.Native;
using AtsEx.PluginHost.Plugins;
using BveTypes.ClassWrappers;
using SlimDX.Direct3D9;

namespace elementary//初級
{
    /// プラグインの本体
    [PluginType(PluginType.MapPlugin)]
    internal class MapPluginMain : AssemblyPluginBase
    {
        //減点秒数（級数により変更）
        int overatc = 2;//atc超過（予告無視）
        int overtime = 5;//時間超過
        int teitsuu = 3;//定通ボーナス
        
        private readonly BeaconPassedEventArgs beaconPassedEventArgs;//オブジェクト参照大作
        private readonly Station station;
        //atc信号
        int atc;
        //持ち時間
        public int life;
        //速度
        public float speed;
        //力行ハンドルの数値
        int PowerNotch;
        //ブレーキハンドルの数値
        int BrakeNotch;
        //現在地
        double Location;
        //字駅の駅インデックス
        int index;
        //次駅までの距離
        double NextLocation;
        //定通した回数
        int teitsuukaisuu;
        //共有メモリ//
        //残り距離
        MemoryMappedViewAccessor accessor;
        //マスコン
        MemoryMappedViewAccessor power;
        //ブレーキ
        MemoryMappedViewAccessor brake;
        //持ち時間
        MemoryMappedViewAccessor lifetime;
        //定通
        MemoryMappedViewAccessor teitsuupoint;
        //通過時刻
        MemoryMappedViewAccessor pasttime;
        //到着時刻
        MemoryMappedViewAccessor arritime;
        //減点内容
        MemoryMappedViewAccessor GentenNaiyou;

        //pipeserver
        NamedPipeServerStream pipeServer;
        ///時刻//
        //現在時刻
        TimeSpan now;
        //到着時刻
        TimeSpan arrival;
        //通貨時刻
        TimeSpan past;
        //送る用
        string SendArrival;
        string sendPast;
        //内部演算用
        int millinow;
        int milliarrival;
        int milliDeperture;
        //通過・停車の判定
        bool Pass;
        /// プラグインが読み込まれた時に呼ばれる
        /// 初期化を実装する
        public MapPluginMain(PluginBuilder builder) : base(builder)
        {
            //にょーり君第5話「定義するぞ」
            //速度
            speed = Native.VehicleState.Speed;
            //マスコン
            PowerNotch = Native.Handles.Power.Notch;
            //ブレーキ
            BrakeNotch = Native.Handles.Brake.Notch;
            //初期持ち時間
            life = 30;
            //共有メモリ//
            //次駅距離
            MemoryMappedFile a = MemoryMappedFile.CreateNew("NeXTStation", 4096);
            accessor = a.CreateViewAccessor();
            //力行
            MemoryMappedFile b = MemoryMappedFile.CreateNew("PowerNotch", 4096);
            power = b.CreateViewAccessor();
            //ブレーキ
            MemoryMappedFile c = MemoryMappedFile.CreateNew("BrakeNotch", 4096);
            brake = c.CreateViewAccessor();
            //持ち時間（life)
            MemoryMappedFile d = MemoryMappedFile.CreateNew("Life", 1024);
            lifetime = d.CreateViewAccessor() ;
            //定通
            MemoryMappedFile e = MemoryMappedFile.CreateNew("Teitsuu", 1024);
            teitsuupoint = e.CreateViewAccessor();//定通したときに1を追加する
            //通過時刻
            MemoryMappedFile f = MemoryMappedFile.CreateNew("Past", 1024);
            pasttime = f.CreateViewAccessor();
            //到着時刻
            MemoryMappedFile g = MemoryMappedFile.CreateNew("Teitsuu", 1024);
            arritime = g.CreateViewAccessor();
            //減点内容
            MemoryMappedFile h = MemoryMappedFile.CreateNew("Teitsuu", 1024);
            GentenNaiyou = h.CreateViewAccessor();
            /*減点内容のIndex
            0=減点なし
            1=遅れ
            値が変更されたときになにかしらのダイアログをUnityで出せ
            */
            //共有メモリEnd//
            //namedPipeの宣言
            pipeServer = new NamedPipeServerStream("SendToUnity", PipeDirection.Out);
            //最初に時刻をUnityへ送るるる
            now = Native.VehicleState.Time;
            string SendNow = now.ToString();
            byte[] mesnow = Encoding.UTF8.GetBytes(SendNow);
            pipeServer.Write(mesnow, 0, mesnow.Length);
            //次駅到着時刻をUnityへ送る
            arrival = station.ArrivalTime;//停車時に呼び出す
            past = station.DepertureTime;//通過時に呼び出す
            SendArrival = arrival.ToString();//string停車
            sendPast = arrival.ToString();//string通過
            //内部演算用にミリ秒でもカウント
            millinow = BveHacker.Scenario.TimeManager.TimeMilliseconds;//ミリ秒の現在時刻
            milliarrival = station.ArrivalTimeMilliseconds;//ミリ秒の到着時刻
            milliDeperture = station.DepertureTimeMilliseconds;//ミリ秒の通過時刻
            //停車時刻・通過時刻を送信
            Pass = station.Pass;//停車・通過の判
            //地上子をatc信号に代入
            Native.BeaconPassed += new BeaconPassedEventHandler(BeaconPassed);
            //定通舌回数は最初は0
            teitsuukaisuu = 0;
            //自列車位置=自列車位置
            Location = Native.VehicleState.Location;
            //字駅の駅インデックス
            index = BveHacker.Scenario.Route.Stations.CurrentIndex;//次駅の駅インデックス
            //次駅の定義
            NextLocation =BveHacker.Scenario.Route.Stations[index].Location - BveHacker.Scenario.LocationManager.Location;
            
        }

        /// プラグインが解放されたときに呼ばれる
        /// 後処理を実装する
        public override void Dispose()
        {
            Native.BeaconPassed -= BeaconPassed;//地上子の通過イベント購読を解除

        }

        /// シナリオ読み込み中に毎フレーム呼び出される
        /// <param name="elapsed">前回フレームからの経過時間</param>
        public override TickResult Tick(TimeSpan elapsed)
        { 
            //時間を送る
            if (Pass == false)//次駅を停車するとき
            {
                //byte[] mesArrival = Encoding.UTF8.GetBytes(SendArrival);
                //pipeServer.Write(mesArrival, 0, mesArrival.Length);
                arritime.Write(0,arrival);
            }
            else//通過時
            {
                //byte[] mespast = Encoding.UTF8.GetBytes(sendPast);
                //pipeServer.Write(mespast, 0, mespast.Length);
                pasttime.Write(0,past);
            }

            //ノッチ数を共有メモリへカキコ
            power.Write(0, PowerNotch);//フレーム毎に
            brake.Write(0, BrakeNotch);

            //ここから持ち時間の減点処理
            if (atc < speed && BrakeNotch == 0)
            {
                life -= overatc;//持ち時間の減点処理
                lifetime.Write(0,life);//共有メモリに持ち時間（life）の値を入力

            }
            //遅れの減点
            if (Pass == false)//次駅停車
            {
                if (milliarrival - millinow > 5000)//Final形式で５秒以上遅れたらまとめて減点
                {
                    life -= overtime;//５点減点
                    //string late = "late" + life.ToString();
                    //byte[] meslate = Encoding.UTF8.GetBytes(late);
                    lifetime.Write(0,life);
                    GentenNaiyou.Write(0,1);
                }
            }
            else//次駅通過
            {
                if (milliDeperture - millinow > 5000)
                {
                    life -= overtime;//５点減点
                    //string late = "late" + life.ToString();
                    //byte[] meslate = Encoding.UTF8.GetBytes(late);
                    lifetime.Write(0,life);
                    
                }
                if (System.Math.Abs(milliarrival - millinow) < 1000 && NextLocation==0)//絶対値処理（定通）
                {
                    life += teitsuu;//３点ボーナス
                    //string teituu = "Teituu" + life.ToString();
                    //byte[] mesteituu = Encoding.UTF8.GetBytes(teituu);
                    //pipeServer.Write(mesteituu, 0, mesteituu.Length);//Unityには持ち時間5の時「teituu5」と表示、teituuの部分
                    lifetime.Write(0,life);
                    teitsuukaisuu = teitsuukaisuu++;//定通した回数に１を加算
                    teitsuupoint.Write(0,)teitsuukaisuu;
                }
            }
            //持ち時間が無くなったときの処理
            if (life == 0)
            {
                //非常ブレーキで減速
                BrakeNotch = Native.Handles.Brake.EmergencyBrakeNotch;
                //Unity側では持ち時間が0になったら音を鳴らしたりする（本家参照）
                if (speed == 0)
                {
                    Thread.Sleep(3000);//停止してから３秒待つ
                    string EndGame = "end";
                    byte[] end = Encoding.UTF8.GetBytes(EndGame);
                    pipeServer.Write(end, 0, end.Length);//Unityへ終了処理を送信
                    Thread.Sleep(1000);//１秒待ってから終了
                    //終了する処理
                }
            }
           

            //以下ひたすらインデックスと対応させるただのデスゲーム
            //ここから過去の努力の遺物
            //double H23 = BveHacker.Scenario.Route.Stations[0].Location;//北千住停留場
            //double H22 = BveHacker.Scenario.Route.Stations[1].Location;//北千住
            //double H21 = BveHacker.Scenario.Route.Stations[2].Location;//南千住
            //double H20 = BveHacker.Scenario.Route.Stations[3].Location;//三ノ輪
            //double H19 = BveHacker.Scenario.Route.Stations[4].Location;//入谷
            //double H18 = BveHacker.Scenario.Route.Stations[5].Location;//上野
            //double H17 = BveHacker.Scenario.Route.Stations[6].Location;//仲御徒町
            //double H16 = BveHacker.Scenario.Route.Stations[7].Location;//秋葉原
            //double H15 = BveHacker.Scenario.Route.Stations[8].Location;//小伝馬町
            //double H14 = BveHacker.Scenario.Route.Stations[9].Location;//人形町
            //double H13 = BveHacker.Scenario.Route.Stations[10].Location;//茅場町
            //double H12 = BveHacker.Scenario.Route.Stations[11].Location;//八丁堀
            //double H11 = BveHacker.Scenario.Route.Stations[12].Location;//築地
            //double H10 = BveHacker.Scenario.Route.Stations[13].Location;//東銀座
            //double H09 = BveHacker.Scenario.Route.Stations[14].Location;//銀座
            //double H08 = BveHacker.Scenario.Route.Stations[15].Location;//日比谷
            //double H07 = BveHacker.Scenario.Route.Stations[16].Location;//霞が関
            //double H06 = BveHacker.Scenario.Route.Stations[17].Location;//虎ノ門ヒルズ
            //double H05 = BveHacker.Scenario.Route.Stations[18].Location;//神谷町
            //double H04 = BveHacker.Scenario.Route.Stations[19].Location;//六本木
            //double H03 = BveHacker.Scenario.Route.Stations[20].Location;//広尾
            //double H02 = BveHacker.Scenario.Route.Stations[21].Location;//恵比寿
            //double H01 = BveHacker.Scenario.Route.Stations[22].Location;//中目黒
            //double H00 = BveHacker.Scenario.Route.Stations[23].Location;//中目黒留置線（ここまで実装できるかは不明
            accessor.Write(0, NextLocation);//次駅までの距離を送信
            
            //実装したい機能
            //現在時刻・到着時刻>>OK
            //持ち時間（遅れでFinal式）>>OK
            //持ち時間（ATC作動時Bをいれてないと予告無視） >>OK
            //定通（ピッタなら加点）>>OK
            //残り距離 >>OK
            //定通ポイント（独自地上子？）
            //定速ポイント（同）
            //ノッチ数変更通知 >>OK

            return new VehiclePluginTickResult();
        }
        //Atc信号の判定
        public void BeaconPassed(BeaconPassedEventArgs e)
        {
            int atc = e.Type;
            switch(e.Type)
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
        }
    }
}    

