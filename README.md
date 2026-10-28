# USBCAN Tool (Visual Studio WinForms)

Windows app that:

1. Opens **RX** and **TX** separately (two USB adapters, or two channels on one adapter)
2. Software-forwards every RX frame to TX (rewrite slots apply only to the forwarded copy)
3. Optionally **rewrites payload** for up to 10 CAN IDs (enable checkbox, ID, 8-byte data)
4. Logs traffic with channel, direction, and CAN / CAN FD type
5. Remembers RX/TX device, baud, protocol, filters, and rewrite slots after close (`%AppData%\USBCANTool\settings.xml`)

## Requirements

- Visual Studio 2019 / 2022 (or later) with **.NET desktop development** workload
- .NET Framework 4.8
- ZLG USBCAN driver installed (from ZLG official package)
- **`zlgcan.dll`** (and the matching kernel DLLs under `kerneldlls`)

> You still need the runtime DLLs from the full ZLG install package
> (usually found next to the official demo or under the driver install folder).

## How to build

1. Open `USBCANTool.sln` in Visual Studio.
2. Select platform **x64** or **x86** to match the `zlgcan.dll` you have
   (prefer x64 if both exist).
3. Build → Debug or Release.

## How to run

1. Copy **`zlgcan.dll`** into the output folder
   (`bin\x64\Debug\` or `bin\x86\Debug\` …).
2. Also copy the **`kerneldlls`** folder next to the EXE if your ZLG package requires it.
3. Run the EXE.
4. Set **RX** and **TX** rows independently: device type, USB **Idx**, **Ch**, protocol, bitrate (and **Data** baud for CAN FD). Same type+idx uses one adapter (two channels). Different type or idx opens two adapters.
5. **Protocol**: CAN or CAN FD (CAN FD only on USBCANFD devices). USBCANFD-100U is 1 channel (`Ch` = 0). On FD hardware use **Term** for the internal resistor and **ISO / Non-ISO** when protocol is CAN FD.
6. Optionally fill **Rewrite** slots: tick **En**, enter hex **ID**, choose **STD** (11-bit) or **EXT** (29-bit), enter 8 data bytes. Empty or unchecked slots are ignored. STD and EXT with the same number are different IDs.
7. Click **Open**. Frames received on RX are sent on TX (one way).
8. If a received ID matches an enabled slot, the **forwarded** frame uses your 8 bytes (`DLC = 8`). The RX log still shows the original data; TX shows the rewritten bytes.
9. Received and forwarded frames appear in the packet list (`Ch` is the hardware channel on that side).

One-channel cards (USBCANFD-100U) work as RX **or** TX. Two-channel cards can be one adapter with RX Ch=0 and TX Ch=1.

Do not wire RX and TX onto the same physical bus or frames loop forever. This is a PC software repeater, not a hardware gateway.

## Project layout

```
USBCANTool.sln
README.md
USBCANTool/
├── USBCANTool.csproj
├── Program.cs
├── Form1.cs / Form1.Designer.cs
├── ZlgCanApi.cs
├── AppSettings.cs
├── App.config
└── Lib/x64, Lib/x86   (ZLG headers only; copy zlgcan.dll next to the EXE)
```

## Notes

- Classic devices: `ZCAN_SetValue(handle, "n/baud_rate", …)` then `TYPE_CAN`.
- USBCANFD devices: `can_type` is always `TYPE_CANFD`. Protocol CAN vs CAN FD is `n/protocol`. Baud is `n/canfd_abit_baud_rate` and, in CAN FD protocol, `n/canfd_dbit_baud_rate`. Clock is 60 MHz. **Term** is the internal 120 Ω resistor (`n/initenal_resistance`, after InitCAN). **ISO / Non-ISO** is `n/canfd_standard` (only when protocol is CAN FD).
- RX and TX are configured one after the other (SetValue then InitCAN per channel) so each side gets its own bitrate. FD frames are sent with `ZCAN_TransmitFD` only when the TX side is CAN FD; otherwise they are truncated to classic 8-byte CAN.
- Bridging runs on a background thread so a busy bus does not freeze the UI. The log keeps a rolling window (10k lines) and drops oldest pending lines if traffic is faster than the display. Many distinct CAN IDs are grouped in memory only for that window, so Group show cannot grow without bound.
- If Open fails, check: driver installed, correct device type and USB index, matching DLL architecture, device not already opened by another program, and that the selected channel exists.
