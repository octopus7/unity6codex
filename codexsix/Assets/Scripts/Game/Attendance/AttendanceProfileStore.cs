using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace CodexSix.TopdownShooter.Game
{
    [Serializable]
    public sealed class AttendanceProfileDocument
    {
        public int Version = 1;
        public int BonusCoins;
        public List<AttendanceEventProgressRecord> Events = new();
    }

    [Serializable]
    public sealed class AttendanceEventProgressRecord
    {
        public string EventId = string.Empty;
        public int ClaimedDayCount;
        public string LastClaimDateKey = string.Empty;
    }

    public sealed class AttendanceProfileStore
    {
        private const int CurrentVersion = 1;

        public AttendanceProfileStore(string filePath = null)
        {
            FilePath = string.IsNullOrWhiteSpace(filePath)
                ? GetDefaultPath()
                : filePath;
        }

        public string FilePath { get; }

        public AttendanceProfileDocument Load()
        {
            if (!File.Exists(FilePath))
            {
                return CreateEmptyDocument();
            }

            try
            {
                var json = File.ReadAllText(FilePath);
                if (string.IsNullOrWhiteSpace(json))
                {
                    return CreateEmptyDocument();
                }

                var document = JsonUtility.FromJson<AttendanceProfileDocument>(json);
                return Sanitize(document);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"AttendanceProfileStore load failed: {exception.Message}");
                return CreateEmptyDocument();
            }
        }

        public void Save(AttendanceProfileDocument document)
        {
            var sanitized = Sanitize(document);
            sanitized.Version = CurrentVersion;

            var directoryPath = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrWhiteSpace(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            var json = JsonUtility.ToJson(sanitized, prettyPrint: true) + Environment.NewLine;
            File.WriteAllText(FilePath, json);
        }

        public void Delete()
        {
            if (File.Exists(FilePath))
            {
                File.Delete(FilePath);
            }
        }

        public static string GetDefaultPath()
        {
            return Path.Combine(Application.persistentDataPath, "Attendance", "attendance_demo_profile.json");
        }

        private static AttendanceProfileDocument CreateEmptyDocument()
        {
            return new AttendanceProfileDocument
            {
                Version = CurrentVersion,
                BonusCoins = 0,
                Events = new List<AttendanceEventProgressRecord>()
            };
        }

        private static AttendanceProfileDocument Sanitize(AttendanceProfileDocument document)
        {
            var sanitized = document ?? CreateEmptyDocument();
            sanitized.Version = CurrentVersion;
            sanitized.BonusCoins = Mathf.Max(0, sanitized.BonusCoins);
            sanitized.Events ??= new List<AttendanceEventProgressRecord>();

            for (var i = sanitized.Events.Count - 1; i >= 0; i--)
            {
                var record = sanitized.Events[i];
                if (record == null)
                {
                    sanitized.Events.RemoveAt(i);
                    continue;
                }

                record.EventId = record.EventId ?? string.Empty;
                record.ClaimedDayCount = Mathf.Max(0, record.ClaimedDayCount);
                record.LastClaimDateKey = record.LastClaimDateKey ?? string.Empty;
            }

            return sanitized;
        }
    }
}
