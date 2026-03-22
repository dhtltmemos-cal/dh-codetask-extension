using System;

namespace DhCodetaskExtension.Core.Models
{
    /// <summary>
    /// Represents a discovered .sln or .csproj file entry.
    /// </summary>
    public class SolutionFileEntry
    {
        /// <summary>File name only (e.g. "MyApp.sln").</summary>
        public string FileName     { get; set; } = string.Empty;

        /// <summary>Absolute path to the file.</summary>
        public string FullPath     { get; set; } = string.Empty;

        /// <summary>Path relative to the scan root.</summary>
        public string RelativePath { get; set; } = string.Empty;

        /// <summary>".sln" or ".csproj".</summary>
        public string Extension    { get; set; } = string.Empty;

        /// <summary>Last write time of the file.</summary>
        public DateTime LastModified { get; set; }

        /// <summary>For display: filename + relative folder hint.</summary>
        public string DisplayName => FileName;
        public string FolderHint  => System.IO.Path.GetDirectoryName(RelativePath) ?? string.Empty;
    }
}
