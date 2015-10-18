﻿namespace LRFMapEditer
{
    partial class NewMapLoadForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.tb_SkipFrame = new System.Windows.Forms.TextBox();
            this.label14 = new System.Windows.Forms.Label();
            this.label11 = new System.Windows.Forms.Label();
            this.tb_LogFileName = new System.Windows.Forms.TextBox();
            this.label9 = new System.Windows.Forms.Label();
            this.num_LRFTime = new System.Windows.Forms.NumericUpDown();
            this.btn_Invert = new System.Windows.Forms.Button();
            this.sb_LRFTime = new System.Windows.Forms.HScrollBar();
            this.pb_LRFLog = new System.Windows.Forms.PictureBox();
            this.btn_LoadLRF = new System.Windows.Forms.Button();
            this.lbl_NumFrame = new System.Windows.Forms.Label();
            this.label1 = new System.Windows.Forms.Label();
            this.cb_StopLayerCut = new System.Windows.Forms.CheckBox();
            ((System.ComponentModel.ISupportInitialize)(this.num_LRFTime)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.pb_LRFLog)).BeginInit();
            this.SuspendLayout();
            // 
            // tb_SkipFrame
            // 
            this.tb_SkipFrame.Font = new System.Drawing.Font("MS UI Gothic", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(128)));
            this.tb_SkipFrame.Location = new System.Drawing.Point(481, 58);
            this.tb_SkipFrame.Name = "tb_SkipFrame";
            this.tb_SkipFrame.Size = new System.Drawing.Size(68, 23);
            this.tb_SkipFrame.TabIndex = 30;
            this.tb_SkipFrame.TextChanged += new System.EventHandler(this.tb_SkipFrame_TextChanged);
            // 
            // label14
            // 
            this.label14.AutoSize = true;
            this.label14.Font = new System.Drawing.Font("MS UI Gothic", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(128)));
            this.label14.Location = new System.Drawing.Point(391, 61);
            this.label14.Name = "label14";
            this.label14.Size = new System.Drawing.Size(84, 16);
            this.label14.TabIndex = 29;
            this.label14.Text = "SkipFrame ";
            // 
            // label11
            // 
            this.label11.AutoSize = true;
            this.label11.Font = new System.Drawing.Font("MS UI Gothic", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(128)));
            this.label11.Location = new System.Drawing.Point(6, 418);
            this.label11.Name = "label11";
            this.label11.Size = new System.Drawing.Size(51, 16);
            this.label11.TabIndex = 28;
            this.label11.Text = "Frame";
            // 
            // tb_LogFileName
            // 
            this.tb_LogFileName.Location = new System.Drawing.Point(69, 12);
            this.tb_LogFileName.Name = "tb_LogFileName";
            this.tb_LogFileName.Size = new System.Drawing.Size(421, 19);
            this.tb_LogFileName.TabIndex = 27;
            // 
            // label9
            // 
            this.label9.AutoSize = true;
            this.label9.Location = new System.Drawing.Point(16, 16);
            this.label9.Name = "label9";
            this.label9.Size = new System.Drawing.Size(45, 12);
            this.label9.TabIndex = 26;
            this.label9.Text = "LRFFile";
            // 
            // num_LRFTime
            // 
            this.num_LRFTime.Font = new System.Drawing.Font("MS UI Gothic", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(128)));
            this.num_LRFTime.Location = new System.Drawing.Point(61, 415);
            this.num_LRFTime.Name = "num_LRFTime";
            this.num_LRFTime.Size = new System.Drawing.Size(80, 23);
            this.num_LRFTime.TabIndex = 25;
            this.num_LRFTime.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            // 
            // btn_Invert
            // 
            this.btn_Invert.Font = new System.Drawing.Font("MS UI Gothic", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(128)));
            this.btn_Invert.Location = new System.Drawing.Point(394, 354);
            this.btn_Invert.Name = "btn_Invert";
            this.btn_Invert.Size = new System.Drawing.Size(138, 44);
            this.btn_Invert.TabIndex = 11;
            this.btn_Invert.Text = "取り込み";
            this.btn_Invert.UseVisualStyleBackColor = true;
            this.btn_Invert.Click += new System.EventHandler(this.btn_Invert_Click);
            // 
            // sb_LRFTime
            // 
            this.sb_LRFTime.Location = new System.Drawing.Point(9, 443);
            this.sb_LRFTime.Name = "sb_LRFTime";
            this.sb_LRFTime.Size = new System.Drawing.Size(540, 21);
            this.sb_LRFTime.TabIndex = 3;
            this.sb_LRFTime.Value = 1;
            this.sb_LRFTime.ValueChanged += new System.EventHandler(this.sb_LRFTime_ValueChanged);
            // 
            // pb_LRFLog
            // 
            this.pb_LRFLog.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.pb_LRFLog.Location = new System.Drawing.Point(18, 48);
            this.pb_LRFLog.Name = "pb_LRFLog";
            this.pb_LRFLog.Size = new System.Drawing.Size(350, 350);
            this.pb_LRFLog.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
            this.pb_LRFLog.TabIndex = 2;
            this.pb_LRFLog.TabStop = false;
            // 
            // btn_LoadLRF
            // 
            this.btn_LoadLRF.Location = new System.Drawing.Point(498, 12);
            this.btn_LoadLRF.Name = "btn_LoadLRF";
            this.btn_LoadLRF.Size = new System.Drawing.Size(51, 23);
            this.btn_LoadLRF.TabIndex = 31;
            this.btn_LoadLRF.Text = "...";
            this.btn_LoadLRF.UseVisualStyleBackColor = true;
            this.btn_LoadLRF.Click += new System.EventHandler(this.btn_LoadLRF_Click);
            // 
            // lbl_NumFrame
            // 
            this.lbl_NumFrame.Font = new System.Drawing.Font("MS UI Gothic", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(128)));
            this.lbl_NumFrame.Location = new System.Drawing.Point(164, 418);
            this.lbl_NumFrame.Name = "lbl_NumFrame";
            this.lbl_NumFrame.Size = new System.Drawing.Size(55, 15);
            this.lbl_NumFrame.TabIndex = 32;
            this.lbl_NumFrame.Text = "0";
            this.lbl_NumFrame.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(147, 421);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(11, 12);
            this.label1.TabIndex = 33;
            this.label1.Text = "/";
            // 
            // cb_StopLayerCut
            // 
            this.cb_StopLayerCut.AutoSize = true;
            this.cb_StopLayerCut.Location = new System.Drawing.Point(395, 300);
            this.cb_StopLayerCut.Name = "cb_StopLayerCut";
            this.cb_StopLayerCut.Size = new System.Drawing.Size(90, 16);
            this.cb_StopLayerCut.TabIndex = 34;
            this.cb_StopLayerCut.Text = "静止なら破棄";
            this.cb_StopLayerCut.UseVisualStyleBackColor = true;
            // 
            // NewMapLoadForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(565, 473);
            this.Controls.Add(this.cb_StopLayerCut);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.lbl_NumFrame);
            this.Controls.Add(this.tb_LogFileName);
            this.Controls.Add(this.label9);
            this.Controls.Add(this.btn_LoadLRF);
            this.Controls.Add(this.tb_SkipFrame);
            this.Controls.Add(this.btn_Invert);
            this.Controls.Add(this.pb_LRFLog);
            this.Controls.Add(this.label14);
            this.Controls.Add(this.num_LRFTime);
            this.Controls.Add(this.label11);
            this.Controls.Add(this.sb_LRFTime);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "NewMapLoadForm";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.Text = "URGLogFile読み込み";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.NewMapLoadForm_FormClosing);
            this.Load += new System.EventHandler(this.NewMapLoadForm_Load);
            this.Shown += new System.EventHandler(this.NewMapLoadForm_Shown);
            ((System.ComponentModel.ISupportInitialize)(this.num_LRFTime)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.pb_LRFLog)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TextBox tb_LogFileName;
        private System.Windows.Forms.Label label9;
        private System.Windows.Forms.TextBox tb_SkipFrame;
        private System.Windows.Forms.Label label14;
        private System.Windows.Forms.Label label11;
        private System.Windows.Forms.NumericUpDown num_LRFTime;
        private System.Windows.Forms.Button btn_Invert;
        private System.Windows.Forms.HScrollBar sb_LRFTime;
        private System.Windows.Forms.PictureBox pb_LRFLog;
        private System.Windows.Forms.Button btn_LoadLRF;
        private System.Windows.Forms.Label lbl_NumFrame;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.CheckBox cb_StopLayerCut;
    }
}