using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace DhCodetaskExtension.Core.Models
{
    public class CompletionReport
    {
        [JsonProperty] public string ReportId         { get; private set; }
        [JsonProperty] public DateTime CreatedAt       { get; private set; }
        [JsonProperty] public string TaskId            { get; private set; }
        [JsonProperty] public string TaskTitle         { get; private set; }
        [JsonProperty] public string TaskUrl           { get; private set; }
        [JsonProperty] public string[] Labels          { get; private set; }
        [JsonProperty] public string Description       { get; private set; }
        [JsonProperty] public DateTime StartedAt       { get; private set; }
        [JsonProperty] public DateTime CompletedAt     { get; private set; }
        [JsonProperty] public TimeSpan TotalElapsed    { get; private set; }
        [JsonProperty] public List<TimeSession> Sessions { get; private set; }
        [JsonProperty] public string WorkNotes         { get; private set; }
        [JsonProperty] public string BusinessLogic     { get; private set; }
        [JsonProperty] public List<TodoItem> Todos     { get; private set; }
        [JsonProperty] public string CommitMessage     { get; private set; }
        [JsonProperty] public string GitBranch         { get; private set; }
        [JsonProperty] public string GitCommitHash     { get; private set; }
        [JsonProperty] public bool   WasPushed         { get; private set; }
        [JsonProperty] public string JsonFilePath      { get; private set; }
        [JsonProperty] public string MarkdownFilePath  { get; private set; }

        /// <summary>
        /// SHA-256 checksum of the report JSON (computed before checksum field is set).
        /// Used to detect external tampering.
        /// </summary>
        [JsonProperty] public string Checksum { get; private set; }

        [JsonIgnore] public int      TodoTotal         => Todos?.Count ?? 0;
        [JsonIgnore] public int      TodoDone          => Todos?.Count(t => t.IsDone) ?? 0;
        [JsonIgnore] public TimeSpan TotalTodoElapsed  =>
            TimeSpan.FromSeconds(Todos?.Sum(t => t.TotalElapsed.TotalSeconds) ?? 0);

        [JsonIgnore] public bool ChecksumValid        { get; private set; }

        [JsonConstructor]
        private CompletionReport() { }

        public void SetFilePaths(string jsonPath, string mdPath)
        {
            JsonFilePath     = jsonPath;
            MarkdownFilePath = mdPath;
        }

        /// <summary>
        /// Sets the checksum after serializing the report (without the checksum field).
        /// Should be called by storage service after all other fields are set.
        /// </summary>
        public void SetChecksum(string checksum)
        {
            Checksum = checksum;
        }

        /// <summary>
        /// Verifies the stored checksum against provided content.
        /// Sets ChecksumValid for UI display.
        /// </summary>
        public bool VerifyChecksum(string contentWithoutChecksum)
        {
            if (string.IsNullOrEmpty(Checksum))
            {
                ChecksumValid = false;
                return false;
            }
            ChecksumValid = Core.Services.ChecksumHelper.Verify(contentWithoutChecksum, Checksum);
            return ChecksumValid;
        }

        public static Builder CreateBuilder() => new Builder();

        public sealed class Builder
        {
            private readonly CompletionReport _r = new CompletionReport
            {
                ReportId  = Guid.NewGuid().ToString(),
                CreatedAt = DateTime.Now,
                Labels    = new string[0],
                Sessions  = new List<TimeSession>(),
                Todos     = new List<TodoItem>()
            };

            public Builder TaskId(string v)          { _r.TaskId          = v; return this; }
            public Builder TaskTitle(string v)        { _r.TaskTitle        = v; return this; }
            public Builder TaskUrl(string v)          { _r.TaskUrl          = v; return this; }
            public Builder Labels(string[] v)         { _r.Labels           = v ?? new string[0]; return this; }
            public Builder Description(string v)      { _r.Description      = v; return this; }
            public Builder StartedAt(DateTime v)      { _r.StartedAt        = v; return this; }
            public Builder CompletedAt(DateTime v)    { _r.CompletedAt      = v; return this; }
            public Builder TotalElapsed(TimeSpan v)   { _r.TotalElapsed     = v; return this; }
            public Builder Sessions(List<TimeSession> v) { _r.Sessions      = v ?? new List<TimeSession>(); return this; }
            public Builder WorkNotes(string v)        { _r.WorkNotes        = v; return this; }
            public Builder BusinessLogic(string v)    { _r.BusinessLogic    = v; return this; }
            public Builder Todos(List<TodoItem> v)    { _r.Todos            = v ?? new List<TodoItem>(); return this; }
            public Builder CommitMessage(string v)    { _r.CommitMessage    = v; return this; }
            public Builder GitBranch(string v)        { _r.GitBranch        = v; return this; }
            public Builder GitCommitHash(string v)    { _r.GitCommitHash    = v; return this; }
            public Builder WasPushed(bool v)          { _r.WasPushed        = v; return this; }
            public CompletionReport Build()           => _r;
        }
    }
}
