using System.Runtime.InteropServices;

namespace MidgardStudio.Core.IO;

/// <summary>
/// Creates a filesystem hardlink (same-volume de-dup) with a copy fallback for paths/filesystems that can't
/// hardlink (cross-volume, non-NTFS, network shares). Used by the backup snapshotter so an unchanged file
/// costs ~0 extra disk while each snapshot stays a complete, independently-restorable folder.
/// </summary>
public static class FileLink
{
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateHardLinkW(string lpFileName, string lpExistingFileName, IntPtr lpSecurityAttributes);

    /// <summary>Hardlink <paramref name="linkPath"/> to <paramref name="existingBlob"/>; on any failure
    /// (cross-volume / non-NTFS / network / missing blob) copy <paramref name="copySource"/> instead.
    /// Returns true if a hardlink was created, false if it fell back to a copy.</summary>
    public static bool HardLinkOrCopy(string linkPath, string existingBlob, string copySource)
    {
        try
        {
            if (OperatingSystem.IsWindows() && File.Exists(existingBlob)
                && CreateHardLinkW(linkPath, existingBlob, IntPtr.Zero))
                return true;
        }
        catch { /* fall through to a plain copy */ }

        File.Copy(copySource, linkPath, overwrite: true);
        return false;
    }
}
