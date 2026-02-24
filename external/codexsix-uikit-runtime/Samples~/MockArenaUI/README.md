# Mock Arena UI Sample

This sample demonstrates how to use `com.codexsix.uikit.runtime` without coupling to game-domain types.

## What it includes
- `MockArenaAdapter`: mock gameplay data producer using `IUiStateChannel<T>` and typed signals.
- `MockArenaUiBootstrap`: subscribes to data/events, drives screens, popups, and toasts.
- `MockArenaSampleSceneMenu`: Unity menu utility to create a demo scene.

## Run
1. Import this sample via Package Manager.
2. In Unity Editor open menu: `Tools > CodexSix > UI Kit > Create Mock Arena Sample Scene`.
3. Enter Play mode.
4. Press `I` to toggle inventory screen.
5. Press `Esc`/`Enter` for built-in close/confirm action keys.

## Notes
- Default toasts use scene-scoped channel.
- Achievement toasts use persistent channel (`UiToastChannels.Persistent`).