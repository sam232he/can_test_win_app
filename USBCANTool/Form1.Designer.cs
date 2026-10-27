namespace USBCANTool
{
    partial class Form1
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
            this.pnlLed = new System.Windows.Forms.Panel();
            this.numChannel = new System.Windows.Forms.NumericUpDown();
            this.lblChannel = new System.Windows.Forms.Label();
            this.numDeviceIndex = new System.Windows.Forms.NumericUpDown();
            this.lblIndex = new System.Windows.Forms.Label();
            this.cmbDeviceType = new System.Windows.Forms.ComboBox();
            this.lblType = new System.Windows.Forms.Label();
            this.btnClose = new System.Windows.Forms.Button();
            this.btnOpen = new System.Windows.Forms.Button();
            this.lblBaud = new System.Windows.Forms.Label();
            this.cmbBaud = new System.Windows.Forms.ComboBox();
            this.grpSend = new System.Windows.Forms.GroupBox();
            this.chkRtr = new System.Windows.Forms.CheckBox();
            this.chkExtended = new System.Windows.Forms.CheckBox();
            this.btnSend = new System.Windows.Forms.Button();
            this.chkPeriodic = new System.Windows.Forms.CheckBox();
            this.numPeriod = new System.Windows.Forms.NumericUpDown();
            this.lblPeriod = new System.Windows.Forms.Label();
            this.txtData = new System.Windows.Forms.TextBox();
            this.lblData = new System.Windows.Forms.Label();
            this.numDlc = new System.Windows.Forms.NumericUpDown();
            this.lblDlc = new System.Windows.Forms.Label();
            this.txtId = new System.Windows.Forms.TextBox();
            this.lblId = new System.Windows.Forms.Label();
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
            ((System.ComponentModel.ISupportInitialize)(this.numChannel)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numDeviceIndex)).BeginInit();
            this.grpSend.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numDlc)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numPeriod)).BeginInit();
            this.grpRx.SuspendLayout();
            this.statusStrip.SuspendLayout();
            this.SuspendLayout();
            // 
            // grpDevice
            // 
            this.grpDevice.Controls.Add(this.pnlLed);
            this.grpDevice.Controls.Add(this.cmbBaud);
            this.grpDevice.Controls.Add(this.lblBaud);
            this.grpDevice.Controls.Add(this.numChannel);
            this.grpDevice.Controls.Add(this.lblChannel);
            this.grpDevice.Controls.Add(this.numDeviceIndex);
            this.grpDevice.Controls.Add(this.lblIndex);
            this.grpDevice.Controls.Add(this.cmbDeviceType);
            this.grpDevice.Controls.Add(this.lblType);
            this.grpDevice.Controls.Add(this.btnClose);
            this.grpDevice.Controls.Add(this.btnOpen);
            this.grpDevice.Location = new System.Drawing.Point(12, 12);
            this.grpDevice.Name = "grpDevice";
            this.grpDevice.Size = new System.Drawing.Size(760, 80);
            this.grpDevice.TabIndex = 0;
            this.grpDevice.TabStop = false;
            this.grpDevice.Text = "Device";
            // 
            // pnlLed
            // 
            this.pnlLed.Location = new System.Drawing.Point(522, 34);
            this.pnlLed.Name = "pnlLed";
            this.pnlLed.Size = new System.Drawing.Size(20, 20);
            this.pnlLed.TabIndex = 10;
            this.pnlLed.Paint += new System.Windows.Forms.PaintEventHandler(this.pnlLed_Paint);
            // 
            // numChannel
            // 
            this.numChannel.Location = new System.Drawing.Point(268, 32);
            this.numChannel.Maximum = new decimal(new int[] { 2, 0, 0, 0 });
            this.numChannel.Name = "numChannel";
            this.numChannel.Size = new System.Drawing.Size(40, 21);
            this.numChannel.TabIndex = 7;
            // 
            // lblChannel
            // 
            this.lblChannel.AutoSize = true;
            this.lblChannel.Location = new System.Drawing.Point(248, 35);
            this.lblChannel.Name = "lblChannel";
            this.lblChannel.Size = new System.Drawing.Size(17, 12);
            this.lblChannel.TabIndex = 6;
            this.lblChannel.Text = "Ch";
            // 
            // numDeviceIndex
            // 
            this.numDeviceIndex.Location = new System.Drawing.Point(200, 32);
            this.numDeviceIndex.Maximum = new decimal(new int[] { 10, 0, 0, 0 });
            this.numDeviceIndex.Name = "numDeviceIndex";
            this.numDeviceIndex.Size = new System.Drawing.Size(40, 21);
            this.numDeviceIndex.TabIndex = 5;
            // 
            // lblIndex
            // 
            this.lblIndex.AutoSize = true;
            this.lblIndex.Location = new System.Drawing.Point(176, 35);
            this.lblIndex.Name = "lblIndex";
            this.lblIndex.Size = new System.Drawing.Size(23, 12);
            this.lblIndex.TabIndex = 4;
            this.lblIndex.Text = "Idx";
            // 
            // cmbDeviceType
            // 
            this.cmbDeviceType.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbDeviceType.FormattingEnabled = true;
            this.cmbDeviceType.Items.AddRange(new object[] {
            "USBCAN1 (3)",
            "USBCAN2 (4)",
            "USBCAN-E-U (20)",
            "USBCAN-2E-U (21)",
            "USBCAN-4E-U (31)",
            "USBCAN-8E-U (34)"});
            this.cmbDeviceType.Location = new System.Drawing.Point(42, 32);
            this.cmbDeviceType.Name = "cmbDeviceType";
            this.cmbDeviceType.Size = new System.Drawing.Size(128, 20);
            this.cmbDeviceType.TabIndex = 3;
            this.cmbDeviceType.SelectedIndex = 1;
            // 
            // lblType
            // 
            this.lblType.AutoSize = true;
            this.lblType.Location = new System.Drawing.Point(12, 35);
            this.lblType.Name = "lblType";
            this.lblType.Size = new System.Drawing.Size(29, 12);
            this.lblType.TabIndex = 2;
            this.lblType.Text = "Type";
            // 
            // btnClose
            // 
            this.btnClose.Location = new System.Drawing.Point(655, 28);
            this.btnClose.Name = "btnClose";
            this.btnClose.Size = new System.Drawing.Size(90, 28);
            this.btnClose.TabIndex = 1;
            this.btnClose.Text = "Close";
            this.btnClose.UseVisualStyleBackColor = true;
            this.btnClose.Click += new System.EventHandler(this.btnClose_Click);
            // 
            // btnOpen
            // 
            this.btnOpen.Location = new System.Drawing.Point(555, 28);
            this.btnOpen.Name = "btnOpen";
            this.btnOpen.Size = new System.Drawing.Size(90, 28);
            this.btnOpen.TabIndex = 0;
            this.btnOpen.Text = "Open";
            this.btnOpen.UseVisualStyleBackColor = true;
            this.btnOpen.Click += new System.EventHandler(this.btnOpen_Click);
            // 
            // lblBaud
            // 
            this.lblBaud.AutoSize = true;
            this.lblBaud.Location = new System.Drawing.Point(316, 35);
            this.lblBaud.Name = "lblBaud";
            this.lblBaud.Size = new System.Drawing.Size(41, 12);
            this.lblBaud.TabIndex = 8;
            this.lblBaud.Text = "Bitrate";
            // 
            // cmbBaud
            // 
            this.cmbBaud.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbBaud.FormattingEnabled = true;
            this.cmbBaud.Location = new System.Drawing.Point(360, 32);
            this.cmbBaud.Name = "cmbBaud";
            this.cmbBaud.Size = new System.Drawing.Size(150, 20);
            this.cmbBaud.TabIndex = 9;
            // 
            // grpSend
            // 
            this.grpSend.Controls.Add(this.lblPeriod);
            this.grpSend.Controls.Add(this.numPeriod);
            this.grpSend.Controls.Add(this.chkPeriodic);
            this.grpSend.Controls.Add(this.chkRtr);
            this.grpSend.Controls.Add(this.chkExtended);
            this.grpSend.Controls.Add(this.btnSend);
            this.grpSend.Controls.Add(this.txtData);
            this.grpSend.Controls.Add(this.lblData);
            this.grpSend.Controls.Add(this.numDlc);
            this.grpSend.Controls.Add(this.lblDlc);
            this.grpSend.Controls.Add(this.txtId);
            this.grpSend.Controls.Add(this.lblId);
            this.grpSend.Location = new System.Drawing.Point(12, 100);
            this.grpSend.Name = "grpSend";
            this.grpSend.Size = new System.Drawing.Size(760, 90);
            this.grpSend.TabIndex = 1;
            this.grpSend.TabStop = false;
            this.grpSend.Text = "Transmit";
            // 
            // chkRtr
            // 
            this.chkRtr.AutoSize = true;
            this.chkRtr.Location = new System.Drawing.Point(280, 30);
            this.chkRtr.Name = "chkRtr";
            this.chkRtr.Size = new System.Drawing.Size(42, 16);
            this.chkRtr.TabIndex = 8;
            this.chkRtr.Text = "RTR";
            this.chkRtr.UseVisualStyleBackColor = true;
            // 
            // chkExtended
            // 
            this.chkExtended.AutoSize = true;
            this.chkExtended.Location = new System.Drawing.Point(200, 30);
            this.chkExtended.Name = "chkExtended";
            this.chkExtended.Size = new System.Drawing.Size(72, 16);
            this.chkExtended.TabIndex = 7;
            this.chkExtended.Text = "Extended";
            this.chkExtended.UseVisualStyleBackColor = true;
            this.chkExtended.CheckedChanged += new System.EventHandler(this.chkExtended_CheckedChanged);
            // 
            // btnSend
            // 
            this.btnSend.Location = new System.Drawing.Point(650, 35);
            this.btnSend.Name = "btnSend";
            this.btnSend.Size = new System.Drawing.Size(90, 35);
            this.btnSend.TabIndex = 6;
            this.btnSend.Text = "Send";
            this.btnSend.UseVisualStyleBackColor = true;
            this.btnSend.Click += new System.EventHandler(this.btnSend_Click);
            // 
            // chkPeriodic
            // 
            this.chkPeriodic.AutoSize = true;
            this.chkPeriodic.Location = new System.Drawing.Point(490, 57);
            this.chkPeriodic.Name = "chkPeriodic";
            this.chkPeriodic.Size = new System.Drawing.Size(66, 16);
            this.chkPeriodic.TabIndex = 9;
            this.chkPeriodic.Text = "Periodic";
            this.chkPeriodic.UseVisualStyleBackColor = true;
            this.chkPeriodic.CheckedChanged += new System.EventHandler(this.chkPeriodic_CheckedChanged);
            // 
            // numPeriod
            // 
            this.numPeriod.Location = new System.Drawing.Point(560, 54);
            this.numPeriod.Maximum = new decimal(new int[] { 60000, 0, 0, 0 });
            this.numPeriod.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            this.numPeriod.Name = "numPeriod";
            this.numPeriod.Size = new System.Drawing.Size(55, 21);
            this.numPeriod.TabIndex = 10;
            this.numPeriod.Value = new decimal(new int[] { 100, 0, 0, 0 });
            this.numPeriod.ValueChanged += new System.EventHandler(this.numPeriod_ValueChanged);
            // 
            // lblPeriod
            // 
            this.lblPeriod.AutoSize = true;
            this.lblPeriod.Location = new System.Drawing.Point(618, 58);
            this.lblPeriod.Name = "lblPeriod";
            this.lblPeriod.Size = new System.Drawing.Size(17, 12);
            this.lblPeriod.TabIndex = 11;
            this.lblPeriod.Text = "ms";
            // 
            // txtData
            // 
            this.txtData.Font = new System.Drawing.Font("Consolas", 9F);
            this.txtData.Location = new System.Drawing.Point(80, 55);
            this.txtData.Name = "txtData";
            this.txtData.Size = new System.Drawing.Size(400, 22);
            this.txtData.TabIndex = 5;
            this.txtData.Text = "01 02 03 04 05 06 07 08";
            this.txtData.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.txtData_KeyPress);
            this.txtData.TextChanged += new System.EventHandler(this.txtData_TextChanged);
            // 
            // lblData
            // 
            this.lblData.AutoSize = true;
            this.lblData.Location = new System.Drawing.Point(15, 58);
            this.lblData.Name = "lblData";
            this.lblData.Size = new System.Drawing.Size(59, 12);
            this.lblData.TabIndex = 4;
            this.lblData.Text = "Data (hex)";
            // 
            // numDlc
            // 
            this.numDlc.Location = new System.Drawing.Point(400, 28);
            this.numDlc.Maximum = new decimal(new int[] { 8, 0, 0, 0 });
            this.numDlc.Name = "numDlc";
            this.numDlc.Size = new System.Drawing.Size(50, 21);
            this.numDlc.TabIndex = 3;
            this.numDlc.Value = new decimal(new int[] { 8, 0, 0, 0 });
            // 
            // lblDlc
            // 
            this.lblDlc.AutoSize = true;
            this.lblDlc.Location = new System.Drawing.Point(360, 32);
            this.lblDlc.Name = "lblDlc";
            this.lblDlc.Size = new System.Drawing.Size(23, 12);
            this.lblDlc.TabIndex = 2;
            this.lblDlc.Text = "DLC";
            // 
            // txtId
            // 
            this.txtId.Font = new System.Drawing.Font("Consolas", 9F);
            this.txtId.Location = new System.Drawing.Point(80, 28);
            this.txtId.MaxLength = 3;
            this.txtId.Name = "txtId";
            this.txtId.Size = new System.Drawing.Size(100, 22);
            this.txtId.TabIndex = 1;
            this.txtId.Text = "100";
            this.txtId.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.txtId_KeyPress);
            this.txtId.TextChanged += new System.EventHandler(this.txtId_TextChanged);
            // 
            // lblId
            // 
            this.lblId.AutoSize = true;
            this.lblId.Location = new System.Drawing.Point(15, 32);
            this.lblId.Name = "lblId";
            this.lblId.Size = new System.Drawing.Size(59, 12);
            this.lblId.TabIndex = 0;
            this.lblId.Text = "ID (11-bit)";
            // 
            // grpRx
            // 
            this.grpRx.Controls.Add(this.lvPackets);
            this.grpRx.Controls.Add(this.btnClear);
            this.grpRx.Controls.Add(this.chkGroup);
            this.grpRx.Controls.Add(this.cmbFilterDir);
            this.grpRx.Controls.Add(this.txtFilterId);
            this.grpRx.Controls.Add(this.lblFilter);
            this.grpRx.Location = new System.Drawing.Point(12, 196);
            this.grpRx.Name = "grpRx";
            this.grpRx.Size = new System.Drawing.Size(760, 328);
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
            this.btnClear.Location = new System.Drawing.Point(650, 18);
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
            this.lvPackets.Size = new System.Drawing.Size(725, 262);
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
            this.statusStrip.Location = new System.Drawing.Point(0, 539);
            this.statusStrip.Name = "statusStrip";
            this.statusStrip.Size = new System.Drawing.Size(784, 22);
            this.statusStrip.TabIndex = 3;
            this.statusStrip.Text = "statusStrip";
            // 
            // lblStatus
            // 
            this.lblStatus.Name = "lblStatus";
            this.lblStatus.Size = new System.Drawing.Size(769, 17);
            this.lblStatus.Spring = true;
            this.lblStatus.Text = "Idle";
            this.lblStatus.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(784, 561);
            this.Controls.Add(this.statusStrip);
            this.Controls.Add(this.grpRx);
            this.Controls.Add(this.grpSend);
            this.Controls.Add(this.grpDevice);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.Name = "Form1";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "USBCAN Tool";
            this.grpDevice.ResumeLayout(false);
            this.grpDevice.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numChannel)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numDeviceIndex)).EndInit();
            this.grpSend.ResumeLayout(false);
            this.grpSend.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numDlc)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numPeriod)).EndInit();
            this.grpRx.ResumeLayout(false);
            this.grpRx.PerformLayout();
            this.statusStrip.ResumeLayout(false);
            this.statusStrip.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        private System.Windows.Forms.GroupBox grpDevice;
        private System.Windows.Forms.Panel pnlLed;
        private System.Windows.Forms.Button btnClose;
        private System.Windows.Forms.Button btnOpen;
        private System.Windows.Forms.ComboBox cmbDeviceType;
        private System.Windows.Forms.Label lblType;
        private System.Windows.Forms.NumericUpDown numDeviceIndex;
        private System.Windows.Forms.Label lblIndex;
        private System.Windows.Forms.NumericUpDown numChannel;
        private System.Windows.Forms.Label lblChannel;
        private System.Windows.Forms.Label lblBaud;
        private System.Windows.Forms.ComboBox cmbBaud;
        private System.Windows.Forms.GroupBox grpSend;
        private System.Windows.Forms.TextBox txtId;
        private System.Windows.Forms.Label lblId;
        private System.Windows.Forms.NumericUpDown numDlc;
        private System.Windows.Forms.Label lblDlc;
        private System.Windows.Forms.TextBox txtData;
        private System.Windows.Forms.Label lblData;
        private System.Windows.Forms.Button btnSend;
        private System.Windows.Forms.CheckBox chkPeriodic;
        private System.Windows.Forms.NumericUpDown numPeriod;
        private System.Windows.Forms.Label lblPeriod;
        private System.Windows.Forms.CheckBox chkExtended;
        private System.Windows.Forms.CheckBox chkRtr;
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
