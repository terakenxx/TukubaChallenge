﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CersioIO
{
    public class LEDControl
    {
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
        public enum LED_PATTERN
        {
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

        private static string[] ledCommand =
        {
            "rainbow",
            "teater",
            "rainbowCycle",
        };


        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public bool UpDate()
        {
            // カウンタ更新
            if (cntHeadLED > 0) cntHeadLED--;

            return true;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="setPattern"></param>
        /// <param name="bForce">強制変更</param>
        /// <returns>変更したor済み..True</returns>
        public bool SetHeadMarkLED(CersioCtrl carCtrl, int setPattern, bool bForce = false)
        {
            if (bForce || (ptnHeadLED != setPattern && cntHeadLED == 0))
            {
                carCtrl.SendCommand("AL," + setPattern.ToString() + ",\n");

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

    }
}
