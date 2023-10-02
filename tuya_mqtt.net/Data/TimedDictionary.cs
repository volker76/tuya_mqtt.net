using Microsoft.Extensions.Caching.Memory;
using System.Collections;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace tuya_mqtt.net.Data
{
    public class TimedDictionary<TKey, TValue> :IEnumerable<KeyValuePair<TKey, TValue>> where TKey:notnull
    {
        public TimeSpan ExpireTime { get; private set; }

        private readonly ReaderWriterLock _rwl = new ReaderWriterLock();
        private readonly Dictionary<TKey, TValue?> _dictionary;
        private readonly MemoryCache _cachedData;
#if DEBUG
        // ReSharper disable once InconsistentNaming
        private readonly int TimeOut = 120000;
#else
        // ReSharper disable once InconsistentNaming
        private const int TimeOut = 100;
#endif

        public event EventHandler<TValue?>? OnListUpdated;

        public TimedDictionary(TimeSpan expireTime)
        {
#if DEBUG
            if (Debugger.IsAttached)
            {
                TimeOut = 120000;
            }
#endif
            ExpireTime = expireTime;
            _cachedData = new MemoryCache(new MemoryCacheOptions());
            _dictionary = new Dictionary<TKey, TValue?>();

            _ = new Timer(CheckTimer, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2));

        }

        public ReadOnlyDictionary<TKey, TValue?> ToReadonlyDictionary()
        {
            _rwl.AcquireReaderLock(TimeOut);
            try {
                CleanupDictionary();
                return new ReadOnlyDictionary<TKey, TValue?>(_dictionary);
            }
            finally { _rwl.ReleaseReaderLock(); }
        }

        private void CheckTimer(object? state)
        {
            _rwl.AcquireReaderLock(TimeOut);
            try
            {
                CleanupDictionary();
            }
            finally { _rwl.ReleaseReaderLock(); }
        }
        
        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            _rwl.AcquireReaderLock(TimeOut);
            try
            {
                CleanupDictionary();
                return _dictionary.GetEnumerator();
            }
            finally { _rwl.ReleaseReaderLock(); }
        }



        private void CleanupDictionary()
        {
            try
            {

                foreach (var i in _dictionary)
                {
                    if (!_cachedData.TryGetValue<byte>(i.Key, out _))
                    {
                        LockCookie lc = _rwl.UpgradeToWriterLock(TimeOut);
                        try
                        {
                            _dictionary.Remove(i.Key);
                        }
                        finally
                        {
                            _rwl.DowngradeFromWriterLock(ref lc);
                        }

                        OnListUpdated?.Invoke(this, i.Value);
                    }
                }
            }
            catch 
            { 
                //ignore
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            _rwl.AcquireReaderLock(TimeOut);
            try
            {
                CleanupDictionary();
                return _dictionary.GetEnumerator();
            }
            finally
            {
                _rwl.ReleaseReaderLock();
            }
        }

        public bool ContainsKey(TKey k)
        {
            _rwl.AcquireReaderLock(TimeOut);
            try
            {
                if (_cachedData.TryGetValue<byte>(k, out _))
                    return true;

                LockCookie lc = _rwl.UpgradeToWriterLock(TimeOut);
                TValue? value;
                try
                {
                    if (_dictionary.TryGetValue(k, out value))
                        _dictionary.Remove(k);
                }
                finally
                {
                    _rwl.DowngradeFromWriterLock(ref lc);
                }
                if (value != null) OnListUpdated?.Invoke(this, value);

                return false;
            }
            finally
            {
                _rwl.ReleaseReaderLock();
            }
        }

        public TValue? this[TKey idx]
        {
            get
            {
                _rwl.AcquireReaderLock(TimeOut);
                try
                {
                    // ReSharper disable once UnusedVariable
                    if (_cachedData.TryGetValue(idx, out var data))
                    {
                        return _dictionary[idx];
                    }

                    TValue? value;
                    LockCookie lc = _rwl.UpgradeToWriterLock(TimeOut);
                    try
                    {
                        if (_dictionary.TryGetValue(idx, out value))
                            _dictionary.Remove(idx);
                    }
                    finally
                    {
                        _rwl.DowngradeFromWriterLock(ref lc);
                    }

                    if (value != null) OnListUpdated?.Invoke(this, value);
                    return _dictionary[idx];
                }
                finally
                {
                    _rwl.ReleaseReaderLock();
                }
            }
            set
            {
                _rwl.AcquireWriterLock(TimeOut);
                try
                {
                    _dictionary[idx] = value;
                    OnListUpdated?.Invoke(this, value);
                    _cachedData.Set<byte>(idx, 1, ExpireTime);
                }
                finally
                {
                    _rwl.ReleaseWriterLock();
                }
            }
        }

        public void Set(TKey key, TValue? value, TimeSpan expire)
        {
            _rwl.AcquireWriterLock(TimeOut);
            try
            {
                _dictionary[key] = value;
                OnListUpdated?.Invoke(this, value);
                _cachedData.Set<byte>(key, 1, expire);
            }
            finally
            {
                _rwl.ReleaseWriterLock();
            }
        }

        public void Set(TKey key, TValue? value)
        {
            Set(key, value, this.ExpireTime);
        }

        public void Clear()
        {
            _rwl.AcquireWriterLock(TimeOut);
            try
            {
                _dictionary.Clear();
                _cachedData.Clear();


            }
            finally
            {
                _rwl.ReleaseWriterLock();
            }
        }
    }
}