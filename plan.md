# Plan: DBC signal limits and out-of-range values

## Goal

Update the DBC signal builder window so selecting a signal shows its DBC minimum and maximum in the footer. Values outside those declared limits may still be encoded when they fit the signal's bit width; show a red footer warning in that case. Values that cannot fit the bit width remain rejected as they are today.

## Implementation

1. In `DbcSignalBuilderForm`, handle signal selection changes and display the selected signal's minimum and maximum in the existing footer status area. Clearly indicate when either DBC limit is unspecified.
2. In `DbcPayloadEncoder.Encode`, retain numeric parsing and the conversion from physical value to raw value, but distinguish DBC-limit violations from raw bit-width violations:
   - If the physical value is outside a specified DBC minimum or maximum but its converted raw value fits the signed or unsigned range for `BitLength`, allow encoding and report a limit warning.
   - If the converted raw value does not fit the existing signed or unsigned bit-width range, keep rejecting it with the current error behavior.
3. In `DbcSignalBuilderForm.UpdateGeneratedPayload`, keep the generated payload and Insert action enabled for values that only violate DBC limits, and show that warning in red in the footer. Restore the normal footer color when there is no such warning; preserve existing errors and message notices.
4. Ensure the footer status updates consistently when a signal is selected, a value is edited, or a message is changed, without allowing the selection display to hide an active validation warning.

## Validation

- Select signals with both limits, one limit, or no limits and verify the footer display.
- Enter values inside and outside DBC limits that still encode within the signal's bit width. Verify the payload updates, Insert remains enabled, and the warning is red only when outside a declared limit.
- Enter values whose converted raw values exceed signed or unsigned bit-width bounds. Verify encoding and Insert remain rejected as before.
- Check that editing or selecting another signal clears or updates the footer warning appropriately, and that existing message-level warnings still appear.
