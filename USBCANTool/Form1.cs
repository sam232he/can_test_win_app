using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace USBCANTool
{
    public partial class Form1 : Form
    {
        private IntPtr _rxDeviceHandle = IntPtr.Zero;
        private IntPtr _txDeviceHandle = IntPtr.Zero;
        private IntPtr _rxChannelHandle = IntPtr.Zero;
        private IntPtr _txChannelHandle = IntPtr.Zero;
        private uint _rxChannelIndex;
        private uint _txChannelIndex;
        private bool _rxIsCanFdDevice;
        private bool _txIsCanFdDevice;
        private bool _rxProtocolFd;
        private bool _txProtocolFd;
        private uint _lastRxDevType;
        private uint _lastTxDevType;
        private System.Windows.Forms.Timer _uiTimer;
        private bool _isOpen;
        private Color _ledColor = Color.Gray;
        private readonly object _canLock = new object();
        private readonly object _rewriteLock = new object();
        private readonly object _pendingLock = new object();
        private readonly Queue<CanPacket> _pending = new Queue<CanPacket>();
        private Thread _rxThread;
        private volatile bool _rxRunning;
        private volatile bool _bridgeTxFailed;
        private DateTime _lastViewRebuild;
        private ZlgCanApi.ZCAN_Receive_Data[] _rxCanBuf;
        private ZlgCanApi.ZCAN_ReceiveFD_Data[] _rxFdBuf;
        private const int RxBatch = 200;
        private const int MaxPending = 2000;
        private const int MaxDrainPerTick = 400;

        private const int RewriteSlotCount = 10;
        private CheckBox[] _rewriteEnable;
        private ComboBox[] _rewriteFrame;
        private TextBox[] _rewriteId;
        private TextBox[] _rewriteData;
        private RewriteSlot[] _rewriteSlots = new RewriteSlot[RewriteSlotCount];
        private bool _formattingRewrite;
        private bool _updatingUi;
        private bool _loadingSettings;
        private System.Windows.Forms.Timer _saveTimer;

        private readonly List<CanPacket> _packets = new List<CanPacket>();
        private readonly List<CanPacket> _view = new List<CanPacket>();
        private readonly Dictionary<string, CanPacket> _groups = new Dictionary<string, CanPacket>();
        private readonly List<CanPacket> _groupView = new List<CanPacket>();
        private HashSet<uint> _filterIds;
        private bool _filterIdInvalid;

        private const int MaxPackets = 10000;
        private const int PacketTrimChunk = 2000;
        private bool _groupsNeedSort;
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
            InitDeviceList();
            InitBaudList();
            InitDataBaudList();
            InitPacketView();
            InitRewriteSlots();
            ParseFilterIds();
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
            _uiTimer = new System.Windows.Forms.Timer { Interval = 50 };
            _uiTimer.Tick += UiTimer_Tick;
            UpdateUiState(false);

            if (!ZlgCanApi.DllFileExists)
                SetFailed("zlgcan.dll missing. Put it next to USBCANTool.exe.");
            else if (!ZlgCanApi.EnsureLoaded())
                SetFailed("Failed to load zlgcan.dll. " + (ZlgCanApi.LastLoadError ?? ZlgCanApi.Diagnose().Trim()));
            else
                SetIdle("Idle");
        }

        private void InitPacketView()
        {
            lvPackets.Columns.Clear();
            lvPackets.Columns.Add("Time", 100);
            lvPackets.Columns.Add("Dir", 40);
            lvPackets.Columns.Add("Ch", 35);
            lvPackets.Columns.Add("ID", 90);
            lvPackets.Columns.Add("Type", 80);
            lvPackets.Columns.Add("DLC", 40);
            lvPackets.Columns.Add("Data", 220);
            lvPackets.Columns.Add("Count", 55);
            lvPackets.Columns.Add("HW ts", 80);
        }

        private void InitRewriteSlots()
        {
            _rewriteEnable = new CheckBox[RewriteSlotCount];
            _rewriteFrame = new ComboBox[RewriteSlotCount];
            _rewriteId = new TextBox[RewriteSlotCount];
            _rewriteData = new TextBox[RewriteSlotCount];

            var font = new Font("Consolas", 9F);
            grpRewrite.Controls.Add(MakeHeader(12, 16, "En"));
            grpRewrite.Controls.Add(MakeHeader(40, 16, "ID"));
            grpRewrite.Controls.Add(MakeHeader(112, 16, "Fmt"));
            grpRewrite.Controls.Add(MakeHeader(170, 16, "Data (8 bytes)"));
            grpRewrite.Controls.Add(MakeHeader(392, 16, "En"));
            grpRewrite.Controls.Add(MakeHeader(420, 16, "ID"));
            grpRewrite.Controls.Add(MakeHeader(492, 16, "Fmt"));
            grpRewrite.Controls.Add(MakeHeader(550, 16, "Data (8 bytes)"));

            for (int i = 0; i < RewriteSlotCount; i++)
            {
                int col = i / 5;
                int row = i % 5;
                int x = 12 + col * 380;
                int y = 34 + row * 26;

                var chk = new CheckBox
                {
                    Location = new Point(x, y + 2),
                    Size = new Size(22, 20),
                    TabIndex = 20 + i * 4
                };
                chk.CheckedChanged += RewriteSlotChanged;
                _rewriteEnable[i] = chk;
                grpRewrite.Controls.Add(chk);

                var txtId = new TextBox
                {
                    Font = font,
                    Location = new Point(x + 28, y),
                    MaxLength = 3,
                    Size = new Size(70, 22),
                    TabIndex = 21 + i * 4,
                    Tag = i
                };
                txtId.KeyPress += HexKeyPress;
                txtId.TextChanged += RewriteIdChanged;
                _rewriteId[i] = txtId;
                grpRewrite.Controls.Add(txtId);

                var cmbFrame = new ComboBox
                {
                    DropDownStyle = ComboBoxStyle.DropDownList,
                    Location = new Point(x + 102, y),
                    Size = new Size(58, 20),
                    TabIndex = 22 + i * 4,
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
                    Location = new Point(x + 164, y),
                    Size = new Size(200, 22),
                    TabIndex = 23 + i * 4,
                    Text = "00 00 00 00 00 00 00 00"
                };
                txtData.KeyPress += HexKeyPress;
                txtData.TextChanged += RewriteDataChanged;
                _rewriteData[i] = txtData;
                grpRewrite.Controls.Add(txtData);
            }

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
            RebuildRewriteSlots();
        }

        private void RewriteIdChanged(object sender, EventArgs e)
        {
            var box = sender as TextBox;
            if (box == null)
                return;
            ClampRewriteId(box);
            RebuildRewriteSlots();
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

        private bool TryGetRewriteData(uint id, bool extended, out byte[] data)
        {
            data = null;
            lock (_rewriteLock)
            {
                RewriteSlot[] slots = _rewriteSlots;
                if (slots == null)
                    return false;
                for (int i = 0; i < slots.Length; i++)
                {
                    RewriteSlot slot = slots[i];
                    if (slot == null || !slot.Enabled || !slot.HasId)
                        continue;
                    if (slot.Id != id || slot.Extended != extended)
                        continue;
                    data = new byte[8];
                    Array.Copy(slot.Data, data, 8);
                    return true;
                }
            }
            return false;
        }

        private void TryRewriteCan(ref ZlgCanApi.can_frame frame)
        {
            if (ZlgCanApi.IS_RTR(frame.can_id) || ZlgCanApi.IS_ERR(frame.can_id))
                return;
            byte[] data;
            if (!TryGetRewriteData(ZlgCanApi.GET_ID(frame.can_id), ZlgCanApi.IS_EFF(frame.can_id), out data))
                return;
            if (frame.data == null || frame.data.Length < 8)
                frame.data = new byte[8];
            Array.Copy(data, frame.data, 8);
            frame.can_dlc = 8;
        }

        private void TryRewriteFd(ref ZlgCanApi.canfd_frame frame)
        {
            if (ZlgCanApi.IS_RTR(frame.can_id) || ZlgCanApi.IS_ERR(frame.can_id))
                return;
            byte[] data;
            if (!TryGetRewriteData(ZlgCanApi.GET_ID(frame.can_id), ZlgCanApi.IS_EFF(frame.can_id), out data))
                return;
            if (frame.data == null || frame.data.Length < ZlgCanApi.CANFD_MAX_DLEN)
                frame.data = new byte[ZlgCanApi.CANFD_MAX_DLEN];
            Array.Clear(frame.data, 0, frame.data.Length);
            Array.Copy(data, frame.data, 8);
            frame.len = 8;
        }

        private void UpdateUiState(bool open)
        {
            _isOpen = open;
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
                    SetFailed("RX and TX cannot be the same channel on the same device.");
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
                _rxIsCanFdDevice = rxFd;
                _txIsCanFdDevice = txFd;
                _rxProtocolFd = rxProtoFd;
                _txProtocolFd = txProtoFd;
                _bridgeTxFailed = false;
                lock (_pendingLock)
                    _pending.Clear();
                UpdateUiState(true);
                StartRxThread();
                _uiTimer.Start();
                started = true;
                string extra = baudOk ? "" : " Baud set failed; device may ignore bitrate.";
                string rxProto = rxProtoFd ? "CAN FD" : "CAN";
                string txProto = txProtoFd ? "CAN FD" : "CAN";
                SetOperating("Open RX→TX. RX " + rxProto + " " + rxBaud + " CH" + rxCh +
                             " / TX " + txProto + " " + txBaud + " CH" + txCh + "." + extra);
            }
            catch (DllNotFoundException ex)
            {
                SetFailed("DLL not found: " + ex.Message);
            }
            catch (BadImageFormatException)
            {
                SetFailed("Architecture mismatch. This process is " + ZlgCanApi.ProcessArch +
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
                    StopRxThread();
                    CloseNative(false);
                    if (_isOpen)
                        UpdateUiState(false);
                }
            }
        }

        private void btnClose_Click(object sender, EventArgs e)
        {
            CloseDevice();
        }

        private void CloseDevice()
        {
            if (_uiTimer != null)
                _uiTimer.Stop();
            _rxRunning = false;
            JoinRxThread(250);
            lock (_pendingLock)
                _pending.Clear();
            CloseNative(true);
            JoinRxThread(1000);
            _rxThread = null;
            UpdateUiState(false);
            SetIdle("Closed");
        }

        private void CloseNative(bool updateFields)
        {
            lock (_canLock)
            {
                ResetChannel(ref _rxChannelHandle);
                ResetChannel(ref _txChannelHandle);
                if (_txDeviceHandle != IntPtr.Zero && _txDeviceHandle != _rxDeviceHandle)
                {
                    try { ZlgCanApi.ZCAN_CloseDevice(_txDeviceHandle); } catch { }
                }
                _txDeviceHandle = IntPtr.Zero;
                if (_rxDeviceHandle != IntPtr.Zero)
                {
                    try { ZlgCanApi.ZCAN_CloseDevice(_rxDeviceHandle); } catch { }
                    _rxDeviceHandle = IntPtr.Zero;
                }
            }
            if (updateFields)
            {
                _rxIsCanFdDevice = false;
                _txIsCanFdDevice = false;
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
                ZlgCanApi.ZCAN_SetValue(device, n + "/clock", "60000000");
                bool ok = ZlgCanApi.ZCAN_SetValue(device, n + "/protocol", protocolFd ? "1" : "0") == ZlgCanApi.STATUS_OK;
                if (protocolFd)
                    ZlgCanApi.ZCAN_SetValue(device, n + "/canfd_standard", nonIso ? "1" : "0");
                ok = ZlgCanApi.ZCAN_SetValue(device, n + "/canfd_abit_baud_rate", baud) == ZlgCanApi.STATUS_OK && ok;
                if (protocolFd)
                    ok = ZlgCanApi.ZCAN_SetValue(device, n + "/canfd_dbit_baud_rate", dataBaud) == ZlgCanApi.STATUS_OK && ok;
                else
                    ZlgCanApi.ZCAN_SetValue(device, n + "/canfd_dbit_baud_rate", baud);
                return ok;
            }
            return ZlgCanApi.ZCAN_SetValue(device, n + "/baud_rate", baud) == ZlgCanApi.STATUS_OK;
        }

        private static void ApplyChannelPostInit(IntPtr device, uint index, bool fdDevice, bool terminatorOn)
        {
            if (!fdDevice)
                return;
            ZlgCanApi.ZCAN_SetValue(device, index + "/initenal_resistance", terminatorOn ? "1" : "0");
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

        private static CanPacket MakeCanPacket(string dir, uint channel, ZlgCanApi.can_frame frame, ulong ts, bool isFd, bool brs)
        {
            return new CanPacket
            {
                Time = DateTime.Now,
                Dir = dir,
                Channel = channel,
                Id = ZlgCanApi.GET_ID(frame.can_id),
                Extended = ZlgCanApi.IS_EFF(frame.can_id),
                Rtr = ZlgCanApi.IS_RTR(frame.can_id),
                IsFd = isFd,
                Brs = brs,
                Dlc = frame.can_dlc > 8 ? (byte)8 : frame.can_dlc,
                Data = BytesToHex(frame.data, frame.can_dlc > 8 ? 8 : frame.can_dlc),
                HwTimestamp = ts,
                Count = 1
            };
        }

        private static CanPacket MakeFdPacket(string dir, uint channel, ZlgCanApi.canfd_frame frame, ulong ts)
        {
            return new CanPacket
            {
                Time = DateTime.Now,
                Dir = dir,
                Channel = channel,
                Id = ZlgCanApi.GET_ID(frame.can_id),
                Extended = ZlgCanApi.IS_EFF(frame.can_id),
                Rtr = false,
                IsFd = true,
                Brs = (frame.flags & ZlgCanApi.CANFD_BRS) != 0,
                Dlc = frame.len > 64 ? (byte)64 : frame.len,
                Data = BytesToHex(frame.data, frame.len > 64 ? 64 : frame.len),
                HwTimestamp = ts,
                Count = 1
            };
        }

        private void InitRxBuffers()
        {
            _rxCanBuf = new ZlgCanApi.ZCAN_Receive_Data[RxBatch];
            for (int i = 0; i < RxBatch; i++)
                _rxCanBuf[i].frame.data = new byte[8];
            _rxFdBuf = new ZlgCanApi.ZCAN_ReceiveFD_Data[RxBatch];
            for (int i = 0; i < RxBatch; i++)
                _rxFdBuf[i].frame.data = new byte[ZlgCanApi.CANFD_MAX_DLEN];
        }

        private void StartRxThread()
        {
            StopRxThread();
            _rxRunning = true;
            _rxThread = new Thread(RxLoop)
            {
                IsBackground = true,
                Name = "CAN bridge",
                Priority = ThreadPriority.AboveNormal
            };
            _rxThread.Start();
        }

        private void StopRxThread()
        {
            _rxRunning = false;
            JoinRxThread(1000);
            _rxThread = null;
        }

        private void JoinRxThread(int milliseconds)
        {
            Thread thread = _rxThread;
            if (thread != null && thread.IsAlive && thread != Thread.CurrentThread)
                thread.Join(milliseconds);
        }

        private void RxLoop()
        {
            while (_rxRunning)
            {
                try
                {
                    IntPtr rx;
                    IntPtr tx;
                    lock (_canLock)
                    {
                        rx = _rxChannelHandle;
                        tx = _txChannelHandle;
                    }
                    if (rx == IntPtr.Zero || tx == IntPtr.Zero)
                    {
                        Thread.Sleep(5);
                        continue;
                    }

                    bool any = PollAndBridge(rx, tx);
                    if (!any)
                        Thread.Sleep(1);
                }
                catch
                {
                    Thread.Sleep(5);
                }
            }
        }

        private void UiTimer_Tick(object sender, EventArgs e)
        {
            if (!_isOpen)
                return;

            int drained = 0;
            lock (_pendingLock)
            {
                while (_pending.Count > 0 && drained < MaxDrainPerTick)
                {
                    AddPacket(_pending.Dequeue());
                    drained++;
                }
            }

            if (drained > 0)
            {
                RebuildView();
                if (_ledColor != Color.LimeGreen && !_bridgeTxFailed)
                    SetOperating("Open RX→TX.");
            }

            if (_bridgeTxFailed)
            {
                _bridgeTxFailed = false;
                SetFailed("Bridge transmit failed. Bus still open.");
            }
        }

        private void EnqueuePacket(CanPacket packet)
        {
            lock (_pendingLock)
            {
                while (_pending.Count >= MaxPending)
                    _pending.Dequeue();
                _pending.Enqueue(packet);
            }
        }

        private bool PollAndBridge(IntPtr src, IntPtr dest)
        {
            bool any = false;
            uint count;
            lock (_canLock)
            {
                if (src == IntPtr.Zero) return false;
                count = ZlgCanApi.ZCAN_Receive(src, _rxCanBuf, (uint)RxBatch, 0);
            }

            for (uint i = 0; i < count; i++)
            {
                var f = _rxCanBuf[i].frame;
                if (ZlgCanApi.IS_ERR(f.can_id))
                    continue;
                EnqueuePacket(MakeCanPacket("RX", _rxChannelIndex, f, _rxCanBuf[i].timestamp, false, false));
                any = true;
                TryRewriteCan(ref f);
                if (!ForwardCan(dest, f))
                    _bridgeTxFailed = true;
                else
                    EnqueuePacket(MakeCanPacket("TX", _txChannelIndex, f, 0, false, false));
            }

            if (!_rxIsCanFdDevice)
                return any;

            uint fdCount;
            lock (_canLock)
            {
                if (src == IntPtr.Zero) return any;
                fdCount = ZlgCanApi.ZCAN_ReceiveFD(src, _rxFdBuf, (uint)RxBatch, 0);
            }

            bool txFdFrames = _txIsCanFdDevice && _txProtocolFd;
            for (uint i = 0; i < fdCount; i++)
            {
                var f = _rxFdBuf[i].frame;
                if (ZlgCanApi.IS_ERR(f.can_id))
                    continue;
                if (f.len > ZlgCanApi.CANFD_MAX_DLEN)
                    f.len = (byte)ZlgCanApi.CANFD_MAX_DLEN;
                EnqueuePacket(MakeFdPacket("RX", _rxChannelIndex, f, _rxFdBuf[i].timestamp));
                any = true;
                TryRewriteFd(ref f);
                if (txFdFrames)
                {
                    if (!ForwardFd(dest, f))
                        _bridgeTxFailed = true;
                    else
                        EnqueuePacket(MakeFdPacket("TX", _txChannelIndex, f, 0));
                }
                else
                {
                    var classic = FdToClassic(f);
                    if (!ForwardCan(dest, classic))
                        _bridgeTxFailed = true;
                    else
                        EnqueuePacket(MakeCanPacket("TX", _txChannelIndex, classic, 0, false, false));
                }
            }

            return any;
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
                data = new byte[8]
            };
            if (fd.data != null && n > 0)
                Array.Copy(fd.data, frame.data, Math.Min(n, fd.data.Length));
            return frame;
        }

        private bool ForwardCan(IntPtr dest, ZlgCanApi.can_frame frame)
        {
            var tx = new ZlgCanApi.ZCAN_Transmit_Data { frame = frame, transmit_type = 0 };
            if (tx.frame.data == null)
            {
                tx.frame.data = new byte[8];
            }
            else
            {
                var copy = new byte[8];
                Array.Copy(tx.frame.data, copy, Math.Min(8, tx.frame.data.Length));
                tx.frame.data = copy;
            }
            try
            {
                lock (_canLock)
                {
                    if (dest == IntPtr.Zero)
                        return false;
                    return ZlgCanApi.ZCAN_Transmit(dest, ref tx, 1) == 1;
                }
            }
            catch
            {
                return false;
            }
        }

        private bool ForwardFd(IntPtr dest, ZlgCanApi.canfd_frame frame)
        {
            var tx = new ZlgCanApi.ZCAN_TransmitFD_Data { frame = frame, transmit_type = 0 };
            var copy = new byte[ZlgCanApi.CANFD_MAX_DLEN];
            if (tx.frame.data != null)
                Array.Copy(tx.frame.data, copy, Math.Min(ZlgCanApi.CANFD_MAX_DLEN, tx.frame.data.Length));
            tx.frame.data = copy;
            try
            {
                lock (_canLock)
                {
                    if (dest == IntPtr.Zero)
                        return false;
                    return ZlgCanApi.ZCAN_TransmitFD(dest, ref tx, 1) == 1;
                }
            }
            catch
            {
                return false;
            }
        }

        private void AddPacket(CanPacket packet)
        {
            _packets.Add(packet);
            UpdateGroup(packet);
            if (_packets.Count > MaxPackets)
                TrimPackets();
        }

        private void UpdateGroup(CanPacket packet)
        {
            string key = packet.GroupKey;
            CanPacket grouped;
            if (_groups.TryGetValue(key, out grouped))
            {
                grouped.Time = packet.Time;
                grouped.Dlc = packet.Dlc;
                grouped.Data = packet.Data;
                grouped.HwTimestamp = packet.HwTimestamp;
                grouped.Count++;
                return;
            }

            _groups[key] = packet.Clone();
            _groupsNeedSort = true;
        }

        private void TrimPackets()
        {
            int keep = MaxPackets - PacketTrimChunk;
            if (keep < 1)
                keep = MaxPackets;
            int drop = _packets.Count - keep;
            if (drop <= 0)
                return;
            _packets.RemoveRange(0, drop);
            RebuildGroupsFromPackets();
        }

        private void RebuildGroupsFromPackets()
        {
            _groups.Clear();
            for (int i = 0; i < _packets.Count; i++)
            {
                CanPacket packet = _packets[i];
                string key = packet.GroupKey;
                CanPacket grouped;
                if (_groups.TryGetValue(key, out grouped))
                {
                    grouped.Time = packet.Time;
                    grouped.Dlc = packet.Dlc;
                    grouped.Data = packet.Data;
                    grouped.HwTimestamp = packet.HwTimestamp;
                    grouped.Count++;
                }
                else
                {
                    var clone = packet.Clone();
                    clone.Count = 1;
                    _groups[key] = clone;
                }
            }
            _groupsNeedSort = true;
        }

        private void FilterChanged(object sender, EventArgs e)
        {
            ParseFilterIds();
            _groupsNeedSort = true;
            RebuildView();
            ScheduleSave();
        }

        private void ParseFilterIds()
        {
            _filterIdInvalid = false;
            _filterIds = null;
            string raw = txtFilterId.Text;
            if (string.IsNullOrWhiteSpace(raw))
                return;

            var set = new HashSet<uint>();
            string[] parts = raw.Split(new[] { ',', ' ', ';', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string part in parts)
            {
                string s = part.Trim();
                if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                    s = s.Substring(2);
                uint id;
                if (!uint.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out id))
                {
                    _filterIdInvalid = true;
                    _filterIds = new HashSet<uint>();
                    return;
                }
                set.Add(id);
            }
            _filterIds = set;
        }

        private bool PassesFilter(CanPacket p)
        {
            if (_filterIdInvalid)
                return false;
            if (_filterIds != null && !_filterIds.Contains(p.Id))
                return false;
            if (cmbFilterDir.SelectedIndex == 1 && p.Dir != "RX")
                return false;
            if (cmbFilterDir.SelectedIndex == 2 && p.Dir != "TX")
                return false;
            return true;
        }

        private bool HasActiveFilter()
        {
            return _filterIdInvalid || _filterIds != null || cmbFilterDir.SelectedIndex != 0;
        }

        private void RebuildView()
        {
            _view.Clear();
            _groupView.Clear();

            if (chkGroup.Checked)
            {
                if (_groupsNeedSort || _groupView.Count == 0 || HasActiveFilter())
                {
                    _groupView.Clear();
                    foreach (var kv in _groups)
                    {
                        if (PassesFilter(kv.Value))
                            _groupView.Add(kv.Value);
                    }
                    _groupView.Sort((a, b) => a.Id.CompareTo(b.Id));
                    _groupsNeedSort = false;
                }
                lvPackets.VirtualListSize = _groupView.Count;
            }
            else if (HasActiveFilter())
            {
                for (int i = 0; i < _packets.Count; i++)
                {
                    if (PassesFilter(_packets[i]))
                        _view.Add(_packets[i]);
                }
                lvPackets.VirtualListSize = _view.Count;
            }
            else
            {
                lvPackets.VirtualListSize = _packets.Count;
            }

            lvPackets.Invalidate();
            DateTime now = DateTime.Now;
            if (lvPackets.VirtualListSize > 0 && !chkGroup.Checked &&
                (now - _lastViewRebuild).TotalMilliseconds >= 80)
            {
                _lastViewRebuild = now;
                lvPackets.EnsureVisible(lvPackets.VirtualListSize - 1);
            }
        }

        private void lvPackets_RetrieveVirtualItem(object sender, RetrieveVirtualItemEventArgs e)
        {
            CanPacket p;
            if (chkGroup.Checked)
            {
                if (e.ItemIndex < 0 || e.ItemIndex >= _groupView.Count)
                {
                    e.Item = new ListViewItem("");
                    return;
                }
                p = _groupView[e.ItemIndex];
            }
            else if (HasActiveFilter())
            {
                if (e.ItemIndex < 0 || e.ItemIndex >= _view.Count)
                {
                    e.Item = new ListViewItem("");
                    return;
                }
                p = _view[e.ItemIndex];
            }
            else
            {
                if (e.ItemIndex < 0 || e.ItemIndex >= _packets.Count)
                {
                    e.Item = new ListViewItem("");
                    return;
                }
                p = _packets[e.ItemIndex];
            }

            var item = new ListViewItem(p.Time.ToString("HH:mm:ss.fff"));
            item.SubItems.Add(p.Dir);
            item.SubItems.Add(p.Channel.ToString());
            item.SubItems.Add("0x" + p.Id.ToString("X"));
            string type;
            if (p.IsFd)
                type = (p.Extended ? "FD EXT" : "FD") + (p.Brs ? " BRS" : "");
            else
                type = (p.Extended ? "EXT" : "STD") + (p.Rtr ? " RTR" : "");
            item.SubItems.Add(type);
            item.SubItems.Add(p.Dlc.ToString());
            item.SubItems.Add(p.Data);
            item.SubItems.Add(p.Count.ToString());
            item.SubItems.Add(p.HwTimestamp == 0 ? "" : p.HwTimestamp.ToString());
            e.Item = item;
        }

        private void btnClear_Click(object sender, EventArgs e)
        {
            _packets.Clear();
            _groups.Clear();
            _view.Clear();
            _groupView.Clear();
            _groupsNeedSort = true;
            lvPackets.VirtualListSize = 0;
            lvPackets.Refresh();
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
            public bool HasId;
            public bool Extended;
            public uint Id;
            public byte[] Data;
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

        private sealed class CanPacket
        {
            public DateTime Time;
            public string Dir;
            public uint Channel;
            public uint Id;
            public bool Extended;
            public bool Rtr;
            public bool IsFd;
            public bool Brs;
            public byte Dlc;
            public string Data;
            public ulong HwTimestamp;
            public int Count;

            public string GroupKey
            {
                get { return Dir + "|" + Channel + "|" + Id + "|" + Extended + "|" + Rtr + "|" + IsFd + "|" + Brs; }
            }

            public CanPacket Clone()
            {
                return new CanPacket
                {
                    Time = Time,
                    Dir = Dir,
                    Channel = Channel,
                    Id = Id,
                    Extended = Extended,
                    Rtr = Rtr,
                    IsFd = IsFd,
                    Brs = Brs,
                    Dlc = Dlc,
                    Data = Data,
                    HwTimestamp = HwTimestamp,
                    Count = Count
                };
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

                txtFilterId.Text = saved.FilterId ?? "";
                if (saved.FilterDir >= 0 && saved.FilterDir < cmbFilterDir.Items.Count)
                    cmbFilterDir.SelectedIndex = saved.FilterDir;
                chkGroup.Checked = saved.GroupShow;

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
            ParseFilterIds();
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
                FilterId = txtFilterId.Text,
                FilterDir = cmbFilterDir.SelectedIndex,
                GroupShow = chkGroup.Checked,
                Slots = new SavedSlot[RewriteSlotCount]
            };

            for (int i = 0; i < RewriteSlotCount; i++)
            {
                saved.Slots[i] = new SavedSlot
                {
                    Enabled = _rewriteEnable != null && _rewriteEnable[i] != null && _rewriteEnable[i].Checked,
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
            CloseDevice();
            base.OnFormClosing(e);
        }
    }
}
