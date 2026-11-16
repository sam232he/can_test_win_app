using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace USBCANTool
{
    internal sealed class DbcSignalBuilderForm : Form
    {
        private readonly ComboBox _messages;
        private readonly Label _messageInfo;
        private readonly DataGridView _signals;
        private readonly TextBox _payload;
        private readonly Label _status;
        private readonly Button _copy;
        private readonly Action<byte[]> _insertData;
        private readonly string _notice;
        private readonly byte[] _initialData;
        private TextBox _activeEditor;

        public DbcSignalBuilderForm(IEnumerable<DbcMessage> messages, Action<byte[]> insertData,
            string statusMessage, byte[] initialData)
        {
            _insertData = insertData;
            _notice = statusMessage ?? "";
            _initialData = new byte[8];
            if (initialData != null)
                Array.Copy(initialData, _initialData, Math.Min(initialData.Length, _initialData.Length));
            Text = "DBC Message Signals";
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new System.Drawing.Size(560, 400);
            ClientSize = new System.Drawing.Size(700, 500);

            var header = new TableLayoutPanel
            {
                ColumnCount = 1,
                Dock = DockStyle.Top,
                Height = 70,
                Padding = new Padding(12, 8, 12, 2),
                RowCount = 2
            };
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            header.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            header.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            _messages = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 2, 0, 2)
            };
            _messages.SelectedIndexChanged += MessageChanged;
            header.Controls.Add(_messages, 0, 0);
            _messageInfo = new Label
            {
                Anchor = AnchorStyles.Left | AnchorStyles.Right,
                AutoEllipsis = true,
                Dock = DockStyle.Fill,
                Text = "Select a message.",
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft
            };
            header.Controls.Add(_messageInfo, 0, 1);

            _signals = new DataGridView
            {
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AutoGenerateColumns = false,
                BackgroundColor = System.Drawing.SystemColors.Window,
                Dock = DockStyle.Fill,
                MultiSelect = false,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.CellSelect
            };
            _signals.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Signal",
                HeaderText = "Signal",
                ReadOnly = true,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                MinimumWidth = 180
            });
            _signals.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Value",
                HeaderText = "Value",
                Width = 180
            });
            _signals.CellValueChanged += SignalValueChanged;
            _signals.EditingControlShowing += SignalsEditingControlShowing;

            var footer = new TableLayoutPanel
            {
                ColumnCount = 3,
                Dock = DockStyle.Bottom,
                Height = 66,
                Padding = new Padding(12, 8, 12, 4),
                RowCount = 2
            };
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 104));
            footer.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            footer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            footer.Controls.Add(new Label
            {
                Anchor = AnchorStyles.Left,
                AutoSize = true,
                Text = "8-byte data"
            }, 0, 0);
            _payload = new TextBox
            {
                Font = new System.Drawing.Font("Consolas", 10F),
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 2, 8, 2),
                ReadOnly = true
            };
            footer.Controls.Add(_payload, 1, 0);
            _copy = new Button
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 1, 0, 1),
                Text = "Insert",
                Enabled = false
            };
            _copy.Click += ActionClicked;
            footer.Controls.Add(_copy, 2, 0);
            _status = new Label
            {
                Dock = DockStyle.Fill,
                AutoEllipsis = true,
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft
            };
            footer.Controls.Add(_status, 0, 1);
            footer.SetColumnSpan(_status, 3);

            Controls.Add(_signals);
            Controls.Add(footer);
            Controls.Add(header);

            if (messages != null)
                SetMessages(messages, statusMessage);
        }

        private void SetMessages(IEnumerable<DbcMessage> messages, string statusMessage)
        {
            _messages.BeginUpdate();
            _messages.Items.Clear();
            foreach (DbcMessage message in messages)
                _messages.Items.Add(message);
            _messages.EndUpdate();
            _status.Text = statusMessage ?? "";
            if (_messages.Items.Count > 0)
                _messages.SelectedIndex = 0;
        }

        private void MessageChanged(object sender, EventArgs e)
        {
            DbcMessage message = _messages.SelectedItem as DbcMessage;
            _signals.Rows.Clear();
            _payload.Clear();
            _copy.Enabled = false;
            if (message == null)
            {
                _messageInfo.Text = "";
                return;
            }

            string frameType = message.IsExtended ? "EXT" : "STD";
            string id = message.IsExtended ? message.CanId.ToString("X8") : message.CanId.ToString("X3");
            _messageInfo.Text = message.Name + "    ID: 0x" + id + " (" + frameType + ")";
            decimal[] initialValues = null;
            if (message.Length <= 8 &&
                !message.Signals.Any(signal => !string.IsNullOrEmpty(signal.Multiplexer)))
            {
                try
                {
                    initialValues = DbcPayloadEncoder.Decode(message, _initialData);
                }
                catch (FormatException ex)
                {
                    _status.Text = "Could not decode the existing payload: " + ex.Message;
                }
                catch (OverflowException)
                {
                    _status.Text = "Could not decode the existing payload: value out of range.";
                }
            }

            for (int i = 0; i < message.Signals.Count; i++)
            {
                DbcSignal signal = message.Signals[i];
                decimal initialValue = signal.Offset;
                if (signal.Minimum.HasValue && initialValue < signal.Minimum.Value)
                    initialValue = signal.Minimum.Value;
                if (signal.Maximum.HasValue && initialValue > signal.Maximum.Value)
                    initialValue = signal.Maximum.Value;
                if (initialValues != null)
                    initialValue = initialValues[i];

                int rowIndex = _signals.Rows.Add(signal.Name, initialValue.ToString(CultureInfo.InvariantCulture));
                _signals.Rows[rowIndex].Tag = signal;
            }

            if (message.Length > 8)
            {
                _status.Text = "Message exceeds 8 bytes.";
            }
            else if (message.Signals.Any(signal => !string.IsNullOrEmpty(signal.Multiplexer)))
            {
                _status.Text = "Multiplexed messages are not supported.";
            }
            else
            {
                _status.Text = "";
                UpdateGeneratedPayload();
            }
        }

        private void SignalsEditingControlShowing(object sender, DataGridViewEditingControlShowingEventArgs e)
        {
            if (_activeEditor != null)
                _activeEditor.TextChanged -= EditingValueChanged;
            _activeEditor = e.Control as TextBox;
            if (_activeEditor != null && _signals.CurrentCell != null &&
                _signals.CurrentCell.OwningColumn.Name == "Value")
                _activeEditor.TextChanged += EditingValueChanged;
        }

        private void SignalValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0)
                UpdateGeneratedPayload();
        }

        private void EditingValueChanged(object sender, EventArgs e)
        {
            UpdateGeneratedPayload();
        }

        private void UpdateGeneratedPayload()
        {
            DbcMessage message = _messages.SelectedItem as DbcMessage;
            if (message == null || message.Length > 8 ||
                message.Signals.Any(signal => !string.IsNullOrEmpty(signal.Multiplexer)))
            {
                _payload.Clear();
                _copy.Enabled = false;
                return;
            }

            try
            {
                byte[] data = DbcPayloadEncoder.Encode(message, _signals, _initialData);
                _payload.Text = string.Join(" ", data.Select(value => value.ToString("X2", CultureInfo.InvariantCulture)));
                _copy.Enabled = true;
                _status.Text = _notice;
            }
            catch (FormatException ex)
            {
                _payload.Clear();
                _copy.Enabled = false;
                _status.Text = ex.Message;
            }
            catch (OverflowException)
            {
                _payload.Clear();
                _copy.Enabled = false;
                _status.Text = "Value out of range.";
            }
        }

        private void ActionClicked(object sender, EventArgs e)
        {
            if (_signals.IsCurrentCellInEditMode)
                _signals.EndEdit();
            UpdateGeneratedPayload();
            if (!_copy.Enabled)
                return;

            DbcMessage message = _messages.SelectedItem as DbcMessage;
            if (message == null)
                return;
            try
            {
                _insertData(DbcPayloadEncoder.Encode(message, _signals, _initialData));
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (FormatException ex)
            {
                _status.Text = ex.Message;
            }
            catch (OverflowException)
            {
                _status.Text = "Value out of range.";
            }
        }
    }

    internal sealed class DbcMessage
    {
        public uint CanId { get; set; }
        public bool IsExtended { get; set; }
        public string Name { get; set; }
        public string SourceFile { get; set; }
        public int Length { get; set; }
        public List<DbcSignal> Signals { get; } = new List<DbcSignal>();

        public override string ToString()
        {
            return string.IsNullOrEmpty(SourceFile)
                ? Name
                : Name + " [" + Path.GetFileName(SourceFile) + "]";
        }
    }

    internal sealed class DbcSignal
    {
        public string Name { get; set; }
        public string Unit { get; set; }
        public string Multiplexer { get; set; }
        public int StartBit { get; set; }
        public int BitLength { get; set; }
        public bool IsLittleEndian { get; set; }
        public bool IsSigned { get; set; }
        public decimal Factor { get; set; }
        public decimal Offset { get; set; }
        public decimal? Minimum { get; set; }
        public decimal? Maximum { get; set; }
    }

    internal static class DbcFileParser
    {
        private const string NumberPattern = @"[-+]?(?:[0-9]+(?:\.[0-9]*)?|\.[0-9]+)(?:[eE][-+]?[0-9]+)?";
        private static readonly Regex MessagePattern = new Regex(
            @"^\s*BO_\s+(?<id>\d+)\s+(?<name>\S+)\s*:\s*(?<length>\d+)\s+\S+",
            RegexOptions.Compiled);
        private static readonly Regex SignalPattern = new Regex(
            @"^\s*SG_\s+(?<name>[A-Za-z0-9_]+)(?:\s+(?<mux>M|m\d+))?\s*:\s*" +
            @"(?<start>\d+)\|(?<length>\d+)@(?<byteOrder>[01])(?<sign>[+-])\s*" +
            @"\(\s*(?<factor>" + NumberPattern + @")\s*,\s*(?<offset>" + NumberPattern + @")\s*\)\s*" +
            @"\[\s*(?<min>" + NumberPattern + @")\s*\|\s*(?<max>" + NumberPattern + @")\s*\]\s*" +
            @"""(?<unit>[^""]*)""",
            RegexOptions.Compiled);

        public static List<DbcMessage> Parse(string path)
        {
            string contents = File.ReadAllText(path, Encoding.UTF8);
            var messages = new List<DbcMessage>();
            DbcMessage currentMessage = null;
            string[] lines = contents.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

            for (int lineNumber = 0; lineNumber < lines.Length; lineNumber++)
            {
                string line = lines[lineNumber];
                Match messageMatch = MessagePattern.Match(line);
                if (messageMatch.Success)
                {
                    uint dbcId;
                    int length;
                    if (!uint.TryParse(messageMatch.Groups["id"].Value, NumberStyles.None,
                            CultureInfo.InvariantCulture, out dbcId) ||
                        !int.TryParse(messageMatch.Groups["length"].Value, NumberStyles.None,
                            CultureInfo.InvariantCulture, out length) ||
                        length < 0 || length > 64)
                    {
                        throw new FormatException("Invalid message ID or length on line " + (lineNumber + 1) + ".");
                    }

                    bool extended = (dbcId & 0x80000000U) != 0 || dbcId > 0x7FFU;
                    uint canId = (dbcId & 0x80000000U) != 0 ? dbcId & 0x1FFFFFFFU : dbcId;
                    if (canId > 0x1FFFFFFFU)
                        throw new FormatException("CAN ID is out of range on line " + (lineNumber + 1) + ".");

                    currentMessage = new DbcMessage
                    {
                        CanId = canId,
                        IsExtended = extended,
                        Name = messageMatch.Groups["name"].Value,
                        Length = length
                    };
                    messages.Add(currentMessage);
                    continue;
                }

                if (!Regex.IsMatch(line, @"^\s*SG_(?=\s)"))
                    continue;
                if (currentMessage == null)
                    throw new FormatException("Signal definition appears before a message on line " + (lineNumber + 1) + ".");

                Match signalMatch = SignalPattern.Match(line);
                if (!signalMatch.Success)
                    throw new FormatException("Unsupported or malformed signal definition on line " + (lineNumber + 1) + ".");

                int startBit;
                int bitLength;
                decimal factor;
                decimal offset;
                decimal minimum;
                decimal maximum;
                if (!int.TryParse(signalMatch.Groups["start"].Value, NumberStyles.None,
                        CultureInfo.InvariantCulture, out startBit) ||
                    !int.TryParse(signalMatch.Groups["length"].Value, NumberStyles.None,
                        CultureInfo.InvariantCulture, out bitLength) ||
                    bitLength < 1 || bitLength > 64 ||
                    !decimal.TryParse(signalMatch.Groups["factor"].Value, NumberStyles.Float,
                        CultureInfo.InvariantCulture, out factor) || factor == 0 ||
                    !decimal.TryParse(signalMatch.Groups["offset"].Value, NumberStyles.Float,
                        CultureInfo.InvariantCulture, out offset) ||
                    !decimal.TryParse(signalMatch.Groups["min"].Value, NumberStyles.Float,
                        CultureInfo.InvariantCulture, out minimum) ||
                    !decimal.TryParse(signalMatch.Groups["max"].Value, NumberStyles.Float,
                        CultureInfo.InvariantCulture, out maximum))
                {
                    throw new FormatException("Invalid signal parameters on line " + (lineNumber + 1) + ".");
                }

                var signal = new DbcSignal
                {
                    Name = signalMatch.Groups["name"].Value,
                    Unit = signalMatch.Groups["unit"].Value,
                    Multiplexer = signalMatch.Groups["mux"].Value,
                    StartBit = startBit,
                    BitLength = bitLength,
                    IsLittleEndian = signalMatch.Groups["byteOrder"].Value == "1",
                    IsSigned = signalMatch.Groups["sign"].Value == "-",
                    Factor = factor,
                    Offset = offset,
                    Minimum = minimum,
                    Maximum = maximum
                };
                ValidateSignalBounds(currentMessage, signal, lineNumber + 1);
                currentMessage.Signals.Add(signal);
            }

            return messages;
        }

        private static void ValidateSignalBounds(DbcMessage message, DbcSignal signal, int lineNumber)
        {
            int position = signal.StartBit;
            for (int bit = 0; bit < signal.BitLength; bit++)
            {
                if (position < 0 || position >= message.Length * 8)
                    throw new FormatException("Signal " + signal.Name + " exceeds its message length on line " + lineNumber + ".");

                if (signal.IsLittleEndian)
                    position++;
                else
                    position = (position & 7) == 0 ? position + 15 : position - 1;
            }
        }
    }

    internal static class DbcPayloadEncoder
    {
        public static decimal[] Decode(DbcMessage message, byte[] data)
        {
            if (message.Length > 8)
                throw new FormatException("The selected message is longer than 8 bytes.");
            if (message.Signals.Any(signal => !string.IsNullOrEmpty(signal.Multiplexer)))
                throw new FormatException("Multiplexed signals are not supported for this message.");
            if (data == null || data.Length < 8)
                throw new FormatException("The payload must contain 8 bytes.");

            var values = new decimal[message.Signals.Count];
            for (int signalIndex = 0; signalIndex < message.Signals.Count; signalIndex++)
            {
                DbcSignal signal = message.Signals[signalIndex];
                ulong raw = 0;
                int position = signal.StartBit;
                for (int bit = 0; bit < signal.BitLength; bit++)
                {
                    int byteIndex = position / 8;
                    int bitIndex = GetByteBitIndex(position);
                    int sourceBit = signal.IsLittleEndian
                        ? bit
                        : signal.BitLength - bit - 1;
                    if ((data[byteIndex] & (1 << bitIndex)) != 0)
                        raw |= 1UL << sourceBit;

                    if (signal.IsLittleEndian)
                        position++;
                    else
                        position = (position & 7) == 0 ? position + 15 : position - 1;
                }

                decimal rawValue = raw;
                if (signal.IsSigned && (raw & (1UL << (signal.BitLength - 1))) != 0)
                    rawValue -= PowerOfTwo(signal.BitLength);
                values[signalIndex] = rawValue * signal.Factor + signal.Offset;
            }

            return values;
        }

        public static byte[] Encode(DbcMessage message, DataGridView grid, byte[] initialData)
        {
            if (message.Length > 8)
                throw new FormatException("The selected message is longer than 8 bytes.");
            if (message.Signals.Any(signal => !string.IsNullOrEmpty(signal.Multiplexer)))
                throw new FormatException("Multiplexed signals are not supported for this message.");

            var data = new byte[8];
            if (initialData != null)
                Array.Copy(initialData, data, Math.Min(initialData.Length, data.Length));
            var occupiedBits = new HashSet<int>();
            foreach (DataGridViewRow row in grid.Rows)
            {
                var signal = row.Tag as DbcSignal;
                if (signal == null)
                    continue;

                decimal physicalValue;
                string text = grid.IsCurrentCellInEditMode && grid.CurrentCell == row.Cells["Value"] &&
                    grid.EditingControl != null
                    ? grid.EditingControl.Text
                    : Convert.ToString(row.Cells["Value"].Value, CultureInfo.CurrentCulture);
                if (!decimal.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out physicalValue) &&
                    !decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out physicalValue))
                {
                    throw new FormatException("Enter a numeric value for signal " + signal.Name + ".");
                }
                if ((signal.Minimum.HasValue && physicalValue < signal.Minimum.Value) ||
                    (signal.Maximum.HasValue && physicalValue > signal.Maximum.Value))
                {
                    throw new FormatException("Value for " + signal.Name + " must be between " +
                        signal.Minimum.Value.ToString(CultureInfo.InvariantCulture) + " and " +
                        signal.Maximum.Value.ToString(CultureInfo.InvariantCulture) + ".");
                }

                decimal rawValue = decimal.Round(
                    (physicalValue - signal.Offset) / signal.Factor,
                    0,
                    MidpointRounding.AwayFromZero);
                decimal modulus = PowerOfTwo(signal.BitLength);
                decimal minimumRaw = signal.IsSigned ? -(modulus / 2m) : 0m;
                decimal maximumRaw = signal.IsSigned ? (modulus / 2m) - 1m : modulus - 1m;
                if (rawValue < minimumRaw || rawValue > maximumRaw)
                {
                    throw new FormatException("Value for " + signal.Name + " does not fit in " +
                        signal.BitLength + " bits.");
                }

                if (rawValue < 0)
                    rawValue += modulus;
                ulong raw = checked((ulong)rawValue);
                int position = signal.StartBit;
                for (int bit = 0; bit < signal.BitLength; bit++)
                {
                    int bitPosition = position;
                    if (!occupiedBits.Add(bitPosition))
                        throw new FormatException("Overlapping signals are not supported in message " + message.Name + ".");

                    int byteIndex = bitPosition / 8;
                    int bitIndex = GetByteBitIndex(bitPosition);
                    int sourceBit = signal.IsLittleEndian
                        ? bit
                        : signal.BitLength - bit - 1;
                    byte mask = (byte)(1 << bitIndex);
                    if (((raw >> sourceBit) & 1UL) != 0)
                        data[byteIndex] |= mask;
                    else
                        data[byteIndex] &= (byte)~mask;

                    if (signal.IsLittleEndian)
                        position++;
                    else
                        position = (position & 7) == 0 ? position + 15 : position - 1;
                }
            }

            return data;
        }

        private static decimal PowerOfTwo(int exponent)
        {
            decimal result = 1m;
            for (int bit = 0; bit < exponent; bit++)
                result *= 2m;
            return result;
        }

        private static int GetByteBitIndex(int dbcBitPosition)
        {
            return dbcBitPosition & 7;
        }
    }
}
