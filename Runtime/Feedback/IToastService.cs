namespace CodexSix.UiKit.Runtime
{
    public interface IToastService
    {
        ToastHandle Enqueue(ToastRequest request);
        bool Dismiss(ToastHandle handle);
    }
}