using System;

namespace DhCodetaskExtension
{
    internal static class PackageGuids
    {
        public const string PackageGuidString    = "D1E2F3A4-B5C6-7890-ABCD-EF0123456789";
        public static readonly Guid PackageGuid  = new Guid(PackageGuidString);

        public const string CommandSetGuidString = "E2F3A4B5-C6D7-8901-BCDE-F01234567890";
        public static readonly Guid CommandSetGuid = new Guid(CommandSetGuidString);
    }

    internal static class PackageIds
    {
        // Legacy (kept for compatibility)
        public const int ShowMainWindowId    = 0x0100;
        // CmdIdSettings (0x0400) removed — replaced by CmdIdJsonSettings
        public const int CmdIdJsonSettings  = 0x0500;
        public const int TopLevelMenu       = 0x1400;
        public const int TopLevelMenuGroup  = 0x1500;

        // DevTaskTracker v3.0 commands
        public const int ShowTrackerWindowId  = 0x0200;
        public const int ShowHistoryWindowId  = 0x0300;
        public const int ShowTaskSettingsId   = 0x0600; // now opens JSON settings
        public const int TrackerMenuGroup     = 0x1600;

        // v3.1 — Logging & Config quick-access
        public const int CmdIdOpenLogFile    = 0x0700;
        public const int CmdIdOpenConfigFile = 0x0800;
        public const int UtilityMenuGroup    = 0x1700;
    }
}
