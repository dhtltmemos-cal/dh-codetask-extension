using System;
using System.ComponentModel;
using System.Windows.Input;
using System.Windows.Threading;
using DhCodetaskExtension.Core.Events;
using DhCodetaskExtension.Core.Interfaces;
using DhCodetaskExtension.Core.Models;
using DhCodetaskExtension.Core.Services;

namespace DhCodetaskExtension.ViewModels
{
    public sealed class TodoItemViewModel : INotifyPropertyChanged
    {
        private readonly IEventBus  _eventBus;
        private readonly string     _taskId;
        private DispatcherTimer     _timer;
        private TimeSession         _currentSession;

        public TodoItem Model { get; }

        public string Text
        {
            get => Model.Text;
            set { Model.Text = value; OnPropertyChanged(nameof(Text)); }
        }

        public bool IsDone => Model.IsDone;
        public TodoStatus Status => Model.Status;

        private string _elapsedDisplay = "00:00:00";
        public string ElapsedDisplay
        {
            get => _elapsedDisplay;
            private set { _elapsedDisplay = value; OnPropertyChanged(nameof(ElapsedDisplay)); }
        }

        public bool IsRunning  => Model.Status == TodoStatus.Running;
        public bool IsPaused   => Model.Status == TodoStatus.Paused;
        public bool IsIdle     => Model.Status == TodoStatus.Idle;
        public bool IsComplete => Model.Status == TodoStatus.Done;
        public bool CanStart   => Model.Status == TodoStatus.Idle || Model.Status == TodoStatus.Paused;
        public bool CanPause   => Model.Status == TodoStatus.Running;
        public bool CanStop    => Model.Status == TodoStatus.Running || Model.Status == TodoStatus.Paused;

        public ICommand StartCommand    { get; }
        public ICommand PauseCommand    { get; }
        public ICommand StopCommand     { get; }
        public ICommand CompleteCommand { get; }
        public ICommand DeleteCommand   { get; }

        public event Action<TodoItemViewModel> DeleteRequested;

        public TodoItemViewModel(TodoItem model, IEventBus eventBus, string taskId)
        {
            Model     = model    ?? throw new ArgumentNullException(nameof(model));
            _eventBus = eventBus;
            _taskId   = taskId;

            StartCommand    = new RelayCommand(StartTodo,    () => CanStart);
            PauseCommand    = new RelayCommand(PauseTodo,    () => CanPause);
            StopCommand     = new RelayCommand(StopTodo,     () => CanStop);
            CompleteCommand = new RelayCommand(CompleteTodo, () => !IsComplete);
            DeleteCommand   = new RelayCommand(() => DeleteRequested?.Invoke(this));

            UpdateElapsed();
        }

        // ── Timer state machine ───────────────────────────────────────────

        public void StartTodo()
        {
            if (!CanStart) return;
            _currentSession = new TimeSession { StartTime = DateTime.UtcNow };
            Model.Sessions.Add(_currentSession);
            Model.Status = TodoStatus.Running;
            RaiseStateChanged();
            StartTimer();
            _eventBus?.Publish(new TodoStartedEvent { Todo = Model, TaskId = _taskId });
        }

        public void PauseTodo()
        {
            if (!CanPause) return;
            FinalizeSession(string.Empty);
            Model.Status = TodoStatus.Paused;
            StopTimer();
            RaiseStateChanged();
            _eventBus?.Publish(new TodoPausedEvent { Todo = Model });
        }

        /// <summary>Stop and mark done — counts as completing the TODO item.</summary>
        public void StopTodo()
        {
            if (!CanStop) return;
            if (Model.Status == TodoStatus.Running)
                FinalizeSession(string.Empty);
            Model.IsDone  = true;
            Model.Status  = TodoStatus.Done;
            StopTimer();
            RaiseStateChanged();
            _eventBus?.Publish(new TodoCompletedEvent { Todo = Model, Elapsed = Model.TotalElapsed });
        }

        /// <summary>Alias kept for compatibility — same as StopTodo.</summary>
        public void CompleteTodo() => StopTodo();

        /// <summary>Called by TrackerViewModel when the parent task is paused.</summary>
        public void AutoPause()
        {
            if (Model.Status == TodoStatus.Running) PauseTodo();
        }

        // ── Private helpers ───────────────────────────────────────────────

        private void FinalizeSession(string pauseReason)
        {
            if (_currentSession != null)
            {
                _currentSession.EndTime     = DateTime.UtcNow;
                _currentSession.PauseReason = pauseReason ?? string.Empty;
                _currentSession             = null;
            }
        }

        private void StartTimer()
        {
            StopTimer();
            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += (s, e) => UpdateElapsed();
            _timer.Start();
        }

        private void StopTimer() { _timer?.Stop(); _timer = null; }

        private void UpdateElapsed()
        {
            var ts = Model.TotalElapsed;
            if (Model.Status == TodoStatus.Running && _currentSession != null)
                ts += DateTime.UtcNow - _currentSession.StartTime;
            ElapsedDisplay = string.Format("{0:D2}:{1:D2}:{2:D2}",
                (int)ts.TotalHours, ts.Minutes, ts.Seconds);
        }

        private void RaiseStateChanged()
        {
            OnPropertyChanged(nameof(Status));
            OnPropertyChanged(nameof(IsRunning));
            OnPropertyChanged(nameof(IsPaused));
            OnPropertyChanged(nameof(IsIdle));
            OnPropertyChanged(nameof(IsComplete));
            OnPropertyChanged(nameof(IsDone));
            OnPropertyChanged(nameof(CanStart));
            OnPropertyChanged(nameof(CanPause));
            OnPropertyChanged(nameof(CanStop));
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
