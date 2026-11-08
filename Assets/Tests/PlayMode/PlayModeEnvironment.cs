using System;
using System.IO;
using NUnit.Framework;

namespace Mahjong.PlayTests
{
    /// <summary>
    /// Keeps the play-mode tests out of the settings of whoever is playing (8.11).
    ///
    /// A <see cref="Mahjong.Unity.MahjongGame"/> reads <c>settings.txt</c> from the player profile on
    /// start, so an ordinary test used to inherit the language, the rules profile, the palette and the
    /// size of the last real game — a test that asks for Russian then failed on a machine where the
    /// player had picked English, and the same run wrote the test's own choices back into the profile.
    ///
    /// The whole assembly therefore runs with <c>MAHJONG_SETTINGS</c> pointing at a file that does not
    /// exist: the same defaults a fresh installation gets. A test that needs a file of its own sets the
    /// variable again for the length of the test (see <c>SettingsTests</c>).
    /// </summary>
    [SetUpFixture]
    public sealed class PlayModeEnvironment
    {
        /// <summary>Folder of the test's own settings file; wiped before and after the run.</summary>
        private static string Folder => Path.Combine(Path.GetTempPath(), "mahjong-playmode-settings");

        private static string File => Path.Combine(Folder, "settings.txt");

        private string? previous = null;

        [OneTimeSetUp]
        public void KeepThePlayersSettingsUnread()
        {
            previous = Environment.GetEnvironmentVariable("MAHJONG_SETTINGS");
            try
            {
                if (Directory.Exists(Folder)) Directory.Delete(Folder, true);
            }
            catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
            {
                // A leftover folder of an earlier run is harmless: the file is read as missing anyway.
            }

            Environment.SetEnvironmentVariable("MAHJONG_SETTINGS", File);
        }

        [OneTimeTearDown]
        public void GiveTheProfileBack()
        {
            Environment.SetEnvironmentVariable("MAHJONG_SETTINGS", previous);
            try
            {
                if (Directory.Exists(Folder)) Directory.Delete(Folder, true);
            }
            catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
            {
                // The temp folder of a test run is allowed to stay.
            }
        }
    }
}
