# USBCANTool – Plan v3 (your answers applied): keeping the bridge reliable and fast

> **v3:** your answers to section 9 are applied (see the next section). Sections that changed are marked **(v3)**. Older text: this replaces the first `plan.md`. I re-read every relevant line of `Form1.cs`, `ZlgCanApi.cs`, `canframe.h` and `typedef.h`, and found **mistakes and gaps in v1**. They are listed in section 1 so you can see exactly what changed.

## Decisions received (v3)

| # | Question | Your answer | Effect on the plan |
|---|---|---|---|
| 1 | Padding short frames | Only rewritten frames | Pass-through frames are forwarded bit-exact; padding only for rewrite slots (3.1, 3.2) |
| 2 | CRC on pass-through frames | No | CRC is recomputed only on rewritten frames (3.3) |
| 3 | Counters | Required | Per-ID counters, only on rewritten frames (3.4). **Width/rule still open** |
| 4 | Latency requirement | As fast as possible | No numeric limit, so every change is judged by measured latency against a Phase 0 baseline; spin-first idle strategy (2.1, 5.4) |
| 5 | Bit rate, load, topology | 500 kbit/s, 50 % load; one bus cut, FD-200U (2 ch) in between, repeats IDs both ways and changes selected ones | A and B are two separate segments at the same bit rate. Capacity is fine (2.3), so jitter and transparency matter more than raw speed |
| 6 | Devices | Both sides on the same FD-200U | One device handle, two channels. Option S first; T4 (two channels of one device) is the relevant thread-safety test. USBCAN2 / FD-100U items dropped |
| 7 | Drop policy | Default | Drop oldest, counted and alarmed (5.3) |
| 8 | PC/app failure | "Bridge stops, buses isolated" is acceptable | No hardware bypass needed; alarms, log and auto-recovery stay (5.5) |
| 9 | `zlgcan.h` | In `USBCANTool\Lib\x64`, accessible | **I could not open it.** Only `canframe.h` and `typedef.h` reached me from that folder. Every **[TEST]** item stays a test until it is uploaded |

**Still open (please answer or send):**
1. **Counter rule:** counter width (4 or 8 bit), which byte/bits, wrap value, and the CRC algorithm per ID (ideally the DBC lines for the counter/CRC IDs). Without this I keep the current code's rule but per ID.
2. **"50 % load":** I assumed it is the load of the original single bus, so `N_A + N_B ≈ 50 %`. If each segment natively carries 50 %, the sum is 100 % and no software can forward everything (see 2.3).
3. **Protocol on the FD-200U channels:** I assumed classic CAN at 500 kbit/s on both (so no FD frames, and 3.5 stays at zero). Tell me if either side uses CAN FD.
4. **Source snapshot:** the `Form1.cs` I received is simpler than the code this plan describes (one-way RX→TX, `RxBatch = 200`, no CRC/counter/echo code). The plan assumes the newer bidirectional code. Please confirm which is current, or send it.
5. **`zlgcan.h`** and the manual pages for `ZCAN_Receive`, `ZCAN_Transmit`, error/status APIs, merge-receive and filters.

---

## 0. Evidence and confidence

| Source | What it supports |
|---|---|
| Your code (`Form1.cs`, `ZlgCanApi.cs`, `.csproj`) | All code findings below – these are certain |
| `Lib/*/canframe.h`, `typedef.h` | Frame layout, flag bits (`TX_ECHO_FLAG 0x20`, `TX_DELAY_SEND_FLAG 0x80`, `TX_DELAY_SEND_TIME_UNIT_FLAG 0x40`, `CANFD_BRS`, `CANFD_ESI`) |
| Public ZLG wrappers/forum posts (not the official manual) | `ZCAN_Receive(handle, buf, len, wait_time = -1)` where the default `-1` means "never time out"; `Transmit(handle, msgs, len)` takes a **count** of messages; `transmit_type`: 0 normal, 1 single-shot, 2 self-send-receive, 3 single self-send-receive |
| **Not available to me (v3):** `zlgcan.h`, ZLG manual | You said `zlgcan.h` is in `Lib\x64`, but only `canframe.h` and `typedef.h` reached me and nothing else is in my workspace. Everything marked **[TEST]** must be proven on your FD-200U (Phase 0). I am not asserting those. |

I have **not compiled or run** anything. All performance numbers below are estimates until Phase 0 measures them.

---

## 1. Corrections to my first plan

1. **`Thread.Sleep(1)` was over-emphasised.** `RxLoop` only sleeps when *both* channels returned nothing (`if (!any) Thread.Sleep(1)`). On a saturated bus it almost never sleeps. The sleep hurts *latency and burst handling* (up to ~15 ms of added delay after an idle moment), **not** steady-state throughput. At saturation the limiter is the per-frame native call + the global lock + single thread.
2. **"Exceeds `RxBatch = 128`" was wrong as a loss point.** `RxBatch` only caps one call; the loop reads again. The real loss point is the **device's hardware FIFO** (size unknown **[TEST]**).
3. **"UI can block on Close because `CloseNative` takes `_canLock`" was wrong.** `CloseDevice` stops and joins the worker first (up to 2 s on the UI thread), and only then calls `CloseNative`. The real behaviour: Close can freeze the UI for up to 2 s if the pump is stuck in a long native call.
4. **"Delete `IsOurEcho` if no echo is seen" was dangerous advice.** If a device *does* echo, removing it creates a **forwarding loop (A→B→A→…) that floods both buses**. Echo handling must be proven on a bench and replaced by a loop guard, not simply deleted (section 3.7).
5. **The "frames lost" scenario for `IsOurEcho` was too broad.** A legitimate frame is dropped only if the *same ID and identical payload* natively appears on the bus within the window. That requires the same ID to exist on both sides. It's a real but narrower risk. The solid problems are: cost per frame and a broken time base (`TickCount` ≈ 10–16 ms resolution vs an "8 ms" window).
6. **The "0 lost frames with both buses at 90–100 % load" acceptance test was physically impossible.** A bridge forwards A's traffic onto B *in addition to* B's own traffic. Each bus must carry (own + forwarded). Capacity rules are in section 2.2 and the test plan is fixed in section 8.
7. **"p99 latency < 2 ms" was an arbitrary target.** On Windows over USB it must come from *your* requirement and be *measured*, not assumed (section 2.1).
8. **"Error frames often indicate overrun" was overstated.** Error frames are bus errors; whether a ZLG device reports FIFO overrun that way is **[TEST]**.
9. **I missed the biggest class of problems: the bridge is not transparent.** It changes pass-through frames (DLC padding, RTR corruption, CRC recomputation, a shared counter, FD truncation). On a sensitive bus that matters more than speed (section 3).
10. **I missed fail-safe behaviour** (watchdog, USB unplug, what happens when the PC stalls) and the **non-real-time nature of Windows** (section 2.1, 5.4).
11. **Drop policy was vague.** "Drop after 3 retries" was kept implicitly. A defined, counted, alarmed policy is needed (section 5.3).
12. **`wait_time` semantics are a trap I didn't call out.** If a ZLG receive waits until `len` frames arrive *or* the timeout expires, then `wait_time = -1` with `len = 128` would block for ages. Must be tested before use (Phase 0, test T2).
13. **Rewrite-table equivalence.** The current code returns the *first* enabled matching slot. A faster table must keep that exact rule.
14. **Batched transmit:** the API takes a count (confirmed from public wrappers), but partial returns, struct stride, and USBCAN2-specific behaviour still need testing **[TEST]**.

---

## 2. Hard constraints you should know before changing anything

### 2.1 Windows + USB is not a real-time system
* A normal Windows thread can be descheduled for tens of ms (antivirus, updates, driver DPCs, power management). While the pump is stalled, the **only** buffer is the ZLG device FIFO.
* Therefore the design goal is not "never stall" but **"stalls shorter than the FIFO can absorb, detected and alarmed if not"**. Measure the *maximum gap between pump iterations* (a jitter histogram) – it is the most important number for "will it lose frames".
* Every store-and-forward hop adds **at least one frame time** (a frame is fully received before it can be re-sent): ≈ 222 µs at 500 kbit/s, ≈ 111 µs at 1 Mbit/s for an 8-byte standard frame, plus USB (1 ms polling granularity on full-speed devices is typical **[TEST]**) and scheduling. If any consumer needs strict < 1–2 ms worst-case latency, a PC bridge cannot *guarantee* it; consider a hardware gateway for that part. **(v3) Decision:** your requirement is "as fast as possible", so there is no pass/fail number. Phase 0 records a baseline (median, p99, p99.9, max forward latency) and every later change must not make it worse. I can minimise the latency but cannot promise a worst case on Windows.

### 2.2 Bus capacity arithmetic (standard 11-bit, 8-byte data frame)
* 111 bits nominal (incl. 3-bit interframe space); worst-case bit-stuffing ≈ 135 bits.
* 500 kbit/s: ≈ 4,500 fps nominal, ≈ 3,700 fps worst case. 1 Mbit/s: ≈ 9,000 / ≈ 7,400 fps.
* Extended 8-byte frame: 131 bits nominal, ≈ 160 worst case.
* **Capacity rule:** for a full-forwarding bridge, `native_load(A) + native_load(B) ≤ ~90 %` of one bus (when both run the same bit rate), otherwise frames **must** be lost no matter how good the software is. Forwarded frames also compete in arbitration on the destination bus: low-priority IDs can starve when the destination is near 100 %.
* Your default bit rate is 500 kbit/s (`DefaultBaud`), so 15 ms of added `Sleep(1)` jitter ≈ 55–67 frames per channel.

### 2.3 Your topology and load (v3)
* One physical bus cut into two segments A and B, with the FD-200U (channel 0 and channel 1) between them. Both sides are 500 kbit/s. All IDs are repeated both ways, and selected IDs are rewritten.
* Reading "50 % load" as the load of the original bus, `N_A + N_B ≈ 50 %`, so each segment carries ≈ 50 % after forwarding (own plus forwarded). That is about 1,850–2,250 frames/s in total for 8-byte standard frames (more if frames are shorter).
* This is about 40 points under the 90 % capacity rule, so **zero loss is achievable**. Any drop means a stall or a bug and must raise an alarm.
* At ≈ 2,250 fps the CPU and per-frame cost are small. The real risks are **scheduling stalls (T6), the device FIFO (T5) and added latency**, not raw throughput. So Phase 2 is the priority, and Phase 3/4 micro-optimisations are probably unnecessary (do them only if the measurements ask for it).
* **Termination:** each cut segment needs 120 Ω at both ends. If the original terminators were at the ends of the bus, keep **Term** ticked on both FD-200U channels (or fit external resistors) so the new ends are terminated. Bad termination causes errors that look like software loss.
* If "50 %" actually means 50 % natively on *each* segment, the sum is 100 % and frames must be lost whatever the software does.

### 2.4 Different bit rates on A and B (not applicable now)
Both sides are 500 kbit/s. If that ever changes, the slower side cannot drain the faster side. Losses are then unavoidable and must be counted and alarmed (never silent).

---

## 3. Transparency and correctness findings (do these before/alongside speed work)

These change what goes on your bus. I list the current behaviour and my recommendation; **(v3) your answers in section 9 are applied below.**

### 3.1 Every short frame is padded to 8 bytes
`EnsureMinimumCanLength` / `EnsureMinimumFdLength` run on **all** forwarded frames, not only rewritten ones. A 3-byte frame leaves the bridge as an 8-byte frame with zeros. Receivers that check DLC will see a different frame.
**Decided (Q1): rewritten frames only.** Pass-through frames are forwarded bit-exact (same DLC, same data). Pad only rewritten slots (`UseUserData`) because they carry 8 bytes by definition.

### 3.2 Remote (RTR) frames are corrupted
An RTR frame has DLC = requested length and no data. `EnsureMinimumCanLength` turns an RTR with DLC 0 into an RTR with DLC 8.
**Follows from Q1:** forward RTR with the original DLC and no data modification.

### 3.3 CRC is recomputed on all frames of listed IDs, even in "Unset" mode
`ApplyBmsCrc` runs for any frame whose ID is in `IsCrcCanId` and DLC is 8 (always true after padding), regardless of whether a rewrite slot applies. So pass-through frames of those IDs are altered if their CRC differs from what this code computes (different algorithm, intentional fault, or a different node with another CRC).
**Decided (Q2: not intentional).** Recompute CRC (and counter) **only** for frames that are actually rewritten (`slotIndex != 0 && UseUserData`).

### 3.4 One global counter for all counter IDs
`GetNextBmsCounter` is a single static byte shared by all 8 IDs in `IsBmsCounterCanId`. Each ID's receiver therefore sees counter values jumping by the number of other counter frames sent in between. If receivers check "counter = previous + 1" per message, this will raise counter faults, and it gets worse on a busy bus.
**Decided (Q3: required).** One counter **per CAN ID**, applied only to rewritten frames. **Still open:** counter width (4 or 8 bit), position, wrap value. I don't have the BMS DBC; until you send it I keep the current code's layout, just per ID.

### 3.5 FD → classic conversion truncates silently
`FdToClassic` clamps `len` to 8 and drops BRS. Frames with 9–64 data bytes lose data when the destination channel is classic CAN.
**Recommendation:** count and alarm these frames (`FdTruncated`); optionally refuse to forward instead of truncating. **(v3)** If both channels run classic CAN (assumed), this counter should stay at 0 and serves as a sanity check.

### 3.6 Flags from received frames are copied into transmitted frames
`ForwardCan` copies the whole received `can_frame`, including `__pad`, `__res0`, `__res1`. In `canframe.h`, `__pad`/`flags` bits have TX meaning (`0x20` echo, `0x80` delay-send, `0x40` delay unit). Received frames flagged `0x20` are skipped, so this is probably harmless **today**, but it is fragile.
**Recommendation:** build the TX frame explicitly: `can_id`, `can_dlc`, `data`; `__pad = __res0 = __res1 = 0`. For FD keep only `CANFD_BRS` (drop `CANFD_ESI`, echo, delay bits). Keep `transmit_type = 0` (normal, retransmits on arbitration loss) for fidelity **[TEST]** whether you ever want 1 (single-shot).

### 3.7 Echo / loop protection
* `IsOurEcho` exists to avoid echo loops. It's O(64) per frame under a lock, and uses `Environment.TickCount` (10–16 ms resolution) with an "8 ms" window – so the effective window is 0–16 ms and random.
* The FD path sets `set_device_tx_echo = 0` and skips frames with `TX_ECHO_FLAG`; for classic USBCAN2 the behaviour is **[TEST]**.
* **Do not remove it blindly.** Replace with: (1) a bench echo test (T3), (2) if no echo, make the filter opt-in, (3) in all cases add a **loop guard**: if forwarded fps in one direction exceeds ~105 % of the physical capacity for the destination bit rate for > 100 ms, raise a latched alarm and (configurable) stop forwarding. This also protects against two bridges/gateways connecting the same two buses.

**Your setup (v3):** the two segments are physically separate and share only the FD-200U, so a loop can only come from an accidental second path (for example a harness reconnected during testing). Echo behaviour only has to be proven on the FD-200U (T3). The loop guard stays because it is cheap and protects against that accident.

### 3.8 Ordering
Frames of the same ID and same type stay in order. Across IDs, and between classic and FD frames on an FD-protocol channel (classic is drained first, then FD), order can differ from bus order. That is normal for a bridge; just be aware.

---

## 4. Performance findings (corrected)

| # | Finding | Evidence | Impact |
|---|---|---|---|
| P1 | One thread services both directions sequentially | `RxLoop` → `PollSide(a,b)` then `PollSide(b,a)` | A 128-frame batch forwarded one-by-one can take many ms, starving the other direction |
| P2 | One native transmit call per frame | `ForwardCan` → `ZCAN_Transmit(dest, tx, 1)` | Dominant per-frame cost at high rates |
| P3 | Global `_canLock` around receive and transmit of both directions; shared RX and TX buffers | `PollSide`, `ForwardCan` | Prevents parallelism; couples directions |
| P4 | Transmit retry spins 3× *while holding the lock*, then drops | `ForwardCan` | Loss + stalls on a busy destination |
| P5 | Idle path `Thread.Sleep(1)` (≈ 15 ms) | `RxLoop` | Latency spikes / FIFO risk after idle (not a saturation limiter) |
| P6 | `IsOurEcho` on every frame | see 3.7 | CPU + lock contention |
| P7 | Rewrite lookup takes a lock and scans 10 slots per frame | `TryGetRewriteSlot` | CPU + contention |
| P8 | Bit-by-bit CRC; `switch` ID classification several times per frame | `CalculateBmsCrc/DcdcCrc`, `IsCrcCanId`… | CPU |
| P9 | `StoreLatestSignal` allocates strings and uses `DateTime.Now` per rewritten frame | `StoreLatestSignal` | GC pauses |
| P10 | Receive/transmit buffers are managed arrays pinned for the process lifetime | `PinBuf` | Heap fragmentation (minor) |
| P11 | `catch { Thread.Sleep(1); }` hides every pump exception | `RxLoop` | You can't see failures |
| P12 | Error frames skipped silently | `PollSide` | No visibility of bus errors |
| P13 | Debug configuration is `Optimize=false` | `.csproj`, `obj/…/Debug` | Never benchmark this |

---

## 5. Target design

### 5.1 Principles
1. **Never alter what you don't intend to alter** (section 3).
2. **Never lose a frame silently.** Every drop is counted, logged, and shown.
3. **Fail loudly and recover automatically** (alarm, watchdog, auto-reopen).
4. **Change one thing at a time**, with a regression test and a rollback switch.

### 5.2 Structure
* `ICanPort` interface (wrapping `ZCAN_Receive/Transmit[FD]`, handle, protocol) with a **fake implementation** for tests (can simulate partial sends, FIFO full, delays, bursts). This is what lets you test the bridge without hardware and prove no loss.
* `FrameProcessor` (pure, no P/Invoke): takes a frame + direction, returns `Forward / Drop` and the (possibly rewritten) frame. Contains transparency rules, rewrite table, CRC/counter. Unit-tested against golden vectors from today's code.
* `BridgePump` per direction: receive → process → pending buffer → batched transmit.
* `HealthMonitor`: counters, watchdog, alarms (UI timer reads them).

### 5.3 Pending buffer and drop policy
* Per direction, a **bounded pending buffer** (start with 2,048 frames ≈ 0.45 s at 4,500 fps) between "received and processed" and "accepted by the destination device".
* Transmit: send as many as the device accepts (`r = ZCAN_Transmit(dst, p, n)`); advance by `r`; if `r < n` stop and retry next iteration (do not spin in a loop that blocks the other direction).
* Overflow policy **(v3, Q7 = default): drop oldest**, because stale data is worse than fresh data on a control bus. Counted per ID in a small table, no protected-ID list. At your ≈ 50 % load a drop is abnormal, so every drop raises an alarm.
* A long-lived transmit failure (e.g. 500 ms with zero accepted frames) = destination bus or device is stuck → latched alarm (bus-off, no ACK, USB problem).

### 5.4 Threads: two options, measure before choosing
* **Option S – one thread, batched, two pending buffers.** Simplest and avoids the unknown "is the ZLG API thread-safe across channels?" question. Idle strategy: spin ≤ ~200 µs, then `Thread.Yield`, then `Sleep(1)` only after ≥ 5 ms of silence. Downside: when idle it uses a core; a blocking wait on one channel delays the other by up to the wait time, so don't use a long `wait_time`.
* **Option T – one thread per direction, each blocking in `ZCAN_Receive` with a short `wait_time` (1–2 ms) on its own handle.** Lowest latency and best fairness, **if** the driver is thread-safe across the two channel handles (test T4). If not, use one lock per *device* handle (not one global lock), keep native calls short.
* Recommendation: build Option S first (lowest risk), measure the jitter histogram and loss at the target load; go to T only if the numbers demand it.
* **(v3)** Both channels are on one FD-200U (one device handle), so a lock per device is the same as the global lock and splitting locks gains nothing. Option T only makes sense if T4 shows the two channel handles can be used in parallel.
* **(v3)** Because you want it "as fast as possible", use the spin-first idle strategy and accept one busy core (or spin ≤ 200 µs, then `Yield`). Use a high thread priority, `timeBeginPeriod(1)` and the power-throttling opt-out. Verify the effect with the Phase 0 latency baseline.
* Raise timer resolution (`timeBeginPeriod(1)`) and opt out of power throttling (`SetProcessInformation` with `ProcessPowerThrottling`, ignore-timer-resolution flag) so Windows 11 doesn't ignore the request when the window is minimized **[TEST: minimize the app and re-measure]**. Optional: MMCSS (`AvSetMmThreadCharacteristics`) for the pump threads **[TEST]**.

### 5.5 Fail-safe behaviour
* **Heartbeat + watchdog:** each pump increments a counter per iteration; the UI timer (and a separate watchdog timer thread) flags "STALLED" if the counter doesn't move for > 50–200 ms. Alarm stays latched until acknowledged.
* **USB unplug / device error:** if `ZCAN_Transmit/Receive` start failing persistently, close and reopen the device/channels automatically with rate limiting; log it.
* **Bus-off recovery:** if the device reports bus-off **[TEST how it is reported]**, reset and restart the channel with rate limiting; count the event. Resetting can discard device buffers, so it must be an alarmed event.
* **Logging:** a small rolling file log (start/stop, drops, alarms, reopen events, with timestamps) so a problem seen in the field can be diagnosed later.
* **What happens when the PC/app dies? (v3, Q8 = acceptable):** the bridge stops and the two segments are isolated. You accepted this, so no hardware bypass or external watchdog is planned. The watchdog, alarms, log and auto-recovery above stay so the operator sees it quickly.

---

## 6. Phased plan (each phase is gated by the tests in section 8)

### Phase 0 – Measure and prove assumptions (no behaviour change)
Add instrumentation (read-only) and run these bench tests with a second device as monitor/generator:

| Test | Question it answers |
|---|---|
| **T1** Transmit batch | Does `ZCAN_Transmit(count > 1)` work on the USBCANFD-200U (the only device you use)? What does it return on a partial send? Is the struct stride right (`sizeof(ZCAN_Transmit_Data)` = 20, `ZCAN_Receive_Data` = 24, `ZCAN_TransmitFD_Data` = 76, `ZCAN_ReceiveFD_Data` = 80 with `Pack = 1`)? |
| **T2** Receive wait semantics | Send 1 frame/s. Call `ZCAN_Receive(len = 128, wait_time = 100)`. Does it return immediately with 1 frame, or after 100 ms (waiting for 128 or timeout)? Measure return time. Decides whether a blocking wait can be used at all. |
| **T3** Echo | One frame through the bridge, no other nodes. Does the *source* bus see the forwarded frame a second time (monitor device)? On the FD-200U in classic mode (your 500 kbit/s setup); FD mode only if you use it. |
| **T4** Thread safety | Two threads, one per channel of the same FD-200U (your setup), 10 min at high load: no crashes, no corrupted frames, no count mismatch. |
| **T5** Device FIFO | How many frames can accumulate while the pump is deliberately stalled (e.g. 50 ms, 200 ms, 1 s) before loss is detected by sequence counters? |
| **T6** Pump jitter | Histogram of gaps between pump iterations on your PC at target load, with and without minimizing the window, with antivirus running, etc. |

Instrumentation to add (all lock-free `Interlocked` counters shown in the UI):
`RxFrames`, `TxFrames`, `TxPartial`, `TxZero` (no frames accepted), `Dropped` (by reason), `DroppedPerId` (small table), `ErrorFrames` (count `CAN_ERR_FLAG` instead of skipping), `FdTruncated`, `PendingMax`, `RxBatchMax`, `LoopGapMaxUs`, `DeviceBacklog` (`ZCAN_GetReceiveNum`, sampled every ~100 ms – not per loop), pump heartbeat, exceptions (count + last message). Replace the silent `catch`.

Also build a **reference loss tester**: a second device sends a frame with a rolling 32-bit sequence + CRC on a reserved ID (never rewritten); the receiving side detects gaps, duplicates, and reordering, and logs the longest gap.

### Phase 1 – Safety and transparency fixes (small, high value)
Implement 3.1, 3.2, 3.3, 3.4, 3.6 (and 3.5 as counting/alarm). Keep a **"Legacy behaviour" switch** (settings) so you can compare and roll back during validation. Replace `catch {}` with counting/logging. Replace echo filter per 3.7 only after T3.
Gate: **transparency test** (section 8) passes: all pass-through IDs are byte-identical on both sides.

### Phase 2 – Throughput/latency core
1. Introduce `ICanPort` + fake, `FrameProcessor` with unit tests (golden vectors from the current code for CRC and rewrite behaviour).
2. Per-direction native buffers (`Marshal.AllocHGlobal`, allocated once) and a bounded pending buffer.
3. Explicit TX frame construction (3.6). Batched transmit with partial-return handling (T1).
4. Option S loop with idle strategy; timer resolution; Release build.
5. Heartbeat/watchdog/alarms; loop guard (3.7).
Gate: loss test and jitter test at the capacity-correct loads in section 8.

### Phase 3 – Only if Phase 2 numbers are not enough
* Option T (two pump threads) if T4 passes; otherwise per-device locks.
* MMCSS/priority tuning, spin-then-wait tuning.
* Hardware features **[TEST]**: acceptance filters (not needed, you forward all IDs), merged-receive mode.

### Phase 4 – Per-frame micro-optimisations (safe once tests exist)
* `volatile` immutable rewrite table with O(1) std lookup (`RewriteSlot[0x800]`) + dictionary for extended IDs; **first enabled match wins** (same rule as today).
* `bool[0x800]` classification tables for CRC/counter/DCDC IDs.
* Table-driven CRC (must be proven equal to the current bitwise code on ≥ 10⁷ random inputs).
* No allocations/`DateTime.Now`/string formatting on the pump (store raw values; format in the UI timer for dirty slots only).
* `GCSettings.LatencyMode = SustainedLowLatency` (optional).

### Phase 5 – UI/operability
* Health panel: A→B / B→A fps, pending, drops (by reason), error frames, last gap, alarm state (latched, acknowledgeable). LED amber/red on drops, stall, loop guard.
* Make Close non-blocking (signal, join with timeout on a background task, report if it didn't stop).
* Warn when A/B bit rates differ or when measured load exceeds the capacity rule.

---

## 7. Code sketches (illustrative – validate against `zlgcan.h` before use)

### 7.1 Timer resolution (and Windows 11 opt-out)
```csharp
[DllImport("winmm.dll")] static extern uint timeBeginPeriod(uint ms);
[DllImport("winmm.dll")] static extern uint timeEndPeriod(uint ms);
// call timeBeginPeriod(1) at Open, timeEndPeriod(1) at Close.
// On Windows 11 also call SetProcessInformation(ProcessPowerThrottling) with the
// "ignore timer resolution" flag so a minimized window keeps 1 ms resolution. [TEST]
```

### 7.2 Batched transmit with partial return (no blocking of the other direction)
```csharp
// tx: native buffer of ZCAN_Transmit_Data; head/tail are indices of pending frames.
unsafe bool Flush(IntPtr dst, ZCAN_Transmit_Data* tx, ref int head, int tail, int maxBatch)
{
    while (head < tail)
    {
        int n = Math.Min(tail - head, maxBatch);
        uint r = ZlgCanApi.ZCAN_Transmit(dst, tx + head, (uint)n);
        if (r == 0) return false;       // device FIFO full: try again next iteration
        head += (int)r;
        if (r < n) return false;        // partial: device is full now
    }
    return true;
}
// Never loop forever here. After a bounded time with r == 0, raise TxStuck and let the
// pending buffer's overflow policy (drop oldest, counted) apply.
```

### 7.3 Build the TX frame explicitly (no flag leakage, bit-exact payload)
```csharp
static unsafe void MakeTx(ZCAN_Transmit_Data* t, uint canId, byte dlc, byte* data)
{
    t->frame.can_id  = canId;       // keeps EFF/RTR flags
    t->frame.can_dlc = dlc;         // original DLC (RTR keeps its DLC, no padding)
    t->frame.__pad = 0; t->frame.__res0 = 0; t->frame.__res1 = 0;
    if (!ZlgCanApi.IS_RTR(canId))
        for (int i = 0; i < dlc; i++) t->frame.data[i] = data[i];
    t->transmit_type = 0;
}
```

### 7.4 Per-ID counter and CRC only for rewritten frames
```csharp
// Applied only when slot.UseUserData:
if (IsCounterId(id)) data[6] = counterById[id]++;   // per-ID counter (confirm width/rule!)
if (IsCrcId(id))     data[7] = Crc(id, data);       // table-driven, same result as today
```

### 7.5 Table-driven CRC (equal to the current bitwise code – prove with a test)
```csharp
static byte[] BuildTable(byte poly)
{
    var t = new byte[256];
    for (int i = 0; i < 256; i++)
    {
        byte c = (byte)i;
        for (int b = 0; b < 8; b++) c = (byte)((c & 0x80) != 0 ? (c << 1) ^ poly : c << 1);
        t[i] = c;
    }
    return t;
}
// BMS : crc = 0x00; for i in 0..len-2: crc = T17[crc ^ d[i]];
// DCDC: crc = 0xFF; for i in 0..len-2: crc = T1D[crc ^ d[i]]; result = (byte)~crc;
```

### 7.6 Lock-free rewrite table (same "first enabled match" rule)
```csharp
sealed class RewriteTable
{
    public readonly RewriteSlot[] Std = new RewriteSlot[0x800];
    public readonly Dictionary<uint, RewriteSlot> Ext = new Dictionary<uint, RewriteSlot>();
}
private volatile RewriteTable _table;   // built on the UI thread, published by assignment
// Build: iterate slots 0..9; skip disabled/no-id; if the table cell is already set, keep the first.
```

---

## 8. Test plan and acceptance criteria (capacity-correct)

Load is defined as bus load % including stuffing, measured by the monitor device. Let `N_A`, `N_B` be native loads on A and B.

| Test | Setup | Pass criteria |
|---|---|---|
| **Transparency** | Third monitor device on each bus; all IDs pass-through (no rewrite); include DLC 0–8, RTR, extended, FD frames | For every ID, the byte stream and DLCs seen on the destination equal the source stream. Zero modified frames. |
| **Rewrite correctness** | Enable slots; counter-ID and CRC-ID frames | Rewritten frames: payload as set, counter increments by exactly 1 **per ID**, CRC valid. Non-rewritten IDs unchanged. |
| **Steady load (your profile, v3)** | `N_A + N_B ≈ 50 %` at 500 kbit/s (e.g. 25 % + 25 %, and also 50 % + 0 and 0 + 50 %), 30 min, then 8 h soak | **0** lost/duplicated frames by sequence counters; zero alarms; latency within the Phase 0 baseline |
| **Near capacity (margin test, v3)** | `N_A + N_B ≈ 90–95 %` of bus capacity | 0 loss *within capacity*; report latency; arbitration starvation of low-priority IDs measured and reported (not a software bug) |
| **Over capacity** | `N_A + N_B > 100 %` | Drops happen only in the pending buffer according to policy, **all counted and alarmed**; no stall, no UI freeze, no crash |
| **Burst** | Idle → 100 % → idle every 1 s | Pending returns to ≈ 0 each cycle; no loss; no alarm |
| **Stall injection** | Deliberately stall the pump 10/50/200 ms (debug hook) | Matches T5: no loss up to the FIFO limit; beyond that loss is detected and alarmed |
| **Mixed bit rates** | Not applicable now (both sides 500 kbit/s). Run only if the configuration changes | Drops only on the slow side, counted/alarmed |
| **Latency/jitter** | External timestamps from the monitor device | Report max/p99/p99.9 forward latency and max pump gap; compare with the Phase 0 baseline (your requirement is "as fast as possible", section 2.1). |
| **Loop guard** | Physically connect A and B through a second path (bench only!) | Guard trips, alarm latched, no sustained flooding |
| **Failure handling** | Unplug USB, short/open the bus, stop the monitor (no ACK), minimize window, run antivirus scan, sleep/resume | Alarm raised, auto-recovery where defined, no hang; log has the events |
| **CPU / GC** | Perf Monitor | No gen-2 GCs in steady state; no pump gap > T6 target caused by GC |
| **Release / unit tests** | CRC and rewrite-table equality vs current code (≥ 10⁷ random inputs); fake-device tests (partial sends, FIFO full) | All pass before any hardware test |

Safety practice: run all destructive/loop tests on a **bench**, never on the live vehicle/system. Keep the previous executable available as a fallback. Change one phase at a time.

---

## 9. Decisions (resolved in v3)

The nine questions are answered in the table at the top. The items still open are listed there under **Still open**: counter rule, meaning of "50 % load", protocol on the FD-200U channels, which source snapshot is current, and `zlgcan.h`.

---

## 10. Risk register

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| Echo loop floods the buses after echo handling is changed | Medium if echo exists | Very high | Bench test T3 first; loop guard; keep legacy switch |
| Batched transmit behaves differently on the FD-200U | Medium | High | T1 on the FD-200U; fall back to count = 1 |
| `ZCAN_Receive` waits for `len` frames | Unknown | High (latency) | T2; use small `len` or `wait_time = 0` + own idle wait |
| Driver not thread-safe across channels | Unknown | High | T4; start with Option S |
| Windows stalls longer than device FIFO | Medium | High | T5/T6, alarms, PC tuning (High-performance power plan, no USB selective suspend, no hubs, no other CAN software) |
| Behaviour change (padding/CRC/counter) breaks a consumer ECU | Medium | High | Transparency test; legacy switch; staged rollout |
| Unexpected loss from capacity overrun | Low at your stated ≈ 50 % total load (High if each side is really 50 %) | High | Capacity rule 2.2/2.3, per-bus load display, alarms; confirm open item 2 |
| Missing termination at the cut ends | Medium | Medium | Keep **Term** on for both channels or fit resistors (2.3) |
| PC/app failure isolates the segments | Accepted (Q8) | Medium | Watchdog alarm and log; no hardware bypass |

---

## 11. Suggested order of work (v3)
1. Answer the open items (counter rule, "50 % load", protocol, source snapshot) and upload `zlgcan.h`.
2. Phase 0 on the FD-200U (T1–T6, counters, loss tester, jitter and latency baseline).
3. Phase 1 (transparency/safety fixes with a legacy switch: pass-through bit-exact, CRC/counter only on rewritten frames, per-ID counters).
4. Phase 2 (ICanPort, FrameProcessor + tests, batched transmit, Option S with spin-first idle, watchdog, loop guard, Release build).
5. Re-measure against the baseline. Do Phase 3/4 only if the numbers ask for it (at ≈ 2,250 fps they probably will not). Phase 5 (health panel, non-blocking Close) is worth doing for operability.
