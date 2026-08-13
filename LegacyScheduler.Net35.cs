#if NET35
using System;
using System.Collections.Generic;
using System.Windows.Threading;

namespace PetFriends
{
    internal sealed class LegacyScheduler
    {
        private abstract class WorkItem
        {
            public abstract bool Tick(DateTime now);
        }

        private sealed class DelayItem : WorkItem
        {
            private readonly DateTime _dueAt;
            private readonly Action _action;

            public DelayItem(int milliseconds, Action action)
            {
                _dueAt = DateTime.UtcNow.AddMilliseconds(milliseconds);
                _action = action;
            }

            public override bool Tick(DateTime now)
            {
                if (now < _dueAt) return false;
                _action();
                return true;
            }
        }

        private sealed class TweenItem : WorkItem
        {
            private readonly DateTime _startedAt = DateTime.UtcNow;
            private readonly int _milliseconds;
            private readonly Func<bool> _isValid;
            private readonly Action<double> _update;
            private readonly Action<bool> _completed;

            public TweenItem(int milliseconds, Func<bool> isValid, Action<double> update, Action<bool> completed)
            {
                _milliseconds = Math.Max(1, milliseconds);
                _isValid = isValid;
                _update = update;
                _completed = completed;
            }

            public override bool Tick(DateTime now)
            {
                if (_isValid != null && !_isValid())
                {
                    if (_completed != null) _completed(false);
                    return true;
                }

                double progress = (now - _startedAt).TotalMilliseconds / _milliseconds;
                if (progress >= 1)
                {
                    _update(1);
                    if (_completed != null) _completed(true);
                    return true;
                }

                double eased = 1 - Math.Pow(1 - Math.Max(0, progress), 2);
                _update(eased);
                return false;
            }
        }

        private readonly DispatcherTimer _timer;
        private readonly List<WorkItem> _items = new List<WorkItem>();
        private readonly List<WorkItem> _pending = new List<WorkItem>();
        private bool _isTicking;

        public LegacyScheduler()
        {
            _timer = new DispatcherTimer(DispatcherPriority.Render);
            _timer.Interval = TimeSpan.FromMilliseconds(33);
            _timer.Tick += OnTick;
            _timer.Start();
        }

        public void After(int milliseconds, Action action)
        {
            Add(new DelayItem(milliseconds, action));
        }

        public void Tween(int milliseconds, Func<bool> isValid, Action<double> update, Action<bool> completed)
        {
            Add(new TweenItem(milliseconds, isValid, update, completed));
        }

        private void Add(WorkItem item)
        {
            if (_isTicking) _pending.Add(item);
            else _items.Add(item);
        }

        private void OnTick(object sender, EventArgs e)
        {
            _isTicking = true;
            DateTime now = DateTime.UtcNow;
            for (int index = _items.Count - 1; index >= 0; index--)
            {
                if (_items[index].Tick(now)) _items.RemoveAt(index);
            }
            _isTicking = false;
            if (_pending.Count > 0)
            {
                _items.AddRange(_pending);
                _pending.Clear();
            }
        }

        public void Stop()
        {
            _timer.Stop();
            _items.Clear();
            _pending.Clear();
        }
    }
}
#endif
