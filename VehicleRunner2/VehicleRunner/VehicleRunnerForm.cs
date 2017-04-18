﻿
// 動作フラグ
//#define EMULATOR_MODE  // LRF エミュレーション起動

#define LOGWRITE_MODE   // ログファイル出力

#define LOGIMAGE_MODE   // イメージログ出力
//#define GPSLOG_OUTPUT   // GPSログ出力
//#define LRFLOG_OUTPUT   // LRFログ出力

//#define UnUseLRF          // LRFを使わない


using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;

using Location;
using CersioIO;
using Navigation;

namespace VehicleRunner
{
    /// <summary>
    /// VehicleRunnerフォーム
    /// </summary>
    public partial class VehicleRunnerForm : Form
    {
        /// <summary>
        /// LRF領域表示ウィンドウモード
        /// </summary>
        public enum LRF_PICBOX_MODE {
            Normal = 0,
            EbsArea,
            end,
            Indicator,
            //end
        };

        /// <summary>
        /// セルシオ管理 クラス
        /// </summary>
        CersioCtrl CersioCt;

        /// <summary>
        /// 動作決定管理クラス
        /// </summary>
        Brain BrainCtrl;

        /// <summary>
        /// 起動時のマップファイル
        /// </summary>
        private const string defaultMapFile = "../../../MapFile/syaoku201702/syaoku20170218.xml";

        Random Rand = new Random();

        /// <summary>
        /// Form描画処理クラス
        /// </summary>
        private VehicleRunnerForm_Draw formDraw = new VehicleRunnerForm_Draw();

        /// <summary>
        /// ログ処理クラス
        /// </summary>
        private VehicleRunnerForm_Log formLog = new VehicleRunnerForm_Log();


        /// <summary>
        /// 自律走行フラグ
        /// </summary>
        public bool bRunAutonomous = false;

        /// <summary>
        /// エミュ プロセス
        /// </summary>
        Process processCarEmu = null;

        // ハードウェア用 周期の短いカウンタ
        private int updateHwCnt = 0;

        /// <summary>
        /// アプリ終了フラグ
        /// </summary>
        private bool appExit = false;

        /// <summary>
        /// bServer IpAddr
        /// </summary>
        private string bServerAddr = "192.168.1.101";

        /// <summary>
        /// bServer エミュレータ
        /// </summary>
        private string bServerEmuAddr = "127.0.0.1";

        // ロータリーエンコーダ座標計算用
        private PointD wlR = new PointD();
        private PointD wlL = new PointD();
        private double reOldR, reOldL;
        private double reAng;

        // ================================================================================================================

        /// <summary>
        /// コンストラクタ
        /// </summary>
        public VehicleRunnerForm()
        {
            InitializeComponent();

            formLog.init();

            // セルシオコントローラ初期化
            CersioCt = new CersioCtrl();

            // bServer Emu 接続開始
            if (CersioCt.Connect_bServer(bServerEmuAddr))
            {
                // bServerエミュレーション表記
                lbl_bServerEmu.Visible = true;
            }
            else
            {
                lbl_bServerEmu.Visible = false;
            }

            // ブレイン起動
            BrainCtrl = new Brain(CersioCt, defaultMapFile);

            // スタート位置
            num_R1X.Value = (decimal)BrainCtrl.LocSys.mapData.startPosition.x;
            num_R1Y.Value = (decimal)BrainCtrl.LocSys.mapData.startPosition.y;
            num_R1Dir.Value = (decimal)BrainCtrl.LocSys.mapData.startDir;

            // マップウィンドウサイズのbmp作成
            formDraw.MakePictureBoxWorldMap( BrainCtrl.LocSys.mapBmp, picbox_AreaMap);

            // センサー値取得 スレッド起動
            Thread trdSensor = new Thread(new ThreadStart(ThreadSensorUpdate_bServer));
            trdSensor.IsBackground = true;
            trdSensor.Priority = ThreadPriority.AboveNormal;
            trdSensor.Start();

            // 位置座標更新　スレッド起動
            /*
            Thread trdLocalize = new Thread(new ThreadStart(ThreadLocalizationUpdate));
            trdLocalize.IsBackground = true;
            //trdSensor.Priority = ThreadPriority.AboveNormal;
            trdLocalize.Start();
            */

            // Accel Flag
            cb_AccelOff_CheckedChanged(this, null);

#if EMULATOR_MODE
            // LRF エミュレーション
            tb_LRFIpAddr.Text = "127.0.0.10";
#endif

        }

        /// <summary>
        /// フォーム初期化
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void VehicleRunnerForm_Load(object sender, EventArgs e)
        {
            // 表示位置指定
            this.SetDesktopLocation(0, 0);

            // マップ名設定
            tb_MapName.Text = BrainCtrl.LocSys.mapData.MapName;

            // 画面更新
            PictureUpdate();

            // ハードウェア更新タイマ起動
            //tm_UpdateHw.Enabled = true;
            // 位置管理定期処理タイマー起動
            tm_Update.Enabled = true;

            //tm_SendCom.Enabled = true;
        }

        /// <summary>
        /// フォームクローズ
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void VehicleRunnerForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            CersioCt.Disconnect();
        }

        /// <summary>
        /// フォームクローズ 完了前処理
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void VehicleRunnerForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            // アプリ終了フラグ
            appExit = true;

            // タイマー停止
            tm_Update.Enabled = false;

#if LOGIMAGE_MODE
            // マップログ出力
            formLog.Output_ImageLog(ref BrainCtrl);
#endif
        }


        // Draw -------------------------------------------------------------------------------------------
        int selAreaMapMode = 0;

        /// <summary>
        /// エリアマップ描画
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void picbox_AreaMap_Paint(object sender, PaintEventArgs e)
        {
            // 書き換えＢＭＰ（追加障害物）描画
            if (BrainCtrl.AvoidRootDispTime != null &&
                BrainCtrl.AvoidRootDispTime > DateTime.Now )
            {
                // 回避イメージ描画
                // ※わくに収まるサイズに
                e.Graphics.DrawImage(BrainCtrl.AvoidRootImage, 0, 0);
            }
            else
            {
                if (selAreaMapMode == 0)
                {
                    // エリアマップ描画
                    formDraw.AreaMap_Draw_Area(e.Graphics, picbox_AreaMap, ref BrainCtrl.LocSys);
                    //formDraw.AreaMap_Draw_Ruler(e.Graphics, ref BrainCtrl, picbox_AreaMap.Width, picbox_AreaMap.Height);
                }
                else if (selAreaMapMode == 1)
                {
                    // ワールドマップ描画
                    formDraw.AreaMap_Draw_WorldMap(e.Graphics, ref CersioCt, ref BrainCtrl);
                }

                // テキスト描画
                formDraw.AreaMap_Draw_Text(e.Graphics, ref BrainCtrl, (long)updateHwCnt);
            }
        }

        /// <summary>
        /// 表示切り替え
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void picbox_AreaMap_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == System.Windows.Forms.MouseButtons.Left)
            {
                // エリア、ワールドマップ切り替え
                selAreaMapMode = (++selAreaMapMode) % 2;
            }
        }


        // ---------------------------------------------------------------------------------------------------
        /// <summary>
        /// インジケーターウィンドウ描画
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void picbox_Indicator_Paint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;

            // LRF取得データを描画
            int ctrX = picbox_Indicator.Width / 2;
            int ctrY = picbox_Indicator.Height * 6 / 8;

            picbox_Indicator.BackColor = Color.Black;
            formDraw.DrawIngicator(g, 0, ref CersioCt, ref BrainCtrl);
        }

        /// <summary>
        /// Form内のピクチャー更新
        /// </summary>
        private void PictureUpdate()
        {
            // 自己位置マーカー入りマップBmp描画
            //BrainCtrl.LocSys.UpdateLocalizeBitmap(true);

            // 各PictureBox更新
            this.picbox_AreaMap.Refresh();
            this.picbox_Indicator.Refresh();
        }

        /// <summary>
        /// VRunner リセット
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btn_PositionReset_Click(object sender, EventArgs e)
        {
            BrainCtrl.Reset_StartPosition( true );
            PictureUpdate();
        }

        //--------------------------------------------------------------------------------------------------------------------
        // タイマイベント処理

        /// <summary>
        /// センサー値更新 スレッド
        /// (ROS-IF)
        /// </summary>
        private void ThreadSensorUpdate_bServer()
        {
            int oldTick = System.Environment.TickCount;

            while (!appExit)
            {
                // bServer ハードウェア(センサー)情報取得
                if (null != CersioCt)
                {
                    CersioCt.UpDate();
                }

                // Sleep
                // 20Hz
                {
                    int nowTick = System.Environment.TickCount;
                    int sleepTick = (oldTick+50) - nowTick;

                    oldTick = (nowTick + (sleepTick < 0 ? 0 : sleepTick));
                    if (sleepTick > 0)
                    {
                        Thread.Sleep(sleepTick);
                    }
                }
            }
        }


        /// <summary>
        /// 描画と親密な定期処理
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void tm_Update_Tick(object sender, EventArgs e)
        {
            if (null != BrainCtrl && null != BrainCtrl.LocSys)
            {
                LocationSystem LocSys = BrainCtrl.LocSys;

                // tm_UpdateHw_Tick
                {
                    // 実走行時、bServerと接続が切れたら再接続
                    if (updateHwCnt % 100 == 0)
                    {
                        // 切断状態なら、自動接続
                        if (!CersioCt.TCP_IsConnected())
                        {
                            //
                            //CersioCt.Connect_bServer_Async(bServerEmuAddr);
                            CersioCt.Connect_bServer_Async(bServerAddr);
                        }
                    }

                    // ロータリーエンコーダ（タイヤ回転数）情報
                    if (CersioCt.bhwRE)
                    {
                        lbl_RErotR.Text = CersioCt.hwRErotR.ToString("f1");
                        lbl_RErotL.Text = CersioCt.hwRErotL.ToString("f1");
                    }

#if true
                    // AMCL
                    LocSys.Input_ROSPosition(CersioCt.hwAMCL_X, CersioCt.hwAMCL_Y, CersioCt.hwAMCL_Ang);
                    if (CersioCt.bhwTrgAMCL)
                    {
                        // 受信再開時の初期化
                        LocSys.Reset_ROSPosition(CersioCt.hwAMCL_X, CersioCt.hwAMCL_Y, CersioCt.hwAMCL_Ang);
                        CersioCt.bhwTrgAMCL = false;
                    }
#else
                    // VehicleRunner Plot
                    {
                        PointD wlPos = new PointD();
                        REncoderToMap.CalcWheelPlotXY( ref wlR, ref wlL, ref reAng,
                                                       CersioCt.hwRErotR,
                                                       CersioCt.hwRErotL,
                                                       reOldR,
                                                       reOldL);

                        wlPos.X = (wlR.X + wlL.X) * 0.5;
                        wlPos.Y = (wlR.Y + wlL.Y) * 0.5;

                        LocSys.Input_VRPosition(wlPos.X, wlPos.Y, -(reAng * 180.0 / Math.PI));
                        if (CersioCt.bhwTrgAMCL)
                        {
                            // 受信再開時の初期化
                            LocSys.Reset_VRPosition(wlPos.X, wlPos.Y, -(reAng * 180.0 / Math.PI));
                            CersioCt.bhwTrgAMCL = false;
                        }

                        reOldR = CersioCt.hwRErotR;
                        reOldL = CersioCt.hwRErotL;
                    }
#endif

                    // LED状態 画面表示
                    if (CersioCt.LEDCtrl.ptnHeadLED == -1)
                    {
                        lbl_LED.Text = "ND";
                    }
                    else
                    {
                        string ledStr = CersioCt.LEDCtrl.ptnHeadLED.ToString();

                        if (CersioCt.LEDCtrl.ptnHeadLED >= 0 && CersioCt.LEDCtrl.ptnHeadLED < LEDControl.LEDMessage.Count())
                        {
                            ledStr += "," + LEDControl.LEDMessage[CersioCt.LEDCtrl.ptnHeadLED];
                        }

                        if (!ledStr.Equals(lbl_LED.Text))
                        {
                            lbl_LED.Text = ledStr;
                        }
                    }

                    // 現在座標 表示
                    lbl_REPlotX.Text = LocSys.GetResultLocationX().ToString("F2");
                    lbl_REPlotY.Text = LocSys.GetResultLocationY().ToString("F2");
                    lbl_REPlotDir.Text = LocSys.GetResultAngle().ToString("F2");

                    // BoxPC接続状態確認
                    if (CersioCt.TCP_IsConnected())
                    {
                        // 接続ＯＫ
                        tb_SendData.BackColor = Color.Lime;
                        tb_ResiveData.BackColor = Color.Lime;
                        lb_BServerConnect.Text = "bServer [" + CersioCt.TCP_GetConnectedAddr() + "] 接続OK";
                        lb_BServerConnect.BackColor = Color.Lime;
                    }
                    else
                    {
                        // 接続ＮＧ
                        tb_SendData.BackColor = SystemColors.Window;
                        tb_ResiveData.BackColor = SystemColors.Window;
                        lb_BServerConnect.Text = "bServer 未接続";
                        lb_BServerConnect.BackColor = SystemColors.Window;
                    }


                    // 送受信文字 画面表示
                    if (null != CersioCt.hwResiveStr)
                    {
                        tb_ResiveData.Text = CersioCt.hwResiveStr.Replace('\n', ' ');
                    }
                    if (null != CersioCt.hwSendStr)
                    {
                        tb_SendData.Text = CersioCt.hwSendStr.Replace('\n', ' ');
                    }

                    updateHwCnt++;
                }

                // マップ上の現在位置更新
                BrainCtrl.LocSys.update_NowLocation();

                // 自律走行(緊急ブレーキ、壁よけ含む)処理 更新
                BrainCtrl.AutonomousProc( false,
                                          false,
                                          cb_InDoorMode.Checked,
                                          bRunAutonomous);

                // 距離計
                tb_Trip.Text = (LocSys.GetResultDistance_mm() * (1.0 / 1000.0)).ToString("f2");
            }

            // REからのスピード表示
            tb_RESpeed.Text = ((CersioCtrl.SpeedMmSec*3600.0)/(1000.0*1000.0)).ToString("f2");


            // ハンドル、アクセル値　表示
            tb_AccelVal.Text = CersioCtrl.nowSendAccValue.ToString("f2");
            tb_HandleVal.Text = CersioCtrl.nowSendHandleValue.ToString("f2");

            // 自律走行情報
            if (bRunAutonomous)
            {
                // 動作内容TextBox表示
                lbl_BackProcess.Text = "自律走行 モード";
            }
            else
            {
                lbl_BackProcess.Text = "モニタリング モード";
            }


#if LOGWRITE_MODE
            // 自律走行、またはロギング中
            if (bRunAutonomous)
            {
                // ログファイル出力
                formLog.Output_VRLog(ref BrainCtrl, ref CersioCt);
            }
#endif  // LOGWRITE_MODE
            
            // ログバッファクリア
            formLog.LogBuffer_Clear(ref BrainCtrl, ref CersioCt);

            // 画面描画
            PictureUpdate();
        }


        /// <summary>
        /// 自律走行モード
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void cb_Autonomous_CheckedChanged(object sender, EventArgs e)
        {
            if (cb_StartAutonomous.Checked)
            {
                LocationSystem LocSys = BrainCtrl.LocSys;

                // 現在座標から開始
                //hwRErotR_Start = CersioCt.hwRErotR;
                //hwRErotL_Start = CersioCt.hwRErotL;

                bRunAutonomous = true;
                cb_StartAutonomous.BackColor = Color.LimeGreen;
            }
            else
            {
                // 停止
                bRunAutonomous = false;

                CersioCt.SendCommand_Stop();
                cb_StartAutonomous.BackColor = SystemColors.Window;
            }
        }

        /// <summary>
        /// タブ切り替え時
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void tabControl_SelectedIndexChanged(object sender, EventArgs e)
        {
            LocationSystem LocSys = BrainCtrl.LocSys;

            num_R1X.Value = (int)LocSys.R1.X;
            num_R1Y.Value = (int)LocSys.R1.Y;
            num_R1Dir.Value = (int)LocSys.R1.Theta;
        }

        /// <summary>
        /// bServerエミュレータ切り替え
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void cb_ConnectBServerEmu_CheckedChanged(object sender, EventArgs e)
        {
            string connectAddr = bServerAddr;

            if (cb_ConnectBServerEmu.Checked)
            {
                if (null == processCarEmu || processCarEmu.HasExited)
                {
                    // エミュレータ起動
                    string stCurrentDir = System.IO.Directory.GetCurrentDirectory();
                    stCurrentDir = stCurrentDir.Substring(0, stCurrentDir.IndexOf("\\VehicleRunner2"));
                    processCarEmu = Process.Start(stCurrentDir + "\\VehicleRunner2\\CersioSim\\bin\\Release\\CersioSim.exe", BrainCtrl.LocSys.mapData.MapFileName);
                }

                // bServerエミュレーション表記
                lbl_bServerEmu.Visible = true;

                // エミュレータIPアドレス
                connectAddr = bServerEmuAddr;
            }
            else
            {
                // bServerエミュレーション表記
                lbl_bServerEmu.Visible = false;
            }

            // bServer接続
            CersioCt.Connect_bServer_Async(connectAddr);
        }

        /// <summary>
        /// Map選択
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btn_MapLoad_Click(object sender, EventArgs e)
        {
            OpenFileDialog dlg = new OpenFileDialog();

            if(dlg.ShowDialog() ==  DialogResult.OK  )
            {
                // 自己位置計算 一時停止
                tm_Update.Enabled = false;

                // Mapロード
                BrainCtrl = new Brain(CersioCt, dlg.FileName);

                // マップウィンドウサイズのbmp作成
                formDraw.MakePictureBoxWorldMap(BrainCtrl.LocSys.mapBmp, picbox_AreaMap);

                // マップ名設定
                tb_MapName.Text = BrainCtrl.LocSys.mapData.MapName;

                // 自己位置計算 再開
                tm_Update.Enabled = true;
            }
        }

        /// <summary>
        /// アクセルOff ボタン
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void cb_AccelOff_CheckedChanged(object sender, EventArgs e)
        {
            if (cb_AccelOff.Checked) CersioCt.bSendAccel = false;
            else CersioCt.bSendAccel = true;
        }
    }
}