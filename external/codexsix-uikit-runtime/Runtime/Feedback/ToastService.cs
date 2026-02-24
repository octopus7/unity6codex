using System;
using System.Collections.Generic;

namespace CodexSix.UiKit.Runtime
{
    public sealed class ToastService : IToastService
    {
        private sealed class ToastEntry
        {
            public ToastHandle Handle;
            public ToastRequest Request;
            public double EnqueuedAt;
            public double QueueExpiresAt;
            public double VisibleUntil;
            public long Sequence;
        }

        private readonly List<ToastEntry> _visible = new();
        private readonly List<ToastEntry> _pending = new();
        private readonly Dictionary<ToastHandle, ToastEntry> _entriesByHandle = new();
        private readonly Dictionary<string, ToastEntry> _entriesByKey = new(StringComparer.Ordinal);
        private readonly Func<double> _timeProvider;

        private readonly int _maxVisible;
        private readonly float _queueTtlSeconds;

        private ulong _nextHandle = 1UL;
        private long _nextSequence = 1L;

        public event Action ToastsChanged;

        public ToastService(ToastServiceOptions options = default, Func<double> timeProvider = null)
        {
            _maxVisible = Math.Max(1, options.MaxVisible <= 0 ? 3 : options.MaxVisible);
            _queueTtlSeconds = Math.Max(0.1f, options.QueueTtlSeconds <= 0f ? 8f : options.QueueTtlSeconds);
            _timeProvider = timeProvider ?? (() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0);
        }

        public ToastHandle Enqueue(ToastRequest request)
        {
            Tick();
            var now = _timeProvider();
            var normalized = Normalize(request);

            if (TryMergeDuplicate(normalized, now, out var mergedHandle))
            {
                ToastsChanged?.Invoke();
                return mergedHandle;
            }

            var handle = new ToastHandle(_nextHandle++);
            var entry = new ToastEntry
            {
                Handle = handle,
                Request = normalized,
                EnqueuedAt = now,
                QueueExpiresAt = now + _queueTtlSeconds,
                VisibleUntil = now + normalized.DurationSeconds,
                Sequence = _nextSequence++
            };

            _entriesByHandle.Add(handle, entry);
            if (!string.IsNullOrWhiteSpace(normalized.Key))
            {
                _entriesByKey[normalized.Key] = entry;
            }

            if (_visible.Count < _maxVisible)
            {
                _visible.Add(entry);
            }
            else
            {
                _pending.Add(entry);
            }

            ToastsChanged?.Invoke();
            return handle;
        }

        public bool Dismiss(ToastHandle handle)
        {
            if (!handle.IsValid || !_entriesByHandle.TryGetValue(handle, out var entry))
            {
                return false;
            }

            var removed = RemoveEntry(entry);
            if (removed)
            {
                PumpPending(_timeProvider());
                ToastsChanged?.Invoke();
            }

            return removed;
        }

        public void Tick()
        {
            var now = _timeProvider();
            var changed = RemoveExpiredVisible(now);
            changed |= RemoveExpiredPending(now);
            changed |= PumpPending(now);

            if (changed)
            {
                ToastsChanged?.Invoke();
            }
        }

        public IReadOnlyList<ActiveToast> SnapshotActiveToasts()
        {
            Tick();
            var now = _timeProvider();
            var snapshot = new List<ActiveToast>(_visible.Count);
            for (var i = 0; i < _visible.Count; i++)
            {
                var entry = _visible[i];
                var remaining = (float)Math.Max(0.0, entry.VisibleUntil - now);
                snapshot.Add(new ActiveToast(entry.Handle, entry.Request.Key, entry.Request.Message, entry.Request.Priority, remaining));
            }

            return snapshot;
        }

        private bool TryMergeDuplicate(ToastRequest request, double now, out ToastHandle handle)
        {
            handle = ToastHandle.Invalid;
            if (string.IsNullOrWhiteSpace(request.Key))
            {
                return false;
            }

            if (!_entriesByKey.TryGetValue(request.Key, out var existing))
            {
                return false;
            }

            existing.Request = request;
            existing.EnqueuedAt = now;
            existing.Sequence = _nextSequence++;

            if (_visible.Contains(existing))
            {
                existing.VisibleUntil = now + request.DurationSeconds;
            }
            else
            {
                existing.QueueExpiresAt = now + _queueTtlSeconds;
            }

            handle = existing.Handle;
            return true;
        }

        private static ToastRequest Normalize(ToastRequest request)
        {
            var key = request.Key ?? string.Empty;
            var message = string.IsNullOrWhiteSpace(request.Message) ? string.Empty : request.Message.Trim();
            var duration = request.DurationSeconds <= 0.01f ? 2f : request.DurationSeconds;
            return request with { Key = key, Message = message, DurationSeconds = duration };
        }

        private bool RemoveExpiredVisible(double now)
        {
            var changed = false;
            for (var i = _visible.Count - 1; i >= 0; i--)
            {
                var entry = _visible[i];
                if (entry.VisibleUntil > now)
                {
                    continue;
                }

                _visible.RemoveAt(i);
                RemoveEntryMaps(entry);
                changed = true;
            }

            return changed;
        }

        private bool RemoveExpiredPending(double now)
        {
            var changed = false;
            for (var i = _pending.Count - 1; i >= 0; i--)
            {
                var entry = _pending[i];
                if (entry.QueueExpiresAt > now)
                {
                    continue;
                }

                _pending.RemoveAt(i);
                RemoveEntryMaps(entry);
                changed = true;
            }

            return changed;
        }

        private bool PumpPending(double now)
        {
            var changed = false;
            while (_visible.Count < _maxVisible && _pending.Count > 0)
            {
                var nextIndex = FindNextPendingIndex();
                if (nextIndex < 0)
                {
                    break;
                }

                var next = _pending[nextIndex];
                _pending.RemoveAt(nextIndex);
                if (next.QueueExpiresAt <= now)
                {
                    RemoveEntryMaps(next);
                    changed = true;
                    continue;
                }

                next.VisibleUntil = now + next.Request.DurationSeconds;
                _visible.Add(next);
                changed = true;
            }

            return changed;
        }

        private int FindNextPendingIndex()
        {
            if (_pending.Count == 0)
            {
                return -1;
            }

            var bestIndex = 0;
            var best = _pending[0];

            for (var i = 1; i < _pending.Count; i++)
            {
                var candidate = _pending[i];
                if (candidate.Request.Priority > best.Request.Priority)
                {
                    best = candidate;
                    bestIndex = i;
                    continue;
                }

                if (candidate.Request.Priority == best.Request.Priority && candidate.Sequence < best.Sequence)
                {
                    best = candidate;
                    bestIndex = i;
                }
            }

            return bestIndex;
        }

        private bool RemoveEntry(ToastEntry entry)
        {
            var removed = _visible.Remove(entry);
            removed |= _pending.Remove(entry);
            if (!removed)
            {
                return false;
            }

            RemoveEntryMaps(entry);
            return true;
        }

        private void RemoveEntryMaps(ToastEntry entry)
        {
            _entriesByHandle.Remove(entry.Handle);

            if (string.IsNullOrWhiteSpace(entry.Request.Key))
            {
                return;
            }

            if (_entriesByKey.TryGetValue(entry.Request.Key, out var current) && ReferenceEquals(current, entry))
            {
                _entriesByKey.Remove(entry.Request.Key);
            }
        }
    }
}