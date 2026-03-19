# Install

## 1) Add package dependency

Add the local package dependency to your Unity project `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.codexsix.ugui.runtime": "file:../../external/codexsix-ugui-runtime"
  }
}
```

## 2) Add runtime root

Create a GameObject and add:
- `UiRuntimeInstaller`

Assign a `UiCatalog` asset if you want registered screens and popups.

`UiRuntimeInstaller` ensures:
- `Canvas`
- `CanvasScaler`
- `GraphicRaycaster`
- `EventSystem` + `InputSystemUIInputModule`
- `screen-layer`
- `blocker-layer`
- `modal-layer`
- `overlay-layer`

## 3) Access services

```csharp
var context = FindFirstObjectByType<UiContext>();
context.ScreenService.Show("hud");
var result = await context.ModalService.ShowAsync(
    new UiPopupRequest("confirm", "Exit", "Leave this area?", "Yes", "No"));
```
