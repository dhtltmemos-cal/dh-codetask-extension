using System;
using System.IO;

namespace DhCodetaskExtension.Core.Models
{
    /// <summary>
    /// Represents a single match returned by ripgrep content search.
    /// </summary>
    public class RipgrepSearchResult
    {
        public string FilePath     { get; set; } = string.Empty;
        public string RelativePath { get; set; } = string.Empty;
        public int    LineNumber   { get; set; }
        public string LineText     { get; set; } = string.Empty;
        public string FileName     => Path.GetFileName(FilePath);
        public string FolderHint   => Path.GetDirectoryName(RelativePath) ?? string.Empty;
        public string Display      => string.Format("{0}:{1}  {2}", FileName, LineNumber, LineText?.TrimStart());
    }
}
