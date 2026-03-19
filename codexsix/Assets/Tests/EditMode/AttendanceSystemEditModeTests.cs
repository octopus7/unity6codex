using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace CodexSix.TopdownShooter.Tests
{
    public sealed class AttendanceSystemEditModeTests
    {
        [Test]
        public void AttendanceProfileStore_LoadMissingFile_ReturnsEmptyDocument()
        {
            var tempPath = CreateTempJsonPath();
            try
            {
                var store = CreateStore(tempPath);
                var document = Invoke(store, "Load");

                Assert.NotNull(document);
                Assert.AreEqual(0, GetInt(document, "BonusCoins"));

                var events = GetList(document, "Events");
                Assert.NotNull(events);
                Assert.AreEqual(0, events.Count);
            }
            finally
            {
                DeleteIfExists(tempPath);
            }
        }

        [Test]
        public void AttendanceProfileStore_SaveAndLoad_PersistsBonusCoinsAndProgress()
        {
            var tempPath = CreateTempJsonPath();
            try
            {
                var store = CreateStore(tempPath);
                var document = CreateDocument();
                SetInt(document, "BonusCoins", 42);

                var eventRecord = CreateEventRecord();
                SetString(eventRecord, "EventId", "attendance-7");
                SetInt(eventRecord, "ClaimedDayCount", 3);
                SetString(eventRecord, "LastClaimDateKey", "2026-03-19");
                GetList(document, "Events").Add(eventRecord);

                Invoke(store, "Save", document);
                var loaded = Invoke(store, "Load");

                Assert.NotNull(loaded);
                Assert.AreEqual(42, GetInt(loaded, "BonusCoins"));

                var loadedEvents = GetList(loaded, "Events");
                Assert.AreEqual(1, loadedEvents.Count);
                Assert.AreEqual("attendance-7", GetString(loadedEvents[0], "EventId"));
                Assert.AreEqual(3, GetInt(loadedEvents[0], "ClaimedDayCount"));
                Assert.AreEqual("2026-03-19", GetString(loadedEvents[0], "LastClaimDateKey"));
            }
            finally
            {
                DeleteIfExists(tempPath);
            }
        }

        private static object CreateStore(string filePath)
        {
            var storeType = GetRuntimeType("CodexSix.TopdownShooter.Game.AttendanceProfileStore");
            return Activator.CreateInstance(storeType, filePath);
        }

        private static object CreateDocument()
        {
            return Activator.CreateInstance(GetRuntimeType("CodexSix.TopdownShooter.Game.AttendanceProfileDocument"));
        }

        private static object CreateEventRecord()
        {
            return Activator.CreateInstance(GetRuntimeType("CodexSix.TopdownShooter.Game.AttendanceEventProgressRecord"));
        }

        private static object Invoke(object instance, string methodName, params object[] args)
        {
            var method = instance.GetType().GetMethod(methodName);
            Assert.NotNull(method, $"Method not found: {methodName}");
            return method.Invoke(instance, args);
        }

        private static IList GetList(object instance, string fieldName)
        {
            var value = GetField(instance, fieldName);
            return value as IList;
        }

        private static int GetInt(object instance, string fieldName)
        {
            return (int)GetField(instance, fieldName);
        }

        private static string GetString(object instance, string fieldName)
        {
            return (string)GetField(instance, fieldName);
        }

        private static void SetInt(object instance, string fieldName, int value)
        {
            SetField(instance, fieldName, value);
        }

        private static void SetString(object instance, string fieldName, string value)
        {
            SetField(instance, fieldName, value);
        }

        private static object GetField(object instance, string fieldName)
        {
            var field = instance.GetType().GetField(fieldName);
            Assert.NotNull(field, $"Field not found: {fieldName}");
            return field.GetValue(instance);
        }

        private static void SetField(object instance, string fieldName, object value)
        {
            var field = instance.GetType().GetField(fieldName);
            Assert.NotNull(field, $"Field not found: {fieldName}");
            field.SetValue(instance, value);
        }

        private static Type GetRuntimeType(string fullName)
        {
            var type = Type.GetType(fullName + ", Assembly-CSharp");
            if (type != null)
            {
                return type;
            }

            var runtimeAssembly = AppDomain.CurrentDomain
                .GetAssemblies()
                .FirstOrDefault(assembly => string.Equals(assembly.GetName().Name, "Assembly-CSharp", StringComparison.Ordinal));

            type = runtimeAssembly?.GetType(fullName);
            Assert.NotNull(type, $"Runtime type not found: {fullName}");
            return type;
        }

        private static string CreateTempJsonPath()
        {
            return Path.Combine(Path.GetTempPath(), $"attendance_{Guid.NewGuid():N}.json");
        }

        private static void DeleteIfExists(string filePath)
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }
}
