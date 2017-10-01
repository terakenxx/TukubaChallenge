﻿
#define EBS_HANDLE_LINK  // ハンドルの向き追従　緊急ブレーキ

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Location;
using CersioIO;
using Axiom.Math;       // Vector3D計算ライブラリ

using System.Drawing;
using VRSystemConfig;

namespace Navigation
{
    /// <summary>
    /// セルシオ制御　頭脳クラス
    /// </summary>
    public class Brain
    {
        /// <summary>
        /// セルシオ制御クラス
        /// </summary>
        public CersioCtrl CarCtrl;

        /// <summary>
        /// 現在位置システム
        /// </summary>
        public LocationSystem LocSys;

        /// <summary>
        /// モード
        /// </summary>
        public ModeControl ModeCtrl;


        /// <summary>
        /// 更新カウンタ
        /// </summary>
        private long UpdateCnt;


        /// <summary>
        /// ゴール到達フラグ
        /// </summary>
        public bool goalFlg = false;

        /// <summary>
        /// 自己位置補正要請フラグ
        /// </summary>
        public bool bRevisionRequest = false;

        /// <summary> bServer接続状態フラグ </summary>
        private bool bServerConnectFlg = false;
        /// <summary> bServer接続タイミング　トリガ </summary>
        private bool trg_bServerConnect = false;

        /// <summary>自律モードフラグ </summary>
        bool bRunAutonomousOld;

        /// <summary>
        /// EBS 緊急ブレーキ
        /// </summary>
        //public EmergencyBrake EBS = new EmergencyBrake();

        /// <summary>
        /// ブレーキ継続カウンタ
        /// </summary>
        public int EmgBrakeContinueCnt = 0;

        /// <summary>
        /// EHS 緊急時ハンドル動作
        /// </summary>
        //public EmergencyHandring EHS = new EmergencyHandring();

        // ログ 追記事項出力
        public static string addLogMsg;


        /// <summary>
        /// 回避ルートイメージ
        /// </summary>
        //public Bitmap AvoidRootImage;

        //public DateTime AvoidRootDispTime;

        // バック開始判定カウンタ
        private int BackStartCnt;

        // ===========================================================================================================

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="ceCtrl"></param>
        public Brain(CersioCtrl ceCtrl)
        {
            CarCtrl = ceCtrl;
        }

        /// <summary>
        /// 初期化
        /// </summary>
        public void Init(MapData mapData)
        {
            ModeCtrl = new ModeControl();
            // チェックポイントへ向かう
            ModeCtrl.SetActionMode(ModeControl.ActionMode.CheckPoint);

            // Locpresump
            //  マップ画像ファイル名、実サイズの横[mm], 実サイズ縦[mm] (北向き基準)
            LocSys = new LocationSystem(mapData);
            UpdateCnt = 0;

            // 現在座標リセット
            Reset_Rooting( true );
        }

        /// <summary>
        /// ルーティング情報をリセット
        /// </summary>
        public void Reset_Rooting( bool bResetSeq )
        {
            // ゴール判定 フラグOff
            goalFlg = false;

            // シーケンス初期化
            if (bResetSeq)
            {
                LocSys.RTS.ResetSeq();
            }
        }

        /// <summary>
        /// 自律走行処理 定期更新
        /// </summary>
        /// <param name="bRunAutonomous"></param>
        /// <param name="bMoveBaseCtrl"></param>
        /// <param name="SpeedKmh"></param>
        /// <returns></returns>
        public bool AutonomousProc( bool bRunAutonomous, bool bMoveBaseCtrl, double SpeedKmh )
        {
            // 自立走行に切り替わった瞬間か？
            bool trgRunAutonomous = (bRunAutonomousOld != bRunAutonomous && bRunAutonomous) ? true : false;

            UpdateCnt++;

            // すべてのルートを回りゴールした。
            if (goalFlg)
            {
                CarCtrl.nowAccValue = 0.0;
                CarCtrl.nowHandleValue = 0.0f;

                CarCtrl.SetCommandAC(0.0, 0.0);
                // スマイル
                CarCtrl.SetHeadMarkLED(LEDControl.LED_PATTERN.SMILE);

                return goalFlg;
            }

            
            // 現在座標更新
            LocSys.RTS.SetNowPostion(LocSys.GetResultLocationX(),
                              LocSys.GetResultLocationY(),
                              LocSys.GetResultAngle());

            // チェックポイント送信
            {
                // チェックポイントをROSへ指示(bServer接続時も配信)
                if (LocSys.RTS.TrgCheckPoint() || trg_bServerConnect || trgRunAutonomous )
                {
                    Vector3 checkPnt = LocSys.RTS.GetCheckPointToWayPoint();// LocSys.RTS.getNowCheckPoint();
                    CarCtrl.SetCommandAG(checkPnt.x, checkPnt.y, checkPnt.z );
                }

                // 再スタート時
                if (trgRunAutonomous)
                {
                    //  0.0を一度送信
                    ModeCtrl.SetActionMode(ModeControl.ActionMode.CheckPoint);
                    CarCtrl.SetCommandAC(0.0, 0.0);
                    CarCtrl.nowAccValue = 0.0;
                    CarCtrl.nowHandleValue = 0.0;
                }
            }

            // 自走モード処理
            ModeUpdate(bRunAutonomous);

            // ルート計算
            LocSys.RTS.CalcRooting(bRunAutonomous);

            if (bRunAutonomous)
            {
                // 走行指示出力
                /*
                // ROSの回転角度から、Benzハンドル角度の上限に合わせる
                double angLimit = (CarCtrl.hwMVBS_Ang * 180.0 / Math.PI);
                if (angLimit > 30.0) angLimit = 30.0;
                if (angLimit < -30.0) angLimit = -30.0;


                double moveAng = -(angLimit / 30.0);//-CarCtrl.hwMVBS_Ang;// * 0.3; 
                */
                double moveAng = CarCtrl.hwMVBS_Ang *2.0;// * 0.3;//-CarCtrl.hwMVBS_Ang;// * 0.3; 

                if (ModeCtrl.GetActionMode() == ModeControl.ActionMode.CheckPoint)
                {
                    if (bMoveBaseCtrl)
                    {
                        // move_base 移動
                        if (CarCtrl.hwMVBS_X > 0.0)
                        {
                            // move_baseから前進指示の場合
                            double speedRate = CarCtrl.hwMVBS_X;

                            // ハンドルで曲がるときは、速度を下げる
                            if (Math.Abs(moveAng) > 0.25) speedRate *= 0.75;

                            CarCtrl.CalcHandleAccelControl(moveAng, CarCtrl.CalcAccelFromSpeed(SpeedKmh * speedRate, true));
                            //CarCtrl.SendCalcHandleAccelControl(moveAng, getAccelValue()*0.5);
                        }
                        else
                        {
                            ModeCtrl.SetActionMode(ModeControl.ActionMode.EmergencyStop);
                        }

                        // ACコマンド送信
                        CarCtrl.SetCommandAC(CarCtrl.nowHandleValue, CarCtrl.nowAccValue);
                    }
                    else
                    {
                        // ルートにそったハンドル、アクセル値を取得
                        double handleTgt = LocSys.RTS.GetHandleValue();
                        //double accTgt = LocSys.RTS.GetAccelValue();

                        // move_baseから前進指示の場合
                        double speedRate = 1.0;

                        // ハンドルで曲がるときは、速度を下げる
                        if (Math.Abs(handleTgt) > 0.25) speedRate = 0.75;

                        CarCtrl.CalcHandleAccelControl(handleTgt, CarCtrl.CalcAccelFromSpeed(SpeedKmh * speedRate, true));

                        // ACコマンド送信
                        CarCtrl.SetCommandAC(CarCtrl.nowHandleValue, CarCtrl.nowAccValue);
                    }

                }
                else if (ModeCtrl.GetActionMode() == ModeControl.ActionMode.EmergencyStop)
                {
                    if (ModeCtrl.GetActionCount() < 5)
                    {
                        // 減速 10%
                        CarCtrl.nowAccValue = 0.1;
                        CarCtrl.CalcHandleAccelControl(moveAng, CarCtrl.nowAccValue);
                        BackStartCnt = 0;
                    }
                    else
                    {
                        // move_baseから停止状態
                        //double speedRate = CarCtrl.hwMVBS_X;
                        //speedRate *= 0.75;

                        // その場、回転
                        if (Math.Abs(CarCtrl.hwMVBS_Ang) >= 0.01)
                        {
                            // 減速 [0.5km/h]
                            //CarCtrl.CalcHandleAccelControl(0.0, 0.0);
                            CarCtrl.CalcHandleAccelControl(moveAng, CarCtrl.CalcAccelFromSpeed(0.25, true));

                            BackStartCnt++;
                            if (BackStartCnt > 20)
                            {
                                // Back モード変更
                                ModeCtrl.SetActionMode(ModeControl.ActionMode.MoveBack);
                                BackStartCnt = 0;
                                CarCtrl.nowAccValue = 0.0;
                            }
                        }
                        else
                        {
                            // 停止
                            CarCtrl.CalcHandleAccelControl(0.0, 0.0);
                            BackStartCnt = 0;
                        }
                    }

                    // ACコマンド送信
                    CarCtrl.SetCommandAC(CarCtrl.nowHandleValue, CarCtrl.nowAccValue);
                }
                else if (ModeCtrl.GetActionMode() == ModeControl.ActionMode.StackStop)
                {
                    // バック動作
                }
                else if (ModeCtrl.GetActionMode() == ModeControl.ActionMode.MoveBack)
                {
                    // バック動作
                    if (ModeCtrl.GetActionCount() < 5)
                    {
                        // スピコンブレーキ解除
                        CarCtrl.SetCommandAC(-CarCtrl.nowHandleValue, 0.0);
                    }
                    else
                    {
                        // 500cm バック
                        //if (ModeCtrl.GetModePassSeconds() > 3)
                        if (ModeCtrl.PassDistanceMm() > 500.0)
                        {
                            ModeCtrl.SetActionMode(ModeControl.ActionMode.CheckPoint);
                            BackStartCnt = 0;
                            CarCtrl.nowAccValue = 0.0;

                            // チェックポイント再送
                            {
                                Vector3 checkPnt = LocSys.RTS.GetCheckPointToWayPoint();
                                CarCtrl.SetCommandAG(checkPnt.x, checkPnt.y, checkPnt.z);
                            }
                        }
                        else
                        {
                            // ハンドル維持して、バック
                            CarCtrl.CalcHandleAccelControl(CarCtrl.nowHandleValue, CarCtrl.CalcAccelFromSpeed(SpeedKmh * 0.5, false));
                        }

                        // ACコマンド送信
                        CarCtrl.SetCommandAC(-CarCtrl.nowHandleValue, CarCtrl.nowAccValue);
                    }
                }
                else
                {
                    ModeCtrl.SetActionMode(ModeControl.ActionMode.CheckPoint);
                }
            }
            else
            {
                // 走行指示出力しない
                CarCtrl.nowAccValue = 0.0;
                CarCtrl.nowHandleValue = 0.0f;
            }

            // 接続トリガ取得
            trg_bServerConnect = false;
            if (!bServerConnectFlg && CarCtrl.TCP_IsConnected()) trg_bServerConnect = true;

            bServerConnectFlg = CarCtrl.TCP_IsConnected();

            bRunAutonomousOld = bRunAutonomous;

            goalFlg = LocSys.RTS.GetGoalStatus();
            return goalFlg;
        }

        /// <summary>
        /// 自己状態確認
        /// ※ヘルスチェック
        /// </summary>
        /// <returns></returns>
        public bool SystemProc()
        {
            // bServer接続確認

            // ROS IFで各センサー情報を確認？

            // 変化のないセンサーを監視？

            // バッテリーの電圧の取得

            return true;
        }

        /// <summary>
        /// 定期更新(100ms周期)
        /// </summary>
        /// <param name="LocSys"></param>
        /// <returns>true...バック中(緊急動作しない)</returns>
        /// 
        public bool ModeUpdate( bool bRunAutonomous )
        {
            ModeCtrl.Update( LocSys.GetResultDistance_mm() );

            if (bRunAutonomous)
            {
                if (ModeCtrl.GetActionMode() == ModeControl.ActionMode.CheckPoint)
                {
                    // LED Normal
                    CarCtrl.SetHeadMarkLED(LEDControl.LED_PATTERN.Normal);

                    // 通常時
                    // チェックポイント通過をLEDで伝える
                    if (LocSys.RTS.TrgCheckPoint())
                    {
                        //CarCtrl.SetHeadMarkLED(LEDControl.LED_PATTERN.WHITE_FLASH, true);
                    }
                }
                else if (ModeCtrl.GetActionMode() == ModeControl.ActionMode.EmergencyStop)
                {
                    // 緊急停止状態
                    // 赤
                    CarCtrl.SetHeadMarkLED(LEDControl.LED_PATTERN.Warning, true);
                }
                else if (ModeCtrl.GetActionMode() == ModeControl.ActionMode.EmergencyStop)
                {
                    // 異常停止状態
                    // 赤
                    CarCtrl.SetHeadMarkLED(LEDControl.LED_PATTERN.RedAlart, true);
                }
                else if (ModeCtrl.GetActionMode() == ModeControl.ActionMode.MoveBack)
                {
                    // ＬＥＤパターン
                    // バック中
                    //CarCtrl.SetHeadMarkLED(LEDControl.LED_PATTERN.Warning);
                }
            }

            return true;
        }
    }
}
