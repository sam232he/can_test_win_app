using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace USBCANTool
{
    public unsafe partial class Form1 : Form
    {
        private IntPtr _rxDeviceHandle = IntPtr.Zero;
        private IntPtr _txDeviceHandle = IntPtr.Zero;
        private IntPtr _rxChannelHandle = IntPtr.Zero;
        private IntPtr _txChannelHandle = IntPtr.Zero;
        private uint _rxChannelIndex;
        private uint _txChannelIndex;
        private bool _rxProtocolFd;
        private bool _txProtocolFd;
        private uint _lastRxDevType;
        private uint _lastTxDevType;
        private System.Windows.Forms.Timer _uiTimer;
        private bool _isOpen;
        private Color _ledColor = Color.Gray;
        private readonly object _canLock = new object();
        private readonly object _rewriteLock = new object();
        private static readonly object BmsCounterLock = new object();
        private static byte _bmsCounter;
        private static byte _dcdcAliveCounter;
        private readonly object _latestSignalLock = new object();
        private readonly LatestSignal[] _latestSignals = new LatestSignal[RewriteSlotCount];
        private readonly bool[] _latestSignalDirty = new bool[RewriteSlotCount];
        private readonly byte[] _receivedDataScratch = new byte[64];
        private ListViewItem[] _slotSignalItems;
        private ListViewItem _latestSignalContextItem;
        private Thread _rxThread;
        private volatile bool _bridgeRunning;
        private volatile bool _bridgeTxFailed;
        private int _txFailStreak;
        private ZlgCanApi.ZCAN_Receive_Data[] _rxCanBuf;
        private ZlgCanApi.ZCAN_ReceiveFD_Data[] _rxFdBuf;
        private ZlgCanApi.ZCAN_Transmit_Data[] _txCanBuf;
        private ZlgCanApi.ZCAN_TransmitFD_Data[] _txFdBuf;
        private GCHandle _rxCanPin;
        private GCHandle _rxFdPin;
        private GCHandle _txCanPin;
        private GCHandle _txFdPin;
        private const int EchoCap = 64;
        private readonly EchoKey[] _echoA = new EchoKey[EchoCap];
        private readonly EchoKey[] _echoB = new EchoKey[EchoCap];
        private int _echoAHead;
        private int _echoBHead;
        private readonly object _echoLock = new object();
        private const int RxBatch = 128;

        private const int RewriteSlotCount = 10;
        private CheckBox[] _rewriteEnable;
        private Button[] _rewriteMode;
        private ComboBox[] _rewriteFrame;
        private TextBox[] _rewriteId;
        private TextBox[] _rewriteData;
        private Button[] _rewriteDbc;
        private RewriteSlot[] _rewriteSlots = new RewriteSlot[RewriteSlotCount];
        private bool _formattingRewrite;
        private bool _updatingUi;
        private bool _loadingSettings;
        private System.Windows.Forms.Timer _saveTimer;

        private const uint DefaultDeviceType = ZlgCanApi.ZCAN_USBCAN2;
        private const string DefaultBaud = "500000";

        private static readonly BaudOption[] BaudRates =
        {
            new BaudOption("1000 kbit/s", "1000000"),
            new BaudOption("800 kbit/s", "800000"),
            new BaudOption("500 kbit/s", "500000"),
            new BaudOption("250 kbit/s", "250000"),
            new BaudOption("125 kbit/s", "125000"),
            new BaudOption("100 kbit/s", "100000"),
            new BaudOption("50 kbit/s", "50000"),
            new BaudOption("20 kbit/s", "20000"),
            new BaudOption("10 kbit/s", "10000"),
        };

        private static readonly BaudOption[] FdBaudRates =
        {
            new BaudOption("1000 kbit/s", "1000000"),
            new BaudOption("800 kbit/s", "800000"),
            new BaudOption("500 kbit/s", "500000"),
            new BaudOption("250 kbit/s", "250000"),
            new BaudOption("125 kbit/s", "125000"),
            new BaudOption("100 kbit/s", "100000"),
            new BaudOption("50 kbit/s", "50000"),
        };

        private static readonly BaudOption[] DataBaudRates =
        {
            new BaudOption("5 Mbit/s", "5000000"),
            new BaudOption("4 Mbit/s", "4000000"),
            new BaudOption("2 Mbit/s", "2000000"),
            new BaudOption("1 Mbit/s", "1000000"),
            new BaudOption("800 kbit/s", "800000"),
            new BaudOption("500 kbit/s", "500000"),
            new BaudOption("250 kbit/s", "250000"),
            new BaudOption("125 kbit/s", "125000"),
        };

        public Form1()
        {
            InitializeComponent();
            ConfigureScrollableContent();
            InitDeviceList();
            InitBaudList();
            InitDataBaudList();
            InitRewriteSlots();
            InitLatestSignalView();
            InitRxBuffers();
            _saveTimer = new System.Windows.Forms.Timer { Interval = 400 };
            _saveTimer.Tick += SaveTimer_Tick;
            cmbRxBaud.SelectedIndexChanged += SettingsChanged;
            cmbRxDataBaud.SelectedIndexChanged += SettingsChanged;
            cmbTxBaud.SelectedIndexChanged += SettingsChanged;
            cmbTxDataBaud.SelectedIndexChanged += SettingsChanged;
            numRxIndex.ValueChanged += SettingsChanged;
            numTxIndex.ValueChanged += SettingsChanged;
            numRxChannel.ValueChanged += SettingsChanged;
            numTxChannel.ValueChanged += SettingsChanged;
            chkRxTerm.CheckedChanged += SettingsChanged;
            chkTxTerm.CheckedChanged += SettingsChanged;
            cmbRxIso.SelectedIndexChanged += SettingsChanged;
            cmbTxIso.SelectedIndexChanged += SettingsChanged;
            LoadSettings();
            UpdateLatestSignalRows();
            chkPauseDisplay.CheckedChanged += chkPauseDisplay_CheckedChanged;
            statusStrip.Dock = DockStyle.Bottom;
            ClientSize = new Size(944, 650);
            _uiTimer = new System.Windows.Forms.Timer { Interval = 500 };
            _uiTimer.Tick += UiTimer_Tick;
            UpdateUiState(false);

            if (!ZlgCanApi.DllFileExists)
                SetFailed("zlgcan.dll missing. Put it next to USBCANTool.exe.");
            else if (!ZlgCanApi.EnsureLoaded())
                SetFailed("Failed to load zlgcan.dll. " + (ZlgCanApi.LastLoadError ?? ZlgCanApi.Diagnose().Trim()));
            else
                SetIdle("Idle");
        }

        private void ConfigureScrollableContent()
        {
            var content = new Panel
            {
                AutoScroll = true,
                AutoScrollMinSize = new Size(904, 620),
                Dock = DockStyle.Fill
            };
            Controls.Remove(grpDevice);
            Controls.Remove(grpRewrite);
            Controls.Remove(grpLatestSignal);
            content.Controls.Add(grpDevice);
            content.Controls.Add(grpRewrite);
            content.Controls.Add(grpLatestSignal);
            grpLatestSignal.Location = new Point(12, 334);
            Controls.Add(content);
            statusStrip.BringToFront();
        }

        private void InitLatestSignalView()
        {
            lvLatestSignal.Columns.Clear();
            lvLatestSignal.Columns.Add("Slot", 45);
            lvLatestSignal.Columns.Add("Time", 80);
            lvLatestSignal.Columns.Add("From", 75);
            lvLatestSignal.Columns.Add("ID", 90);
            lvLatestSignal.Columns.Add("Received data", 185);
            lvLatestSignal.Columns.Add("To", 75);
            lvLatestSignal.Columns.Add("Forwarded data", 185);
            lvLatestSignal.Columns.Add("Result", 75);
            var contextMenu = new ContextMenuStrip();
            AddLatestSignalMenuItem(contextMenu, "Copy received data", LatestSignalMenuAction.CopyReceived);
            AddLatestSignalMenuItem(contextMenu, "Use received data in Rewrite", LatestSignalMenuAction.UseReceived);
            contextMenu.Opening += LatestSignalContextMenu_Opening;
            lvLatestSignal.ContextMenuStrip = contextMenu;
            lvLatestSignal.MouseDown += LatestSignalView_MouseDown;
            _slotSignalItems = new ListViewItem[RewriteSlotCount];
            for (int i = 0; i < RewriteSlotCount; i++)
            {
                var item = new ListViewItem((i + 1).ToString());
                for (int column = 1; column < lvLatestSignal.Columns.Count; column++)
                    item.SubItems.Add("");
                _slotSignalItems[i] = item;
                _latestSignals[i] = new LatestSignal
                {
                    Slot = i + 1,
                    ReceivedBytes = new byte[64],
                    ForwardedBytes = new byte[64]
                };
            }
        }

        private void AddLatestSignalMenuItem(ContextMenuStrip menu, string text,
            LatestSignalMenuAction action)
        {
            var item = new ToolStripMenuItem(text) { Tag = action };
            item.Click += LatestSignalMenuItem_Click;
            menu.Items.Add(item);
        }

        private void LatestSignalView_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right)
                return;
            ListViewHitTestInfo hit = lvLatestSignal.HitTest(e.Location);
            _latestSignalContextItem = hit.Item;
            if (hit.Item != null)
                hit.Item.Selected = true;
        }

        private void LatestSignalContextMenu_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_latestSignalContextItem == null)
            {
                e.Cancel = true;
                return;
            }
            string received = _latestSignalContextItem.SubItems[4].Text;
            ContextMenuStrip menu = sender as ContextMenuStrip;
            foreach (ToolStripItem menuItem in menu.Items)
            {
                ToolStripMenuItem item = menuItem as ToolStripMenuItem;
                if (item == null || !(item.Tag is LatestSignalMenuAction))
                    continue;

                item.Enabled = !string.IsNullOrWhiteSpace(received);
            }
        }

        private void LatestSignalMenuItem_Click(object sender, EventArgs e)
        {
            var menuItem = sender as ToolStripMenuItem;
            if (menuItem == null || !(menuItem.Tag is LatestSignalMenuAction) ||
                _latestSignalContextItem == null)
                return;

            LatestSignalMenuAction action = (LatestSignalMenuAction)menuItem.Tag;
            string data = _latestSignalContextItem.SubItems[4].Text;
            if (string.IsNullOrWhiteSpace(data))
                return;

            if (action == LatestSignalMenuAction.CopyReceived)
            {
                try
                {
                    Clipboard.SetText(data);
                }
                catch (ExternalException ex)
                {
                    MessageBox.Show(this, "Could not copy frame data to the clipboard. " + ex.Message,
                        "Copy frame data", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                return;
            }

            int slot;
            if (!int.TryParse(_latestSignalContextItem.SubItems[0].Text, out slot) ||
                slot < 1 || slot > _rewriteData.Length)
                return;

            byte[] bytes = ParseDataBytes(data);
            _rewriteData[slot - 1].Text = BytesToHex(bytes, Math.Min(8, bytes.Length));
            _rewriteData[slot - 1].SelectionStart = _rewriteData[slot - 1].Text.Length;
            _rewriteData[slot - 1].Focus();
        }

        private void InitRewriteSlots()
        {
            _rewriteEnable = new CheckBox[RewriteSlotCount];
            _rewriteMode = new Button[RewriteSlotCount];
            _rewriteFrame = new ComboBox[RewriteSlotCount];
            _rewriteId = new TextBox[RewriteSlotCount];
            _rewriteData = new TextBox[RewriteSlotCount];
            _rewriteDbc = new Button[RewriteSlotCount];

            var font = new Font("Consolas", 9F);
            grpRewrite.Controls.Add(MakeHeader(12, 16, "En"));
            grpRewrite.Controls.Add(MakeHeader(32, 16, "ID"));
            grpRewrite.Controls.Add(MakeHeader(99, 16, "Fmt"));
            grpRewrite.Controls.Add(MakeHeader(152, 16, "Data (8 bytes)"));
            grpRewrite.Controls.Add(MakeHeader(325, 16, "DBC"));
            grpRewrite.Controls.Add(MakeHeader(376, 16, "Mode"));
            grpRewrite.Controls.Add(MakeHeader(442, 16, "En"));
            grpRewrite.Controls.Add(MakeHeader(462, 16, "ID"));
            grpRewrite.Controls.Add(MakeHeader(529, 16, "Fmt"));
            grpRewrite.Controls.Add(MakeHeader(582, 16, "Data (8 bytes)"));
            grpRewrite.Controls.Add(MakeHeader(755, 16, "DBC"));
            grpRewrite.Controls.Add(MakeHeader(806, 16, "Mode"));
            grpRewrite.Size = new Size(880, grpRewrite.Height);

            for (int i = 0; i < RewriteSlotCount; i++)
            {
                int col = i / 5;
                int row = i % 5;
                int x = 12 + col * 430;
                int y = 34 + row * 26;

                var chk = new CheckBox
                {
                    Location = new Point(x, y + 2),
                    Size = new Size(18, 20),
                    TabIndex = 20 + i * 5,
                    Tag = i
                };
                chk.CheckedChanged += RewriteSlotChanged;
                _rewriteEnable[i] = chk;
                grpRewrite.Controls.Add(chk);

                var mode = new Button
                {
                    Location = new Point(x + 364, y - 1),
                    Size = new Size(62, 23),
                    TabIndex = 24 + i * 5,
                    Tag = i,
                    Text = "Unset",
                    UseVisualStyleBackColor = true
                };
                mode.Click += RewriteModeClicked;
                _rewriteMode[i] = mode;
                grpRewrite.Controls.Add(mode);

                var dbc = new Button
                {
                    Location = new Point(x + 313, y - 1),
                    Size = new Size(48, 23),
                    TabIndex = 24 + i * 5,
                    Tag = i,
                    Text = "DBC",
                    UseVisualStyleBackColor = true
                };
                dbc.Click += RewriteDbcClicked;
                _rewriteDbc[i] = dbc;
                grpRewrite.Controls.Add(dbc);

                var txtId = new TextBox
                {
                    Font = font,
                    Location = new Point(x + 20, y),
                    MaxLength = 3,
                    Size = new Size(64, 22),
                    TabIndex = 21 + i * 5,
                    Tag = i
                };
                txtId.KeyPress += HexKeyPress;
                txtId.TextChanged += RewriteIdChanged;
                _rewriteId[i] = txtId;
                grpRewrite.Controls.Add(txtId);

                var cmbFrame = new ComboBox
                {
                    DropDownStyle = ComboBoxStyle.DropDownList,
                    Location = new Point(x + 87, y),
                    Size = new Size(50, 20),
                    TabIndex = 22 + i * 5,
                    Tag = i
                };
                cmbFrame.Items.Add("STD");
                cmbFrame.Items.Add("EXT");
                cmbFrame.SelectedIndex = 0;
                cmbFrame.SelectedIndexChanged += RewriteFrameChanged;
                _rewriteFrame[i] = cmbFrame;
                grpRewrite.Controls.Add(cmbFrame);

                var txtData = new TextBox
                {
                    Font = font,
                    Location = new Point(x + 140, y),
                    Size = new Size(170, 22),
                    TabIndex = 23 + i * 5,
                    Text = "00 00 00 00 00 00 00 00"
                };
                txtData.KeyPress += HexKeyPress;
                txtData.TextChanged += RewriteDataChanged;
                _rewriteData[i] = txtData;
                grpRewrite.Controls.Add(txtData);
            }

            UpdateRewriteDbcButtons();
            RebuildRewriteSlots();
        }

        private static Label MakeHeader(int x, int y, string text)
        {
            return new Label
            {
                AutoSize = true,
                Location = new Point(x, y),
                Text = text
            };
        }

        private void RewriteSlotChanged(object sender, EventArgs e)
        {
            var checkBox = sender as CheckBox;
            if (checkBox != null && checkBox.Tag is int)
                ClearLatestSignalSlot((int)checkBox.Tag);
            UpdateRewriteDbcButtons();
            RebuildRewriteSlots();
            UpdateLatestSignalRows();
        }

        private void RewriteModeClicked(object sender, EventArgs e)
        {
            var button = sender as Button;
            if (button == null || !(button.Tag is int))
                return;
            button.Text = button.Text == "Set" ? "Unset" : "Set";
            RebuildRewriteSlots();
        }

        private void RewriteFrameChanged(object sender, EventArgs e)
        {
            var cmb = sender as ComboBox;
            if (cmb == null || !(cmb.Tag is int))
                return;
            int index = (int)cmb.Tag;
            if (_rewriteId != null && index >= 0 && index < _rewriteId.Length && _rewriteId[index] != null)
                ClampRewriteId(_rewriteId[index]);
            if (_rewriteData != null && index >= 0 && index < _rewriteData.Length && _rewriteData[index] != null)
                FormatRewriteData(_rewriteData[index]);
            ClearLatestSignalSlot(index);
            UpdateRewriteDbcButtons();
            RebuildRewriteSlots();
            UpdateLatestSignalRows();
        }

        private void RewriteIdChanged(object sender, EventArgs e)
        {
            var box = sender as TextBox;
            if (box == null)
                return;
            ClampRewriteId(box);
            int index = (int)box.Tag;
            if (_rewriteData != null && index >= 0 && index < _rewriteData.Length && _rewriteData[index] != null)
                FormatRewriteData(_rewriteData[index]);
            ClearLatestSignalSlot(index);
            UpdateRewriteDbcButtons();
            RebuildRewriteSlots();
            UpdateLatestSignalRows();
        }

        private void UpdateRewriteDbcButtons()
        {
            if (_rewriteDbc == null)
                return;

            for (int i = 0; i < _rewriteDbc.Length; i++)
            {
                uint id;
                bool hasId = _rewriteId[i] != null &&
                    !string.IsNullOrWhiteSpace(_rewriteId[i].Text) &&
                    TryParseCanId(_rewriteId[i].Text,
                        _rewriteFrame[i] != null && _rewriteFrame[i].SelectedIndex == 1
                            ? ZlgCanApi.CAN_EFF_MASK
                            : ZlgCanApi.CAN_SFF_MASK,
                        out id);
                _rewriteDbc[i].Enabled = _rewriteEnable[i] != null && _rewriteEnable[i].Checked && hasId;
            }
        }

        private void RewriteDbcClicked(object sender, EventArgs e)
        {
            var button = sender as Button;
            if (button == null || !(button.Tag is int))
                return;
            int index = (int)button.Tag;
            if (index < 0 || index >= RewriteSlotCount ||
                _rewriteEnable[index] == null || !_rewriteEnable[index].Checked)
                return;

            bool extended = _rewriteFrame[index] != null && _rewriteFrame[index].SelectedIndex == 1;
            uint id;
            uint maxId = extended ? ZlgCanApi.CAN_EFF_MASK : ZlgCanApi.CAN_SFF_MASK;
            if (_rewriteId[index] == null ||
                !TryParseCanId(_rewriteId[index].Text, maxId, out id) ||
                string.IsNullOrWhiteSpace(_rewriteId[index].Text))
            {
                MessageBox.Show(this, "Enter a valid CAN ID before searching the DBC database.",
                    "DBC lookup", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string databaseDirectory = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "Database");
            List<DbcMessage> matches = new List<DbcMessage>();
            var errors = new List<string>();
            try
            {
                Directory.CreateDirectory(databaseDirectory);
                string[] files = Directory.GetFiles(databaseDirectory, "*.dbc", SearchOption.AllDirectories);
                foreach (string file in files)
                {
                    try
                    {
                        foreach (DbcMessage message in DbcFileParser.Parse(file))
                        {
                            if (message.CanId != id || message.IsExtended != extended)
                                continue;
                            message.SourceFile = file;
                            message.Signals.RemoveAll(IsManagedDbcSignal);
                            matches.Add(message);
                        }
                    }
                    catch (IOException ex)
                    {
                        errors.Add(Path.GetFileName(file) + ": " + ex.Message);
                    }
                    catch (UnauthorizedAccessException ex)
                    {
                        errors.Add(Path.GetFileName(file) + ": " + ex.Message);
                    }
                    catch (FormatException ex)
                    {
                        errors.Add(Path.GetFileName(file) + ": " + ex.Message);
                    }
                }
            }
            catch (IOException ex)
            {
                MessageBox.Show(this, "Could not search the DBC database folder.\r\n" + ex.Message,
                    "DBC lookup", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            catch (UnauthorizedAccessException ex)
            {
                MessageBox.Show(this, "Access to the DBC database folder was denied.\r\n" + ex.Message,
                    "DBC lookup", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (matches.Count == 0)
            {
                string message = "No matching " + (extended ? "extended" : "standard") +
                    " message for ID 0x" + id.ToString(extended ? "X8" : "X3") +
                    " was found in:\r\n" + databaseDirectory;
                if (errors.Count > 0)
                    message += "\r\n\r\nSome DBC files could not be searched:\r\n" +
                        string.Join("\r\n", errors.GetRange(0, Math.Min(errors.Count, 5)).ToArray());
                MessageBox.Show(this, message, "DBC lookup", MessageBoxButtons.OK,
                    errors.Count > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
                return;
            }

            string scanWarning = errors.Count > 0
                ? errors.Count + " DBC file(s) could not be searched."
                : "";
            byte[] seedData = new byte[8];
            byte[] enteredData = ParseDataBytes(_rewriteData[index].Text);
            Array.Copy(enteredData, seedData, Math.Min(seedData.Length, enteredData.Length));
            if (IsCrcCanId(id) || IsNdrChecksumCanId(id))
                seedData[7] = 0;
            if (IsBmsCounterCanId(id))
                seedData[6] = 0;
            else if (IsDcdcCounterCanId(id))
                seedData[6] &= 0xF0;

            using (var builder = new DbcSignalBuilderForm(matches, data =>
            {
                _rewriteMode[index].Text = "Set";
                _rewriteData[index].Text = string.Join(" ",
                    Array.ConvertAll(data, value => value.ToString("X2", CultureInfo.InvariantCulture)));
                RebuildRewriteSlots();
            }, scanWarning, seedData))
            {
                builder.ShowDialog(this);
            }

            if (errors.Count > 0)
            {
                MessageBox.Show(this,
                    "Some DBC files could not be searched:\r\n" +
                    string.Join("\r\n", errors.GetRange(0, Math.Min(errors.Count, 5)).ToArray()),
                    "DBC lookup", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void RewriteDataChanged(object sender, EventArgs e)
        {
            var box = sender as TextBox;
            if (box == null)
                return;
            FormatRewriteData(box);
            RebuildRewriteSlots();
        }

        private void RebuildRewriteSlots()
        {
            var next = new RewriteSlot[RewriteSlotCount];
            for (int i = 0; i < RewriteSlotCount; i++)
            {
                var slot = new RewriteSlot { Data = new byte[8] };
                if (_rewriteEnable != null && _rewriteEnable[i] != null)
                    slot.Enabled = _rewriteEnable[i].Checked;
                if (_rewriteMode != null && _rewriteMode[i] != null)
                    slot.UseUserData = _rewriteMode[i].Text == "Set";
                slot.Index = i + 1;

                bool extended = _rewriteFrame != null && _rewriteFrame[i] != null &&
                    _rewriteFrame[i].SelectedIndex == 1;
                slot.Extended = extended;
                uint maxId = extended ? ZlgCanApi.CAN_EFF_MASK : ZlgCanApi.CAN_SFF_MASK;
                uint id;
                if (_rewriteId != null && _rewriteId[i] != null &&
                    TryParseCanId(_rewriteId[i].Text, maxId, out id) &&
                    !string.IsNullOrWhiteSpace(_rewriteId[i].Text))
                {
                    slot.HasId = true;
                    slot.Id = id;
                }

                byte[] data = ParseDataBytes(_rewriteData != null && _rewriteData[i] != null ? _rewriteData[i].Text : "");
                Array.Copy(data, slot.Data, Math.Min(8, data.Length));
                next[i] = slot;
            }

            lock (_rewriteLock)
                _rewriteSlots = next;
            ScheduleSave();
        }

        private RewriteSlot TryGetRewriteSlot(uint id, bool extended)
        {
            lock (_rewriteLock)
            {
                RewriteSlot[] slots = _rewriteSlots;
                if (slots == null)
                    return null;
                for (int i = 0; i < slots.Length; i++)
                {
                    RewriteSlot slot = slots[i];
                    if (slot == null || !slot.Enabled || !slot.HasId)
                        continue;
                    if (slot.Id != id || slot.Extended != extended)
                        continue;
                    return slot;
                }
            }
            return null;
        }

        private int TryRewriteCan(ref ZlgCanApi.can_frame frame, byte[] receivedData,
            out byte receivedLength, out bool applyCounter)
        {
            receivedLength = 0;
            applyCounter = false;
            if (ZlgCanApi.IS_RTR(frame.can_id) || ZlgCanApi.IS_ERR(frame.can_id))
                return 0;
            RewriteSlot slot = TryGetRewriteSlot(ZlgCanApi.GET_ID(frame.can_id), ZlgCanApi.IS_EFF(frame.can_id));
            if (slot == null)
                return 0;

            receivedLength = frame.can_dlc > 8 ? (byte)8 : frame.can_dlc;
            fixed (byte* originalData = frame.data)
            {
                for (int i = 0; i < receivedLength; i++)
                    receivedData[i] = originalData[i];
            }

            if (slot.UseUserData)
            {
                applyCounter = true;
                fixed (byte* data = frame.data)
                {
                    for (int i = 0; i < 8; i++)
                        data[i] = slot.Data[i];
                }
                frame.can_dlc = 8;
            }
            return slot.Index;
        }

        private int TryRewriteFd(ref ZlgCanApi.canfd_frame frame, byte[] receivedData,
            out byte receivedLength, out bool applyCounter)
        {
            receivedLength = 0;
            applyCounter = false;
            if (ZlgCanApi.IS_ERR(frame.can_id))
                return 0;
            RewriteSlot slot = TryGetRewriteSlot(ZlgCanApi.GET_ID(frame.can_id), ZlgCanApi.IS_EFF(frame.can_id));
            if (slot == null)
                return 0;

            receivedLength = frame.len > ZlgCanApi.CANFD_MAX_DLEN
                ? (byte)ZlgCanApi.CANFD_MAX_DLEN
                : frame.len;
            fixed (byte* originalData = frame.data)
            {
                for (int i = 0; i < receivedLength; i++)
                    receivedData[i] = originalData[i];
            }

            if (slot.UseUserData)
            {
                applyCounter = true;
                fixed (byte* data = frame.data)
                {
                    for (int i = 0; i < 8; i++)
                        data[i] = slot.Data[i];
                    for (int i = 8; i < ZlgCanApi.CANFD_MAX_DLEN; i++)
                        data[i] = 0;
                }
                frame.len = 8;
            }
            return slot.Index;
        }

        private void UpdateUiState(bool open)
        {
            _isOpen = open;
            if (!open)
                chkPauseDisplay.Checked = false;
            btnOpen.Enabled = !open;
            btnClose.Enabled = open;
            cmbRxDevice.Enabled = !open;
            cmbTxDevice.Enabled = !open;
            numRxIndex.Enabled = !open;
            numTxIndex.Enabled = !open;
            numRxChannel.Enabled = !open;
            numTxChannel.Enabled = !open;
            cmbRxBaud.Enabled = !open;
            cmbTxBaud.Enabled = !open;
            ApplyProtocolUi();
        }

        private void SetIdle(string text)
        {
            _ledColor = Color.Gray;
            pnlLed.Invalidate();
            lblStatus.ForeColor = Color.Black;
            lblStatus.Text = text;
        }

        private void SetOperating(string text)
        {
            _ledColor = Color.LimeGreen;
            pnlLed.Invalidate();
            lblStatus.ForeColor = Color.DarkGreen;
            lblStatus.Text = text;
        }

        private void SetFailed(string text)
        {
            _ledColor = Color.Red;
            pnlLed.Invalidate();
            lblStatus.ForeColor = Color.Firebrick;
            lblStatus.Text = text;
        }

        private void pnlLed_Paint(object sender, PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = new Rectangle(1, 1, pnlLed.Width - 3, pnlLed.Height - 3);
            using (var brush = new SolidBrush(_ledColor))
                e.Graphics.FillEllipse(brush, rect);
            using (var pen = new Pen(Color.DimGray))
                e.Graphics.DrawEllipse(pen, rect);
        }

        private void btnOpen_Click(object sender, EventArgs e)
        {
            bool started = false;
            try
            {
                if (!ZlgCanApi.EnsureLoaded())
                {
                    SetFailed("Cannot load zlgcan.dll. " + (ZlgCanApi.LastLoadError ?? "").Trim() +
                              " Build platform must match DLL (x64 app with x64 zlgcan.dll).");
                    return;
                }

                uint rxType = GetSelectedDeviceType(cmbRxDevice);
                uint txType = GetSelectedDeviceType(cmbTxDevice);
                uint rxIndex = (uint)numRxIndex.Value;
                uint txIndex = (uint)numTxIndex.Value;
                uint rxCh = (uint)numRxChannel.Value;
                uint txCh = (uint)numTxChannel.Value;
                bool sameDevice = rxType == txType && rxIndex == txIndex;
                if (sameDevice && rxCh == txCh)
                {
                    SetFailed("A and B cannot be the same channel on the same device.");
                    return;
                }

                bool rxFd = ZlgCanApi.IsCanFdDevice(rxType);
                bool txFd = ZlgCanApi.IsCanFdDevice(txType);
                bool rxProtoFd = rxFd && cmbRxProtocol.SelectedIndex == 1;
                bool txProtoFd = txFd && cmbTxProtocol.SelectedIndex == 1;
                string rxBaud = GetSelectedBaud(cmbRxBaud);
                string txBaud = GetSelectedBaud(cmbTxBaud);
                string rxDataBaud = GetSelectedBaud(cmbRxDataBaud, "2000000");
                string txDataBaud = GetSelectedBaud(cmbTxDataBaud, "2000000");

                string error;
                if (!TryOpenDevice(rxType, rxIndex, out _rxDeviceHandle, out error))
                {
                    SetFailed(error);
                    return;
                }

                if (sameDevice)
                    _txDeviceHandle = _rxDeviceHandle;
                else if (!TryOpenDevice(txType, txIndex, out _txDeviceHandle, out error))
                {
                    CloseNative(false);
                    SetFailed(error);
                    return;
                }

                if (!ChannelExists(_rxDeviceHandle, rxCh) || !ChannelExists(_txDeviceHandle, txCh))
                {
                    CloseNative(false);
                    SetFailed("Open failed: selected channel does not exist on that device.");
                    return;
                }

                bool baudOk = ApplyChannelPreInit(_rxDeviceHandle, rxCh, rxFd, rxProtoFd, rxBaud, rxDataBaud,
                    cmbRxIso.SelectedIndex == 1);
                if (!TryInitChannel(_rxDeviceHandle, rxCh, rxFd, out _rxChannelHandle, out error))
                {
                    CloseNative(false);
                    SetFailed(error);
                    return;
                }
                ApplyChannelPostInit(_rxDeviceHandle, rxCh, rxFd, chkRxTerm.Checked);
                if (!TryStartChannel(_rxChannelHandle, rxCh, out error))
                {
                    CloseNative(false);
                    SetFailed(error);
                    return;
                }

                baudOk = ApplyChannelPreInit(_txDeviceHandle, txCh, txFd, txProtoFd, txBaud, txDataBaud,
                    cmbTxIso.SelectedIndex == 1) && baudOk;
                if (!TryInitChannel(_txDeviceHandle, txCh, txFd, out _txChannelHandle, out error))
                {
                    CloseNative(false);
                    SetFailed(error);
                    return;
                }
                ApplyChannelPostInit(_txDeviceHandle, txCh, txFd, chkTxTerm.Checked);
                if (!TryStartChannel(_txChannelHandle, txCh, out error))
                {
                    CloseNative(false);
                    SetFailed(error);
                    return;
                }

                _rxChannelIndex = rxCh;
                _txChannelIndex = txCh;
                _rxProtocolFd = rxProtoFd;
                _txProtocolFd = txProtoFd;
                _bridgeTxFailed = false;
                _txFailStreak = 0;
                lock (_echoLock)
                {
                    for (int i = 0; i < EchoCap; i++)
                    {
                        _echoA[i].CanId = 0xFFFFFFFF;
                        _echoB[i].CanId = 0xFFFFFFFF;
                    }
                    _echoAHead = 0;
                    _echoBHead = 0;
                }
                ClearLatestSignalDisplay();
                try { ZlgCanApi.ZCAN_ClearBuffer(_rxChannelHandle); } catch { }
                try { ZlgCanApi.ZCAN_ClearBuffer(_txChannelHandle); } catch { }
                UpdateUiState(true);
                StartBridgeThreads();
                _uiTimer.Start();
                started = true;
                string extra = baudOk ? "" : " Baud set failed; device may ignore bitrate.";
                string rxProto = rxProtoFd ? "CAN FD" : "CAN";
                string txProto = txProtoFd ? "CAN FD" : "CAN";
                SetOperating("Bridge A↔B. A " + rxProto + " " + rxBaud + " CH" + rxCh +
                             " / B " + txProto + " " + txBaud + " CH" + txCh + "." + extra);
            }
            catch (DllNotFoundException ex)
            {
                SetFailed("DLL not found: " + ex.Message);
            }
            catch (BadImageFormatException)
            {
                SetFailed("Architecture mismatch.  process is " + ZlgCanApi.ProcessArch +
                          ". Use matching zlgcan.dll.");
            }
            catch (Exception ex)
            {
                SetFailed(ex.GetType().Name + ": " + ex.Message);
            }
            finally
            {
                if (!started)
                {
                    if (_uiTimer != null)
                        _uiTimer.Stop();
                    if (StopBridgeThreads())
                    {
                        CloseNative(false);
                        UpdateUiState(false);
                    }
                    else
                    {
                        SetFailed("Bridge worker did not stop; CAN handles remain open for safety.");
                    }
                }
            }
        }

        private void btnClose_Click(object sender, EventArgs e)
        {
            CloseDevice();
        }

        private bool CloseDevice()
        {
            if (_uiTimer != null)
                _uiTimer.Stop();

            // Stop accepting new work first, then close the native channels/device
            // even if the worker is stuck inside a vendor call.  Leaving the device
            // open because a faulted bus worker did not join would prevent a clean
            // reopen and could also block application shutdown.
            _bridgeRunning = false;
            ClearLatestSignalDisplay();
            CloseNative(true);

            Thread worker = _rxThread;
            JoinWorker(worker, 500);
            if (worker == null || !worker.IsAlive)
                _rxThread = null;

            UpdateUiState(false);
            SetIdle("Closed");
            return true;
        }

        private void CloseNative(bool updateFields)
        {
            // Atomically detach the handles before calling into the vendor DLL.
            // This prevents the RX loop from starting another operation after
            // shutdown has begun, while allowing shutdown to proceed even when
            // the worker is currently holding _canLock in a native call.
            IntPtr rxChannel = Interlocked.Exchange(ref _rxChannelHandle, IntPtr.Zero);
            IntPtr txChannel = Interlocked.Exchange(ref _txChannelHandle, IntPtr.Zero);
            IntPtr txDevice = Interlocked.Exchange(ref _txDeviceHandle, IntPtr.Zero);
            IntPtr rxDevice = Interlocked.Exchange(ref _rxDeviceHandle, IntPtr.Zero);

            bool sameChannel = txChannel != IntPtr.Zero && txChannel == rxChannel;
            ResetChannel(ref rxChannel);
            if (!sameChannel)
                ResetChannel(ref txChannel);

            if (txDevice != IntPtr.Zero && txDevice != rxDevice)
            {
                try { ZlgCanApi.ZCAN_CloseDevice(txDevice); } catch { }
            }
            if (rxDevice != IntPtr.Zero)
            {
                try { ZlgCanApi.ZCAN_CloseDevice(rxDevice); } catch { }
            }

            if (updateFields)
            {
                _rxProtocolFd = false;
                _txProtocolFd = false;
            }
        }

        private static void ResetChannel(ref IntPtr handle)
        {
            if (handle == IntPtr.Zero)
                return;
            try { ZlgCanApi.ZCAN_ResetCAN(handle); } catch { }
            handle = IntPtr.Zero;
        }

        private static bool TryOpenDevice(uint type, uint index, out IntPtr handle, out string error)
        {
            handle = ZlgCanApi.ZCAN_OpenDevice(type, index, 0);
            if (handle == IntPtr.Zero)
            {
                handle = IntPtr.Zero;
                error = "Open failed: ZCAN_OpenDevice type " + type + " idx " + index +
                        ". Plug in USB, install driver, close other CAN software.";
                return false;
            }
            error = null;
            return true;
        }

        private static bool ChannelExists(IntPtr device, uint channel)
        {
            var info = new ZlgCanApi.ZCAN_DEVICE_INFO
            {
                str_Serial_Num = new byte[20],
                str_hw_Type = new byte[40],
                reserved = new ushort[4]
            };
            if (ZlgCanApi.ZCAN_GetDeviceInf(device, ref info) != ZlgCanApi.STATUS_OK)
                return true;
            return info.can_Num > channel;
        }

        private static bool ApplyChannelPreInit(IntPtr device, uint index, bool fdDevice, bool protocolFd,
            string baud, string dataBaud, bool nonIso)
        {
            string n = index.ToString();
            if (fdDevice)
            {
                bool ok = ZlgCanApi.ZCAN_SetValue(device, n + "/clock", "60000000") == ZlgCanApi.STATUS_OK;
                ok = ZlgCanApi.ZCAN_SetValue(device, n + "/protocol", protocolFd ? "1" : "0") == ZlgCanApi.STATUS_OK && ok;
                if (protocolFd)
                    ok = ZlgCanApi.ZCAN_SetValue(device, n + "/canfd_standard", nonIso ? "1" : "0") == ZlgCanApi.STATUS_OK && ok;
                ok = ZlgCanApi.ZCAN_SetValue(device, n + "/canfd_abit_baud_rate", baud) == ZlgCanApi.STATUS_OK && ok;
                if (protocolFd)
                    ok = ZlgCanApi.ZCAN_SetValue(device, n + "/canfd_dbit_baud_rate", dataBaud) == ZlgCanApi.STATUS_OK && ok;
                else
                    ok = ZlgCanApi.ZCAN_SetValue(device, n + "/canfd_dbit_baud_rate", baud) == ZlgCanApi.STATUS_OK && ok;
                return ok;
            }
            return ZlgCanApi.ZCAN_SetValue(device, n + "/baud_rate", baud) == ZlgCanApi.STATUS_OK;
        }

        private static void ApplyChannelPostInit(IntPtr device, uint index, bool fdDevice, bool terminatorOn)
        {
            if (!fdDevice)
                return;
            ZlgCanApi.ZCAN_SetValue(device, index + "/initenal_resistance", terminatorOn ? "1" : "0");
            try
            {
                ZlgCanApi.ZCAN_SetValue(device, index + "/filter_clear", "0");
                ZlgCanApi.ZCAN_SetValue(device, index + "/set_device_tx_echo", "0");
            }
            catch
            {
            }
        }

        private static bool TryInitChannel(IntPtr device, uint index, bool fdDevice, out IntPtr handle, out string error)
        {
            handle = IntPtr.Zero;
            error = null;
            if (fdDevice)
            {
                var cfg = new ZlgCanApi.ZCAN_CHANNEL_INIT_CONFIG_CANFD
                {
                    can_type = ZlgCanApi.TYPE_CANFD,
                    acc_code = 0,
                    acc_mask = 0xFFFFFFFF,
                    abit_timing = 0,
                    dbit_timing = 0,
                    brp = 0,
                    filter = 0,
                    mode = 0,
                    pad = 0,
                    reserved = 0
                };
                handle = ZlgCanApi.ZCAN_InitCAN(device, index, ref cfg);
            }
            else
            {
                var cfg = new ZlgCanApi.ZCAN_CHANNEL_INIT_CONFIG
                {
                    can_type = ZlgCanApi.TYPE_CAN,
                    acc_code = 0,
                    acc_mask = 0xFFFFFFFF,
                    reserved = 0,
                    filter = 0,
                    timing0 = 0,
                    timing1 = 0,
                    mode = 0,
                    pad1 = 0,
                    pad2 = 0,
                    pad3 = 0
                };
                handle = ZlgCanApi.ZCAN_InitCAN(device, index, ref cfg);
            }

            if (handle == IntPtr.Zero)
            {
                error = "Open failed: ZCAN_InitCAN channel " + index + ".";
                handle = IntPtr.Zero;
                return false;
            }
            return true;
        }

        private static bool TryStartChannel(IntPtr handle, uint index, out string error)
        {
            error = null;
            if (ZlgCanApi.ZCAN_StartCAN(handle) != ZlgCanApi.STATUS_OK)
            {
                error = "Open failed: ZCAN_StartCAN channel " + index + ".";
                try { ZlgCanApi.ZCAN_ResetCAN(handle); } catch { }
                return false;
            }
            try { ZlgCanApi.ZCAN_ClearBuffer(handle); } catch { }
            return true;
        }

        private void InitRxBuffers()
        {
            _rxCanBuf = new ZlgCanApi.ZCAN_Receive_Data[RxBatch];
            _rxFdBuf = new ZlgCanApi.ZCAN_ReceiveFD_Data[RxBatch];
            _txCanBuf = new ZlgCanApi.ZCAN_Transmit_Data[1];
            _txFdBuf = new ZlgCanApi.ZCAN_TransmitFD_Data[1];
            PinBuf(ref _rxCanPin, _rxCanBuf);
            PinBuf(ref _rxFdPin, _rxFdBuf);
            PinBuf(ref _txCanPin, _txCanBuf);
            PinBuf(ref _txFdPin, _txFdBuf);
        }

        private static void PinBuf<T>(ref GCHandle handle, T[] buf) where T : struct
        {
            if (handle.IsAllocated)
                handle.Free();
            handle = GCHandle.Alloc(buf, GCHandleType.Pinned);
        }

        private void FreePins()
        {
            if (_rxCanPin.IsAllocated) _rxCanPin.Free();
            if (_rxFdPin.IsAllocated) _rxFdPin.Free();
            if (_txCanPin.IsAllocated) _txCanPin.Free();
            if (_txFdPin.IsAllocated) _txFdPin.Free();
        }

        private void StartBridgeThreads()
        {
            if (!StopBridgeThreads())
                throw new InvalidOperationException("The previous CAN worker is still running.");
            _bridgeRunning = true;
            _rxThread = new Thread(RxLoop)
            {
                IsBackground = true,
                Name = "CAN RX",
                Priority = ThreadPriority.AboveNormal
            };
            _rxThread.Start();
        }

        private bool StopBridgeThreads()
        {
            _bridgeRunning = false;
            Thread worker = _rxThread;
            JoinWorker(worker, 2000);
            if (worker != null && worker.IsAlive)
                return false;
            _rxThread = null;
            return true;
        }

        private static void JoinWorker(Thread thread, int milliseconds)
        {
            if (thread != null && thread.IsAlive && thread != Thread.CurrentThread)
                thread.Join(milliseconds);
        }

        private void RxLoop()
        {
            while (_bridgeRunning)
            {
                try
                {
                    IntPtr a;
                    IntPtr b;
                    lock (_canLock)
                    {
                        a = _rxChannelHandle;
                        b = _txChannelHandle;
                    }
                    if (a == IntPtr.Zero || b == IntPtr.Zero)
                    {
                        Thread.Sleep(5);
                        continue;
                    }

                    bool any = PollSide(a, b, _rxProtocolFd, _txProtocolFd);
                    any |= PollSide(b, a, _txProtocolFd, _rxProtocolFd);
                    if (!any)
                        Thread.Sleep(1);
                }
                catch
                {
                    Thread.Sleep(1);
                }
            }
        }

        private void UiTimer_Tick(object sender, EventArgs e)
        {
            if (!_isOpen)
                return;

            if (!chkPauseDisplay.Checked)
                RefreshLatestSignalDisplay();
            if (_bridgeTxFailed)
            {
                _bridgeTxFailed = false;
                SetFailed("Bridge transmit failed. Bus still open.");
            }
        }

        private void RefreshLatestSignalDisplay()
        {
            var updates = new LatestSignal[RewriteSlotCount];
            lock (_latestSignalLock)
            {
                for (int i = 0; i < _latestSignals.Length; i++)
                {
                    if (!_latestSignalDirty[i])
                        continue;
                    updates[i] = _latestSignals[i].Clone();
                    _latestSignalDirty[i] = false;
                }
            }
            for (int i = 0; i < updates.Length; i++)
                if (updates[i] != null)
                    UpdateLatestSignal(updates[i]);
        }

        private void chkPauseDisplay_CheckedChanged(object sender, EventArgs e)
        {
            if (!chkPauseDisplay.Checked && _isOpen)
                RefreshLatestSignalDisplay();
        }

        private void UpdateLatestSignalRows()
        {
            if (_slotSignalItems == null || _rewriteEnable == null)
                return;

            lvLatestSignal.BeginUpdate();
            lvLatestSignal.Items.Clear();
            for (int i = 0; i < _slotSignalItems.Length; i++)
            {
                if (_rewriteEnable[i] != null && _rewriteEnable[i].Checked)
                {
                    bool extended = _rewriteFrame[i] != null && _rewriteFrame[i].SelectedIndex == 1;
                    uint id;
                    if (_rewriteId[i] != null &&
                        !string.IsNullOrWhiteSpace(_rewriteId[i].Text) &&
                        TryParseCanId(_rewriteId[i].Text,
                        extended ? ZlgCanApi.CAN_EFF_MASK : ZlgCanApi.CAN_SFF_MASK, out id))
                    {
                        _slotSignalItems[i].SubItems[3].Text =
                            (extended ? "EXT " : "STD ") + "0x" +
                            id.ToString(extended ? "X8" : "X3");
                    }
                    else
                    {
                        _slotSignalItems[i].SubItems[3].Text = "";
                    }
                    if (!string.IsNullOrWhiteSpace(_slotSignalItems[i].SubItems[3].Text))
                        lvLatestSignal.Items.Add(_slotSignalItems[i]);
                }
            }
            lvLatestSignal.EndUpdate();
        }

        private void ClearLatestSignalSlot(int index)
        {
            if (index < 0 || index >= _latestSignals.Length)
                return;

            lock (_latestSignalLock)
            {
                _latestSignalDirty[index] = false;
                _latestSignals[index].Clear();
            }

            if (_slotSignalItems == null || _slotSignalItems[index] == null)
                return;
            for (int i = 1; i < _slotSignalItems[index].SubItems.Count; i++)
                _slotSignalItems[index].SubItems[i].Text = "";
        }

        private void ClearLatestSignalDisplay()
        {
            lock (_latestSignalLock)
            {
                for (int i = 0; i < _latestSignals.Length; i++)
                {
                    _latestSignals[i].Clear();
                    _latestSignalDirty[i] = false;
                }
            }

            for (int i = 0; i < _slotSignalItems.Length; i++)
                for (int column = 1; column < _slotSignalItems[i].SubItems.Count; column++)
                    _slotSignalItems[i].SubItems[column].Text = "";
            UpdateLatestSignalRows();
        }

        private bool PollSide(IntPtr src, IntPtr dest, bool srcProtocolFd, bool destProtocolFd)
        {
            bool any = false;
            uint count = 0;
            lock (_canLock)
            {
                if (src == IntPtr.Zero)
                    return false;

                // On an FD channel, query the classic-CAN queue before reading it;
                // the driver exposes CAN and CAN-FD queues separately. Keep the
                // classic-only path identical to the known-good CAN path, but
                // still reject an unsigned native -1/error return.
                uint request = (uint)RxBatch;
                if (srcProtocolFd)
                {
                    uint pending = ZlgCanApi.ZCAN_GetReceiveNum(src, (byte)ZlgCanApi.TYPE_CAN);
                    if (pending == 0 || pending == uint.MaxValue)
                        request = 0;
                    else if (pending < request)
                        request = pending;
                }
                if (request != 0)
                {
                    uint returned = ZlgCanApi.ZCAN_Receive(src,
                        (ZlgCanApi.ZCAN_Receive_Data*)_rxCanPin.AddrOfPinnedObject().ToPointer(),
                        request, 0);
                    if (returned <= request)
                        count = returned;
                }
            }

            for (uint i = 0; i < count; i++)
            {
                var f = _rxCanBuf[i].frame;
                if (f.can_dlc > 8)
                    f.can_dlc = 8;
                if (ZlgCanApi.IS_ERR(f.can_id))
                    continue;
                if ((f.__pad & ZlgCanApi.TX_ECHO_FLAG) != 0)
                    continue;
                byte* data = f.data;
                if (IsOurEcho(src, f.can_id, data, f.can_dlc))
                    continue;
                any = true;
                byte receivedLength;
                bool applyCounter;
                int slotIndex = TryRewriteCan(ref f, _receivedDataScratch,
                    out receivedLength, out applyCounter);
                EnsureMinimumCanLength(ref f);
                if (applyCounter)
                {
                    ApplyBmsCrc(ref f, true);
                    ApplyNdrChecksum(ref f);
                }
                bool sent = ForwardCan(dest, f);
                NoteTxResult(sent);
                if (sent)
                    RememberTx(dest, f.can_id, f.data, f.can_dlc);
                if (slotIndex != 0)
                    StoreLatestSignal(slotIndex, src, dest, f.can_id, _receivedDataScratch,
                        receivedLength, f.data, f.can_dlc, sent);
            }

            if (!srcProtocolFd)
                return any;

            uint fdCount = 0;
            lock (_canLock)
            {
                if (src == IntPtr.Zero)
                    return any;

                uint pending = ZlgCanApi.ZCAN_GetReceiveNum(src, (byte)ZlgCanApi.TYPE_CANFD);
                if (pending != 0 && pending != uint.MaxValue)
                {
                    uint request = pending > (uint)RxBatch ? (uint)RxBatch : pending;
                    uint returned = ZlgCanApi.ZCAN_ReceiveFD(src,
                        (ZlgCanApi.ZCAN_ReceiveFD_Data*)_rxFdPin.AddrOfPinnedObject().ToPointer(),
                        request, 0);
                    if (returned <= request)
                        fdCount = returned;
                }
            }

            for (uint i = 0; i < fdCount; i++)
            {
                var f = _rxFdBuf[i].frame;
                if (ZlgCanApi.IS_ERR(f.can_id))
                    continue;
                if (f.len > ZlgCanApi.CANFD_MAX_DLEN)
                    f.len = (byte)ZlgCanApi.CANFD_MAX_DLEN;
                if ((f.flags & ZlgCanApi.TX_ECHO_FLAG) != 0)
                    continue;
                byte* data = f.data;
                if (IsOurEcho(src, f.can_id, data, f.len))
                    continue;
                any = true;
                byte receivedLength;
                bool applyCounter;
                int slotIndex = TryRewriteFd(ref f, _receivedDataScratch,
                    out receivedLength, out applyCounter);
                bool sent;
                byte* forwardedData;
                int forwardedLength;
                if (destProtocolFd)
                {
                    EnsureMinimumFdLength(ref f);
                    if (applyCounter)
                    {
                        ApplyBmsCrc(ref f, true);
                        ApplyNdrChecksum(ref f);
                    }
                    sent = ForwardFd(dest, f);
                    if (sent)
                        RememberTx(dest, f.can_id, f.data, f.len);
                    forwardedData = f.data;
                    forwardedLength = f.len;
                }
                else
                {
                    var classic = FdToClassic(f);
                    EnsureMinimumCanLength(ref classic);
                    if (applyCounter)
                    {
                        ApplyBmsCrc(ref classic, true);
                        ApplyNdrChecksum(ref classic);
                    }
                    sent = ForwardCan(dest, classic);
                    if (sent)
                        RememberTx(dest, classic.can_id, classic.data, classic.can_dlc);
                    forwardedData = classic.data;
                    forwardedLength = classic.can_dlc;
                }
                NoteTxResult(sent);
                if (slotIndex != 0)
                    StoreLatestSignal(slotIndex, src, dest, f.can_id, _receivedDataScratch,
                        receivedLength, forwardedData, forwardedLength, sent);
            }

            return any;
        }

        private void StoreLatestSignal(int slotIndex, IntPtr src, IntPtr dest, uint canId,
            byte[] receivedData, int receivedLength, byte* forwardedData, int forwardedLength,
            bool sent)
        {
            bool fromRx = src == _rxChannelHandle;
            bool toRx = dest == _rxChannelHandle;
            lock (_latestSignalLock)
            {
                LatestSignal entry = _latestSignals[slotIndex - 1];
                entry.Time = DateTime.Now;
                entry.From = (fromRx ? "RX CH" : "TX CH") +
                    (fromRx ? _rxChannelIndex : _txChannelIndex);
                entry.To = (toRx ? "RX CH" : "TX CH") +
                    (toRx ? _rxChannelIndex : _txChannelIndex);
                entry.Id = (ZlgCanApi.IS_EFF(canId) ? "EXT " : "STD ") + "0x" +
                    ZlgCanApi.GET_ID(canId).ToString(
                        ZlgCanApi.IS_EFF(canId) ? "X8" : "X3");
                entry.ReceivedLength = (byte)Math.Min(receivedLength, entry.ReceivedBytes.Length);
                entry.ForwardedLength = (byte)Math.Min(forwardedLength, entry.ForwardedBytes.Length);
                Array.Copy(receivedData, entry.ReceivedBytes, entry.ReceivedLength);
                for (int i = 0; i < entry.ForwardedLength; i++)
                    entry.ForwardedBytes[i] = forwardedData[i];
                entry.Sent = sent;
                _latestSignalDirty[slotIndex - 1] = true;
            }
        }

        private void UpdateLatestSignal(LatestSignal entry)
        {
            if (entry.Slot < 1 || entry.Slot > _slotSignalItems.Length ||
                _rewriteEnable[entry.Slot - 1] == null ||
                !_rewriteEnable[entry.Slot - 1].Checked)
                return;
            ListViewItem item = _slotSignalItems[entry.Slot - 1];
            item.SubItems[1].Text = entry.Time.ToString("HH:mm:ss.fff");
            item.SubItems[2].Text = entry.From;
            item.SubItems[3].Text = entry.Id;
            item.SubItems[4].Text = BytesToHex(entry.ReceivedBytes, entry.ReceivedLength);
            item.SubItems[5].Text = entry.To;
            item.SubItems[6].Text = BytesToHex(entry.ForwardedBytes, entry.ForwardedLength);
            item.SubItems[7].Text = entry.Sent ? "Sent" : "Failed";
        }

        private static ZlgCanApi.can_frame FdToClassic(ZlgCanApi.canfd_frame fd)
        {
            byte n = fd.len;
            if (n > 8)
                n = 8;
            var frame = new ZlgCanApi.can_frame
            {
                can_id = fd.can_id & ~ZlgCanApi.CAN_ERR_FLAG,
                can_dlc = n,
            };
            if (n > 0)
            {
                byte* source = fd.data;
                byte* target = frame.data;
                for (int i = 0; i < n; i++)
                    target[i] = source[i];
            }
            return frame;
        }

        private struct EchoKey
        {
            public uint CanId;
            public ulong Data;
            public int Tick;
        }

        private static ulong PackData(byte* data, int len)
        {
            ulong v = 0;
            if (data == null)
                return 0;
            int n = len;
            if (n < 0) n = 0;
            if (n > 8) n = 8;
            for (int i = 0; i < n; i++)
                v |= (ulong)data[i] << (8 * i);
            return v;
        }

        private bool IsOurEcho(IntPtr src, uint canId, byte* data, int len)
        {
            ulong packed = PackData(data, len);
            int now = Environment.TickCount;
            lock (_echoLock)
            {
                EchoKey[] ring = src == _rxChannelHandle ? _echoA : _echoB;
                for (int i = 0; i < ring.Length; i++)
                {
                    if (ring[i].CanId == 0xFFFFFFFF)
                        continue;
                    if (unchecked(now - ring[i].Tick) > 8)
                    {
                        ring[i].CanId = 0xFFFFFFFF;
                        continue;
                    }
                    if (ring[i].CanId == canId && ring[i].Data == packed)
                    {
                        ring[i].CanId = 0xFFFFFFFF;
                        return true;
                    }
                }
            }
            return false;
        }

        private void RememberTx(IntPtr dest, uint canId, byte* data, int len)
        {
            var key = new EchoKey
            {
                CanId = canId,
                Data = PackData(data, len),
                Tick = Environment.TickCount
            };
            lock (_echoLock)
            {
                if (dest == _rxChannelHandle)
                {
                    _echoA[_echoAHead] = key;
                    _echoAHead = (_echoAHead + 1) % EchoCap;
                }
                else
                {
                    _echoB[_echoBHead] = key;
                    _echoBHead = (_echoBHead + 1) % EchoCap;
                }
            }
        }

        private void NoteTxResult(bool ok)
        {
            if (ok)
            {
                _txFailStreak = 0;
                return;
            }
            _txFailStreak++;
            if (_txFailStreak >= 40)
                _bridgeTxFailed = true;
        }

        private static bool IsBmsCrcCanId(uint canId)
        {
            if (ZlgCanApi.IS_EFF(canId) || ZlgCanApi.IS_RTR(canId) || ZlgCanApi.IS_ERR(canId))
                return false;

            switch (ZlgCanApi.GET_ID(canId))
            {
                case 0x61B:
                case 0x64B:
                case 0x62B:
                case 0x63B:
                case 0x66B:
                case 0x67B:
                case 0x6AB:
                case 0x61A:
                case 0x64A:
                case 0x65A:
                case 0x66A:
                case 0x67A:
                case 0x68A:
                case 0x69A:
                case 0x6AA:
                case 0x6BA:
                case 0x6CA:
                case 0x61E:
                case 0x3AA:
                case 0x3BA:
                case 0x3CA:
                case 0x79D:
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsDcdcCrcCanId(uint canId)
        {
            if (ZlgCanApi.IS_EFF(canId) || ZlgCanApi.IS_RTR(canId) || ZlgCanApi.IS_ERR(canId))
                return false;

            switch (ZlgCanApi.GET_ID(canId))
            {
                case 0x1CA:
                case 0x452:
                case 0x122:
                case 0x171:
                case 0x111:
                case 0x123:
                case 0x271:
                case 0x145:
                case 0x146:
                case 0x318:
                case 0x355:
                case 0x3D5:
                case 0x3A3:
                case 0x3B5:
                case 0x445:
                case 0x461:
                case 0x462:
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsBmsCounterCanId(uint canId)
        {
            if (ZlgCanApi.IS_EFF(canId) || ZlgCanApi.IS_RTR(canId) || ZlgCanApi.IS_ERR(canId))
                return false;

            switch (ZlgCanApi.GET_ID(canId))
            {
                case 0x61B:
                case 0x64B:
                case 0x62B:
                case 0x63B:
                case 0x66B:
                case 0x67B:
                case 0x6AB:
                case 0x61A:
                    return true;
                default:
                    return false;
            }
        }

        private static byte GetNextBmsCounter()
        {
            lock (BmsCounterLock)
            {
                byte counter = _bmsCounter;
                _bmsCounter++;
                return counter;
            }
        }

        private static byte GetNextDcdcAliveCounter()
        {
            lock (BmsCounterLock)
            {
                byte counter = _dcdcAliveCounter;
                _dcdcAliveCounter = (byte)((_dcdcAliveCounter + 1) & 0x0F);
                return counter;
            }
        }

        private static byte CalculateBmsCrc(byte* data, int length)
        {
            byte crc = 0x00;
            for (int i = 0; i < length - 1; i++)
            {
                crc ^= data[i];
                for (int bit = 0; bit < 8; bit++)
                    crc = (byte)((crc & 0x80) != 0 ? (crc << 1) ^ 0x17 : crc << 1);
            }
            return crc;
        }

        private static byte CalculateDcdcCrc(byte* data, int length)
        {
            byte crc = 0xFF;
            for (int i = 0; i < length - 1; i++)
            {
                crc ^= data[i];
                for (int bit = 0; bit < 8; bit++)
                    crc = (byte)((crc & 0x80) != 0 ? (crc << 1) ^ 0x1D : crc << 1);
            }
            return (byte)~crc;
        }

        private static bool IsCrcCanId(uint canId)
        {
            return IsBmsCrcCanId(canId) || IsDcdcCrcCanId(canId);
        }

        private static bool IsNdrChecksumCanId(uint canId)
        {
            return !ZlgCanApi.IS_EFF(canId) &&
                   !ZlgCanApi.IS_RTR(canId) &&
                   !ZlgCanApi.IS_ERR(canId) &&
                   ZlgCanApi.GET_ID(canId) == 0x22C;
        }

        private static bool IsDcdcCounterCanId(uint canId)
        {
            return !ZlgCanApi.IS_EFF(canId) &&
                   !ZlgCanApi.IS_RTR(canId) &&
                   !ZlgCanApi.IS_ERR(canId) &&
                   (ZlgCanApi.GET_ID(canId) == 0x1CA ||
                    ZlgCanApi.GET_ID(canId) == 0x145 ||
                    ZlgCanApi.GET_ID(canId) == 0x146);
        }

        private static bool IsManagedDbcSignal(DbcSignal signal)
        {
            if (signal == null || string.IsNullOrEmpty(signal.Name))
                return false;

            return HasSignalNameSuffix(signal.Name, "D_Counter") ||
                   HasSignalNameSuffix(signal.Name, "D_CRC") ||
                   HasSignalNameSuffix(signal.Name, "D_CRC8");
        }

        private static bool HasSignalNameSuffix(string signalName, string suffix)
        {
            return signalName.Equals(suffix, StringComparison.OrdinalIgnoreCase) ||
                   signalName.EndsWith("_" + suffix, StringComparison.OrdinalIgnoreCase);
        }

        private static byte CalculateCrc(uint canId, byte* data, int length)
        {
            return IsDcdcCrcCanId(canId)
                ? CalculateDcdcCrc(data, length)
                : CalculateBmsCrc(data, length);
        }

        private static byte CalculateNdrChecksum(byte* data)
        {
            ushort checksum = 0;
            for (int i = 0; i < 7; i++)
                checksum += data[i];

            checksum &= 0xFF;
            checksum ^= 0xFF;
            return (byte)checksum;
        }

        private static void ApplyBmsCrc(ref ZlgCanApi.can_frame frame, bool applyCounter)
        {
            if (!IsCrcCanId(frame.can_id) || frame.can_dlc != 8)
                return;

            fixed (byte* data = frame.data)
            {
                if (applyCounter)
                {
                    if (IsBmsCounterCanId(frame.can_id))
                        data[6] = GetNextBmsCounter();
                    else if (IsDcdcCounterCanId(frame.can_id))
                        data[6] = (byte)((data[6] & 0xF0) | GetNextDcdcAliveCounter());
                }
                data[7] = CalculateCrc(frame.can_id, data, frame.can_dlc);
            }
        }

        private static void ApplyBmsCrc(ref ZlgCanApi.canfd_frame frame, bool applyCounter)
        {
            if (!IsCrcCanId(frame.can_id) || frame.len != 8)
                return;

            fixed (byte* data = frame.data)
            {
                if (applyCounter)
                {
                    if (IsBmsCounterCanId(frame.can_id))
                        data[6] = GetNextBmsCounter();
                    else if (IsDcdcCounterCanId(frame.can_id))
                        data[6] = (byte)((data[6] & 0xF0) | GetNextDcdcAliveCounter());
                }
                data[7] = CalculateCrc(frame.can_id, data, frame.len);
            }
        }

        private static void ApplyNdrChecksum(ref ZlgCanApi.can_frame frame)
        {
            if (!IsNdrChecksumCanId(frame.can_id) || frame.can_dlc != 8)
                return;

            fixed (byte* data = frame.data)
                data[7] = CalculateNdrChecksum(data);
        }

        private static void ApplyNdrChecksum(ref ZlgCanApi.canfd_frame frame)
        {
            if (!IsNdrChecksumCanId(frame.can_id) || frame.len != 8)
                return;

            fixed (byte* data = frame.data)
                data[7] = CalculateNdrChecksum(data);
        }

        private static void EnsureMinimumCanLength(ref ZlgCanApi.can_frame frame)
        {
            if (frame.can_dlc >= 8)
                return;

            fixed (byte* data = frame.data)
            {
                for (int i = frame.can_dlc; i < 8; i++)
                    data[i] = 0;
            }
            frame.can_dlc = 8;
        }

        private static void EnsureMinimumFdLength(ref ZlgCanApi.canfd_frame frame)
        {
            if (frame.len >= 8)
                return;

            fixed (byte* data = frame.data)
            {
                for (int i = frame.len; i < 8; i++)
                    data[i] = 0;
            }
            frame.len = 8;
        }

        private bool ForwardCan(IntPtr dest, ZlgCanApi.can_frame frame)
        {
            try
            {
                lock (_canLock)
                {
                    if (dest == IntPtr.Zero)
                        return false;
                    for (int attempt = 0; attempt < 3; attempt++)
                    {
                        _txCanBuf[0].frame = frame;
                        _txCanBuf[0].transmit_type = 0;
                        var tx = (ZlgCanApi.ZCAN_Transmit_Data*)_txCanPin.AddrOfPinnedObject().ToPointer();
                        if (ZlgCanApi.ZCAN_Transmit(dest, tx, 1) == 1)
                            return true;
                    }
                    return false;
                }
            }
            catch
            {
                return false;
            }
        }

        private bool ForwardFd(IntPtr dest, ZlgCanApi.canfd_frame frame)
        {
            try
            {
                lock (_canLock)
                {
                    if (dest == IntPtr.Zero)
                        return false;
                    for (int attempt = 0; attempt < 3; attempt++)
                    {
                        _txFdBuf[0].frame = frame;
                        _txFdBuf[0].transmit_type = 0;
                        var tx = (ZlgCanApi.ZCAN_TransmitFD_Data*)_txFdPin.AddrOfPinnedObject().ToPointer();
                        if (ZlgCanApi.ZCAN_TransmitFD(dest, tx, 1) == 1)
                            return true;
                    }
                    return false;
                }
            }
            catch
            {
                return false;
            }
        }

        private void InitBaudList()
        {
            FillBaud(cmbRxBaud, BaudRates, DefaultBaud);
            FillBaud(cmbTxBaud, BaudRates, DefaultBaud);
        }

        private void InitDataBaudList()
        {
            FillBaud(cmbRxDataBaud, DataBaudRates, "2000000");
            FillBaud(cmbTxDataBaud, DataBaudRates, "2000000");
        }

        private static void FillBaud(ComboBox combo, BaudOption[] options, string defaultValue)
        {
            combo.Items.Clear();
            combo.Items.AddRange(options);
            int defaultIndex = -1;
            int fallbackIndex = 0;
            for (int i = 0; i < options.Length; i++)
            {
                if (options[i].Value == defaultValue)
                    defaultIndex = i;
                if (options[i].Value == DefaultBaud)
                    fallbackIndex = i;
            }
            if (defaultIndex < 0)
                defaultIndex = fallbackIndex;
            combo.SelectedIndex = defaultIndex;
        }

        private void RefreshArbBaud(ComboBox cmbDevice, ComboBox cmbBaud)
        {
            bool fd = ZlgCanApi.IsCanFdDevice(GetSelectedDeviceType(cmbDevice));
            FillBaud(cmbBaud, fd ? FdBaudRates : BaudRates, GetSelectedBaud(cmbBaud));
        }

        private void InitDeviceList()
        {
            _updatingUi = true;
            FillDeviceCombo(cmbRxDevice);
            FillDeviceCombo(cmbTxDevice);
            FillProtocol(cmbRxProtocol);
            FillProtocol(cmbTxProtocol);
            cmbRxDevice.SelectedIndex = 0;
            cmbTxDevice.SelectedIndex = 0;
            cmbRxProtocol.SelectedIndex = 0;
            cmbTxProtocol.SelectedIndex = 0;
            _lastRxDevType = GetSelectedDeviceType(cmbRxDevice);
            _lastTxDevType = GetSelectedDeviceType(cmbTxDevice);
            ClampChannel(numRxChannel, _lastRxDevType);
            ClampChannel(numTxChannel, _lastTxDevType);
            _updatingUi = false;
        }

        private static void FillDeviceCombo(ComboBox combo)
        {
            combo.Items.Clear();
            combo.Items.Add(new DeviceTypeItem("USBCAN2 (4)", ZlgCanApi.ZCAN_USBCAN2));
            combo.Items.Add(new DeviceTypeItem("USBCANFD-100U (42)", ZlgCanApi.ZCAN_USBCANFD_100U));
            combo.Items.Add(new DeviceTypeItem("USBCANFD-200U (41)", ZlgCanApi.ZCAN_USBCANFD_200U));
        }

        private static void FillProtocol(ComboBox combo)
        {
            combo.Items.Clear();
            combo.Items.Add("CAN");
            combo.Items.Add("CAN FD");
        }

        private static string GetSelectedBaud(ComboBox combo)
        {
            return GetSelectedBaud(combo, DefaultBaud);
        }

        private static string GetSelectedBaud(ComboBox combo, string fallback)
        {
            var option = combo.SelectedItem as BaudOption;
            return option != null ? option.Value : fallback;
        }

        private static uint GetSelectedDeviceType(ComboBox combo)
        {
            var item = combo.SelectedItem as DeviceTypeItem;
            return item != null ? item.Type : DefaultDeviceType;
        }

        private void RxDeviceOrProtocolChanged(object sender, EventArgs e)
        {
            OnSideDeviceOrProtocolChanged(cmbRxDevice, cmbRxProtocol, numRxChannel, ref _lastRxDevType);
        }

        private void TxDeviceOrProtocolChanged(object sender, EventArgs e)
        {
            OnSideDeviceOrProtocolChanged(cmbTxDevice, cmbTxProtocol, numTxChannel, ref _lastTxDevType);
        }

        private void OnSideDeviceOrProtocolChanged(ComboBox cmbDevice, ComboBox cmbProtocol, NumericUpDown numCh, ref uint lastType)
        {
            if (_updatingUi || cmbDevice.Items.Count == 0 || cmbProtocol.Items.Count == 0)
                return;

            uint type = GetSelectedDeviceType(cmbDevice);
            bool fdDevice = ZlgCanApi.IsCanFdDevice(type);
            _updatingUi = true;
            if (!fdDevice)
                cmbProtocol.SelectedIndex = 0;
            else if (!ZlgCanApi.IsCanFdDevice(lastType))
                cmbProtocol.SelectedIndex = 1;
            lastType = type;
            ClampChannel(numCh, type);
            if (cmbDevice == cmbRxDevice)
                RefreshArbBaud(cmbRxDevice, cmbRxBaud);
            else
                RefreshArbBaud(cmbTxDevice, cmbTxBaud);
            _updatingUi = false;
            if (!_isOpen)
                ApplyProtocolUi();
            ScheduleSave();
        }

        private static decimal ChannelMax(uint type)
        {
            return type == ZlgCanApi.ZCAN_USBCANFD_100U ? 0 : 1;
        }

        private static void ClampChannel(NumericUpDown num, uint type)
        {
            num.Maximum = ChannelMax(type);
            if (num.Value > num.Maximum)
                num.Value = num.Maximum;
        }

        private void ApplyProtocolUi()
        {
            uint rxType = GetSelectedDeviceType(cmbRxDevice);
            uint txType = GetSelectedDeviceType(cmbTxDevice);
            bool rxFd = ZlgCanApi.IsCanFdDevice(rxType);
            bool txFd = ZlgCanApi.IsCanFdDevice(txType);
            bool rxProtoFd = _isOpen ? _rxProtocolFd : (rxFd && cmbRxProtocol.SelectedIndex == 1);
            bool txProtoFd = _isOpen ? _txProtocolFd : (txFd && cmbTxProtocol.SelectedIndex == 1);
            if (!_isOpen)
            {
                cmbRxProtocol.Enabled = rxFd;
                cmbTxProtocol.Enabled = txFd;
            }
            else
            {
                cmbRxProtocol.Enabled = false;
                cmbTxProtocol.Enabled = false;
            }
            cmbRxDataBaud.Enabled = !_isOpen && rxProtoFd;
            cmbTxDataBaud.Enabled = !_isOpen && txProtoFd;
            chkRxTerm.Enabled = !_isOpen && rxFd;
            chkTxTerm.Enabled = !_isOpen && txFd;
            cmbRxIso.Enabled = !_isOpen && rxProtoFd;
            cmbTxIso.Enabled = !_isOpen && txProtoFd;
            lblColBaud.Text = (rxProtoFd || txProtoFd) ? "Arb" : "Bitrate";
        }

        private static void HexKeyPress(object sender, KeyPressEventArgs e)
        {
            if (char.IsControl(e.KeyChar)) return;
            if (Uri.IsHexDigit(e.KeyChar)) return;
            e.Handled = true;
        }

        private uint RewriteMaxId(TextBox box)
        {
            int index = box.Tag is int ? (int)box.Tag : -1;
            bool extended = index >= 0 && _rewriteFrame != null && index < _rewriteFrame.Length &&
                _rewriteFrame[index] != null && _rewriteFrame[index].SelectedIndex == 1;
            box.MaxLength = extended ? 8 : 3;
            return extended ? ZlgCanApi.CAN_EFF_MASK : ZlgCanApi.CAN_SFF_MASK;
        }

        private void ClampRewriteId(TextBox box)
        {
            if (_formattingRewrite) return;

            uint maxId = RewriteMaxId(box);
            string raw = box.Text ?? "";
            var cleaned = new StringBuilder(raw.Length);
            foreach (char c in raw)
            {
                if (Uri.IsHexDigit(c))
                    cleaned.Append(c);
            }

            uint value = 0;
            if (cleaned.Length > 0)
            {
                if (!uint.TryParse(cleaned.ToString(), NumberStyles.HexNumber, null, out value))
                    value = maxId;
            }

            if (value > maxId)
                value = maxId;

            string formatted = cleaned.Length == 0 ? "" : value.ToString("X");
            if (box.Text == formatted)
                return;

            int caret = box.SelectionStart;
            _formattingRewrite = true;
            box.Text = formatted;
            box.SelectionStart = Math.Min(caret, box.Text.Length);
            _formattingRewrite = false;
        }

        private void FormatRewriteData(TextBox box)
        {
            if (_formattingRewrite) return;

            string raw = box.Text ?? "";
            int caret = box.SelectionStart;
            int hexBeforeCaret = 0;
            for (int i = 0; i < caret && i < raw.Length; i++)
            {
                if (Uri.IsHexDigit(raw[i]))
                    hexBeforeCaret++;
            }

            var hex = new StringBuilder();
            foreach (char c in raw)
            {
                if (!Uri.IsHexDigit(c))
                    continue;
                if (hex.Length >= 16)
                    break;
                hex.Append(char.ToUpperInvariant(c));
            }

            if (hex.Length == 16 && TryGetRewriteCrcId(box, out uint canId))
            {
                byte[] data = ParseDataBytes(hex.ToString());
                byte checksum;
                fixed (byte* bytes = data)
                    checksum = IsNdrChecksumCanId(canId)
                        ? CalculateNdrChecksum(bytes)
                        : CalculateCrc(canId, bytes, data.Length);
                string checksumText = checksum.ToString("X2");
                hex[14] = checksumText[0];
                hex[15] = checksumText[1];
            }

            var formatted = new StringBuilder();
            for (int i = 0; i < hex.Length; i++)
            {
                if (i > 0 && (i % 2) == 0)
                    formatted.Append(' ');
                formatted.Append(hex[i]);
            }

            string result = formatted.ToString();
            if (box.Text == result)
                return;

            int newCaret = result.Length;
            int seen = 0;
            for (int i = 0; i < result.Length; i++)
            {
                if (Uri.IsHexDigit(result[i]))
                {
                    seen++;
                    if (seen >= hexBeforeCaret)
                    {
                        newCaret = i + 1;
                        break;
                    }
                }
            }

            _formattingRewrite = true;
            box.Text = result;
            box.SelectionStart = Math.Min(newCaret, box.Text.Length);
            _formattingRewrite = false;
        }

        private bool TryGetRewriteCrcId(TextBox box, out uint canId)
        {
            canId = 0;
            int index = Array.IndexOf(_rewriteData, box);
            if (index < 0 || _rewriteId[index] == null || _rewriteFrame[index] == null ||
                _rewriteFrame[index].SelectedIndex != 0 || _rewriteMode[index] == null ||
                _rewriteMode[index].Text != "Set")
                return false;

            return TryParseCanId(_rewriteId[index].Text, ZlgCanApi.CAN_SFF_MASK, out canId) &&
                (IsCrcCanId(canId) || IsNdrChecksumCanId(canId));
        }

        private static bool TryParseCanId(string s, uint maxId, out uint id)
        {
            id = 0;
            if (string.IsNullOrWhiteSpace(s))
                return true;

            s = s.Trim();
            if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                s = s.Substring(2);

            if (!uint.TryParse(s, NumberStyles.HexNumber, null, out id))
                return false;
            if (id > maxId)
                return false;
            return true;
        }

        private static byte[] ParseDataBytes(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return new byte[0];
            var hex = new StringBuilder();
            foreach (char c in s)
            {
                if (Uri.IsHexDigit(c))
                    hex.Append(c);
            }
            if (hex.Length % 2 == 1)
                hex.Length--;
            var list = new List<byte>();
            for (int i = 0; i + 1 < hex.Length; i += 2)
                list.Add(Convert.ToByte(hex.ToString(i, 2), 16));
            return list.ToArray();
        }

        private static string BytesToHex(byte[] data, int len)
        {
            if (data == null || len <= 0) return "";
            var sb = new StringBuilder();
            for (int i = 0; i < len && i < data.Length; i++)
            {
                if (i > 0) sb.Append(' ');
                sb.Append(data[i].ToString("X2"));
            }
            return sb.ToString();
        }

        private sealed class RewriteSlot
        {
            public bool Enabled;
            public bool UseUserData;
            public bool HasId;
            public bool Extended;
            public uint Id;
            public int Index;
            public byte[] Data;
        }

        private sealed class LatestSignal
        {
            public DateTime Time;
            public int Slot;
            public string From;
            public string To;
            public string Id;
            public byte[] ReceivedBytes;
            public byte ReceivedLength;
            public byte[] ForwardedBytes;
            public byte ForwardedLength;
            public bool Sent;

            public LatestSignal Clone()
            {
                var clone = new LatestSignal
                {
                    Time = Time,
                    Slot = Slot,
                    From = From,
                    To = To,
                    Id = Id,
                    ReceivedLength = ReceivedLength,
                    ForwardedLength = ForwardedLength,
                    Sent = Sent,
                    ReceivedBytes = new byte[ReceivedBytes.Length],
                    ForwardedBytes = new byte[ForwardedBytes.Length]
                };
                Array.Copy(ReceivedBytes, clone.ReceivedBytes, ReceivedBytes.Length);
                Array.Copy(ForwardedBytes, clone.ForwardedBytes, ForwardedBytes.Length);
                return clone;
            }

            public void Clear()
            {
                Time = DateTime.MinValue;
                From = "";
                To = "";
                Id = "";
                ReceivedLength = 0;
                ForwardedLength = 0;
                Sent = false;
                Array.Clear(ReceivedBytes, 0, ReceivedBytes.Length);
                Array.Clear(ForwardedBytes, 0, ForwardedBytes.Length);
            }
        }

        private enum LatestSignalMenuAction
        {
            CopyReceived,
            UseReceived
        }

        private sealed class BaudOption
        {
            public string Text { get; }
            public string Value { get; }

            public BaudOption(string text, string value)
            {
                Text = text;
                Value = value;
            }

            public override string ToString()
            {
                return Text;
            }
        }

        private sealed class DeviceTypeItem
        {
            public string Text { get; }
            public uint Type { get; }

            public DeviceTypeItem(string text, uint type)
            {
                Text = text;
                Type = type;
            }

            public override string ToString()
            {
                return Text;
            }
        }

        private void SettingsChanged(object sender, EventArgs e)
        {
            ScheduleSave();
        }

        private void SaveTimer_Tick(object sender, EventArgs e)
        {
            _saveTimer.Stop();
            SaveSettings();
        }

        private void ScheduleSave()
        {
            if (_loadingSettings || _saveTimer == null)
                return;
            _saveTimer.Stop();
            _saveTimer.Start();
        }

        private void LoadSettings()
        {
            SavedSettings saved = AppSettings.Load();
            if (saved == null)
                return;

            _loadingSettings = true;
            _updatingUi = true;
            try
            {
                uint rxType = saved.RxDeviceType != 0 ? saved.RxDeviceType : saved.DeviceType;
                uint txType = saved.TxDeviceType != 0 ? saved.TxDeviceType : saved.DeviceType;
                SelectDeviceType(cmbRxDevice, rxType);
                SelectDeviceType(cmbTxDevice, txType);
                _lastRxDevType = GetSelectedDeviceType(cmbRxDevice);
                _lastTxDevType = GetSelectedDeviceType(cmbTxDevice);
                ClampChannel(numRxChannel, _lastRxDevType);
                ClampChannel(numTxChannel, _lastTxDevType);

                SetNumeric(numRxIndex, saved.RxDeviceType != 0 ? saved.RxDeviceIndex : saved.DeviceIndex);
                SetNumeric(numTxIndex, saved.TxDeviceType != 0 ? saved.TxDeviceIndex : saved.DeviceIndex);
                SetNumeric(numRxChannel, saved.RxDeviceType != 0 ? saved.RxChannel : 0);
                SetNumeric(numTxChannel, saved.TxDeviceType != 0 ? saved.TxChannel : 1);

                cmbRxProtocol.SelectedIndex = ZlgCanApi.IsCanFdDevice(_lastRxDevType) &&
                    (saved.RxDeviceType != 0 ? saved.RxProtocolFd : saved.ProtocolFd) ? 1 : 0;
                cmbTxProtocol.SelectedIndex = ZlgCanApi.IsCanFdDevice(_lastTxDevType) &&
                    (saved.TxDeviceType != 0 ? saved.TxProtocolFd : saved.ProtocolFd) ? 1 : 0;

                SelectBaud(cmbRxBaud, FirstNonEmpty(saved.RxBaud, saved.Baud), DefaultBaud);
                SelectBaud(cmbTxBaud, FirstNonEmpty(saved.TxBaud, saved.Baud), DefaultBaud);
                SelectBaud(cmbRxDataBaud, FirstNonEmpty(saved.RxDataBaud, saved.DataBaud), "2000000");
                SelectBaud(cmbTxDataBaud, FirstNonEmpty(saved.TxDataBaud, saved.DataBaud), "2000000");

                chkRxTerm.Checked = saved.RxTerm;
                chkTxTerm.Checked = saved.TxTerm;
                cmbRxIso.SelectedIndex = saved.RxNonIso ? 1 : 0;
                cmbTxIso.SelectedIndex = saved.TxNonIso ? 1 : 0;

                if (saved.Slots != null)
                {
                    int n = Math.Min(RewriteSlotCount, saved.Slots.Length);
                    for (int i = 0; i < n; i++)
                    {
                        SavedSlot slot = saved.Slots[i];
                        if (slot == null)
                            continue;
                        if (_rewriteFrame[i] != null)
                            _rewriteFrame[i].SelectedIndex = slot.Extended ? 1 : 0;
                        if (_rewriteId[i] != null)
                            _rewriteId[i].Text = slot.Id ?? "";
                        if (_rewriteData[i] != null)
                            _rewriteData[i].Text = string.IsNullOrWhiteSpace(slot.Data)
                                ? "00 00 00 00 00 00 00 00"
                                : slot.Data;
                        if (_rewriteMode[i] != null)
                            _rewriteMode[i].Text = slot.ModeConfigured && slot.Set ? "Set" : "Unset";
                        if (_rewriteEnable[i] != null)
                            _rewriteEnable[i].Checked = slot.Enabled;
                    }
                }
            }
            finally
            {
                _updatingUi = false;
                _loadingSettings = false;
            }

            RefreshArbBaud(cmbRxDevice, cmbRxBaud);
            RefreshArbBaud(cmbTxDevice, cmbTxBaud);
            ApplyProtocolUi();
            RebuildRewriteSlots();
        }

        private static void SelectDeviceType(ComboBox combo, uint type)
        {
            for (int i = 0; i < combo.Items.Count; i++)
            {
                var item = combo.Items[i] as DeviceTypeItem;
                if (item != null && item.Type == type)
                {
                    combo.SelectedIndex = i;
                    return;
                }
            }
        }

        private static void SetNumeric(NumericUpDown num, int value)
        {
            decimal v = value;
            if (v < num.Minimum)
                v = num.Minimum;
            if (v > num.Maximum)
                v = num.Maximum;
            num.Value = v;
        }

        private static string FirstNonEmpty(string a, string b)
        {
            return string.IsNullOrEmpty(a) ? b : a;
        }

        private static void SelectBaud(ComboBox combo, string value, string fallback)
        {
            string want = string.IsNullOrEmpty(value) ? fallback : value;
            for (int i = 0; i < combo.Items.Count; i++)
            {
                var option = combo.Items[i] as BaudOption;
                if (option != null && option.Value == want)
                {
                    combo.SelectedIndex = i;
                    return;
                }
            }
        }

        private void SaveSettings()
        {
            if (_loadingSettings)
                return;

            var saved = new SavedSettings
            {
                DeviceType = GetSelectedDeviceType(cmbRxDevice),
                DeviceIndex = (int)numRxIndex.Value,
                ProtocolFd = cmbRxProtocol.SelectedIndex == 1,
                Baud = GetSelectedBaud(cmbRxBaud),
                DataBaud = GetSelectedBaud(cmbRxDataBaud, "2000000"),
                RxDeviceType = GetSelectedDeviceType(cmbRxDevice),
                TxDeviceType = GetSelectedDeviceType(cmbTxDevice),
                RxDeviceIndex = (int)numRxIndex.Value,
                TxDeviceIndex = (int)numTxIndex.Value,
                RxChannel = (int)numRxChannel.Value,
                TxChannel = (int)numTxChannel.Value,
                RxProtocolFd = cmbRxProtocol.SelectedIndex == 1,
                TxProtocolFd = cmbTxProtocol.SelectedIndex == 1,
                RxBaud = GetSelectedBaud(cmbRxBaud),
                TxBaud = GetSelectedBaud(cmbTxBaud),
                RxDataBaud = GetSelectedBaud(cmbRxDataBaud, "2000000"),
                TxDataBaud = GetSelectedBaud(cmbTxDataBaud, "2000000"),
                RxTerm = chkRxTerm.Checked,
                TxTerm = chkTxTerm.Checked,
                RxNonIso = cmbRxIso.SelectedIndex == 1,
                TxNonIso = cmbTxIso.SelectedIndex == 1,
                Slots = new SavedSlot[RewriteSlotCount]
            };

            for (int i = 0; i < RewriteSlotCount; i++)
            {
                saved.Slots[i] = new SavedSlot
                {
                    Enabled = _rewriteEnable != null && _rewriteEnable[i] != null && _rewriteEnable[i].Checked,
                    Set = _rewriteMode != null && _rewriteMode[i] != null && _rewriteMode[i].Text == "Set",
                    ModeConfigured = true,
                    Extended = _rewriteFrame != null && _rewriteFrame[i] != null && _rewriteFrame[i].SelectedIndex == 1,
                    Id = _rewriteId != null && _rewriteId[i] != null ? _rewriteId[i].Text : "",
                    Data = _rewriteData != null && _rewriteData[i] != null ? _rewriteData[i].Text : ""
                };
            }

            AppSettings.Save(saved);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (_saveTimer != null)
                _saveTimer.Stop();
            SaveSettings();
            if (!CloseDevice())
            {
                e.Cancel = true;
                return;
            }
            if (_rxThread == null || !_rxThread.IsAlive)
                FreePins();
            base.OnFormClosing(e);
        }
    }
}
