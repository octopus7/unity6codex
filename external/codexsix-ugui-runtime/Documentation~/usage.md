# Usage

## 1) Create a catalog

Create a `UiCatalog` asset and register:
- `UiScreenDefinition`
- `UiPopupDefinition`

Each screen definition needs:
- `Id`
- `Prefab`
- `CacheInstance`

Each popup definition needs:
- `Id`
- `Prefab`

## 2) Add a screen

Create a prefab with a component derived from `UiScreenView`.

```csharp
public sealed class InventoryScreen : UiScreenView
{
    protected override void OnShow()
    {
        // Refresh visible state here.
    }
}
```

Show it with:

```csharp
context.ScreenService.Show("inventory");
```

## 3) Add a popup

Create a prefab with a component derived from `UiPopupView`.

```csharp
public sealed class ConfirmPopup : UiPopupView
{
    public override void Bind(UiPopupRequest request, UiModalHandle handle)
    {
        base.Bind(request, handle);
        // Wire title/body/button labels here.
    }
}
```

Open it with:

```csharp
var result = await context.ModalService.ShowAsync(
    new UiPopupRequest("confirm", "Delete", "Delete this item?", "Delete", "Cancel"));
```

## 4) Read gameplay input blocking

`modalDepth > 0` sets `IsGameplayInputBlocked = true`.

```csharp
if (context.InputBlockService.IsGameplayInputBlocked)
{
    return;
}
```

## 5) Run the sample

Use:
- `Tools > CodexSix > uGUI Runtime > Create Stacked uGUI Demo Scene`

On the first run, the menu copies the sample source to `Assets/CodexSixSamples/StackedUguiDemo`.
After Unity recompiles, run the same menu once more to generate the scene.
