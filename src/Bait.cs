using System;
using System.Text.RegularExpressions;

namespace SomeFishingGPO
{
    public sealed class BaitReading
    {
        public int? Count;
        public long Sequence;
        public double SampledAt;
        public string Detail = "Contador no leído";
    }

    public static class BaitText
    {
        // Accept only one quantity. Never turn missing text, an 'O', a decimal,
        // or several inventory rows into a zero or a concatenated count.
        public static int? Parse(string text)
        {
            if (string.IsNullOrWhiteSpace(text) || text.Length > 64) return null;
            Match match = Regex.Match(text, @"\A\s*[xX×*]?\s*([0-9]{1,5})\s*\z");
            int value;
            return match.Success && int.TryParse(match.Groups[1].Value, out value) ? (int?)value : null;
        }
    }

    public sealed class BaitMonitor
    {
        private long lastSequence = -1;
        private int? candidate, confirmed;
        private int streak;
        private double sampledAt = double.NegativeInfinity;
        public int? Count { get; private set; }
        public string Detail { get; private set; }
        public bool Empty { get { return Count.HasValue && Count.Value == 0; } }
        public void Reset()
        {
            lastSequence = -1; candidate = confirmed = Count = null;
            streak = 0; sampledAt = double.NegativeInfinity; Detail = "Esperando lectura…";
        }
        public void Update(BaitReading reading, double now)
        {
            if (reading == null || now - reading.SampledAt > 5000 || now < reading.SampledAt)
            { Count = null; candidate = confirmed = null; streak = 0; Detail = "Contador sin lectura reciente"; return; }
            if (reading.Sequence != lastSequence)
            {
                lastSequence = reading.Sequence; sampledAt = reading.SampledAt;
                Detail = reading.Detail;
                if (!reading.Count.HasValue)
                { Count = null; candidate = confirmed = null; streak = 0; return; }
                if (candidate == reading.Count) streak++; else { candidate = reading.Count; streak = 1; }
                int required = candidate.Value == 0 ? 3 : 2;
                // An unconfirmed changed count invalidates the old number.
                if (streak >= required) { confirmed = candidate; Detail = "Cantidad confirmada"; }
                else { confirmed = null; Detail = "Comprobando cantidad…"; }
            }
            Count = now - sampledAt <= 5000 ? confirmed : null;
        }
    }
}
