namespace CodexSix.UiKit.Runtime
{
    public interface IUiScreenService
    {
        string? CurrentScreenId { get; }
        void ShowScreen(string screenId);
        bool TryGoBack();
    }
}
