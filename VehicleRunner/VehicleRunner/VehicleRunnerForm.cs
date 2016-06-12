﻿
// 動作フラグ

#define LOGWRITE_MODE   // ログファイル出力

#define LOGIMAGE_MODE   // イメージログ出力
#define GPSLOG_OUTPUT   // GPSログ出力
#define LRFLOG_OUTPUT   // LRFログ出力

//#define UnUseLRF          // LRFを使わない


using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

using LocationPresumption;
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
        /// 位置推定管理 クラス
        /// </summary>
        LocPreSumpSystem LocSys;

        /// <summary>
        /// セルシオ管理 クラス
        /// </summary>
        CersioCtrl CersioCt;

        /// <summary>
        /// 動作決定管理クラス
        /// </summary>
        public Brain BrainCtrl;


        /// <summary>
        /// USB GPSクラス
        /// </summary>
        UsbIOport usbGPS;

        Random Rand = new Random();

        double LRFViewScale = 1.0;

        private int selPicboxLRFmode = 1;

        /// <summary>
        /// Form描画処理クラス
        /// </summary>
        private VehicleRunnerForm_Draw formDraw = new VehicleRunnerForm_Draw();

        /// <summary>
        /// ログ処理クラス
        /// </summary>
        private VehicleRunnerForm_Log formLog = new VehicleRunnerForm_Log();


        /// <summary>
        /// 位置補正指示トリガー
        /// </summary>
        private bool bLocRivisionTRG = false;

        /// <summary>
        /// 自律走行フラグ
        /// </summary>
        public bool bRunAutonomous = false;

        /// <summary>
        /// マッピング、ロギングモード フラグ
        /// </summary>
        public bool bRunMappingAndLogging = false;

        // Form内のクラスを更新
        static int updateMainCnt = 0;

        // ハードウェア用 周期の短いカウンタ
        private int updateHwCnt = 0;


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
            CersioCt.Start();

            BrainCtrl = new Brain(CersioCt);
            BrainCtrl.Reset();

            // BoxPc(bServer)接続
            CersioCt.ConnectBoxPC();

            // 自己位置推定初期化
            LocSys = new LocPreSumpSystem();

            // マップ情報設定
            //  マップファイル名、実サイズの横[mm], 実サイズ縦[mm] (北向き基準)
            LocSys.InitWorld( RootingData.MapFileName, RootingData.RealWidth, RootingData.RealHeight);


            // スタート位置をセット
            btn_PositionReset_Click(null, null);

            // マップウィンドウサイズのbmp作成
            formDraw.MakePictureBoxWorldMap(LocSys.worldMap.mapBmp, picbox_AreaMap);

            // LRF 入力スケール調整反映
            tb_LRFScale.Text = trackBar_LRFViewScale.Value.ToString();
            //btm_LRFScale_Click(null, null);
            tb_LRFScale_TextChanged(null, null);


            // bServerエミュレーション表記
            lbl_bServerEmu.Visible = CersioCt.bServerEmu;

            // 位置管理定期処理タイマー起動
            tm_LocUpdate.Enabled = true;
        }

        /// <summary>
        /// フォーム初期化
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void VehicleRunnerForm_Load(object sender, EventArgs e)
        {
            this.SetDesktopLocation(0, 0);

            // USB Connect Select
            {
                // すべてのシリアル・ポート名を取得する
                string[] ports = System.IO.Ports.SerialPort.GetPortNames();

                // シリアルポートを毎回取得して表示するために表示の度にリストをクリアする
                cb_UsbSirial.Items.Clear();
                cmbbox_UsbSH2Connect.Items.Clear();

                foreach (string port in ports)
                {
                    // 取得したシリアル・ポート名を出力する
                    cb_UsbSirial.Items.Add(port);
                    cmbbox_UsbSH2Connect.Items.Add(port);
                }

                if (cb_UsbSirial.Items.Count > 0)
                {
                    cb_UsbSirial.SelectedIndex = 0;
                    cmbbox_UsbSH2Connect.SelectedIndex = 0;
                }
            }

            // フォームのパラメータ反映
            // 自己位置 更新方法
            //LocPreSumpSystem.bMoveUpdateGPS = cb_UseGPS_Move.Checked;

            cb_AlwaysPFCalc.Enabled = rb_UsePF_Revision.Checked;

            // 画面更新
            PictureUpdate();

            // ハードウェア更新タイマ起動
            tm_UpdateHw.Enabled = true;
            tm_SendCom.Enabled = true;
        }

        /// <summary>
        /// フォームクローズ
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void VehicleRunnerForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            LocSys.Close();
            CersioCt.Close();
        }

        /// <summary>
        /// フォームクローズ 完了前処理
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void VehicleRunnerForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            // タイマー停止
            tm_SendCom.Enabled = false;
            tm_UpdateHw.Enabled = false;
            tm_LocUpdate.Enabled = false;

#if LOGIMAGE_MODE
            // マップログ出力
            formLog.Output_ImageLog(ref BrainCtrl, ref LocSys);
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
            if (selAreaMapMode == 0) formDraw.AreaMap_Draw_Area( e.Graphics, ref LocSys, ref CersioCt, ref BrainCtrl);
            else if (selAreaMapMode == 1) formDraw.AreaMap_Draw_WorldMap(e.Graphics, ref LocSys,ref CersioCt, ref BrainCtrl);

            formDraw.AreaMap_Draw_Text(e.Graphics, ref LocSys, ref BrainCtrl, (long)updateHwCnt);
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
#region "LRF,Indicatorエリア 描画"

        /// <summary>
        /// LRFウィンドウデータ描画
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void picbox_LRF_Paint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;

            // LRF取得データを描画
            {
                int ctrX = picbox_LRF.Width / 2;
                int ctrY = picbox_LRF.Height * 6 / 8;

                float scale = 1.0f;

                // 背景色
                switch (selPicboxLRFmode)
                {
                    case (int)LRF_PICBOX_MODE.Normal:
                        ctrX = picbox_LRF.Width / 2;
                        ctrY = picbox_LRF.Height * 6 / 10;

                        picbox_LRF.BackColor = Color.Gray;//Color.White;
                        scale = 1.0f;
                        break;
                    case (int)LRF_PICBOX_MODE.EbsArea:
                        ctrX = picbox_LRF.Width / 2;
                        ctrY = picbox_LRF.Height * 6 / 8;

                        picbox_LRF.BackColor = Color.Black;

                        scale = 5.0f;

                        // EBSに反応があればズーム
                        //scale += ((float)CersioCt.BrainCtrl.EBS_CautionLv * 3.0f / (float)Brain.EBS_CautionLvMax);

                        // EHS
                        //if (CersioCt.BrainCtrl.EHS_Result != Brain.EHS_MODE.None) scale = 10.0f;
                        break;
                    case (int)LRF_PICBOX_MODE.Indicator:
                        picbox_LRF.BackColor = Color.Black;
                        break;
                }


                if (selPicboxLRFmode == (int)LRF_PICBOX_MODE.Normal ||
                    selPicboxLRFmode == (int)LRF_PICBOX_MODE.EbsArea)
                {
                    // ガイドライン描画
                    formDraw.LRF_Draw_GuideLine(g, ref LocSys, ctrX, ctrY, scale);
                }

                switch (selPicboxLRFmode)
                {
                    case (int)LRF_PICBOX_MODE.Normal:
                        // LRF描画
                        if (LocSys.LRF.getData() != null)
                        {
                            formDraw.LRF_Draw_Point(g, LocSys.LRF.getData(), ctrX, ctrY, (LRFViewScale / 1000.0f)*scale);
                        }

                        {
                            int iH = 80;

                            //g.FillRectangle(Brushes.Black, 0, picbox_LRF.Height - iH, picbox_LRF.Width, iH);
                            formDraw.DrawIngicator(g, picbox_LRF.Height - iH, ref CersioCt, ref BrainCtrl);
                        }
                        break;

                    case (int)LRF_PICBOX_MODE.EbsArea:
                        // EBS範囲描画
                        formDraw.LRF_Draw_PointEBS(g, ref LocSys, ref BrainCtrl, LocSys.LRF.getData_UntiNoise(), ctrX, ctrY, scale, (LRFViewScale / 1000.0f) * scale);
                        break;
                    case (int)LRF_PICBOX_MODE.Indicator:
                        picbox_LRF.BackColor = Color.Black;
                        formDraw.DrawIngicator(g, picbox_LRF.Height / 2 - 50, ref CersioCt, ref BrainCtrl);
                        break;
                }
            }
        }

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

#endregion

        /// <summary>
        /// 表示切り替え
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void picbox_LRF_Click(object sender, EventArgs e)
        {
            // モード切り替え
            selPicboxLRFmode = (++selPicboxLRFmode) % (int)LRF_PICBOX_MODE.end;
            picbox_LRF.Invalidate();
        }

        /// <summary>
        /// LRF接続ボタン
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void cb_LRFConnect_CheckedChanged(object sender, EventArgs e)
        {
            if (cb_LRFConnect.Checked)
            {
                // LRF接続

                // 元のカーソルを保持
                Cursor preCursor = Cursor.Current;
                // カーソルを待機カーソルに変更
                Cursor.Current = Cursors.WaitCursor;

                {
                    int intLrfPot;
                    if (int.TryParse(tb_LRFPort.Text, out intLrfPot))
                    {
                        // 指定のIP,Portでオープン
                        LocSys.LRF.Open(tb_LRFIpAddr.Text, intLrfPot);

                        if (LocSys.LRF.IsConnect())
                        {
                            // 接続OK
                            tb_LRFIpAddr.BackColor = Color.Lime;
                            tb_LRFPort.BackColor = Color.Lime;
                        }
                    }
                }

                // カーソルを元に戻す
                Cursor.Current = preCursor;
            }
            else
            {
                // LRF切断
                LocSys.LRF.Close();

                tb_LRFIpAddr.BackColor = SystemColors.Window;
                tb_LRFPort.BackColor = SystemColors.Window;
            }
        }

        // Form内のピクチャー更新
        private void PictureUpdate()
        {
            // 自己位置マーカー入りマップBmp描画
            LocSys.UpdateLocalizeBitmap(cb_AlwaysPFCalc.Checked, true);

            // 各PictureBox更新
            this.picbox_AreaMap.Refresh();
            this.picbox_LRF.Refresh();
            this.picbox_Indicator.Refresh();
        }

        /// <summary>
        /// VRunner リセット
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btn_PositionReset_Click(object sender, EventArgs e)
        {
            // スタート位置をセット
            LocSys.SetStartPostion((int)RootingData.startPosition.x,
                                    (int)RootingData.startPosition.y,
                                    RootingData.startDir);

            // REをリセット
            CersioCt.SendCommand_RE_Reset();

            PictureUpdate();
        }

        //--------------------------------------------------------------------------------------------------------------------
        // タイマイベント処理


        /// <summary>
        /// ハードウェア系の更新
        /// (間隔短め 50MS)
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void tm_UpdateHw_Tick(object sender, EventArgs e)
        {
#if !UnUseLRF
            // LRF更新
            {
                bool resultLRF = LocSys.LRF.Update();

                try
                {
                    if (LocSys.LRF.IsConnect())
                    {
                        // LRFから取得
                        if (resultLRF) lb_LRFResult.Text = "OK";    // 接続して、データも取得
                        else lb_LRFResult.Text = "OK(noData)";      // 接続しているが、データ取得ならず
                    }
                    else
                    {
                        // 仮想マップから取得
                        lb_LRFResult.Text = "Disconnect";
                    }
                }
                catch
                {
                }
            }
#endif

            // 実走行時、bServerと接続が切れたら再接続
            if (updateHwCnt % 50 == 0)
            {
                // 状態を見て、自動接続
                if (!CersioCt.TCP_IsConnected())
                {
                    CersioCt.ConnectBoxPC();
                }
            }

            // bServer ハードウェア(センサー)情報取得
            CersioCt.GetHWStatus( ((usbGPS!=null)?true:false) );

            // ロータリーエンコーダ(Plot座標)情報
            if (CersioCt.bhwREPlot)
            {
                // 自己位置推定に渡す
                LocSys.Input_RotaryEncoder(CersioCt.hwREX, CersioCt.hwREY, CersioCt.hwREDir);

                // 受信情報を画面に表示
                lbl_REPlotX.Text = CersioCt.hwREX.ToString("f1");
                lbl_REPlotY.Text = CersioCt.hwREY.ToString("f1");
                lbl_REPlotDir.Text = CersioCt.hwREDir.ToString("f1");
            }

            // ロータリーエンコーダ（タイヤ回転数）情報
            if (CersioCt.bhwRE)
            {
                lbl_RErotR.Text = CersioCt.hwRErotR.ToString("f1");
                lbl_RErotL.Text = CersioCt.hwRErotL.ToString("f1");
            }

            // 地磁気情報
            if (CersioCt.bhwCompass)
            {
                // 自己位置推定に渡す
                LocSys.Input_Compass(CersioCt.hwCompass);

                // 画面表示
                lbl_Compass.Text = CersioCt.hwCompass.ToString();
            }

            // GPS情報
            if (CersioCt.bhwGPS)
            {
                // 途中からでもGPSのデータを拾う
                if (!LocPreSumpSystem.bEnableGPS)
                {
                    // 応対座標算出のために、取得開始時点のマップ座標とGPS座標をリンク
                    LocPreSumpSystem.Set_GPSStart(CersioCt.hwGPS_LandX,
                                                  CersioCt.hwGPS_LandY,
                                                  (int)(LocSys.R1.X+0.5),
                                                  (int)(LocSys.R1.Y+0.5) );
                }

                // 自己位置推定に渡す
                LocSys.Input_GPSData(CersioCt.hwGPS_LandX, CersioCt.hwGPS_LandY, CersioCt.hwGPS_MoveDir);

                // 画面表示
                lbl_GPS_Y.Text = CersioCt.hwGPS_LandY.ToString("f5");
                lbl_GPS_X.Text = CersioCt.hwGPS_LandX.ToString("f5");
            }

            // LED状態 画面表示
            if (CersioCt.ptnHeadLED == -1)
            {
                lbl_LED.Text = "ND";
            }
            else
            {
                string ledStr = CersioCt.ptnHeadLED.ToString();

                if (CersioCt.ptnHeadLED >= 0 && CersioCt.ptnHeadLED < CersioCtrl.LEDMessage.Count())
                {
                    ledStr += "," + CersioCtrl.LEDMessage[CersioCt.ptnHeadLED];
                }

                if (!ledStr.Equals(lbl_LED.Text))
                {
                    lbl_LED.Text = ledStr;
                }
            }

            // BoxPC接続状態確認
            if (CersioCt.TCP_IsConnected())
            {
                tb_SendData.BackColor = Color.Lime;
                tb_ResiveData.BackColor = Color.Lime;
                lb_BServerConnect.Text = "BServer 接続OK";
                lb_BServerConnect.BackColor = Color.Lime;
            }
            else
            {
                tb_SendData.BackColor = SystemColors.Window;
                tb_ResiveData.BackColor = SystemColors.Window;
                lb_BServerConnect.Text = "BServer 未接続";
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

            // USB GPSからの取得情報画面表示
            if (null != usbGPS)
            {
                tb_SirialResive.Text = usbGPS.resiveStr;

                if (!string.IsNullOrEmpty(usbGPS.resiveStr))
                {
                    CersioCt.usbGPSResive.Add(usbGPS.resiveStr);
                }
                usbGPS.resiveStr = "";

                if (CersioCt.usbGPSResive.Count > 30)
                {
                    CersioCt.usbGPSResive.RemoveRange(0, CersioCt.usbGPSResive.Count - 30);
                }
            }


            updateHwCnt++;
        }

        /// <summary>
        /// 自己位置推定計算用　更新
        /// (間隔長め)
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void tm_LocUpdate_Tick(object sender, EventArgs e)
        {
            // 現在位置更新
            LocSys.update_NowLocation();

            // MAP座標更新処理
            LocSys.MapArea_Update();

            // REからのスピード表示
            tb_RESpeed.Text = CersioCtrl.SpeedMH.ToString("f1");

            // 自律走行処理 更新
            if (bRunAutonomous)
            {
                // セルシオ コントロール
                // 自己位置更新処理とセルシオ管理
                BrainCtrl.AutonomousProc(LocSys,
                                            cb_EmgBrake.Checked, cb_EHS.Checked,
                                            bLocRivisionTRG, cb_AlwaysPFCalc.Checked);

                // 動作内容TextBox表示

                // ハンドル、アクセル値　表示
                tb_AccelVal.Text = CersioCtrl.nowSendAccValue.ToString("f2");
                tb_HandleVal.Text = CersioCtrl.nowSendHandleValue.ToString("f2");

                // エマージェンシーブレーキ 動作カラー表示
                {
                    if (BrainCtrl.EBS.EmgBrk && cb_EmgBrake.Checked) cb_EmgBrake.BackColor = Color.Red;
                    else cb_EmgBrake.BackColor = SystemColors.Control;

                    if (BrainCtrl.EHS.Result != Brain.EmergencyHandring.EHS_MODE.None && cb_EHS.Checked)
                    {
                        cb_EHS.BackColor = Color.Orange;
                    }
                    else cb_EHS.BackColor = SystemColors.Control;

                    // UntiEBS Cnt
                    lbl_BackCnt.Text = "EBS cnt:" + BrainCtrl.EmgBrakeContinueCnt.ToString();
                    lbl_BackProcess.Visible = BrainCtrl.bNowBackProcess;
                }

                bLocRivisionTRG = false;
            }
            updateMainCnt++;


#if LOGWRITE_MODE
            if (bRunAutonomous || bRunMappingAndLogging)
            {

#if LRFLOG_OUTPUT
                // LRFログ出力 
                // データ量が多いので、周期の長い定期処理で実行
                if (LocSys.LRF.IsConnect())
                {
                    formLog.Output_LRFLog(LocSys.LRF.getData());
                }
#endif
                // ログファイル出力
                formLog.Output_VRLog(ref BrainCtrl, ref CersioCt, ref LocSys);

#if GPSLOG_OUTPUT
                if (null != usbGPS)
                {
                    // ログ出力
                    formLog.Output_GPSLog(usbGPS.resiveStr);
                }
#endif

            }
#endif


            // 画面描画
            PictureUpdate();
        }

        /// <summary>
        /// LRF Scale変更
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void tb_LRFScale_TextChanged(object sender, EventArgs e)
        {
            double tval;
            double.TryParse(tb_LRFScale.Text, out tval);

            if (tval != 0.0)
            {
                LRFViewScale = tval;
            }
        }

        /// <summary>
        /// 位置補正 開始
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btn_LocRevision_Click(object sender, EventArgs e)
        {
            // 強制位置補正
            bLocRivisionTRG = true;
        }

        /// <summary>
        /// 直進ルート生成
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void cb_StraightMode_CheckedChanged(object sender, EventArgs e)
        {
            if (cb_StraightMode.Checked)
            {
                BrainCtrl.RTS.ResetStraightMode();
            }

        }

        /// <summary>
        /// 送信コマンド処理 タイマー
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void tm_SendCom_Tick(object sender, EventArgs e)
        {
            if (null != CersioCt)
            {
                CersioCt.SendCommandTick();
            }
        }

        /// <summary>
        /// usbGPS取得ボタン
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void cb_SirialConnect_CheckedChanged(object sender, EventArgs e)
        {
            // 接続中なら一度切断
            if (null != usbGPS)
            {
                usbGPS.Close();
                usbGPS = null;
            }

            if (cb_SirialConnect.Checked)
            {
                // USB GPS接続
                usbGPS = new UsbIOport();
                if (usbGPS.Open(cb_UsbSirial.Text, 4800))
                {
                    // 接続成功
                    tb_SirialResive.BackColor = Color.Lime;
                }
                else
                {
                    // 接続失敗
                    tb_SirialResive.BackColor = SystemColors.Control;
                    tb_SirialResive.Text = "ConnectFail";
                    usbGPS = null;
                }
            }
            else
            {
                tb_SirialResive.BackColor = SystemColors.Control;
            }
        }

        /// <summary>
        /// モータードライバー SH2 USB接続
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void cb_UsbSH2Connect_CheckedChanged(object sender, EventArgs e)
        {
            if (CersioCt != null) return;

            // 接続中なら切断
            if (null != CersioCt.UsbMotorDriveIO)
            {
                CersioCt.UsbMotorDriveIO.Close();
                CersioCt.UsbMotorDriveIO = null;
            }

            if (cb_UsbSH2Connect.Checked)
            {
                // 接続
                CersioCt.UsbMotorDriveIO = new DriveIOport();
                if (CersioCt.UsbMotorDriveIO.Open(cmbbox_UsbSH2Connect.Text, 57600))
                {
                    // 接続成功
                    cmbbox_UsbSH2Connect.BackColor = Color.Lime;
                }
                else
                {
                    // 接続失敗
                    cmbbox_UsbSH2Connect.BackColor = SystemColors.Control;
                    CersioCt.UsbMotorDriveIO = null;
                }
            }
            else
            {
                cmbbox_UsbSH2Connect.BackColor = SystemColors.Control;
            }

        }

        /// <summary>
        /// 移動量入力元変更
        /// ラジオボタンクリック
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void rb_Move_CheckedChanged(object sender, EventArgs e)
        {
            // 移動入力元　センサー切り替え
            LocSys.Setting.bMoveSrcRePlot = false;
            LocSys.Setting.bMoveSrcGPS  = false;
            LocSys.Setting.bMoveSrcSVO  = false;

            if (sender == rb_MoveREPlot)
            {
                LocSys.Setting.bMoveSrcRePlot = true;
            }
            else if (sender == rb_MoveGPS)
            {
                LocSys.Setting.bMoveSrcGPS = true;
            }
            else if (sender == rb_MoveSVO)
            {
                LocSys.Setting.bMoveSrcSVO = true;
            }
        }
        /// <summary>
        /// 向き入力元変更
        /// ラジオボタンクリック
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void rb_Dir_CheckedChanged(object sender, EventArgs e)
        {
            // 向き入力元　センサー切り替え
            LocSys.Setting.bDirSrcRePlot = false;
            LocSys.Setting.bDirSrcGPS = false;
            LocSys.Setting.bDirSrcSVO  = false;
            LocSys.Setting.bDirSrcCompus = false;

            if (sender == rb_DirREPlot)
            {
                LocSys.Setting.bDirSrcRePlot = true;
            }
            else if (sender == rb_DirGPS)
            {
                LocSys.Setting.bDirSrcGPS = true;
            }
            else if (sender == rb_DirSVO)
            {
                LocSys.Setting.bDirSrcSVO = true;
            }
            else if (sender == rb_DirCompus)
            {
                LocSys.Setting.bDirSrcCompus = true;
            }
        }

        /// <summary>
        /// スケールバー変更
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void trackBar_LRFViewScale_Scroll(object sender, EventArgs e)
        {
            tb_LRFScale.Text = trackBar_LRFViewScale.Value.ToString();
            tb_LRFScale_TextChanged(sender, null);
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
                // 現在座標から開始

                // 開始時のリセット
                CersioCt.SendCommandList.Clear();

                LocSys.SetStartPostion( (int)(LocSys.R1.X+0.5),
                                        (int)(LocSys.R1.Y+0.5),
                                        LocSys.R1.Theta);

                // GPS情報があれば GPSの初期値をセット
                if (CersioCt.bhwGPS)
                {
                    // 起点情報をセット
                    LocPreSumpSystem.Set_GPSStart(CersioCt.hwGPS_LandX,
                                                  CersioCt.hwGPS_LandY,
                                                  (int)(LocSys.R1.X + 0.5),
                                                  (int)(LocSys.R1.Y + 0.5) );
                }

                bRunAutonomous = true;
            }
            else
            {
                bRunAutonomous = false;
            }
        }

        /// <summary>
        /// ログ・マッピングモード
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void cb_StartLogMapping_CheckedChanged(object sender, EventArgs e)
        {
            if (cb_StartAutonomous.Checked)
            {
                formLog.init();
                bRunMappingAndLogging = true;
                cb_StartLogMapping.BackColor = Color.Red;
            }
            else
            {
                bRunMappingAndLogging = false;
                cb_StartLogMapping.BackColor = SystemColors.Window;
            }
        }

        /// <summary>
        /// タブ切り替え時
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void tabControl_SelectedIndexChanged(object sender, EventArgs e)
        {
            num_R1X.Value = (int)LocSys.R1.X;
            num_R1Y.Value = (int)LocSys.R1.Y;
            num_R1Dir.Value = (int)LocSys.R1.Theta;
        }

        /// <summary>
        /// GPSから現在位置設定に代入
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btn_GetGPStoR1_Click(object sender, EventArgs e)
        {
            num_R1X.Value = (int)LocSys.G1.X;
            num_R1Y.Value = (int)LocSys.G1.Y;
        }

        /// <summary>
        /// 地磁気から現在位置設定に代入
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btn_GetCompustoR1_Click(object sender, EventArgs e)
        {
            num_R1Dir.Value = (int)LocSys.C1.Theta;
        }

        /// <summary>
        /// 現在位置設定をR1にセット
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btn_ResetR1_Click(object sender, EventArgs e)
        {
            LocSys.R1.X = (double)num_R1X.Value;
            LocSys.R1.Y = (double)num_R1Y.Value;
            LocSys.R1.Theta = (double)num_R1Dir.Value;
        }
    }
}
