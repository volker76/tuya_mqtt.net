using System.Collections.Generic;

namespace tuya_mqtt.net.Helper
{
    public class RateCounter
    {
        private readonly List<DateTime> _counter = new List <DateTime>();
        private readonly TimeSpan _duration;
        public RateCounter(TimeSpan duration)
        {
            _duration = duration;

        }
        public void Clear()
        {
            _counter.Clear();
        }
        public void Count()
        {
            _counter.Add(DateTime.UtcNow);
            Cleanup();
        }

        private void Cleanup()
        {
            IEnumerable<DateTime> enumerableThing = _counter;
            foreach (var x in enumerableThing.Reverse())
            {
                if ((x + _duration) < DateTime.UtcNow )
                    _counter.Remove(x);
            }
        }

        public int Rate
        {
            get
            {
                Cleanup();
                return _counter.Count;
            }
        }
    }
}
