# API Overview

## Core
- `UiRuntimeInstaller`: bootstrap installer for runtime services and toolkit presenter.
- `UiContext`: service hub.
- `UiRoot`: root and layer access (`screen-layer`, `modal-layer`, `overlay-layer`).

## State
- `IUiStateChannel<T>`, `UiStateChannel<T>`
- `IUiSignalBus`, `UiSignalBus`

## Navigation / Modal
- `IUiScreenService`, `UiScreenService`
- `IUiModalService`, `UiModalService`
- `PopupService`

## Feedback
- `IToastService`, `ToastService`
- `PopupRequest`, `PopupResult`, `ToastRequest`, `ToastHandle`

## Input
- `IShortcutService`, `ShortcutService`
- `IInputBlockPolicy`, `DefaultInputBlockPolicy`
- `InputContext`, `ShortcutBinding`

## Built-in action keys
Enabled by default in `ShortcutService`:
- `Esc`: close/back
- `Enter`: confirm