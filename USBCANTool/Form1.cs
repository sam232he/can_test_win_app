using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace USBCANTool
{
    public partial class Form1 : Form
    {
        private IntPtr _deviceHandle = IntPtr.Zero;
        private IntPtr _channelHandle = IntPtr.Zero;
        private System.Windows.Forms.Timer _rxTimer;
        private bool _isOpen;
        private Thread _txThread;
        private volatile bool _periodicRunning;
        private volatile int _periodMs = 100;
        private DateTime _lastTxViewRebuild;
        private Color _ledColor = Color.Gray;
        private readonly object _canLock = new object();
        private readonly object _txFrameLock = new object();
        private ZlgCanApi.can_frame _txFrame;
        private bool _txFrameValid;

        private readonly List<CanPacket> _packets = new List<CanPacket>();
        private readonly List<CanPacket> _view = new List<CanPacket>();
        private readonly Dictionary<string, CanPacket> _groups = new Dictionary<string, CanPacket>();
        private readonly List<CanPacket> _groupView = new List<CanPacket>();
        private HashSet<uint> _filterIds;
        private bool _filterIdInvalid;

        private const int MaxPackets = 10000;
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

        public Form1()
        {
            InitializeComponent();
            InitBaudList();
            InitPacketView();
            ParseFilterIds();
            _rxTimer = new System.Windows.Forms.Timer { Interval = 50 };
            _rxTimer.Tick += RxTimer_Tick;
            UpdateUiState(false);
            ApplyCanIdFormat();

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
            lvPackets.Columns.Add("ID", 90);
            lvPackets.Columns.Add("Type", 70);
            lvPackets.Columns.Add("DLC", 40);
            lvPackets.Columns.Add("Data", 220);
            lvPackets.Columns.Add("Count", 55);
            lvPackets.Columns.Add("HW ts", 80);
        }

        private void UpdateUiState(bool open)
        {
            _isOpen = open;
            btnOpen.Enabled = !open;
            btnClose.Enabled = open;
            btnSend.Enabled = open;
            cmbDeviceType.Enabled = !open;
            numDeviceIndex.Enabled = !open;
            numChannel.Enabled = !open;
            cmbBaud.Enabled = !open;
            if (!open)
                StopPeriodic();
            UpdateSendButton();
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
            try
            {
                if (!ZlgCanApi.EnsureLoaded())
                {
                    SetFailed("Cannot load zlgcan.dll. " + (ZlgCanApi.LastLoadError ?? "").Trim() +
                              " Build platform must match DLL (x64 app with x64 zlgcan.dll).");
                    return;
                }

                uint devType = GetSelectedDeviceType();
                uint devIndex = (uint)Math.Min(numDeviceIndex.Value, numDeviceIndex.Maximum);
                uint channel = (uint)Math.Min(numChannel.Value, numChannel.Maximum);

                _deviceHandle = ZlgCanApi.ZCAN_OpenDevice(devType, devIndex, 0);
                if (_deviceHandle == IntPtr.Zero || (int)_deviceHandle == ZlgCanApi.INVALID_DEVICE_HANDLE)
                {
                    SetFailed("Open failed: ZCAN_OpenDevice. Plug in USB, install driver, close other CAN software.");
                    return;
                }

                string baud = GetSelectedBaud();
                string path = channel + "/baud_rate";
                bool baudOk = ZlgCanApi.ZCAN_SetValue(_deviceHandle, path, baud) == ZlgCanApi.STATUS_OK;

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

                _channelHandle = ZlgCanApi.ZCAN_InitCAN(_deviceHandle, channel, ref cfg);
                if (_channelHandle == IntPtr.Zero || (int)_channelHandle == ZlgCanApi.INVALID_CHANNEL_HANDLE)
                {
                    ZlgCanApi.ZCAN_CloseDevice(_deviceHandle);
                    _deviceHandle = IntPtr.Zero;
                    SetFailed("Open failed: ZCAN_InitCAN.");
                    return;
                }

                if (ZlgCanApi.ZCAN_StartCAN(_channelHandle) != ZlgCanApi.STATUS_OK)
                {
                    ZlgCanApi.ZCAN_CloseDevice(_deviceHandle);
                    _deviceHandle = IntPtr.Zero;
                    _channelHandle = IntPtr.Zero;
                    SetFailed("Open failed: ZCAN_StartCAN.");
                    return;
                }

                UpdateUiState(true);
                _rxTimer.Start();
                string extra = baudOk ? "" : " Baud set failed; device may ignore bitrate.";
                SetOperating("Open and operating. " + baud + " bps, channel " + channel + "." + extra);
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
        }

        private void btnClose_Click(object sender, EventArgs e)
        {
            CloseDevice();
        }

        private void CloseDevice()
        {
            StopPeriodic();
            if (_rxTimer != null)
                _rxTimer.Stop();
            lock (_canLock)
            {
                if (_channelHandle != IntPtr.Zero)
                {
                    try { ZlgCanApi.ZCAN_ResetCAN(_channelHandle); } catch { }
                    _channelHandle = IntPtr.Zero;
                }
                if (_deviceHandle != IntPtr.Zero)
                {
                    try { ZlgCanApi.ZCAN_CloseDevice(_deviceHandle); } catch { }
                    _deviceHandle = IntPtr.Zero;
                }
            }
            UpdateUiState(false);
            SetIdle("Closed");
        }

        private void btnSend_Click(object sender, EventArgs e)
        {
            if (!_isOpen || _channelHandle == IntPtr.Zero) return;

            if (_periodicRunning)
            {
                StopPeriodic();
                SetOperating("Periodic transmit stopped.");
                return;
            }

            if (chkPeriodic.Checked)
            {
                if (!RefreshTxTemplate())
                    return;
                _periodMs = Math.Max(1, (int)numPeriod.Value);
                _periodicRunning = true;
                _txThread = new Thread(PeriodicTxLoop)
                {
                    IsBackground = true,
                    Name = "CAN periodic TX",
                    Priority = ThreadPriority.AboveNormal
                };
                _txThread.Start();
                UpdateSendButton();
                SetOperating("Periodic transmit every " + _periodMs + " ms.");
                return;
            }

            TransmitOnce(true);
        }

        private void chkPeriodic_CheckedChanged(object sender, EventArgs e)
        {
            if (!chkPeriodic.Checked)
                StopPeriodic();
            UpdateSendButton();
        }

        private void numPeriod_ValueChanged(object sender, EventArgs e)
        {
            _periodMs = Math.Max(1, (int)numPeriod.Value);
        }

        private void StopPeriodic()
        {
            _periodicRunning = false;
            Thread thread = _txThread;
            if (thread != null && thread.IsAlive && thread != Thread.CurrentThread)
                thread.Join(1000);
            _txThread = null;
            UpdateSendButton();
        }

        private void PeriodicTxLoop()
        {
            timeBeginPeriod(1);
            try
            {
                var clock = Stopwatch.StartNew();
                long nextTicks = 0;
                while (_periodicRunning)
                {
                    WaitUntil(clock, nextTicks);
                    if (!_periodicRunning)
                        break;

                    if (!TransmitSnapshot())
                    {
                        try
                        {
                            BeginInvoke(new Action(() =>
                            {
                                StopPeriodic();
                                SetFailed("Periodic transmit stopped: send failed.");
                            }));
                        }
                        catch { }
                        break;
                    }

                    long periodTicks = Math.Max(1, _periodMs) * Stopwatch.Frequency / 1000L;
                    nextTicks += periodTicks;
                    long now = clock.ElapsedTicks;
                    if (now > nextTicks)
                    {
                        long missed = ((now - nextTicks) / periodTicks) + 1;
                        nextTicks += missed * periodTicks;
                    }
                }
            }
            finally
            {
                timeEndPeriod(1);
            }
        }

        private static void WaitUntil(Stopwatch clock, long targetTicks)
        {
            while (true)
            {
                long remaining = targetTicks - clock.ElapsedTicks;
                if (remaining <= 0)
                    return;

                double remainMs = 1000.0 * remaining / Stopwatch.Frequency;
                if (remainMs > 2.0)
                    Thread.Sleep(1);
                else
                    Thread.SpinWait(80);
            }
        }

        private void UpdateSendButton()
        {
            if (btnSend == null)
                return;
            if (_periodicRunning)
                btnSend.Text = "Stop";
            else if (chkPeriodic.Checked)
                btnSend.Text = "Start";
            else
                btnSend.Text = "Send";
        }

        private bool TransmitOnce(bool refreshView)
        {
            if (!RefreshTxTemplate())
                return false;
            if (!TransmitSnapshot())
                return false;
            RebuildView();
            if (!_periodicRunning)
                SetOperating("Open and operating.");
            return true;
        }

        private bool RefreshTxTemplate()
        {
            string error;
            ZlgCanApi.can_frame frame;
            if (!TryBuildTxFrame(out frame, out error))
            {
                SetFailed(error);
                return false;
            }
            lock (_txFrameLock)
            {
                _txFrame = frame;
                _txFrameValid = true;
            }
            return true;
        }

        private bool TryBuildTxFrame(out ZlgCanApi.can_frame frame, out string error)
        {
            frame = new ZlgCanApi.can_frame { data = new byte[8] };
            error = null;
            try
            {
                bool extended = chkExtended.Checked;
                uint maxId = extended ? ZlgCanApi.CAN_EFF_MASK : ZlgCanApi.CAN_SFF_MASK;
                uint id;
                if (!TryParseCanId(txtId.Text, maxId, out id))
                {
                    error = extended
                        ? "Send failed: extended ID must be hex 0-1FFFFFFF."
                        : "Send failed: standard ID must be hex 0-7FF.";
                    return false;
                }

                byte[] data = ParseDataBytes(txtData.Text);
                if (data.Length > 8)
                {
                    error = "Send failed: data longer than 8 bytes.";
                    return false;
                }

                byte dlc = (byte)(numDlc.Value > 0 ? (int)numDlc.Value : data.Length);
                if (dlc > 8) dlc = 8;

                frame.can_id = ZlgCanApi.MAKE_CAN_ID(id, extended, rtr: chkRtr.Checked, err: false);
                frame.can_dlc = dlc;
                Array.Copy(data, frame.data, Math.Min(data.Length, 8));
                return true;
            }
            catch (Exception ex)
            {
                error = "Send error: " + ex.Message;
                return false;
            }
        }

        private bool TransmitSnapshot()
        {
            ZlgCanApi.can_frame frame;
            lock (_txFrameLock)
            {
                if (!_txFrameValid)
                    return false;
                frame = _txFrame;
                frame.data = new byte[8];
                if (_txFrame.data != null)
                    Array.Copy(_txFrame.data, frame.data, Math.Min(8, _txFrame.data.Length));
            }

            var tx = new ZlgCanApi.ZCAN_Transmit_Data
            {
                frame = frame,
                transmit_type = 0
            };

            uint sent;
            try
            {
                lock (_canLock)
                {
                    if (_channelHandle == IntPtr.Zero)
                        return false;
                    sent = ZlgCanApi.ZCAN_Transmit(_channelHandle, ref tx, 1);
                }
            }
            catch
            {
                return false;
            }

            if (sent != 1)
            {
                if (InvokeRequired)
                    return false;
                SetFailed("Transmit failed (returned " + sent + ").");
                return false;
            }

            var packet = new CanPacket
            {
                Time = DateTime.Now,
                Dir = "TX",
                Id = ZlgCanApi.GET_ID(frame.can_id),
                Extended = ZlgCanApi.IS_EFF(frame.can_id),
                Rtr = ZlgCanApi.IS_RTR(frame.can_id),
                Dlc = frame.can_dlc,
                Data = BytesToHex(frame.data, frame.can_dlc),
                HwTimestamp = 0,
                Count = 1
            };

            if (InvokeRequired)
            {
                try
                {
                    BeginInvoke(new Action(() => LogTxPacket(packet)));
                }
                catch { }
            }
            else
            {
                LogTxPacket(packet);
            }
            return true;
        }

        private void LogTxPacket(CanPacket packet)
        {
            AddPacket(packet);
            if ((DateTime.Now - _lastTxViewRebuild).TotalMilliseconds >= 80)
            {
                _lastTxViewRebuild = DateTime.Now;
                RebuildView();
            }
        }

        private void RxTimer_Tick(object sender, EventArgs e)
        {
            if (!_isOpen || _channelHandle == IntPtr.Zero) return;

            if (_periodicRunning)
            {
                ZlgCanApi.can_frame liveFrame;
                string unused;
                if (TryBuildTxFrame(out liveFrame, out unused))
                {
                    lock (_txFrameLock)
                    {
                        _txFrame = liveFrame;
                        _txFrameValid = true;
                    }
                }
            }

            try
            {
                const int max = 100;
                var buf = new ZlgCanApi.ZCAN_Receive_Data[max];
                for (int i = 0; i < max; i++)
                    buf[i].frame.data = new byte[8];

                uint count;
                lock (_canLock)
                {
                    if (_channelHandle == IntPtr.Zero) return;
                    count = ZlgCanApi.ZCAN_Receive(_channelHandle, buf, (uint)max, 0);
                }
                if (count == 0) return;

                for (uint i = 0; i < count; i++)
                {
                    var f = buf[i].frame;
                    AddPacket(new CanPacket
                    {
                        Time = DateTime.Now,
                        Dir = "RX",
                        Id = ZlgCanApi.GET_ID(f.can_id),
                        Extended = ZlgCanApi.IS_EFF(f.can_id),
                        Rtr = ZlgCanApi.IS_RTR(f.can_id),
                        Dlc = f.can_dlc,
                        Data = BytesToHex(f.data, f.can_dlc),
                        HwTimestamp = buf[i].timestamp,
                        Count = 1
                    });
                }
                RebuildView();
                if (_isOpen && _ledColor != Color.LimeGreen)
                    SetOperating("Open and operating.");
            }
            catch (Exception ex)
            {
                SetFailed("Receive failed: " + ex.Message);
            }
        }

        private void AddPacket(CanPacket packet)
        {
            _packets.Add(packet);
            if (_packets.Count > MaxPackets)
                _packets.RemoveRange(0, _packets.Count - MaxPackets);

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
                _groups[key] = packet.Clone();
            }

        }

        private void FilterChanged(object sender, EventArgs e)
        {
            ParseFilterIds();
            RebuildView();
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

        private void RebuildView()
        {
            _view.Clear();
            _groupView.Clear();

            if (chkGroup.Checked)
            {
                foreach (var kv in _groups)
                {
                    if (PassesFilter(kv.Value))
                        _groupView.Add(kv.Value);
                }
                _groupView.Sort((a, b) => a.Id.CompareTo(b.Id));
                lvPackets.VirtualListSize = _groupView.Count;
            }
            else
            {
                for (int i = 0; i < _packets.Count; i++)
                {
                    if (PassesFilter(_packets[i]))
                        _view.Add(_packets[i]);
                }
                lvPackets.VirtualListSize = _view.Count;
            }

            lvPackets.Refresh();
            if (lvPackets.VirtualListSize > 0 && !chkGroup.Checked)
            {
                int last = lvPackets.VirtualListSize - 1;
                lvPackets.EnsureVisible(last);
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
            else
            {
                if (e.ItemIndex < 0 || e.ItemIndex >= _view.Count)
                {
                    e.Item = new ListViewItem("");
                    return;
                }
                p = _view[e.ItemIndex];
            }

            var item = new ListViewItem(p.Time.ToString("HH:mm:ss.fff"));
            item.SubItems.Add(p.Dir);
            item.SubItems.Add("0x" + p.Id.ToString("X"));
            item.SubItems.Add((p.Extended ? "EXT" : "STD") + (p.Rtr ? " RTR" : ""));
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
            lvPackets.VirtualListSize = 0;
            lvPackets.Refresh();
        }

        private void InitBaudList()
        {
            cmbBaud.Items.Clear();
            cmbBaud.Items.AddRange(BaudRates);
            int defaultIndex = 2;
            for (int i = 0; i < BaudRates.Length; i++)
            {
                if (BaudRates[i].Value == DefaultBaud)
                {
                    defaultIndex = i;
                    break;
                }
            }
            cmbBaud.SelectedIndex = defaultIndex;
        }

        private string GetSelectedBaud()
        {
            var option = cmbBaud.SelectedItem as BaudOption;
            return option != null ? option.Value : DefaultBaud;
        }

        private uint GetSelectedDeviceType()
        {
            switch (cmbDeviceType.SelectedIndex)
            {
                case 0: return ZlgCanApi.ZCAN_USBCAN1;
                case 1: return ZlgCanApi.ZCAN_USBCAN2;
                case 2: return ZlgCanApi.ZCAN_USBCAN_E_U;
                case 3: return ZlgCanApi.ZCAN_USBCAN_2E_U;
                case 4: return ZlgCanApi.ZCAN_USBCAN_4E_U;
                case 5: return ZlgCanApi.ZCAN_USBCAN_8E_U;
                default: return DefaultDeviceType;
            }
        }

        private bool _clampingId;
        private bool _formattingData;

        private void txtData_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (char.IsControl(e.KeyChar)) return;
            if (Uri.IsHexDigit(e.KeyChar)) return;
            e.Handled = true;
        }

        private void txtData_TextChanged(object sender, EventArgs e)
        {
            FormatDataText();
        }

        private void FormatDataText()
        {
            if (_formattingData) return;

            string raw = txtData.Text ?? "";
            int caret = txtData.SelectionStart;
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
            if (txtData.Text == result)
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

            _formattingData = true;
            txtData.Text = result;
            txtData.SelectionStart = Math.Min(newCaret, txtData.Text.Length);
            _formattingData = false;
        }

        private uint MaxCanId =>
            chkExtended.Checked ? ZlgCanApi.CAN_EFF_MASK : ZlgCanApi.CAN_SFF_MASK;

        private void chkExtended_CheckedChanged(object sender, EventArgs e)
        {
            ApplyCanIdFormat();
        }

        private void txtId_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (char.IsControl(e.KeyChar)) return;
            if (Uri.IsHexDigit(e.KeyChar)) return;
            e.Handled = true;
        }

        private void txtId_TextChanged(object sender, EventArgs e)
        {
            ClampCanIdText();
        }

        private void ApplyCanIdFormat()
        {
            bool extended = chkExtended.Checked;
            txtId.MaxLength = extended ? 8 : 3;
            lblId.Text = extended ? "ID (29-bit)" : "ID (11-bit)";
            ClampCanIdText();
        }

        private void ClampCanIdText()
        {
            if (_clampingId) return;

            string raw = txtId.Text ?? "";
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
                    value = MaxCanId;
            }

            if (value > MaxCanId)
                value = MaxCanId;

            string formatted = cleaned.Length == 0 ? "" : value.ToString("X");
            if (txtId.Text == formatted)
                return;

            int caret = txtId.SelectionStart;
            _clampingId = true;
            txtId.Text = formatted;
            txtId.SelectionStart = Math.Min(caret, txtId.Text.Length);
            _clampingId = false;
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

        [DllImport("winmm.dll")]
        private static extern uint timeBeginPeriod(uint uMilliseconds);

        [DllImport("winmm.dll")]
        private static extern uint timeEndPeriod(uint uMilliseconds);

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

        private sealed class CanPacket
        {
            public DateTime Time;
            public string Dir;
            public uint Id;
            public bool Extended;
            public bool Rtr;
            public byte Dlc;
            public string Data;
            public ulong HwTimestamp;
            public int Count;

            public string GroupKey
            {
                get { return Dir + "|" + Id + "|" + Extended + "|" + Rtr; }
            }

            public CanPacket Clone()
            {
                return new CanPacket
                {
                    Time = Time,
                    Dir = Dir,
                    Id = Id,
                    Extended = Extended,
                    Rtr = Rtr,
                    Dlc = Dlc,
                    Data = Data,
                    HwTimestamp = HwTimestamp,
                    Count = Count
                };
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            CloseDevice();
            base.OnFormClosing(e);
        }
    }
}
