using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Game.Core;
using UnityEngine;

namespace Game.Client
{
    // Keeps the local profile in one text file on the device. No accounts and no network: the file is the only copy.
    public sealed class ProfileStore
    {
        public const string FileName = "profile.txt";
        public const string DamagedFileName = "profile.damaged.txt";

        readonly string directory;
        readonly IReadOnlyList<string> starterWeapons;

        public ProfileStore(string directory, IReadOnlyList<string> starterWeapons)
        {
            this.directory = directory;
            this.starterWeapons = starterWeapons;
        }

        // The device's own folder, with the three starter weapons.
        public static ProfileStore ForDevice()
        {
            return new ProfileStore(Application.persistentDataPath, new[] { "sword_and_shield", "staff", "healing_mace" });
        }

        public string Path => System.IO.Path.Combine(directory, FileName);

        // What was saved, or a new character. A file this build cannot read is kept aside as profile.damaged.txt, never overwritten.
        public PlayerProfile Load()
        {
            string text;
            try
            {
                if (!File.Exists(Path))
                {
                    return PlayerProfile.Starter(starterWeapons);
                }

                text = File.ReadAllText(Path, Encoding.UTF8);
            }
            catch (IOException exception)
            {
                Debug.LogWarning("Profile could not be read: " + exception.Message);
                return PlayerProfile.Starter(starterWeapons);
            }

            if (ProfileText.TryParse(text, out var profile, out var error))
            {
                return profile;
            }

            Debug.LogError($"Profile file unreadable ({error}); keeping it as {DamagedFileName} and starting a new character.");
            KeepAside();
            return PlayerProfile.Starter(starterWeapons);
        }

        // Writes to a temporary file first, so a crash mid-save cannot leave half a profile.
        public bool Save(PlayerProfile profile)
        {
            try
            {
                Directory.CreateDirectory(directory);
                var temporary = Path + ".tmp";
                File.WriteAllText(temporary, ProfileText.Write(profile), new UTF8Encoding(false));
                if (File.Exists(Path))
                {
                    File.Delete(Path);
                }

                File.Move(temporary, Path);
                return true;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                Debug.LogWarning("Profile could not be saved: " + exception.Message);
                return false;
            }
        }

        void KeepAside()
        {
            try
            {
                var aside = System.IO.Path.Combine(directory, DamagedFileName);
                if (File.Exists(aside))
                {
                    File.Delete(aside);
                }

                File.Move(Path, aside);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                Debug.LogWarning("Damaged profile could not be moved aside: " + exception.Message);
            }
        }
    }
}
