using System;
using System.Collections.Generic;
using System.Linq;
using DhCodetaskExtension.Core.Models;

namespace DhCodetaskExtension.Core.Services
{
    public enum TrackingState { Idle, Running, Paused, Completed }

    /// <summary>
    /// State machine: IDLE -> RUNNING -> PAUSED <-> RUNNING -> COMPLETED.
    /// Thread-safe reads; callers must serialize writes on one thread (UI thread).
    /// </summary>
    public sealed class TimeTrackingService
    {
        private TrackingState  _state = TrackingState.Idle;
        private readonly List<TimeSession> _sessions = new List<TimeSession>();
        private TimeSession    _currentSession;

        public TrackingState State     => _state;
        public DateTime?     StartedAt { get; private set; }

        public TimeSpan GetElapsed()
        {
            double total = 0;
            foreach (var s in _sessions) total += s.ElapsedSeconds;
            if (_currentSession != null && _state == TrackingState.Running)
                total += (DateTime.UtcNow - _currentSession.StartTime).TotalSeconds;
            return TimeSpan.FromSeconds(total);
        }

        public void Start()
        {
            if (_state != TrackingState.Idle) return;
            _state    = TrackingState.Running;
            StartedAt = DateTime.Now;
            _currentSession = new TimeSession { StartTime = DateTime.UtcNow };
        }

        /// <param name="pauseReason">Optional reason recorded in the session.</param>
        public void Pause(string pauseReason = null)
        {
            if (_state != TrackingState.Running) return;
            FinalizeCurrentSession(pauseReason);
            _state = TrackingState.Paused;
        }

        public void Resume()
        {
            if (_state != TrackingState.Paused) return;
            _state          = TrackingState.Running;
            _currentSession = new TimeSession { StartTime = DateTime.UtcNow };
        }

        public List<TimeSession> Stop()
        {
            if (_state == TrackingState.Running) FinalizeCurrentSession(null);
            _state = TrackingState.Completed;
            return new List<TimeSession>(_sessions);
        }

        public void Reset()
        {
            _sessions.Clear();
            _currentSession = null;
            _state   = TrackingState.Idle;
            StartedAt = null;
        }

        public void RestoreFrom(List<TimeSession> sessions, TrackingState state)
        {
            _sessions.Clear();
            if (sessions != null) _sessions.AddRange(sessions);
            _state = state;
            if (_sessions.Count > 0) StartedAt = _sessions[0].StartTime.ToLocalTime();
        }

        /// <summary>
        /// Returns finalized sessions only (no current running session).
        /// </summary>
        public List<TimeSession> GetSessions() => new List<TimeSession>(_sessions);

        /// <summary>
        /// Fix #6: Returns all finalized sessions PLUS a snapshot of the currently-running
        /// session (if any), without mutating runtime state. Used by autosave / BuildWorkLog()
        /// so that elapsed time is not lost if VS crashes before the next Pause/Stop.
        /// The snapshot EndTime is set to UtcNow but the real _currentSession stays open.
        /// </summary>
        public List<TimeSession> GetSessionsWithCurrentSnapshot()
        {
            var result = new List<TimeSession>(_sessions);
            if (_currentSession != null && _state == TrackingState.Running)
            {
                // Create a read-only snapshot — do NOT modify _currentSession
                result.Add(new TimeSession
                {
                    StartTime   = _currentSession.StartTime,
                    EndTime     = DateTime.UtcNow,          // snapshot end = now
                    PauseReason = string.Empty              // not paused, just snapshotting
                });
            }
            return result;
        }

        private void FinalizeCurrentSession(string pauseReason)
        {
            if (_currentSession == null) return;
            _currentSession.EndTime     = DateTime.UtcNow;
            _currentSession.PauseReason = pauseReason ?? string.Empty;
            _sessions.Add(_currentSession);
            _currentSession = null;
        }
    }
}
