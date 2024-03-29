﻿using System;
using System.IO.MemoryMappedFiles;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using AtsEx.PluginHost.Native;
using AtsEx.PluginHost.Plugins;
using BveTypes.ClassWrappers;

namespace elementary//初級
{
    /// プラグインの本体
    [PluginType(PluginType.MapPlugin)]
    internal class MapPluginMain : AssemblyPluginBase
    {
        //級数により変更
        int overatc = 2;//atc超過（予告無視）
        int overtime = 5;//時間超過
        int teitsuu = 3;//定通ボーナス
        int grate = 5;//Grate停車
        int good = 3;//Good停
        int GoukakuHani = 4;//合格範囲(m)
        int saikasoku = 5;//駅構内再加速
        int HijouSeidouTeisya = 5;//非常制動停車
        int hijouseidou = 3;//非常制動
        int teisokupoint =1;//定速ポインㇳ/定通ポインと
        private readonly Station station;
        //atc信号
        int atc;
        //持ち時間
        int life;
        //速度
        float speed;
        //力行ハンドルの数値
        int PowerNotch;
        //ブレーキハンドルの数値
        int BrakeNotch;
        //現在地
        double NowLocation;
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
 
        //減点内容
        MemoryMappedViewAccessor GentenNaiyou;
        //通過・停車の判定
        MemoryMappedViewAccessor passhantei;
        //pipeserver
        NamedPipeServerStream pipeServer;
        ///時刻//
        //現在時刻
        TimeSpan now;
        //到着時刻
        TimeSpan arrival;
        //通貨時刻
        //送る用
        string SendArrival;
        string sendPast;
        //内部演算用
        int millinow;
        int milliarrival;
        int milliDeperture;
        //通過・停車の判定
        bool pass;

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
            lifetime = d.CreateViewAccessor();
            //定通
            MemoryMappedFile e = MemoryMappedFile.CreateNew("Teitsuu", 1024);
            teitsuupoint = e.CreateViewAccessor();//定通したときに1を追加する
            //通過時刻
            MemoryMappedFile f = MemoryMappedFile.CreateNew("Past", 1024);
            pasttime = f.CreateViewAccessor();
            //到着時刻
            MemoryMappedFile g = MemoryMappedFile.CreateNew("Arrival", 1024);
            MemoryMappedViewAccessor arritime = g.CreateViewAccessor();
            //減点内容
            MemoryMappedFile h = MemoryMappedFile.CreateNew("Gentennaiyou", 1024);
            GentenNaiyou = h.CreateViewAccessor();
            /*減点内容のIndex
            0=減点なし
            1=延着
            2＝延通
            3=Grate停車(加点)
            4=Good停車(加点)
            5=駅構内再加速
            6=非常制動停車
            7=非常制動
            8=オーバーラン(オーバーしたm数はUnity側で出せ)
            9=定通/定速ポイント（加点）
            値が変更されたときになにかしらのダイアログをUnityで出せ
            */
            //通過・停止を判定
            MemoryMappedFile i = MemoryMappedFile.CreateNew("Pass", 1024);
            passhantei = i.CreateViewAccessor();
            /*
            0=通過
            1=停車
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
            TimeSpan past = station.DepertureTime;//通過時に呼び出す
            SendArrival = arrival.ToString();//string停車
            sendPast = arrival.ToString();//string通過
            //内部演算用にミリ秒でもカウント
            millinow = BveHacker.Scenario.TimeManager.TimeMilliseconds;//ミリ秒の現在時刻
            milliarrival = station.ArrivalTimeMilliseconds;//ミリ秒の到着時刻
            milliDeperture = station.DepertureTimeMilliseconds;//ミリ秒の通過時刻
            //停車時刻・通過時刻を送信
            //地上子をatc信号に代入
            Native.BeaconPassed += new BeaconPassedEventHandler(BeaconPassed);
            //定通舌回数は最初は0
            teitsuukaisuu = 0;
            //自列車位置=自列車位置
            NowLocation = Native.VehicleState.Location;
            //字駅の駅インデックス
            index = BveHacker.Scenario.Route.Stations.CurrentIndex;//次駅の駅インデックス
            //次駅の定義
            NextLocation = BveHacker.Scenario.Route.Stations[index].Location - BveHacker.Scenario.LocationManager.Location;

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
            if (pass == false)//次駅を停車するとき
            {
                char[] Sendarrival = SendArrival.ToCharArray();
                pasttime.WriteArray(sizeof(int), Sendarrival, 0, Sendarrival.Length);
                passhantei.Write(0,0);
            }
            else//通過時
            {
                char[] SendPast = sendPast.ToCharArray();
                pasttime.WriteArray(sizeof(int), SendPast, 0, SendPast.Length);
                passhantei.Write(0,1);
            }

            //ノッチ数を共有メモリへカキコ
            power.Write(0, PowerNotch);//フレーム毎に
            brake.Write(0, BrakeNotch);

            //ここから持ち時間の加点減点処理
            if (atc < speed && BrakeNotch == 0)
            {
                life -= overatc;//持ち時間の減点処理
                lifetime.Write(0, life);//共有メモリに持ち時間（life）の値を入力
            }
            //遅れの減点
            if (pass == false)//次駅停車
            {
                if (milliarrival - millinow > 5000)//Final形式で５秒以上遅れたらまとめて減点
                {
                    life -= overtime;//５点減点
                    //string late = "late" + life.ToString();
                    //byte[] meslate = Encoding.UTF8.GetBytes(late);
                    lifetime.Write(0, life);
                    GentenNaiyou.Write(0, 1);//延着
                }
                if (System.Math.Abs(milliarrival - millinow) < 1000 && System.Math.Abs(NextLocation) < 1)//Great!停車(定着&停止位置1m以内)
                {
                    life += grate;
                    lifetime.Write(0, life);
                    GentenNaiyou.Write(0, 3);//Great停車
                }
                else if (System.Math.Abs(NextLocation) < 1)
                {
                    life += good;
                    lifetime.Write(0, life);
                    GentenNaiyou.Write(0, 4);//Good停車
                }
            }
            else//次駅通過
            {
                if (milliDeperture - millinow > 5000)
                {
                    life -= overtime;//５点減点
                    //string late = "late" + life.ToString();
                    //byte[] meslate = Encoding.UTF8.GetBytes(late);
                    lifetime.Write(0, life);
                    GentenNaiyou.Write(0, 2);//延通
                }
                if (Math.Abs(milliarrival - millinow) < 1000 && NextLocation == 0)//絶対値処理（定通）
                {
                    life += teitsuu;//３点ボーナス
                    //string teituu = "Teituu" + life.ToString();
                    //byte[] mesteituu = Encoding.UTF8.GetBytes(teituu);
                    //pipeServer.Write(mesteituu, 0, mesteituu.Length);//Unityには持ち時間5の時「teituu5」と表示、teituuの部分
                    lifetime.Write(0, life);
                    teitsuukaisuu = teitsuukaisuu++;//定通した回数に１を加算
                    teitsuupoint.Write(0, teitsuukaisuu);
                }
            }
            //駅構内再加速と非常制動停車
            if (pass == false)
            {
                if (NextLocation < 140 && PowerNotch == 0)//140m（ホーム）上での再加速
                {
                    life -= saikasoku;
                    lifetime.Write(0, life);
                    GentenNaiyou.Write(0, 5);
                }
                if (BrakeNotch == Native.Handles.Brake.EmergencyBrakeNotch)
                {
                    life -= HijouSeidouTeisya;
                    lifetime.Write(0, life);
                    GentenNaiyou.Write(0, 6);
                }
            }
            //非常制動
            if (NextLocation > 140 && BrakeNotch == Native.Handles.Brake.EmergencyBrakeNotch)
            {
                life -= hijouseidou;
                lifetime.Write(0, life);
                GentenNaiyou.Write(0, 7);
            }
            //オーバーラン
            if (NowLocation > GoukakuHani + NextLocation)
            {
                if (speed == 0)
                {
                    int overrun = Convert.ToInt32(NowLocation - NextLocation);
                    life -= overrun;
                    lifetime.Write(0, life);
                    GentenNaiyou.Write(0, 8);
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
                    //string EndGame = "end";
                    //byte[] end = Encoding.UTF8.GetBytes(EndGame);
                    //pipeServer.Write(end, 0, end.Length);//~~Unityへ終了処理を送信~~
                    //Unity側で終了しろ
                    Thread.Sleep(1000);//1秒待ってから終了
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
            //オーバーラン減点 >>OK

            return new VehiclePluginTickResult();
        }
        //Atc信号の判定
        public void BeaconPassed(BeaconPassedEventArgs e)
        {
            switch (e.Type)
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
                case 26://そうでないときはクソめんどいのでATC80
                    atc = 80;
                    break;
                //定通ポイント
                case 200://低速(40km)
                    if(Convert.ToInt32(speed)==40)
                    {
                        life+=teisokupoint;
                        lifetime.Write(0,life);
                        GentenNaiyou.Write(0,9);
                    }
                    break;
                case 201://低速(60km)
                    if(Convert.ToInt32(speed)==60)
                    {
                        life+=teisokupoint;
                        lifetime.Write(0,life);
                        GentenNaiyou.Write(0,9);
                    }
                    break;
                
            }
        }
    }
}

