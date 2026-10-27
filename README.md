# USBCAN Tool (Visual Studio WinForms)

Simple Windows application that:

1. Opens a ZLG USBCAN device with **default config** and **500 kb/s** baud rate  
2. Lets you fill one CAN packet (ID, DLC, data, Extended/RTR) and press **Send**  
3. Continuously receives frames and shows them in a log box  

## Requirements

- Visual Studio 2019 / 2022 (or later) with **.NET desktop development** workload  
- .NET Framework 4.8  
- ZLG USBCAN driver installed (from ZLG official package)  
- **`zlgcan.dll`** (and the matching kernel DLLs under `kerneldlls`)  

> The library package you provided contains headers + `.lib` only.  
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
2. Also copy the **`kerneldlls`** folder (with its XML + device DLLs) next to the EXE  
   if your ZLG package requires it (many devices load kernel modules from there).  
3. Run the EXE.  
4. Choose device type (default = **USBCAN2**), index 0, channel 0.  
5. Click **Open** → baud is set to **500000** automatically.  
6. Enter ID (hex), data bytes (space-separated hex), press **Send**.  
7. Received frames appear in the bottom log box.

## Project layout

```
USBCANTool/
├── USBCANTool.sln
├── README.md
└── USBCANTool/
    ├── USBCANTool.csproj
    ├── Program.cs
    ├── Form1.cs / Form1.Designer.cs
    ├── ZlgCanApi.cs          ← P/Invoke definitions
    ├── App.config
    ├── Properties/AssemblyInfo.cs
    └── Lib/
        ├── x64/   (headers + zlgcan.lib)
        └── x86/   (headers + zlgcan.lib)
```

## Notes

- Baud is set via `ZCAN_SetValue(handle, "0/baud_rate", "500000")` before `ZCAN_InitCAN`  
  (the official recommended way for modern ZLG devices).  
- Classic CAN only (TYPE_CAN). CAN-FD is not enabled in this simple tool.  
- Receive is polled every 50 ms on the UI thread; for high bus load you may want a background thread later.  
- If Open fails, check: driver installed, correct device type, matching DLL architecture, device not already opened by another program.
