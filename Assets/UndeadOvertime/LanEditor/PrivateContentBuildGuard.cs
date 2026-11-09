using System;
using System.IO;
using System.Security;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace UndeadOvertime.LanPrototype.Editor
{
    /// <summary>
    /// Fail closed for ordinary player builds while local licensed trial content is present.
    /// Git ignores do not keep Resources or referenced assets out of a Unity player.
    /// No private-content player distribution has been approved by this implementation.
    /// </summary>
    public sealed class PrivateContentBuildGuard : IPreprocessBuildWithReport
    {
        public const string PrivateFolderName = "UndeadOvertimePrivate";
        public const string PrivateAssetPath = "Assets/" + PrivateFolderName;
        public int callbackOrder => int.MinValue;

        public void OnPreprocessBuild(BuildReport report)
        {
            ValidateAssetsDirectoryForPlayerBuild(Application.dataPath);
        }

        // Public for focused EditMode tests against synthetic temporary directories.
        // This method only inspects filesystem entries, never their licensed contents.
        public static void ValidateAssetsDirectoryForPlayerBuild(string assetsDirectory)
        {
            try
            {
                // Enumerating, rather than Directory.Exists, makes unreadable/missing Assets fail closed.
                foreach (string entry in Directory.EnumerateFileSystemEntries(assetsDirectory))
                {
                    if (!string.Equals(Path.GetFileName(entry), PrivateFolderName, StringComparison.OrdinalIgnoreCase)) continue;
                    FileAttributes attributes = File.GetAttributes(entry);
                    if ((attributes & FileAttributes.ReparsePoint) != 0 || (attributes & FileAttributes.Directory) == 0)
                        throw Blocked("The private-content boundary is a link or a file instead of a normal directory.");
                    foreach (string unused in Directory.EnumerateFileSystemEntries(entry))
                        throw Blocked("Local private trial content is present.");
                }
            }
            catch (IOException)
            {
                throw Blocked("The private-content boundary could not be inspected.");
            }
            catch (UnauthorizedAccessException)
            {
                throw Blocked("The private-content boundary could not be inspected.");
            }
            catch (SecurityException)
            {
                throw Blocked("The private-content boundary could not be inspected.");
            }
        }

        private static BuildFailedException Blocked(string reason)
        {
            return new BuildFailedException(reason + " Player build blocked. " + PrivateAssetPath +
                " is reserved for editor-only trials. Use a clean checkout without private content for public prototype builds. " +
                "Do not move licensed files elsewhere under Assets or remove this guard to bypass it. " +
                "See Documentation/PRIVATE_CONTENT_BOUNDARY.md for the separate distribution review required.");
        }
    }
}
