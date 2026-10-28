namespace USBCANTool
{
    unsafe partial class Form1
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            this.grpDevice = new System.Windows.Forms.GroupBox();
            this.lblColType = new System.Windows.Forms.Label();
            this.lblColIdx = new System.Windows.Forms.Label();
            this.lblColCh = new System.Windows.Forms.Label();
            this.lblColProtocol = new System.Windows.Forms.Label();
            this.lblColBaud = new System.Windows.Forms.Label();
            this.lblColData = new System.Windows.Forms.Label();
            this.lblRx = new System.Windows.Forms.Label();
            this.cmbRxDevice = new System.Windows.Forms.ComboBox();
            this.numRxIndex = new System.Windows.Forms.NumericUpDown();
            this.numRxChannel = new System.Windows.Forms.NumericUpDown();
            this.cmbRxProtocol = new System.Windows.Forms.ComboBox();
            this.cmbRxBaud = new System.Windows.Forms.ComboBox();
            this.cmbRxDataBaud = new System.Windows.Forms.ComboBox();
            this.chkRxTerm = new System.Windows.Forms.CheckBox();
            this.cmbRxIso = new System.Windows.Forms.ComboBox();
            this.lblTx = new System.Windows.Forms.Label();
            this.cmbTxDevice = new System.Windows.Forms.ComboBox();
            this.numTxIndex = new System.Windows.Forms.NumericUpDown();
            this.numTxChannel = new System.Windows.Forms.NumericUpDown();
            this.cmbTxProtocol = new System.Windows.Forms.ComboBox();
            this.cmbTxBaud = new System.Windows.Forms.ComboBox();
            this.cmbTxDataBaud = new System.Windows.Forms.ComboBox();
            this.chkTxTerm = new System.Windows.Forms.CheckBox();
            this.cmbTxIso = new System.Windows.Forms.ComboBox();
            this.lblColTerm = new System.Windows.Forms.Label();
            this.lblColIso = new System.Windows.Forms.Label();
            this.pnlLed = new System.Windows.Forms.Panel();
            this.btnClose = new System.Windows.Forms.Button();
            this.btnOpen = new System.Windows.Forms.Button();
            this.grpRewrite = new System.Windows.Forms.GroupBox();
            this.grpRx = new System.Windows.Forms.GroupBox();
            this.lblFilter = new System.Windows.Forms.Label();
            this.txtFilterId = new System.Windows.Forms.TextBox();
            this.cmbFilterDir = new System.Windows.Forms.ComboBox();
            this.chkGroup = new System.Windows.Forms.CheckBox();
            this.btnClear = new System.Windows.Forms.Button();
            this.lvPackets = new System.Windows.Forms.ListView();
            this.statusStrip = new System.Windows.Forms.StatusStrip();
            this.lblStatus = new System.Windows.Forms.ToolStripStatusLabel();
            this.grpDevice.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numRxIndex)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numRxChannel)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numTxIndex)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numTxChannel)).BeginInit();
            this.grpRewrite.SuspendLayout();
            this.grpRx.SuspendLayout();
            this.statusStrip.SuspendLayout();
            this.SuspendLayout();
            // 
            // grpDevice
            // 
            this.grpDevice.Controls.Add(this.lblColType);
            this.grpDevice.Controls.Add(this.lblColIdx);
            this.grpDevice.Controls.Add(this.lblColCh);
            this.grpDevice.Controls.Add(this.lblColProtocol);
            this.grpDevice.Controls.Add(this.lblColBaud);
            this.grpDevice.Controls.Add(this.lblColData);
            this.grpDevice.Controls.Add(this.lblRx);
            this.grpDevice.Controls.Add(this.cmbRxDevice);
            this.grpDevice.Controls.Add(this.numRxIndex);
            this.grpDevice.Controls.Add(this.numRxChannel);
            this.grpDevice.Controls.Add(this.cmbRxProtocol);
            this.grpDevice.Controls.Add(this.cmbRxBaud);
            this.grpDevice.Controls.Add(this.cmbRxDataBaud);
            this.grpDevice.Controls.Add(this.chkRxTerm);
            this.grpDevice.Controls.Add(this.cmbRxIso);
            this.grpDevice.Controls.Add(this.lblTx);
            this.grpDevice.Controls.Add(this.cmbTxDevice);
            this.grpDevice.Controls.Add(this.numTxIndex);
            this.grpDevice.Controls.Add(this.numTxChannel);
            this.grpDevice.Controls.Add(this.cmbTxProtocol);
            this.grpDevice.Controls.Add(this.cmbTxBaud);
            this.grpDevice.Controls.Add(this.cmbTxDataBaud);
            this.grpDevice.Controls.Add(this.chkTxTerm);
            this.grpDevice.Controls.Add(this.cmbTxIso);
            this.grpDevice.Controls.Add(this.lblColTerm);
            this.grpDevice.Controls.Add(this.lblColIso);
            this.grpDevice.Controls.Add(this.pnlLed);
            this.grpDevice.Controls.Add(this.btnClose);
            this.grpDevice.Controls.Add(this.btnOpen);
            this.grpDevice.Location = new System.Drawing.Point(12, 12);
            this.grpDevice.Name = "grpDevice";
            this.grpDevice.Size = new System.Drawing.Size(880, 140);
            this.grpDevice.TabIndex = 0;
            this.grpDevice.TabStop = false;
            this.grpDevice.Text = "RX / TX";
            // 
            // lblColType
            // 
            this.lblColType.AutoSize = true;
            this.lblColType.Location = new System.Drawing.Point(40, 18);
            this.lblColType.Name = "lblColType";
            this.lblColType.Size = new System.Drawing.Size(29, 12);
            this.lblColType.TabIndex = 0;
            this.lblColType.Text = "Type";
            // 
            // lblColIdx
            // 
            this.lblColIdx.AutoSize = true;
            this.lblColIdx.Location = new System.Drawing.Point(238, 18);
            this.lblColIdx.Name = "lblColIdx";
            this.lblColIdx.Size = new System.Drawing.Size(23, 12);
            this.lblColIdx.TabIndex = 1;
            this.lblColIdx.Text = "Idx";
            // 
            // lblColCh
            // 
            this.lblColCh.AutoSize = true;
            this.lblColCh.Location = new System.Drawing.Point(280, 18);
            this.lblColCh.Name = "lblColCh";
            this.lblColCh.Size = new System.Drawing.Size(17, 12);
            this.lblColCh.TabIndex = 2;
            this.lblColCh.Text = "Ch";
            // 
            // lblColProtocol
            // 
            this.lblColProtocol.AutoSize = true;
            this.lblColProtocol.Location = new System.Drawing.Point(322, 18);
            this.lblColProtocol.Name = "lblColProtocol";
            this.lblColProtocol.Size = new System.Drawing.Size(53, 12);
            this.lblColProtocol.TabIndex = 3;
            this.lblColProtocol.Text = "Protocol";
            // 
            // lblColBaud
            // 
            this.lblColBaud.AutoSize = true;
            this.lblColBaud.Location = new System.Drawing.Point(406, 18);
            this.lblColBaud.Name = "lblColBaud";
            this.lblColBaud.Size = new System.Drawing.Size(41, 12);
            this.lblColBaud.TabIndex = 4;
            this.lblColBaud.Text = "Bitrate";
            // 
            // lblColData
            // 
            this.lblColData.AutoSize = true;
            this.lblColData.Location = new System.Drawing.Point(530, 18);
            this.lblColData.Name = "lblColData";
            this.lblColData.Size = new System.Drawing.Size(29, 12);
            this.lblColData.TabIndex = 5;
            this.lblColData.Text = "Data";
            // 
            // lblRx
            // 
            this.lblRx.AutoSize = true;
            this.lblRx.Location = new System.Drawing.Point(12, 40);
            this.lblRx.Name = "lblRx";
            this.lblRx.Size = new System.Drawing.Size(17, 12);
            this.lblRx.TabIndex = 6;
            this.lblRx.Text = "RX";
            // 
            // cmbRxDevice
            // 
            this.cmbRxDevice.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbRxDevice.FormattingEnabled = true;
            this.cmbRxDevice.Location = new System.Drawing.Point(40, 36);
            this.cmbRxDevice.Name = "cmbRxDevice";
            this.cmbRxDevice.Size = new System.Drawing.Size(192, 20);
            this.cmbRxDevice.TabIndex = 7;
            this.cmbRxDevice.SelectedIndexChanged += new System.EventHandler(this.RxDeviceOrProtocolChanged);
            // 
            // numRxIndex
            // 
            this.numRxIndex.Location = new System.Drawing.Point(238, 36);
            this.numRxIndex.Maximum = new decimal(new int[] { 10, 0, 0, 0 });
            this.numRxIndex.Name = "numRxIndex";
            this.numRxIndex.Size = new System.Drawing.Size(36, 21);
            this.numRxIndex.TabIndex = 8;
            // 
            // numRxChannel
            // 
            this.numRxChannel.Location = new System.Drawing.Point(280, 36);
            this.numRxChannel.Maximum = new decimal(new int[] { 1, 0, 0, 0 });
            this.numRxChannel.Name = "numRxChannel";
            this.numRxChannel.Size = new System.Drawing.Size(36, 21);
            this.numRxChannel.TabIndex = 9;
            // 
            // cmbRxProtocol
            // 
            this.cmbRxProtocol.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbRxProtocol.FormattingEnabled = true;
            this.cmbRxProtocol.Location = new System.Drawing.Point(322, 36);
            this.cmbRxProtocol.Name = "cmbRxProtocol";
            this.cmbRxProtocol.Size = new System.Drawing.Size(78, 20);
            this.cmbRxProtocol.TabIndex = 10;
            this.cmbRxProtocol.SelectedIndexChanged += new System.EventHandler(this.RxDeviceOrProtocolChanged);
            // 
            // cmbRxBaud
            // 
            this.cmbRxBaud.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbRxBaud.FormattingEnabled = true;
            this.cmbRxBaud.Location = new System.Drawing.Point(406, 36);
            this.cmbRxBaud.Name = "cmbRxBaud";
            this.cmbRxBaud.Size = new System.Drawing.Size(118, 20);
            this.cmbRxBaud.TabIndex = 11;
            // 
            // cmbRxDataBaud
            // 
            this.cmbRxDataBaud.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbRxDataBaud.FormattingEnabled = true;
            this.cmbRxDataBaud.Location = new System.Drawing.Point(530, 36);
            this.cmbRxDataBaud.Name = "cmbRxDataBaud";
            this.cmbRxDataBaud.Size = new System.Drawing.Size(118, 20);
            this.cmbRxDataBaud.TabIndex = 12;
            // 
            // chkRxTerm
            // 
            this.chkRxTerm.Checked = true;
            this.chkRxTerm.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkRxTerm.Location = new System.Drawing.Point(658, 38);
            this.chkRxTerm.Name = "chkRxTerm";
            this.chkRxTerm.Size = new System.Drawing.Size(20, 20);
            this.chkRxTerm.TabIndex = 13;
            this.chkRxTerm.UseVisualStyleBackColor = true;
            // 
            // cmbRxIso
            // 
            this.cmbRxIso.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbRxIso.FormattingEnabled = true;
            this.cmbRxIso.Items.AddRange(new object[] {
            "ISO",
            "Non-ISO"});
            this.cmbRxIso.Location = new System.Drawing.Point(708, 36);
            this.cmbRxIso.Name = "cmbRxIso";
            this.cmbRxIso.Size = new System.Drawing.Size(78, 20);
            this.cmbRxIso.TabIndex = 14;
            this.cmbRxIso.SelectedIndex = 0;
            // 
            // lblTx
            // 
            this.lblTx.AutoSize = true;
            this.lblTx.Location = new System.Drawing.Point(12, 68);
            this.lblTx.Name = "lblTx";
            this.lblTx.Size = new System.Drawing.Size(17, 12);
            this.lblTx.TabIndex = 13;
            this.lblTx.Text = "TX";
            // 
            // cmbTxDevice
            // 
            this.cmbTxDevice.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbTxDevice.FormattingEnabled = true;
            this.cmbTxDevice.Location = new System.Drawing.Point(40, 64);
            this.cmbTxDevice.Name = "cmbTxDevice";
            this.cmbTxDevice.Size = new System.Drawing.Size(192, 20);
            this.cmbTxDevice.TabIndex = 14;
            this.cmbTxDevice.SelectedIndexChanged += new System.EventHandler(this.TxDeviceOrProtocolChanged);
            // 
            // numTxIndex
            // 
            this.numTxIndex.Location = new System.Drawing.Point(238, 64);
            this.numTxIndex.Maximum = new decimal(new int[] { 10, 0, 0, 0 });
            this.numTxIndex.Name = "numTxIndex";
            this.numTxIndex.Size = new System.Drawing.Size(36, 21);
            this.numTxIndex.TabIndex = 15;
            // 
            // numTxChannel
            // 
            this.numTxChannel.Location = new System.Drawing.Point(280, 64);
            this.numTxChannel.Maximum = new decimal(new int[] { 1, 0, 0, 0 });
            this.numTxChannel.Name = "numTxChannel";
            this.numTxChannel.Size = new System.Drawing.Size(36, 21);
            this.numTxChannel.TabIndex = 16;
            this.numTxChannel.Value = new decimal(new int[] { 1, 0, 0, 0 });
            // 
            // cmbTxProtocol
            // 
            this.cmbTxProtocol.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbTxProtocol.FormattingEnabled = true;
            this.cmbTxProtocol.Location = new System.Drawing.Point(322, 64);
            this.cmbTxProtocol.Name = "cmbTxProtocol";
            this.cmbTxProtocol.Size = new System.Drawing.Size(78, 20);
            this.cmbTxProtocol.TabIndex = 17;
            this.cmbTxProtocol.SelectedIndexChanged += new System.EventHandler(this.TxDeviceOrProtocolChanged);
            // 
            // cmbTxBaud
            // 
            this.cmbTxBaud.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbTxBaud.FormattingEnabled = true;
            this.cmbTxBaud.Location = new System.Drawing.Point(406, 64);
            this.cmbTxBaud.Name = "cmbTxBaud";
            this.cmbTxBaud.Size = new System.Drawing.Size(118, 20);
            this.cmbTxBaud.TabIndex = 18;
            // 
            // cmbTxDataBaud
            // 
            this.cmbTxDataBaud.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbTxDataBaud.FormattingEnabled = true;
            this.cmbTxDataBaud.Location = new System.Drawing.Point(530, 64);
            this.cmbTxDataBaud.Name = "cmbTxDataBaud";
            this.cmbTxDataBaud.Size = new System.Drawing.Size(118, 20);
            this.cmbTxDataBaud.TabIndex = 19;
            // 
            // chkTxTerm
            // 
            this.chkTxTerm.Checked = true;
            this.chkTxTerm.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkTxTerm.Location = new System.Drawing.Point(658, 66);
            this.chkTxTerm.Name = "chkTxTerm";
            this.chkTxTerm.Size = new System.Drawing.Size(20, 20);
            this.chkTxTerm.TabIndex = 20;
            this.chkTxTerm.UseVisualStyleBackColor = true;
            // 
            // cmbTxIso
            // 
            this.cmbTxIso.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbTxIso.FormattingEnabled = true;
            this.cmbTxIso.Items.AddRange(new object[] {
            "ISO",
            "Non-ISO"});
            this.cmbTxIso.Location = new System.Drawing.Point(708, 64);
            this.cmbTxIso.Name = "cmbTxIso";
            this.cmbTxIso.Size = new System.Drawing.Size(78, 20);
            this.cmbTxIso.TabIndex = 21;
            this.cmbTxIso.SelectedIndex = 0;
            // 
            // lblColTerm
            // 
            this.lblColTerm.AutoSize = true;
            this.lblColTerm.Location = new System.Drawing.Point(654, 18);
            this.lblColTerm.Name = "lblColTerm";
            this.lblColTerm.Size = new System.Drawing.Size(29, 12);
            this.lblColTerm.TabIndex = 22;
            this.lblColTerm.Text = "Term";
            // 
            // lblColIso
            // 
            this.lblColIso.AutoSize = true;
            this.lblColIso.Location = new System.Drawing.Point(708, 18);
            this.lblColIso.Name = "lblColIso";
            this.lblColIso.Size = new System.Drawing.Size(23, 12);
            this.lblColIso.TabIndex = 23;
            this.lblColIso.Text = "ISO";
            // 
            // pnlLed
            // 
            this.pnlLed.Location = new System.Drawing.Point(40, 100);
            this.pnlLed.Name = "pnlLed";
            this.pnlLed.Size = new System.Drawing.Size(20, 20);
            this.pnlLed.TabIndex = 20;
            this.pnlLed.Paint += new System.Windows.Forms.PaintEventHandler(this.pnlLed_Paint);
            // 
            // btnClose
            // 
            this.btnClose.Location = new System.Drawing.Point(655, 96);
            this.btnClose.Name = "btnClose";
            this.btnClose.Size = new System.Drawing.Size(90, 28);
            this.btnClose.TabIndex = 22;
            this.btnClose.Text = "Close";
            this.btnClose.UseVisualStyleBackColor = true;
            this.btnClose.Click += new System.EventHandler(this.btnClose_Click);
            // 
            // btnOpen
            // 
            this.btnOpen.Location = new System.Drawing.Point(555, 96);
            this.btnOpen.Name = "btnOpen";
            this.btnOpen.Size = new System.Drawing.Size(90, 28);
            this.btnOpen.TabIndex = 21;
            this.btnOpen.Text = "Open";
            this.btnOpen.UseVisualStyleBackColor = true;
            this.btnOpen.Click += new System.EventHandler(this.btnOpen_Click);
            // 
            // grpRewrite
            // 
            this.grpRewrite.Location = new System.Drawing.Point(12, 158);
            this.grpRewrite.Name = "grpRewrite";
            this.grpRewrite.Size = new System.Drawing.Size(880, 170);
            this.grpRewrite.TabIndex = 1;
            this.grpRewrite.TabStop = false;
            this.grpRewrite.Text = "Rewrite";
            // 
            // grpRx
            // 
            this.grpRx.Controls.Add(this.lvPackets);
            this.grpRx.Controls.Add(this.btnClear);
            this.grpRx.Controls.Add(this.chkGroup);
            this.grpRx.Controls.Add(this.cmbFilterDir);
            this.grpRx.Controls.Add(this.txtFilterId);
            this.grpRx.Controls.Add(this.lblFilter);
            this.grpRx.Location = new System.Drawing.Point(12, 334);
            this.grpRx.Name = "grpRx";
            this.grpRx.Size = new System.Drawing.Size(880, 328);
            this.grpRx.TabIndex = 2;
            this.grpRx.TabStop = false;
            this.grpRx.Text = "Packets";
            // 
            // lblFilter
            // 
            this.lblFilter.AutoSize = true;
            this.lblFilter.Location = new System.Drawing.Point(15, 24);
            this.lblFilter.Name = "lblFilter";
            this.lblFilter.Size = new System.Drawing.Size(59, 12);
            this.lblFilter.TabIndex = 0;
            this.lblFilter.Text = "Filter ID";
            // 
            // txtFilterId
            // 
            this.txtFilterId.Font = new System.Drawing.Font("Consolas", 9F);
            this.txtFilterId.Location = new System.Drawing.Point(80, 20);
            this.txtFilterId.Name = "txtFilterId";
            this.txtFilterId.Size = new System.Drawing.Size(180, 22);
            this.txtFilterId.TabIndex = 1;
            this.txtFilterId.TextChanged += new System.EventHandler(this.FilterChanged);
            // 
            // cmbFilterDir
            // 
            this.cmbFilterDir.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbFilterDir.FormattingEnabled = true;
            this.cmbFilterDir.Items.AddRange(new object[] {
            "All",
            "RX",
            "TX"});
            this.cmbFilterDir.Location = new System.Drawing.Point(270, 21);
            this.cmbFilterDir.Name = "cmbFilterDir";
            this.cmbFilterDir.Size = new System.Drawing.Size(70, 20);
            this.cmbFilterDir.TabIndex = 2;
            this.cmbFilterDir.SelectedIndex = 0;
            this.cmbFilterDir.SelectedIndexChanged += new System.EventHandler(this.FilterChanged);
            // 
            // chkGroup
            // 
            this.chkGroup.AutoSize = true;
            this.chkGroup.Location = new System.Drawing.Point(355, 23);
            this.chkGroup.Name = "chkGroup";
            this.chkGroup.Size = new System.Drawing.Size(84, 16);
            this.chkGroup.TabIndex = 3;
            this.chkGroup.Text = "Group show";
            this.chkGroup.UseVisualStyleBackColor = true;
            this.chkGroup.CheckedChanged += new System.EventHandler(this.FilterChanged);
            // 
            // btnClear
            // 
            this.btnClear.Location = new System.Drawing.Point(770, 18);
            this.btnClear.Name = "btnClear";
            this.btnClear.Size = new System.Drawing.Size(90, 26);
            this.btnClear.TabIndex = 4;
            this.btnClear.Text = "Clear";
            this.btnClear.UseVisualStyleBackColor = true;
            this.btnClear.Click += new System.EventHandler(this.btnClear_Click);
            // 
            // lvPackets
            // 
            this.lvPackets.Font = new System.Drawing.Font("Consolas", 9F);
            this.lvPackets.FullRowSelect = true;
            this.lvPackets.GridLines = true;
            this.lvPackets.HideSelection = false;
            this.lvPackets.Location = new System.Drawing.Point(15, 50);
            this.lvPackets.Name = "lvPackets";
            this.lvPackets.Size = new System.Drawing.Size(845, 262);
            this.lvPackets.TabIndex = 5;
            this.lvPackets.UseCompatibleStateImageBehavior = false;
            this.lvPackets.View = System.Windows.Forms.View.Details;
            this.lvPackets.VirtualMode = true;
            this.lvPackets.RetrieveVirtualItem += new System.Windows.Forms.RetrieveVirtualItemEventHandler(this.lvPackets_RetrieveVirtualItem);
            // 
            // statusStrip
            // 
            this.statusStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.lblStatus});
            this.statusStrip.Location = new System.Drawing.Point(0, 677);
            this.statusStrip.Name = "statusStrip";
            this.statusStrip.Size = new System.Drawing.Size(904, 22);
            this.statusStrip.TabIndex = 3;
            this.statusStrip.Text = "statusStrip";
            // 
            // lblStatus
            // 
            this.lblStatus.Name = "lblStatus";
            this.lblStatus.Size = new System.Drawing.Size(889, 17);
            this.lblStatus.Spring = true;
            this.lblStatus.Text = "Idle";
            this.lblStatus.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(904, 699);
            this.Controls.Add(this.statusStrip);
            this.Controls.Add(this.grpRx);
            this.Controls.Add(this.grpRewrite);
            this.Controls.Add(this.grpDevice);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.Name = "Form1";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "USBCAN Tool";
            this.grpDevice.ResumeLayout(false);
            this.grpDevice.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numRxIndex)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numRxChannel)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numTxIndex)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numTxChannel)).EndInit();
            this.grpRewrite.ResumeLayout(false);
            this.grpRewrite.PerformLayout();
            this.grpRx.ResumeLayout(false);
            this.grpRx.PerformLayout();
            this.statusStrip.ResumeLayout(false);
            this.statusStrip.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        private System.Windows.Forms.GroupBox grpDevice;
        private System.Windows.Forms.Label lblColType;
        private System.Windows.Forms.Label lblColIdx;
        private System.Windows.Forms.Label lblColCh;
        private System.Windows.Forms.Label lblColProtocol;
        private System.Windows.Forms.Label lblColBaud;
        private System.Windows.Forms.Label lblColData;
        private System.Windows.Forms.Label lblRx;
        private System.Windows.Forms.ComboBox cmbRxDevice;
        private System.Windows.Forms.NumericUpDown numRxIndex;
        private System.Windows.Forms.NumericUpDown numRxChannel;
        private System.Windows.Forms.ComboBox cmbRxProtocol;
        private System.Windows.Forms.ComboBox cmbRxBaud;
        private System.Windows.Forms.ComboBox cmbRxDataBaud;
        private System.Windows.Forms.CheckBox chkRxTerm;
        private System.Windows.Forms.ComboBox cmbRxIso;
        private System.Windows.Forms.Label lblTx;
        private System.Windows.Forms.ComboBox cmbTxDevice;
        private System.Windows.Forms.NumericUpDown numTxIndex;
        private System.Windows.Forms.NumericUpDown numTxChannel;
        private System.Windows.Forms.ComboBox cmbTxProtocol;
        private System.Windows.Forms.ComboBox cmbTxBaud;
        private System.Windows.Forms.ComboBox cmbTxDataBaud;
        private System.Windows.Forms.CheckBox chkTxTerm;
        private System.Windows.Forms.ComboBox cmbTxIso;
        private System.Windows.Forms.Label lblColTerm;
        private System.Windows.Forms.Label lblColIso;
        private System.Windows.Forms.Panel pnlLed;
        private System.Windows.Forms.Button btnClose;
        private System.Windows.Forms.Button btnOpen;
        private System.Windows.Forms.GroupBox grpRewrite;
        private System.Windows.Forms.GroupBox grpRx;
        private System.Windows.Forms.Label lblFilter;
        private System.Windows.Forms.TextBox txtFilterId;
        private System.Windows.Forms.ComboBox cmbFilterDir;
        private System.Windows.Forms.CheckBox chkGroup;
        private System.Windows.Forms.Button btnClear;
        private System.Windows.Forms.ListView lvPackets;
        private System.Windows.Forms.StatusStrip statusStrip;
        private System.Windows.Forms.ToolStripStatusLabel lblStatus;
    }
}
