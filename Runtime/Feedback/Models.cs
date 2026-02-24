using System;

namespace CodexSix.UiKit.Runtime
{
    public enum PopupResultKind
    {
        Confirmed = 0,
        Cancelled = 1,
        Timeout = 2,
        Dismissed = 3
    }

    public enum PopupDismissReason
    {
        Back = 0,
        Programmatic = 1,
        FocusLost = 2,
        Destroyed = 3
    }

    public enum ToastPriority
    {
        Low = 0,
        Normal = 1,
        High = 2,
        Critical = 3
    }

    public readonly record struct PopupRequest(string Id, string Title, string Body, string ConfirmText, string CancelText);

    public readonly record struct PopupResult(PopupResultKind Kind, string PopupId);

    public readonly record struct ToastRequest(string Key, string Message, float DurationSeconds, ToastPriority Priority);

    public readonly struct ToastHandle : IEquatable<ToastHandle>
    {
        public static ToastHandle Invalid => new(0UL);

        public ulong Value { get; }

        public ToastHandle(ulong value)
        {
            Value = value;
        }

        public bool IsValid => Value > 0UL;

        public bool Equals(ToastHandle other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            return obj is ToastHandle other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        public override string ToString()
        {
            return Value.ToString();
        }

        public static bool operator ==(ToastHandle left, ToastHandle right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ToastHandle left, ToastHandle right)
        {
            return !(left == right);
        }
    }

    public readonly record struct ActiveToast(
        ToastHandle Handle,
        string Key,
        string Message,
        ToastPriority Priority,
        float SecondsRemaining);

    public readonly struct ToastServiceOptions
    {
        public int MaxVisible { get; }
        public float QueueTtlSeconds { get; }

        public ToastServiceOptions(int maxVisible = 3, float queueTtlSeconds = 8f)
        {
            MaxVisible = maxVisible;
            QueueTtlSeconds = queueTtlSeconds;
        }
    }

    public static class UiToastChannels
    {
        public const string Default = "default";
        public const string Persistent = "persistent";
    }
}