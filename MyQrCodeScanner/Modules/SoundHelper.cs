using System;
using System.IO;
using System.Media;

namespace MyQrCodeScanner.Modules
{
    /// <summary>
    /// Plays the scan result prompt sounds. The files are copied next to the
    /// executable under Resources/Sounds so they work from any working directory.
    /// </summary>
    public static class SoundHelper
    {
        private static readonly object SyncRoot = new object();
        private static readonly SoundPlayer DiPlayer = CreatePlayer("di.wav");
        private static readonly SoundPlayer DuPlayer = CreatePlayer("du.wav");

        public static void PlayForScan(bool postProcessMatched)
        {
            if (postProcessMatched && GlobalSettings.playSoundMatched)
            {
                Play(DiPlayer);
                return;
            }

            if (GlobalSettings.playSoundSuccess)
                Play(DuPlayer);
        }

        public static void PlaySuccessPreview()
        {
            Play(DuPlayer);
        }

        public static void PlayMatchedPreview()
        {
            Play(DiPlayer);
        }

        private static SoundPlayer CreatePlayer(string fileName)
        {
            try
            {
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Sounds", fileName);
                if (File.Exists(path))
                    return new SoundPlayer(path);
            }
            catch
            {
            }
            return null;
        }

        private static void Play(SoundPlayer player)
        {
            if (player == null)
                return;

            try
            {
                lock (SyncRoot)
                {
                    player.Play();
                }
            }
            catch
            {
                // A missing or busy audio device must never break scanning.
            }
        }
    }
}
