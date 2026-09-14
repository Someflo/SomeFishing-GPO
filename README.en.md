# SomeFishing GPO

Visual fishing macro for GPO on Windows. Source is included for inspection and local compilation. **Local version 0.8.1.** [Guía en español](README.md).

Use **Idioma / Language** in the sidebar to choose English or Español. Switching is immediate and saved. The Windows OCR language is a separate setting.

## Get started

Extract the entire ZIP and open `SomeFishingGPO.exe`. Requires 64-bit Windows 10/11 and .NET Framework 4.8. No administrator permission is needed. Preserve `ajustes.xml` when updating.

1. Under **Bait**, enter your legendary, rare and common amounts. Enter 0 for unavailable types and click **Apply inventory**. This is required before starting fishing with input enabled.
2. Under **Bait → Bait buttons**, mark the centers of the **Legendary**, **Rare** and **Common** rows you will use. Keep the menu in its usual position; common also needs a point if you will restock it.
3. Equip the rod and cast manually once. Use **F6** to select the entire blue bar with room for sideways movement. Enter or F6 confirms; Esc cancels.
4. Under **Areas**, mark the water point and, if buying, all three purchase buttons. Close any dialogs before starting.
5. Choose restocking under **Home**, check the buttons under **Tests**, finish any open round, enable **Allow clicks and keys**, return to Roblox and press **F8**.

**F10 stops.** F8 while running, switching windows or moving the mouse to the top-left corner also stops and releases input. Starting from the app gives you three seconds to return to the game.

## Inventory and rounds

Bait is used in this order: **legendary → rare → common**, keeping **1 of each type** so its row stays visible. A type is eligible only when its estimated count is greater than 1: starting with 20 legendary bait uses 19 before switching to rare. Common is replenished before consuming its final unit; if buying is unavailable, that reserve is kept.

Selection moves the pointer to your marked point and clicks. **It does not require name or yellow-outline recognition, and does not verify which type was selected.** Check the points in the game and mark them again if the menu, window or resolution changes. The three bait points are separate from the three purchase-dialog buttons.

Inventory is an **estimate** based on the numbers you entered. One bait is deducted when a minigame round is confirmed. A cast without a detected minigame does not consume the estimate, and repeated frames of one round do not cause repeated deductions. Finished rounds include both catches and escapes; **this is not a fish-caught counter**. Cast attempts are displayed separately.

Estimated amounts are saved with settings in the background, keeping slow disk writes off the control loop. Correct and apply them if you use bait outside the macro, notice a mismatch or encounter an uncertain purchase. The app cannot know about bait used while it was not observing.

Optional OCR compares the active row's count with the estimate and shows disagreements. Enable it under **Advanced** and select the optional OCR menu under **Areas**. It never replaces your entered inventory automatically or converts an unreadable count into zero. Choose its Windows language separately. Processing stays local and reader operations have time limits.

The supporting crop follows the active row's counter and separates its characters from the yellow outline. This improves the image sent to the reader; it does not guarantee that Windows OCR will recognize every number.

## Buy common bait

Automatic restocking buys **common bait only**. Stand within E range of the barrel in a position where you can also fish. Mark the text centers of the purchase buttons:

| Point | Purpose |
|---|---|
| Left | Yes and Buy |
| Middle | Editable quantity and final … button |
| Right | No / Cancel, also used during recovery |

These points must remain in the same positions between dialogs. Re-select them after changing the window, resolution or layout. The selection click acts on a screenshot and is not sent to the game.

Enable **Buy bait** under **Home** and choose:

- **Inventory and rounds:** at 2 common bait or less, request enough to reach 300. With 2 remaining, request 298. Threshold and capacity are adjustable. This does not read the dialog's MAX or your Peli balance.
- **Timer:** request 50 common bait every 40 minutes, both adjustable and limited to available room in the configured capacity. It waits until common bait is active; reaching the final reserved unit may trigger earlier restocking without consuming it.

Restocking waits for the round to close, then performs **E → Yes → double-click quantity → type amount → Buy → …**. If the timer expires during a pending cast, it waits for that round to be identified or the bite timeout to expire, preserving the bait deduction. Afterwards the pointer returns gradually to the water and settles before fishing resumes.

## Long sessions and recovery

**Recover temporary failures** under **Home** is enabled when first configuring this version. It allows bounded retries after recoverable problems without requiring idle jumps. Under **Advanced → Long sessions**, set consecutive failures and the initial pause: defaults are 3 and 20 seconds. Repeated failures increase the pause; a completed round or purchase resets the consecutive-failure count.

Adjust the **purchase limit** under **Advanced** for your planned duration. With recovery enabled, this counts submitted orders; failures before Buy do not use it up. The limit is never increased automatically. Home displays elapsed time, purchases and recoveries.

**Advanced → Recovery** contains missing-minigame retries, purchase-phase retries and the phase timeout. Defaults are 2, 2 and 10 seconds. Leave advanced settings unchanged if you are unsure about them.

Fresh images distinguish Yes/No, quantity, the three-dot closing dialog and absent dialogs. The app retries E only while no dialog is visible, Yes only while the question remains, and closing only while the three dots remain. Delayed actions recheck the menu before sending input. Besides retry limits, purchases have a global 90-second limit and recovery is also bounded. **Buy is never sent again after an attempted submission**, including when an input call returns an error.

After a failed phase, recovery uses No/Cancel in the question or quantity menu, or the three dots in the closing menu. Fishing resumes only after the dialog is observed to be closed. With recovery enabled, the app can pause and retry cleanup within the configured limit; it also recovers failed casts. A persistent failure cannot cause indefinite repeated clicks.

An observed completed dialog permits adding the requested amount to the **estimate**. It does not prove that the entered number was accepted or that all bait was delivered. A submitted purchase with an uncertain result receives no inventory credit and is not resubmitted. The app closes the dialog if possible and asks you to correct the inventory before continuing.

## Tests and limitations

**Test purchase** performs one purchase of your chosen amount without waiting for OCR, the threshold or the timer. **It spends in-game Peli** and does not start fishing afterwards. **Test without bait** simulates depletion to exercise the configured action. Both need input permission and Roblox in the foreground. F10 cancels. The log is saved to `ultima-prueba.txt`.

Fishing finds the blue bar's actual borders rather than requiring it to fill a fixed percentage of the selected area's height. It tracks the white marker and gray gap inside those borders and ignores green progress. Leave room for sideways movement; multiple detected bars require a narrower selection.

A brief image loss keeps the last control decision for up to 180 ms after the last valid detection. Longer loss releases the click without inventing a fish position or charging another bait. With recovery enabled, prolonged incomplete detection waits for fresh bar images or confirmed closure, for up to 90 seconds. A recovered bar continues the same round; an unrecoverable one stops. Lost window focus or an uncertain purchase still require intervention.

This version includes **32 checks across accelerated 24-hour and 12-hour sessions**, injecting image losses, missed casts, incorrect OCR and delayed purchase menus. They check independent inventory accounting, the reserve and duplicate-order prevention. **These are not real hours in Roblox and do not verify native input delivery.**

Idle jumps are optional. Detection depends on scale, colors, background and area placement. Simulations cannot establish that Roblox received an input. Catch rates, preventing disconnects and retaining items are not guaranteed.

## Build and transparency

Run `compilar.cmd` with .NET Framework 4.8 and the Windows 10/11 SDK installed. Compilation uses local tools and downloads no packages. Running the distributed executable does not require the SDK.

`SomeFishingGPO.exe --self-test output-folder` runs simulated checks without game input. Build-specific results belong in `docs/VERIFICACION.txt`. Source covers inventory and reserves (`ManualInventory.cs`, `EngineInventory.cs`), session recovery (`LongSession.cs`), optional OCR row detection (`BaitSelection.cs`), purchase recovery (`DirectPurchase.cs`, `ShopVisual.cs`), guarded input (`Native.cs`), background settings writes (`AsyncSettingsStore.cs`) and OCR time limits (`WinRtWait.cs`). Translations are embedded in the executable.

The app does not use network connections, download updates, inject code or read game memory. Antivirus results cannot establish an absolute guarantee. See [SECURITY.md](SECURITY.md). Source is provided **without granting a license at this time**; [NOTICE.md](NOTICE.md) is preserved.
