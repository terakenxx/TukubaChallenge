﻿#define EMULATOR_MODE  // bServer エミュレーション起動


using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

using LocationPresumption;
using SCIP_library;
using System.Diagnostics;


namespace CersioIO
{
    public class CersioCtrl
    {
        // BOXPC(bServer)通信クラス
#if EMULATOR_MODE
        // エミュレータ接続先
        TCPClient objTCPSC = new TCPClient("127.0.0.1", 50001);

        // エミュレータ用プロセス
        Process processEmuSim = null;

        public bool bServerEmu = true;
#else
        TCPClient objTCPSC = new TCPClient("192.168.1.1", 50001);
        public bool bServerEmu = false;
#endif

        // モータードライバー直結時
        // アクセル、ハンドルコントローラ
        public DriveIOport UsbMotorDriveIO;


        // 最近　送信したハンドル、アクセル
        static public double nowSendHandleValue;
        static public double nowSendAccValue;


        // ハンドル、アクセル上限値
        static public double HandleRate = 1.0;
        static public double AccRate = 0.5;

        // ハンドル、アクセルの変化係数
        public const double HandleControlPow = 0.125; // 0.15;
        public const double AccControlPowUP = 0.100; // 0.15;   // 加速時は緩やかに
        public const double AccControlPowDOWN = 0.150;

        // HW
        public int hwCompass = 0;
        public bool bhwCompass = false;

        public double hwREX = 0.0;
        public double hwREY = 0.0;
        public double hwREDir = 0.0;

        public double hwREStartX = 0.0;
        public double hwREStartY = 0.0;
        public double hwREStartDir = 0.0;   // 向きをリセットした値
        public bool bhwREPlot = false;

        public double hwRErotR = 0.0;
        public double hwRErotL = 0.0;
        public bool bhwRE = false;

        public double hwGPS_LandX = 0.0;
        public double hwGPS_LandY = 0.0;
        /// <summary>
        /// GPS移動情報からの向き
        /// 比較的遅れる
        /// </summary>
        public double hwGPS_MoveDir = 0.0;  // 0 ～ 359度
        public bool bhwGPS = false;
        public bool bhwUsbGPS = false;

        // 受信文字
        public string hwResiveStr;
        public string hwSendStr;

        /// <summary>
        /// USB GPS取得データ
        /// </summary>
        public List<string> usbGPSResive = new List<string>();

        /// <summary>
        /// ROS中継　通信オブジェクト
        /// </summary>
        private IpcServer ipc = new IpcServer();

        // --------------------------------------------------------------------------------------------------
        public CersioCtrl()
        {
        }

        /// <summary>
        /// 起動
        /// </summary>
        public void Start()
        {
#if EMULATOR_MODE
            // セルシオ　エミュレータ起動
            processEmuSim = Process.Start(@"..\..\..\CersioSim\bin\CersioSim.exe");

            //アイドル状態になるまで待機
            //processEmuSim.WaitForInputIdle();
#endif
        }

        /// <summary>
        /// 終了
        /// </summary>
        public void Close()
        {
            SendCommand_Stop();

            if (null != UsbMotorDriveIO)
            {
                UsbMotorDriveIO.Close();
            }

            objTCPSC.Dispose();

#if EMULATOR_MODE
            // エミュレータ終了
            if (null != processEmuSim && !processEmuSim.HasExited)
            {
                processEmuSim.Kill();
            }
#endif
        }

        /// <summary>
        /// BoxPcと接続
        /// </summary>
        /// <returns></returns>
        public bool ConnectBoxPC()
        {
            if (TCP_IsConnected())
            {
                objTCPSC.Dispose();
                // 少し待つ
                System.Threading.Thread.Sleep(100);
            }

            // 回線オープン
            return objTCPSC.Start();
        }

        /// <summary>
        /// 静止指示
        /// </summary>
        public void SendCommand_Stop()
        {
            if (TCP_IsConnected())
            {
                // 動力停止
                TCP_SendCommand("AC,0.0,0.0\n");
                System.Threading.Thread.Sleep(50);

                // LEDを戻す
                TCP_SendCommand("AL,0,\n");
                System.Threading.Thread.Sleep(50);
            }
        }

        /// <summary>
        /// ロータリーエンコーダにスタート情報をセット
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="dir"></param>
        public void SendCommand_RE_Reset(double x, double y, double dir)
        {
            // 現在座標をセット
            SendCommand("AD," + ((float)x).ToString("f") + "," + ((float)y).ToString("f") + "\n");

            // 角度をリセット
            SendCommand_RE_Reset(dir);
        }

        public void SendCommand_RE_Reset()
        {
            SendCommand_RE_Reset(0.0, 0.0, 0.0);
        }

        /// <summary>
        /// RE回転のみリセット
        /// </summary>
        /// <param name="dir"></param>
        public void SendCommand_RE_Reset(double dir)
        {
            // 角度をパイに、回転の+-を調整
            double rad = -dir * Math.PI / 180.0;
            SendCommand("AR," + rad.ToString("f") + "\n");
        }

        public void setREPlot_Start(double mmX, double mmY, double dir)
        {
            hwREStartX = mmX;
            hwREStartY = mmY;
            hwREStartDir = dir;
        }


        /// <summary>
        /// ハードウェアステータス取得
        /// </summary>
        /// <param name="useUsbGPS">USB接続のGPSを使う</param>
        public void GetHWStatus(bool useUsbGPS)
        {
            if (TCP_IsConnected())
            {
                // 受信コマンド解析
                TCP_ReciveCommand();

                // センサーデータ要求コマンド送信
                // ロータリーエンコーダ値(回転累計)
                SendCommand("A1" + "\n");

                // コンパス取得
                SendCommand("A2" + "\n");

                // GPS取得
                // usbGPSを使う場合は、bServerのGPS情報を取得しない。
                if (!useUsbGPS)
                {
                    SendCommand("A3" + "\n");
                }

                // ロータリーエンコーダ　絶対値取得
                SendCommand("A4" + "\n");

                // ロータリーエンコーダ　ハンドル値修正
                //double rad = (double)(-hwCompass) * Math.PI / 180.0;
                //TCP_SendCommand("AR," + rad.ToString("f") + "\n");


                // ROS-IFへデータ書き込み
                {
                    // REPlotX,Y
                    ipc.RemoteObject.rePlotX = hwREX;
                    ipc.RemoteObject.rePlotY = hwREY;
                    ipc.RemoteObject.reAng = hwREDir;

                    // Compus
                    if (bhwCompass)
                    {
                        ipc.RemoteObject.compusDir = hwCompass;
                    }

                    // RE パルス値
                    ipc.RemoteObject.reRpulse = hwRErotR;
                    ipc.RemoteObject.reLpulse = hwRErotL;

                    if (bhwGPS && !bhwUsbGPS)
                    {
                        ipc.RemoteObject.gpsGrandX = hwGPS_LandX;
                        ipc.RemoteObject.gpsGrandY = hwGPS_LandY;
                    }
                }

                // コマンド送信
                SendCommandQue();
            }

            // USB GPS情報取得
            if (useUsbGPS)
            {
                AnalizeUsbGPS();

                // Ros-IF
                // GPS
                ipc.RemoteObject.gpsGrandX = hwGPS_LandX;
                ipc.RemoteObject.gpsGrandY = hwGPS_LandY;
            }

            // カウンタ更新
            if (cntHeadLED > 0) cntHeadLED--;
        }

        /// <summary>
        /// ROSのLRFデータ取得
        /// </summary>
        /// <returns></returns>
        public double[] GetROS_LRFdata()
        {
            return ipc.RemoteObject.urgData;
        }

        /// <summary>
        /// ACコマンド発行
        /// </summary>
        /// <param name="sendHandle"></param>
        /// <param name="sendAcc"></param>
        public void SetCommandAC( double sendHandle, double sendAcc )
        {
            if (TCP_IsConnected())
            {
                // LAN接続
                SendCommand("AC," + sendHandle.ToString("f2") + "," + sendAcc.ToString("f2") + "\n");
            }
            else if (null != UsbMotorDriveIO)
            {
                // USB接続時
                if (UsbMotorDriveIO.IsConnect())
                {
                    UsbMotorDriveIO.Send_AC_Command(sendHandle, sendAcc);
                }
            }
        }

        /// <summary>
        /// 現在値でコマンド発行
        /// </summary>
        public void SetCommandAC()
        {
            SetCommandAC(nowSendHandleValue, nowSendAccValue);
        }

        /// <summary>
        /// 滑らかに動くハンドル、アクセルワークを計算する
        /// </summary>
        /// <param name="targetHandleVal"></param>
        /// <param name="targetAccelVal"></param>
        public void CalcHandleAccelControl(double targetHandleVal, double targetAccelVal )
        {
            double handleTgt = targetHandleVal * CersioCtrl.HandleRate;
            double accTgt = targetAccelVal * CersioCtrl.AccRate;
            double diffAcc = (accTgt - CersioCtrl.nowSendAccValue);

            // ハンドル、アクセル操作を徐々に目的値に変更する
            CersioCtrl.nowSendHandleValue += (handleTgt - CersioCtrl.nowSendHandleValue) * CersioCtrl.HandleControlPow;
            CersioCtrl.nowSendAccValue += ((diffAcc > 0.0) ? (diffAcc * CersioCtrl.AccControlPowUP) : (diffAcc * CersioCtrl.AccControlPowDOWN));
        }

        // =====================================================================================
        /* patternは、0～9    (,dmyのところは、,だけでも可、dmyはなんでもok
        pattern
        0 通常表示   赤->北の方向、青->向いている方向、緑->北を向いている時
        1 全赤　　　　　　　　　緊急停止時
        2 全緑　　　　　　　　　
        3 全青　　　　　　　　　
        4 白黒点滅回転　　　　　チェックポイント通過時
        5 ?点滅回転　　　　　　　
        6 ?点滅回転　　　　　　　
        7 スマイル               ゴール時
        8 ハザード               回避、徐行時
        9 緑ＬＥＤぐるぐる       BoxPC起動時
        */
        public enum LED_PATTERN {
            Normal = 0,     // 0 通常表示
            RED,            // 1 全赤
            GREEN,          // 2 全緑
            BLUE,           // 3 全青
            WHITE_FLASH,    // 4 白黒点滅回転
            UnKnown1,       // 5 ?点滅回転
            UnKnown2,       // 6 ?点滅回転
            SMILE,          // 7 スマイル 
            HAZERD,         // 8 ハザード
            ROT_GREEN,      // 9 緑ＬＥＤぐるぐる
        };
        public int ptnHeadLED = -1;
        private int cntHeadLED = 0;
        /// <summary>
        /// 
        /// </summary>
        /// <param name="setPattern"></param>
        /// <param name="bForce">強制変更</param>
        /// <returns>変更したor済み..True</returns>
        public bool SetHeadMarkLED(int setPattern, bool bForce=false )
        {
            if (!TCP_IsConnected()) return false;


            if (bForce || (ptnHeadLED != setPattern && cntHeadLED == 0))
            {
                SendCommand("AL," + setPattern.ToString() + ",\n");

                cntHeadLED = 20 * 1;          // しばらく変更しない
                ptnHeadLED = setPattern;
                return true;
            }


            // 通常以外は、現在設定中でさらに送られてきたら延長
            if (setPattern != (int)LED_PATTERN.Normal && ptnHeadLED == setPattern)
            {
                cntHeadLED = 10 * 1;          // 占有時間延長
                return true;
            }
            return false;
        }

        public static string[] LEDMessage = { "Normal", "RED:緊急停止", "GREEN", "BLUE:壁回避", "WHITE_FLASH:チェックポイント通過", "UnKnown1", "UnKnown2", "SMILE:ルート完了", "HAZERD:徐行", "ROT_GREEN" };


        //--------------------------------------------------------------------------------------------------------------
        // BoxPC通信
        private List<string> SendCommandList = new List<string>();

        /// <summary>
        /// コマンド分割送信
        /// </summary>
        private void SendCommandQue()
        {
            if (TCP_IsConnected())
            {
                // 先頭から順に送信
                if (SendCommandList.Count > 0)
                {
                    // コマンド結合
                    string sendMsg = "";
                    for (int i = 0; i < 10; i++)
                    {
                        sendMsg += SendCommandList[0];
                        SendCommandList.RemoveAt(0);

                        if (SendCommandList.Count <= 0) break;
                    }

                    TCP_SendCommand(sendMsg);
                }

                // もし、たまりすぎたら捨てる
                if (SendCommandList.Count > 100)
                {
                    SendCommandList.Clear();
                    // ※ログ出力先　再検討
                    //Brain.addLogMsg += "SendCommandTick: OverFlow!\n";
                }
            }
            else
            {
                // 接続されていないなら、リストをクリア
                SendCommandList.Clear();
            }
        }

        /// <summary>
        /// 送信コマンド受付
        /// リストに積んでいく
        /// </summary>
        /// <param name="comStr"></param>
        public void SendCommand( string comStr )
        {
            SendCommandList.Add(comStr);
        }

        /// <summary>
        /// コマンド送信
        /// </summary>
        /// <param name="comStr"></param>
        public void TCP_SendCommand(string comStr)
        {
            System.Net.Sockets.NetworkStream objStm = objTCPSC.MyProperty;

            if (objStm != null)
            {
                Byte[] dat = System.Text.Encoding.GetEncoding("SHIFT-JIS").GetBytes(comStr);

                try
                {
                    objStm.Write(dat, 0, dat.GetLength(0));
                }
                catch (Exception e)
                {
                    // 接続エラー
                    // ※ログ出力先　再検討
                    //Brain.addLogMsg += "TCP_SendCommand:Error "+e.Message+"\n";

                    objTCPSC.DisConnect();
                }

                hwSendStr += "/" + comStr;
            }
        }



        // -----------------------------------------------------------------------------------------
        //
        //
        public static int SpeedMH = 0;   // 速度　mm/Sec
        double oldResiveMS;         // 速度計測用 受信時間差分
        double oldWheelR;              // 速度計測用　前回ロータリーエンコーダ値

        public double emuGPSX = 134.0000;
        public double emuGPSY = 35.0000;

        /// <summary>
        /// 受信コマンド解析
        /// </summary>
        /// <returns></returns>
        public string TCP_ReciveCommand()
        {
            System.Net.Sockets.TcpClient objSck = objTCPSC.SckProperty;
            System.Net.Sockets.NetworkStream objStm = objTCPSC.MyProperty;
            string readStr = "";



            if (objStm != null && objSck != null)
            {
                // ソケット受信
                if (objSck.Available > 0 && objStm.DataAvailable)
                {
                    Byte[] dat = new Byte[objSck.Available];

                    if (0 == objStm.Read(dat, 0, dat.GetLength(0)))
                    {
                        // 切断を検知
                        objTCPSC.Dispose();
                        return "";
                    }

                    readStr = System.Text.Encoding.GetEncoding("SHIFT-JIS").GetString(dat);
                    hwResiveStr = readStr;

                    {
                        string[] rsvCmd = readStr.Split('$');

                        SpeedMH = 0;   // 未計測
                        for (int i = 0; i < rsvCmd.Length; i++)
                        {
                            if (rsvCmd[i].Length <= 3) continue;

                            // ロータリーエンコーダから　速度を計算
                            if (rsvCmd[i].Substring(0, 3) == "A1,")
                            {
                                const double tiyeSize = 140.0;  // タイヤ直径 [mm]
                                const double OnePuls = 260.0;   // 一周のパルス数
                                double ResiveMS;
                                //string[] splStr = (rsvCmd[i].Replace('[', ',').Replace(']', ',').Replace(' ', ',')).Split(',');
                                string[] splStr = rsvCmd[i].Split(',');

                                // 0 A1
                                double.TryParse(splStr[1], out ResiveMS); // ms? 万ミリ秒に思える
                                double.TryParse(splStr[2], out hwRErotR);        // Right Wheel
                                double.TryParse(splStr[3], out hwRErotL);        // Left Wheel

                                // 絶対値用計算 10000  万ミリ秒
                                SpeedMH = (int)(((double)(hwRErotR - oldWheelR) / OnePuls * Math.PI * tiyeSize) * 10000.0 / (ResiveMS - oldResiveMS));
                                // mm/sec
                                //SpeedMH = (int)(((double)wheelR / 260.0 * Math.PI * 140.0) * 10000.0 / 200.0);

                                oldResiveMS = ResiveMS;
                                oldWheelR = hwRErotR;

                                bhwRE = true;
                            }
                            else if (rsvCmd[i].Substring(0, 3) == "A2,")
                            {
                                // コンパス情報
                                // A2,22.5068,210$
                                double ResiveMS;
                                int ResiveCmp;
                                string[] splStr = rsvCmd[i].Split(',');

                                // splStr[0] "A2"
                                // ミリ秒取得
                                double.TryParse(splStr[1], out ResiveMS); // ms? 万ミリ秒に思える
                                int.TryParse(splStr[2], out ResiveCmp);   // デジタルコンパス値
                                hwCompass = ResiveCmp;
                                bhwCompass = true;
                            }
                            else if (rsvCmd[i].Substring(0, 3) == "A3,")
                            {
                                // GPS情報
                                // $A3,38.266,36.8002,140.11559$
                                double ResiveMS;
                                double ResiveLandX; // 緯度
                                double ResiveLandY; // 経度
                                string[] splStr = rsvCmd[i].Split(',');

                                // splStr[0] "A3"
                                // ミリ秒取得
                                double.TryParse(splStr[1], out ResiveMS); // ms? 万ミリ秒に思える

                                double.TryParse(splStr[2], out ResiveLandX);   // GPS値
                                double.TryParse(splStr[3], out ResiveLandY);
                                hwGPS_LandX = ResiveLandX;
                                hwGPS_LandY = ResiveLandY;
                                bhwGPS = true;
                                bhwUsbGPS = false;
                            }
                            else if (rsvCmd[i].Substring(0, 3) == "A4,")
                            {
                                // ロータリーエンコーダ  プロット座標
                                // 開始時　真北基準
                                /*
                                 * コマンド
                                    A4
                                    ↓
                                    戻り値
                                    A4,絶対座標X,絶対座標Y,絶対座標上での向きR$

                                    絶対座標X[mm]
                                    絶対座標Y[mm]
                                    絶対座標上での向き[rad]　-2π～2π
                                    浮動小数点です。
                                    */
                                double ResiveMS;
                                double ResiveX;
                                double ResiveY;
                                double ResiveRad;

                                string[] splStr = rsvCmd[i].Split(',');

                                // splStr[0] "A4"
                                // ミリ秒取得
                                double.TryParse(splStr[1], out ResiveMS); // ms? 万ミリ秒に思える
                                double.TryParse(splStr[2], out ResiveX);  // 絶対座標X mm
                                double.TryParse(splStr[3], out ResiveY);  // 絶対座標Y mm
                                double.TryParse(splStr[4], out ResiveRad);  // 向き -2PI 2PI

                                // 座標系変換
                                // 右上から右下へ

                                // リセットした時点での電子コンパスの向きを元にマップ座標へ変換する
                                // x*cos - y*sin
                                // x*sin + y*cos
                                {
                                    hwREDir = ((-ResiveRad * 180.0) / Math.PI) + hwREStartDir;

                                    double theta = hwREStartDir / 180.0 * Math.PI;
                                    hwREX = (ResiveX * Math.Cos(theta) - ResiveY * Math.Sin(theta)) + hwREStartX;
                                    hwREY = (ResiveX * Math.Sin(theta) + ResiveY * Math.Cos(theta)) + hwREStartY;
                                }

                                bhwREPlot = true;
                            }

                        }
                    }
                }
            }

            return readStr;
        }

        /// <summary>
        /// BoxPCとの通信状態をかえす
        /// </summary>
        /// <returns></returns>
        public bool TCP_IsConnected()
        {
            System.Net.Sockets.TcpClient objSck = objTCPSC.SckProperty;
            System.Net.Sockets.NetworkStream objStm = objTCPSC.MyProperty;

            if (objStm != null && objSck != null)
            {
                return true;
            }
            return false;
        }


        // -----------------------------------------------------------------------------------------------
        //const double GPSScale = 1.852 * 1000.0 * 1000.0;

        //const double GPSScaleX = 1.51985 * 1000.0 * 1000.0;    // 経度係数  35度時
        //const double GPSScaleY = 1.85225 * 1000.0 * 1000.0;    // 緯度係数

        /// <summary>
        /// USB GPSデータ解析
        /// </summary>
        /// <returns></returns>
        private bool AnalizeUsbGPS()
        {
            if (usbGPSResive.Count <= 10) return false;

            string strBuf = "";

            foreach (var lineStr in usbGPSResive)
            {
                strBuf += lineStr;
            }

            {
                byte[] dat = System.Text.Encoding.GetEncoding("SHIFT-JIS").GetBytes(strBuf);
                MemoryStream mst = new MemoryStream(dat, false);
                StreamReader fsr = new StreamReader(mst, Encoding.GetEncoding("Shift_JIS"));

                string str;

                do
                {
                    str = fsr.ReadLine();

                    if (str == null) break;
                    if (str.Length == 0) continue;

                    string[] dataWord = str.Split(',');



                    switch (dataWord[0])
                    {
                        case "$GPRMC":
                            try
                            {
                                // $GPRMC,020850.000,A,3604.8100,N,14006.9366,E,0.00,15.03,171015,,,A*54
                                if (dataWord.Length < 13) break;

                                double ido = 0.0;

                                // 定世界時(UTC）での時刻。日本標準時は協定世界時より9時間進んでいる。hhmmss.ss
                                //lsData.ms = ParseGPS_MS(dataWord[1]);

                                // dataWord[2] A,V  ステータス。V = 警告、A = 有効
                                if (dataWord[2].ToUpper() != "A") break;    // 受信不良時は受け取らない

                                {
                                    // dataWord[3] 緯度。dddmm.mmmm
                                    string[] dataGPS = dataWord[3].Split('.');
                                    string strDo = dataWord[3].Substring(0, dataGPS[0].Length - 2);
                                    string strHun = dataWord[3].Substring(strDo.Length, dataWord[3].Length - strDo.Length);

                                    hwGPS_LandY = double.Parse(strDo) + (double.Parse(strHun) / 60.0);
                                    ido = double.Parse(strDo);
                                }
                                // dataWord[4] N,S N = 北緯、South = 南緯

                                {
                                    // dataWord[5] 経度。dddmm.mmmm
                                    string[] dataGPS = dataWord[5].Split('.');

                                    string strDo = dataWord[5].Substring(0, dataGPS[0].Length - 2);
                                    string strHun = dataWord[5].Substring(strDo.Length, dataWord[5].Length - strDo.Length);

                                    hwGPS_LandX = double.Parse(strDo) + (double.Parse(strHun) / 60.0);
                                }
                                // dataWord[6] E = 東経、West = 西経

                                // dataWord[7] 地表における移動の速度。000.0～999.9[knot]
                                // dataWord[8] 地表における移動の真方位。000.0～359.9度
                                hwGPS_MoveDir = -double.Parse(dataWord[8]);

                                // dataWord[9] 協定世界時(UTC）での日付。ddmmyy
                                // dataWord[10] 磁北と真北の間の角度の差。000.0～359.9度 	
                                // dataWord[11] 磁北と真北の間の角度の差の方向。E = 東、W = 西 	
                                // dataWord[12] モード, N = データなし, A = Autonomous（自律方式）, D = Differential（干渉測位方式）, E = Estimated（推定）* チェックサム

                                bhwGPS = true;
                                bhwUsbGPS = true;
                            }
                            catch
                            {
                            }
                            break;
                        case "$GPGGA":
                            // ※未対応
                            break;
                        case "$GPGSA":
                            // ※未対応
                            break;
                    }
                }
                while (true);



                // Close
                {
                    if (null != fsr)
                    {
                        fsr.Close();
                        fsr = null;
                    }
                    if (null != mst)
                    {
                        mst.Close();
                        mst = null;
                    }
                }
            }

            usbGPSResive.Clear();

            return true;
        }

        private long ParseGPS_MS(string str)
        {
            return (long)(double.Parse(str) * 1000.0);
        }


    }
}
