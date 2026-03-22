using System;

namespace DhCodetaskExtension.Core.Models
{
    public class TimeSession
    {
        public DateTime  StartTime    { get; set; }
        public DateTime? EndTime      { get; set; }

        /// <summary>
        /// Reason provided when this session was ended (paused/stopped).
        /// Empty for sessions ended via Stop/Complete.
        /// </summary>
        public string PauseReason { get; set; } = string.Empty;

        public double ElapsedSeconds
        {
            get
            {
                if (EndTime.HasValue)
                    return (EndTime.Value - StartTime).TotalSeconds;
                return (DateTime.UtcNow - StartTime).TotalSeconds;
            }
        }

        public TimeSpan Duration => TimeSpan.FromSeconds(ElapsedSeconds);
    }
}
