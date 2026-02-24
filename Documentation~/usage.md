# Usage

## 1) Add runtime root
Create a GameObject and add:
- `UiRuntimeInstaller`
- `UiDocumentBinder`
- `UiToolkitPresenter`

`UiRuntimeInstaller` auto-adds missing components when needed.

## 2) Access services
```csharp
var context = FindFirstObjectByType<UiContext>();
context.ScreenService.ShowScreen("hud");
context.ToastService.Enqueue(new ToastRequest("ready", "UI Ready", 2f, ToastPriority.Normal));
```

## 3) Modals (Task-based)
```csharp
var result = await context.ModalService.ShowAsync(
    new PopupRequest("exit", "Exit", "Leave this area?", "Yes", "No"));
```

## 4) Typed state and signal
```csharp
var hpChannel = new UiStateChannel<int>();
hpChannel.Publish(100);

using var sub = context.SignalBus.Subscribe<MySignal>(OnSignal);
context.SignalBus.Publish(new MySignal());
```

## 5) Toast channels
- Default (scene scoped): `context.ToastService`
- Persistent channel: `context.GetToastService(UiToastChannels.Persistent)`