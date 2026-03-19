# Stacked uGUI Demo

This sample demonstrates the `com.codexsix.ugui.runtime` package with:
- single active screen + back history
- async popup stack
- modal backdrop blocking
- gameplay input blocking via `IUiInputBlockService`

## Run
1. Open `Tools > CodexSix > uGUI Runtime > Create Stacked uGUI Demo Scene`.
2. On the first run, the menu copies this sample to `Assets/CodexSixSamples/StackedUguiDemo`.
3. After Unity recompiles, run the same menu again.
4. Enter Play mode.
5. Click the screen buttons to switch screens and open popups.
6. Press `Escape` / `Enter` to test shared cancel/confirm handling.
7. Press `Space` or click the gameplay button to verify gameplay block behavior.
