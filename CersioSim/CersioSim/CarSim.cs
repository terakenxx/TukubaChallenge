﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Axiom.Math;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Diagnostics;

using LocationPresumption;

// +Yを上方向
// ※0度を X+方向にする。

namespace CersioSim
{
    public class CarSim
    {
        // センサー系シミュレート変数
        // car senser emu
        public MarkPoint mkp = new MarkPoint(0, 0, 0);

        // 走行系シミュレート変数
        const double mm2m = 1.0 / 1000.0;
        // car drive emu
        const double carWidth = 550.0 * mm2m;     // 左右のタイヤ間の距離 mm
        const double carWidthHf = carWidth / 2.0;
        const double carHeight = 550.0 * mm2m;    // ホイールベース

        const double carTireSize = 240.0 * mm2m;    // タイヤ直径
        const double carTireDispSizeHf = carTireSize / 2.0 * 1.5;   // 表示表現サイズ

        // 
        // 各タイヤの初期位置
        public Vector3 wdFL = new Vector3();
        public Vector3 wdFR = new Vector3();
        public Vector3 wdRL = new Vector3();
        public Vector3 wdRR = new Vector3();

        public Vector3 wdRLOld = new Vector3();
        public Vector3 wdRROld = new Vector3();

        // クルマの中心点
        // 車の基準点を前輪軸の真ん中とする
        public Vector3 wdCarF;
        public Vector3 wdCarR;
        // クルマの向き
        public double wdCarAng = 0.0;

        // ハンドル角(度)
        public double carHandleAngMax = 30.0;  // +-30.0
        public double carHandleAng = 0.0;

        // アクセル
        // +で前進
        public double carAccVal = 0.0;

        // ロータリーエンコーダ パルス値
        public double wheelPulseR = 0.0;
        public double wheelPulseL = 0.0;

        /// <summary>
        /// クルマ初期化
        /// </summary>
        public void CarInit(double posx, double posy, double ang)
        {
            carHandleAng = 0.0;
            carAccVal = 0.0;

            wdCarAng = ang;

            wdCarF = new Vector3(posx, posy, 0.0);
            //wdCarR = new Vector3(posx, posy+carHeight, 0.0);
            {
                Vector3 carVec = new Vector3();

                // 車体のベクトル
                Quaternion rotRQt = new Quaternion();
                rotRQt.RollInDegrees = wdCarAng;
                carVec.x = -carHeight;
                carVec = rotRQt.ToRotationMatrix() * carVec;

                wdCarR = new Vector3(carVec.x + posx, carVec.y + posy, carVec.z);
            }

            wdFL = new Vector3();
            wdFR = new Vector3();
            wdRL = new Vector3();
            wdRR = new Vector3();

            oldMS = DateTime.Now.Millisecond;

            calcTirePos(0);

            wdRLOld = new Vector3(wdRL.x, wdRL.y, wdRL.z);
            wdRROld = new Vector3(wdRR.x, wdRR.y, wdRR.z);

            wheelPulseR = 0.0;
            wheelPulseL = 0.0;
        }

        public void CarInit(MarkPoint _mkp)
        {
            CarInit(_mkp.X, _mkp.Y, _mkp.Theta);
            mkp.Set(_mkp);
        }

        /// <summary>
        /// センサー情報更新
        /// </summary>
        public void SenserUpdate( bool bLRF, bool bRE )
        {
            if (bRE)
            {
                // R.E.
                CalcWheelPosToREPulse();
            }
        }

        /// <summary>
        /// 2つのベクトルがなす角をかえす
        /// </summary>
        /// <param name="vecA"></param>
        /// <param name="vecB"></param>
        /// <returns></returns>
        public double VecToRad(Vector3 vecA, Vector3 vecB)
        {
            vecA.Normalize();
            vecB.Normalize();

            double rad = vecA.Dot(vecB);
            if (rad > 1.0) rad = 1.0;

            double dir = (double)(Math.Asin(rad) - (Math.PI / 2.0));

            if (double.IsNaN(dir))
            {
                Debug.WriteLine("Math Error!! NAn Occurd");
            }

            Vector3 resVec = vecA.Cross(vecB);
            if (resVec.z < 0) dir = -dir;

            return dir;
        }

        long oldMS;

        /// <summary>
        /// タイヤ位置計算
        /// </summary>
        /// <param name="timeTick"></param>
        public void calcTirePos(long timeTick)
        {
            long difMS = timeTick; //DateTime.Now.Millisecond - oldMS;
            //double moveRad = (wdCarAng + carHandleAng) * Math.PI / 180.0;

            double moveLength = (4.0 * 1000.0) / (60*60*1000);      // 1ミリ秒の移動量 時速4Km計算

            // 時間辺りの移動量を求める
            moveLength = moveLength * carAccVal * (double)difMS;

            oldMS = DateTime.Now.Millisecond;

            {
                // 車の行きたいベクトル
                Vector3 moveVec = new Vector3();
                Quaternion rotQt = new Quaternion();

                rotQt.RollInDegrees = wdCarAng + carHandleAng;
                moveVec.x = moveLength;
                moveVec = rotQt.ToRotationMatrix() * moveVec;

                // ハンドリングの影響を加えて、クルマの向きを求める
#if true
                {
                    Vector3 carVec = new Vector3(1.0, 0.0,0.0);
                    Vector3 movedcarVec = new Vector3();

                    // 車体の向きベクトル
                    Quaternion rotRQt = new Quaternion();
                    rotRQt.RollInDegrees = wdCarAng;
                    //carVec.x = carHeight;
                    carVec = rotRQt.ToRotationMatrix() * carVec;

                    movedcarVec = carVec + moveVec;
                    double addRad = VecToRad(movedcarVec, carVec);
                    wdCarAng += addRad * 180.0 / Math.PI;
                }
#else
            wdCarAng += carHandleAng;
#endif
            }

            // クルマの向きに対する移動を求める
            {
                Quaternion rotQt = new Quaternion();
                Vector3 moveVec = new Vector3(moveLength,0.0,0.0);

                rotQt.RollInDegrees = wdCarAng;
                moveVec = rotQt.ToRotationMatrix() * moveVec;

                if (moveVec.IsInvalid)
                {
                    // error
                    moveVec += moveVec;
                }
                // フロント中心軸を移動量分加算
                wdCarF += moveVec;

                // mkpへ反映
                {
                    mkp.X += moveVec.x;
                    mkp.Y += moveVec.y;
                    mkp.Theta = wdCarAng;
                }
                //Debug.WriteLine(wdCarF);

                // 差分ように更新前の値を保存
                wdRLOld = new Vector3(wdRL.x, wdRL.y, wdRL.z);
                wdRROld = new Vector3(wdRR.x, wdRR.y, wdRR.z);

                // 各車輪の位置座標計算
                Vector3 shaftVec = new Vector3();
                Vector3 wheelFRvec = new Vector3();
                Vector3 wheelFLvec = new Vector3();
                Vector3 wheelRvec = new Vector3();
                Vector3 wheelLvec = new Vector3();

                // 前輪位置 算出
                wheelFLvec.y = carWidthHf;
                wheelFRvec.y = -carWidthHf;

                wheelFRvec = rotQt.ToRotationMatrix() * wheelFRvec;
                wheelFLvec = rotQt.ToRotationMatrix() * wheelFLvec;

                wdFR = wheelFRvec + wdCarF;
                wdFL = wheelFLvec + wdCarF;

                // バック側　中心位置
                shaftVec.x = -carHeight;
                shaftVec = rotQt.ToRotationMatrix() * shaftVec;
                shaftVec += wdCarF;

                wdCarR = shaftVec;

                // 後輪位置算出
                wheelRvec.x = carHeight;
                wheelRvec.y = -carWidthHf;

                wheelLvec.x = carHeight;
                wheelLvec.y = carWidthHf;

                wheelRvec = rotQt.ToRotationMatrix() * wheelRvec;
                wheelLvec = rotQt.ToRotationMatrix() * wheelLvec;

                wdRR = wheelRvec + wdCarF;
                wdRL = wheelLvec + wdCarF;
            }

            // フロント軸位置とクルマの向きを元に、車輪の位置を求める
            // フロント車軸位置に(クルマの向き*carHeight)で、リア車軸位置
            // フロント車軸位置にクルマの向き+90度の-+方向にフロント車輪がある
            // リア車軸位置にクルマの向き+90度の-+方向にリア車輪がある
        }




        /// <summary>
        /// ホイールの移動量から
        /// ロータリーエンコーダ　回転パルス値を計算
        /// </summary>
        public void CalcWheelPosToREPulse()
        {
            const double WheelSize = carTireSize;//172;    // ホイール直径
            const double OneRotValue = 240;   // １回転分の分解能

            Vector3 wheelLmov, wheelRmov;

            Real signL, signR;
            // 移動量と移動方向(+,-)を求める
            {
                Quaternion rotQt = new Quaternion();
                Vector3 moveVec = new Vector3();

                rotQt.RollInDegrees = wdCarAng;
                moveVec.x = 1.0;
                moveVec = rotQt.ToRotationMatrix() * moveVec;

                // 移動差分から、移動量を求める
                wheelLmov = new Vector3(wdRL.x - wdRLOld.x,
                                                 wdRL.y - wdRLOld.y,
                                                 wdRL.z - wdRLOld.z);

                wheelRmov = new Vector3(wdRR.x - wdRROld.x,
                                                 wdRR.y - wdRROld.y,
                                                 wdRR.z - wdRROld.z);

                if (moveVec.Dot(wheelLmov) > 0.0) signL = 1.0;
                else signL = -1.0;
                if (moveVec.Dot(wheelRmov) > 0.0) signR = 1.0;
                else signR = -1.0;
            }

            // 移動量(長さ) / ホイール１回転の長さ * １回転のパルス数
            wheelPulseL += (wheelLmov.Length / (WheelSize * Math.PI) * OneRotValue) * signL;
            wheelPulseR += (wheelRmov.Length / (WheelSize * Math.PI) * OneRotValue) * signR;

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="g"></param>
        /// <param name="col"></param>
        /// <param name="cx"></param>
        /// <param name="cy"></param>
        /// <param name="x1"></param>
        /// <param name="y1"></param>
        /// <param name="x2"></param>
        /// <param name="y2"></param>
        /// <param name="rad"></param>
        /// <param name="scale"></param>
        private void DrawCarParts(Graphics g, Pen col, double cx, double cy, double x1, double y1, double x2, double y2, double ang, double scale)
        {
            double rad = ang * Math.PI / 180.0;
            g.DrawLine( col,
                        (float)((cx + (Math.Cos(rad) * x1) - (Math.Sin(rad) * y1)) * scale), (float)((cy + (Math.Sin(rad) * x1) + (Math.Cos(rad) * y1))* scale),
                        (float)((cx + (Math.Cos(rad) * x2) - (Math.Sin(rad) * y2)) * scale), (float)((cy + (Math.Sin(rad) * x2) + (Math.Cos(rad) * y2)) * scale));
        }

        /// <summary>
        /// クルマ描画
        /// </summary>
        /// <param name="g"></param>
        /// <param name="ScaleRealToPixel"></param>
        /// <param name="ViewScale"></param>
        /// <param name="viewX"></param>
        /// <param name="viewY"></param>
        public void DrawCar(Graphics g, double ScaleRealToPixel)
        {
            // 左右車輪間のライン、前後車軸間のライン
            // クルマの向きと同じ角度で、車輪位置にタイヤライン (フロントはクルマの向き+ハンドル角)

            g.TranslateTransform((float)(wdCarF.x * ScaleRealToPixel), (float)(wdCarF.y * ScaleRealToPixel), MatrixOrder.Prepend);
            g.RotateTransform((float)wdCarAng, MatrixOrder.Prepend);

            // 車軸
            DrawCarParts(g, Pens.Red,
                          0.0, 0.0,
                          0.0, 0.0,
                          -carHeight, 0.0f,
                          0.0, ScaleRealToPixel);

            // 前輪軸
            DrawCarParts(g, Pens.Red,
                          0.0, 0.0,
                          0.0, -carWidthHf,
                          0.0, carWidthHf,
                          0.0, ScaleRealToPixel);

            // 後輪軸
            DrawCarParts(g, Pens.Red,
                          -carHeight, 0.0,
                          0.0, -carWidthHf,
                          0.0, carWidthHf,
                          0.0, ScaleRealToPixel);

            // 後輪タイヤ
            DrawCarParts(g, Pens.Red,
                          -carHeight, 0.0,
                          -carTireDispSizeHf, -carWidthHf,
                          carTireDispSizeHf, -carWidthHf,
                          0.0, ScaleRealToPixel);

            DrawCarParts(g, Pens.Red,
                          -carHeight, 0.0,
                          -carTireDispSizeHf, carWidthHf,
                          carTireDispSizeHf, carWidthHf,
                          0.0, ScaleRealToPixel);

            // 前輪左
            DrawCarParts(g, Pens.Green,
                          0.0, -carWidthHf,
                          -carTireDispSizeHf, 0.0,
                          carTireDispSizeHf, 0.0,
                          carHandleAng, ScaleRealToPixel);

            // 前輪右
            DrawCarParts(g, Pens.Blue,
                          0.0, carWidthHf,
                          -carTireDispSizeHf, 0.0f,
                          carTireDispSizeHf, 0.0f,
                          carHandleAng, ScaleRealToPixel);

        }

    }
}
