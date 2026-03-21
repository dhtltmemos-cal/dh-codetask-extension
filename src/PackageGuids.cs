using System;

namespace DhCodetaskExtension
{
    /// <summary>
    /// Centralised GUID + command-ID constants.
    /// Values must match the GuidSymbol / IDSymbol entries in CommandTable.vsct.
    ///
    /// HOW TO CUSTOMIZE:
    ///   1. Generate new GUIDs (Tools > Create GUID in Visual Studio, or use online generator)
    ///   2. Replace PackageGuidString and CommandSetGuidString with your new GUIDs
    ///   3. Update CommandTable.vsct GuidSymbol values to match
    ///   4. Update source.extension.vsixmanifest Identity Id
    /// </summary>
    internal static class PackageGuids
    {
        // TODO: Replace with your own generated GUID
        public const string PackageGuidString    = "D1E2F3A4-B5C6-7890-ABCD-EF0123456789";
        public static readonly Guid PackageGuid  = new Guid(PackageGuidString);

        // TODO: Replace with your own generated GUID
        public const string CommandSetGuidString = "E2F3A4B5-C6D7-8901-BCDE-F01234567890";
        public static readonly Guid CommandSetGuid = new Guid(CommandSetGuidString);
    }

    /// <summary>
    /// Integer command IDs — must match IDSymbol values in CommandTable.vsct.
    /// </summary>
    internal static class PackageIds
    {
        // Main tool window show command
        public const int ShowMainWindowId    = 0x0100;

        // Settings dialog (simple form)
        public const int CmdIdSettings      = 0x0400;

        // JSON settings dialog
        public const int CmdIdJsonSettings  = 0x0500;
    }
}
