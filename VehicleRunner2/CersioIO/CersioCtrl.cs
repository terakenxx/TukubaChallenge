﻿//#define EMULATOR_MODE  // bServer エミュレーション起動


using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

using System.Diagnostics;
using System.Threading;


namespace CersioIO
{
    public class CersioCtrl
    {
        // BOXPC(bServer)通信クラス
        // エミュレータ用プロセス
        Process processEmuSim = null;

        /// <summary>
        /// bServerEmuratorフラグ
        /// </summary>
        public bool bServerEmu = false;

        /// <summary>
        /// bServer通信ソケット
        /// </summary>
        TCPClient objTCPSC = null;


        // モータードライバー直結時
        // アクセル、ハンドルコントローラ
        public DriveIOport UsbMotorDriveIO;


        // 最近　送信したハンドル、アクセル
        static public double nowSendHandleValue;
        static public double nowSendAccValue;

        // 最大ハンドル角
        // 角度から　左　1.0　～　右　-1.0の範囲に変換に使う
        // 値が大きいほど、変化が緩やかになる
        public const double MaxHandleAngle = 20.0;

        // ハンドル、アクセル上限値
        static public double HandleRate = 1.0;
        static public double AccRate = 0.20;

        // ハンドル、アクセルの変化係数
        public const double HandleControlPow = 0.5;//0.125; // 0.15;
        public const double AccControlPowUP = 0.100; // 0.15;   // 加速時は緩やかに
        public const double AccControlPowDOWN = 0.150;

        // HW
        // ロータリーエンコーダ値
        public double hwRErotR = 0.0;
        public double hwRErotL = 0.0;
        public bool bhwRE = false;

        // ロータリーエンコーダ開始値
        private double hwRErotR_Start = 0;
        private double hwRErotL_Start = 0;

        // ROS tf 座標
        public double hwAMCL_X = 0.0;
        public double hwAMCL_Y = 0.0;
        public double hwAMCL_Ang = 0.0;

        /// <summary>
        /// AMCLを受信している
        /// </summary>
        public bool bhwAMCL = false;
        /// <summary>
        /// AMCL取得トリガ
        /// </summary>
        public bool bhwTrgAMCL = false;

        // 送信 ハンドル・アクセル値
        private double sendHandle = 0.0;
        private double sendAccel = 0.0;

        /// <summary>
        /// アクセル情報送信 フラグ
        /// </summary>
        public bool bSendAccel = true;

        // 受信文字
        public string hwResiveStr;
        public string hwSendStr;

        /// <summary>
        /// bServer IpAddr
        /// </summary>
        //private string bServerAddr = "192.168.1.101";

        /// <summary>
        /// bServer エミュレータ
        /// </summary>
        //private string bServerEmuAddr = "127.0.0.1";

        /// <summary>
        /// bServer ポートNo
        /// </summary>
        private int bServerPort = 50001;


        /// <summary>
        /// 
        /// </summary>
        public LEDControl LEDCtrl;

        // --------------------------------------------------------------------------------------------------
        public CersioCtrl()
        {
            LEDCtrl = new LEDControl();
        }

        /// <summary>
        /// 終了
        /// </summary>
        public void Disconnect()
        {
            // 停止コマンド送信
            SendCommand_Stop();

            // USB SH制御解除
            if (null != UsbMotorDriveIO)
            {
                UsbMotorDriveIO.Close();
            }

            // bServer切断
            if (null != objTCPSC)
            {
                objTCPSC.Dispose();
                objTCPSC = null;
            }

            // エミュレータ終了
            if (null != processEmuSim && !processEmuSim.HasExited)
            {
                processEmuSim.Kill();
            }
        }

        // --------------------------------------------------------------------------------------------------
        /// <summary>
        /// bServerと接続
        /// 非同期
        /// </summary>
        /// <returns></returns>
        public void Connect_bServer_Async(string _bServerAddr, int bServerPort = 50001)
        {
            // 接続中なら切断
            if (TCP_IsConnected())
            {
                objTCPSC.Dispose();
                objTCPSC = null;

                // 少し待つ
                System.Threading.Thread.Sleep(100);
            }

            // 通信接続
            objTCPSC = new TCPClient(_bServerAddr, bServerPort);
            // 接続開始(非同期)
            objTCPSC.StartAsync();

            return;
        }

        /// <summary>
        /// bServerと接続
        /// 同期
        /// </summary>
        /// <returns></returns>
        public bool Connect_bServer(string _bServerAddr, int bServerPort = 50001)
        {
            // 接続中なら切断
            if (TCP_IsConnected())
            {
                objTCPSC.Dispose();
                objTCPSC = null;

                // 少し待つ
                System.Threading.Thread.Sleep(100);
            }

            // 通信接続
            objTCPSC = new TCPClient(_bServerAddr, bServerPort);
            // 接続開始(同期)
            return objTCPSC.Start();
        }

        /// <summary>
        /// 静止指示
        /// </summary>
        public void SendCommand_Stop()
        {
            if (TCP_IsConnected())
            {
                // 他のACコマンドを発行するスレッドの終了を待つ
                System.Threading.Thread.Sleep(250);

                // 動力停止
                TCP_SendCommand("AC,0.0,0.0\n");
                System.Threading.Thread.Sleep(50);

                // LEDを戻す
                TCP_SendCommand("AL,0,\n");
                System.Threading.Thread.Sleep(50);

                sendHandle = 0.0;
                sendAccel = 0.0;
            }
        }

        /// <summary>
        /// bServerとの通信状態をかえす
        /// </summary>
        /// <returns></returns>
        public bool TCP_IsConnected()
        {
            if (null == objTCPSC) return false;

            System.Net.Sockets.TcpClient objSck = objTCPSC.SckProperty;
            System.Net.Sockets.NetworkStream objStm = objTCPSC.MyProperty;

            if (objStm != null && objSck != null)
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// 接続先 アドレス取得
        /// </summary>
        /// <returns></returns>
        public string TCP_GetConnectedAddr()
        {
            if (!TCP_IsConnected()) return "";

            return objTCPSC.ipStringProperty;
        }


        // --------------------------------------------------------------------------------------------------

        /// <summary>
        /// ハードウェアステータス取得
        /// </summary>
        /// <returns>true..取得 / false..取得不可</returns>
        public bool UpDate()
        {
            LEDCtrl.UpDate();

            if (!TCP_IsConnected()) return false;

            {
                // 受信コマンド解析
                TCP_ReciveCommand();

                // センサーデータ要求コマンド送信
                // ロータリーエンコーダ値(回転累計)
                SendCommand("A1" + "\n");

                // ロータリーエンコーダ　絶対値取得
                SendCommand("A4" + "\n");

                // コマンド送信
                //SendCommandQue();
            }

            return true;
        }


        // --------------------------------------------------------------------------------------------------
        // --------------------------------------------------------------------------------------------------

        /// <summary>
        /// 滑らかに動くハンドル、アクセルワークを計算する
        /// </summary>
        /// <param name="targetHandleVal"></param>
        /// <param name="targetAccelVal"></param>
        public void SendCalcHandleAccelControl(double targetHandleVal, double targetAccelVal)
        {
            double handleTgt = targetHandleVal * HandleRate;
            double accTgt = targetAccelVal * AccRate;
            double diffAcc = (accTgt - nowSendAccValue);

            // ハンドル操作を徐々に目的値に変更する
            nowSendHandleValue += (handleTgt - nowSendHandleValue) * HandleControlPow;
            // アクセル　加速時、減速時で係数を変更
            nowSendAccValue += ((diffAcc > 0.0) ? (diffAcc * AccControlPowUP) : (diffAcc * AccControlPowDOWN));

            // ACコマンド送信
            SetCommandAC(nowSendHandleValue, nowSendAccValue);
        }

        /// <summary>
        /// 滑らかに動くハンドル、アクセルワークを計算する
        /// スピードで、アクセルをコントロール
        /// </summary>
        /// <param name="targetHandleVal"></param>
        /// <param name="targetAccelVal"></param>
        public void SendCalcHandleSpeedControl(double targetHandleVal, double targetSpeedMmSec )
        {
            double targetAccel = 0.0;
            double diffSpeed = (targetSpeedMmSec - SpeedMmSec);

            // 小さい差分は無視
            if(Math.Abs(diffSpeed) < 5.0 ) diffSpeed = 0.0;

            // 時速4Km は 秒速 1100mm
            // 1100mmを差分1.0とするか？
            targetAccel = nowSendAccValue + (diffSpeed/100.0);

            // スピード更新カウンタ
            if (SpeedUpdateCnt > 0)
            {
                SpeedUpdateCnt--;
            }
            else
            {
                // スピード情報が更新されておらず、あてにならない場合
                targetAccel = 0.0;
                if (targetSpeedMmSec > 5.0) targetAccel = 1.0;
                if (targetSpeedMmSec < -5.0) targetAccel = -1.0;
            }

            SendCalcHandleAccelControl(targetHandleVal, targetAccel);
        }

        /// <summary>
        /// １回転のパルス値セット
        /// </summary>
        /// <param name="dir"></param>
        public void SendCommand_RE_OneRotatePulse_Reset(double wheelL, double wheelR)
        {
            SendCommand("EP," + wheelL.ToString("f") + "," + wheelR.ToString("f") + "\n");
        }

        /// <summary>
        /// ACコマンド発行
        /// </summary>
        /// <param name="_sendHandle"></param>
        /// <param name="_sendAcc"></param>
        public void SetCommandAC( double _sendHandle, double _sendAcc )
        {
            sendHandle = _sendHandle;

            if (bSendAccel)
            {
                sendAccel = _sendAcc;
            }
            else
            {
                sendAccel = 0.0;
            }

            if (TCP_IsConnected())
            {
                // LAN接続
                SendCommand("AC," + sendHandle.ToString("f2") + "," + sendAccel.ToString("f2") + "\n");
            }
            else if (null != UsbMotorDriveIO)
            {
                // USB接続時
                if (UsbMotorDriveIO.IsConnect())
                {
                    UsbMotorDriveIO.Send_AC_Command(sendHandle, sendAccel);
                }
            }
        }

        //--------------------------------------------------------------------------------------------------------------
        // bServerコマンド送信
        /*
        private List<string> SendCommandList = new List<string>();

        /// <summary>
        /// コマンド分割送信
        /// </summary>
        private void SendCommandQue()
        {
            if (TCP_IsConnected())
            {
                // 先頭から順に送信
                while (SendCommandList.Count > 0)
                {
                    string sendMsg = SendCommandList[0];
                    TCP_SendCommand(sendMsg);
                    Thread.Sleep(10);

                    if (SendCommandList.Count > 0)
                    {
                        SendCommandList.RemoveAt(0);
                    }
                }
            }

            // 接続されていないなら、リストをクリア
            SendCommandList.Clear();
        }
        */

        /// <summary>
        /// 送信コマンド受付
        /// リストに積んでいく
        /// </summary>
        /// <param name="comStr"></param>
        public void SendCommand( string comStr )
        {
            // キューに積む
            //SendCommandList.Add(comStr);

            TCP_SendCommand(comStr);
        }

        /// <summary>
        /// コマンド送信
        /// </summary>
        /// <param name="comStr"></param>
        public void TCP_SendCommand(string comStr)
        {
            if (null == objTCPSC) return;

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
                    Debug.WriteLine("Exception Error!! TCP_SendCommand()" + e.Message);

                    objTCPSC.DisConnect();
                }

                hwSendStr += "/" + comStr;
            }
        }


        /// <summary>
        /// Ring LED
        /// </summary>
        /// <param name="setPattern"></param>
        /// <param name="bForce"></param>
        /// <returns></returns>
        public bool SetHeadMarkLED(LEDControl.LED_PATTERN setPattern, bool bForce = false)
        {
            if (!TCP_IsConnected()) return false;

            return LEDCtrl.SetHeadMarkLED(this, (int)setPattern, bForce);
        }

        // -----------------------------------------------------------------------------------------
        //
        //
        public static int SpeedMmSec = 0;   // 速度　mm/Sec
        int SpeedUpdateCnt = 0;
        double oldSpeedSec;         // 速度計測用 受信時間差分

        double oldWheelR;              // 速度計測用　前回ロータリーエンコーダ値
        double oldWheelL;              // 速度計測用　前回ロータリーエンコーダ値

        // RE初期値
        public bool bInitRePulse = true;   // 初期化要求フラグ

        public double emuGPSX = 134.0000;
        public double emuGPSY = 35.0000;

        /// <summary>
        /// 受信コマンド解析
        /// </summary>
        /// <returns></returns>
        public string TCP_ReciveCommand()
        {
            if (null == objTCPSC) return "";

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
                    hwResiveStr += readStr;

                    {
                        string[] rsvCmd = readStr.Split('$');

                        for (int i = 0; i < rsvCmd.Length; i++)
                        {
                            if (rsvCmd[i].Length <= 3) continue;

                            // ロータリーエンコーダから　速度を計算
                            if (rsvCmd[i].Substring(0, 3) == "A1,")
                            {
                                const double tiyeSize = 65.0;  // タイヤ直径 [mm]
                                const double OnePuls = 240.0;   // 一周のパルス数
                                double ResiveMS;
                                double ResReR, ResReL;
                                string[] splStr = rsvCmd[i].Split(',');

                                // 0 A1
                                double.TryParse(splStr[1], out ResiveMS);        // 小数点付き 秒
                                double.TryParse(splStr[2], out ResReR);        // Right Wheel
                                double.TryParse(splStr[3], out ResReL);        // Left Wheel

                                {
                                    double SpeedSec = (double)System.Environment.TickCount / 1000.0;
                                    // 0.25秒以上の経過時間があれば計算 (あまりに瞬間的な値では把握しにくいため)
                                    if ((SpeedSec - oldSpeedSec) > 0.25)
                                    {
                                        // 速度計算(非動輪を基準)
                                        double wheelPulse = ((hwRErotR - oldWheelR) + (hwRErotL - oldWheelL)) * 0.5;

                                        SpeedMmSec = (int)((wheelPulse / OnePuls * (Math.PI * tiyeSize)) * (SpeedSec - oldSpeedSec));

                                        oldSpeedSec = SpeedSec;
                                        oldWheelR = hwRErotR;
                                        oldWheelL = hwRErotL;
                                        SpeedUpdateCnt = 20;
                                    }
                                }

                                // 初回のパルス値リセット
                                if(bInitRePulse)
                                {
                                    hwRErotR = 0;
                                    hwRErotL = 0;
                                    bInitRePulse = false;
                                }

                                // 取得した差分を加算する
                                hwRErotR += ResReR;
                                hwRErotL += ResReL;



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
                                //hwCompass = ResiveCmp;
                                //bhwCompass = true;
                            }
                            else if (rsvCmd[i].Substring(0, 3) == "A3,")
                            {
                                // GPS情報
                                // $A3,38.266,36.8002,140.11559$
                                double ResiveMS;
                                double ResiveLandX; // 緯度
                                double ResiveLandY; // 経度
                                string[] splStr = rsvCmd[i].Split(',');

                                // データが足らないことがある
                                if (splStr.Length >= 4)
                                {
                                    // splStr[0] "A3"
                                    // ミリ秒取得
                                    double.TryParse(splStr[1], out ResiveMS); // ms? 万ミリ秒に思える

                                    double.TryParse(splStr[2], out ResiveLandX);   // GPS値
                                    double.TryParse(splStr[3], out ResiveLandY);
                                    //bhwGPS = true;
                                    //bhwUsbGPS = false;
                                }
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
                                double.TryParse(splStr[2], out ResiveX);  // ROS World座標X m
                                double.TryParse(splStr[3], out ResiveY);  // ROS World座標Y m
                                double.TryParse(splStr[4], out ResiveRad);  // 向き -2PI 2PI

                                hwAMCL_Ang = -ResiveRad;
                                hwAMCL_X = ResiveX;
                                hwAMCL_Y = -ResiveY;

                                if (!bhwAMCL)
                                {
                                    // AMCL受信トリガ ON
                                    bhwTrgAMCL = true;
                                }
                                bhwAMCL = true;
                            }

                        }
                    }
                }
            }

            return readStr;
        }
    }
}