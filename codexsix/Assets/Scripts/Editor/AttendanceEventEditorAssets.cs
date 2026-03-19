using CodexSix.TopdownShooter.Game;
using CodexSix.UguiRuntime;
using UnityEditor;
using UnityEngine;

namespace CodexSix.TopdownShooter.EditorTools
{
    public static class AttendanceEventEditorAssets
    {
        private const string RootFolder = "Assets/AttendanceDemo";
        private const string PrefabsFolder = RootFolder + "/Prefabs";
        private const string EventCatalogPath = RootFolder + "/AttendanceEventCatalog.asset";
        private const string UiCatalogPath = RootFolder + "/AttendanceUiCatalog.asset";
        private const string LauncherPrefabPath = PrefabsFolder + "/AttendanceLauncherScreen.prefab";
        private const string ModalPrefabPath = PrefabsFolder + "/AttendanceModalPopup.prefab";

        public static AttendanceEventCatalog LoadOrCreateEventCatalog()
        {
            EnsureFolders();

            var catalog = AssetDatabase.LoadAssetAtPath<AttendanceEventCatalog>(EventCatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<AttendanceEventCatalog>();
                AssetDatabase.CreateAsset(catalog, EventCatalogPath);
            }

            if (catalog.Events == null || catalog.Events.Count == 0)
            {
                PopulateDefaultEvents(catalog);
                EditorUtility.SetDirty(catalog);
                AssetDatabase.SaveAssets();
            }

            return catalog;
        }

        public static UiCatalog LoadOrCreateUiCatalog()
        {
            EnsureFolders();

            var launcherPrefab = CreateOrUpdatePrefab<AttendanceLauncherScreen>(LauncherPrefabPath, "AttendanceLauncherScreen");
            var modalPrefab = CreateOrUpdatePrefab<AttendanceModalPopup>(ModalPrefabPath, "AttendanceModalPopup");

            var catalog = AssetDatabase.LoadAssetAtPath<UiCatalog>(UiCatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<UiCatalog>();
                AssetDatabase.CreateAsset(catalog, UiCatalogPath);
            }

            catalog.Screens.Clear();
            catalog.Screens.Add(new UiScreenDefinition
            {
                Id = AttendanceUiController.LauncherScreenId,
                Prefab = launcherPrefab,
                CacheInstance = true
            });

            catalog.Popups.Clear();
            catalog.Popups.Add(new UiPopupDefinition
            {
                Id = AttendanceUiController.ModalPopupId,
                Prefab = modalPrefab
            });

            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            return catalog;
        }

        private static T CreateOrUpdatePrefab<T>(string assetPath, string prefabName) where T : Component
        {
            var prefabRoot = new GameObject(prefabName, typeof(RectTransform), typeof(T));
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, assetPath);
            Object.DestroyImmediate(prefabRoot);

            var savedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            return savedPrefab != null ? savedPrefab.GetComponent<T>() : null;
        }

        private static void PopulateDefaultEvents(AttendanceEventCatalog catalog)
        {
            catalog.Events.Clear();
            catalog.Events.Add(CreateEvent("attendance-5", "5 Days", AttendancePresentationMode.Fixed, new[]
            {
                CreateDay(1, "Day 1", RewardCoins(15)),
                CreateDay(2, "Day 2", RewardGems(1)),
                CreateDay(3, "Day 3", RewardItem(10005, 1)),
                CreateDay(4, "Day 4", RewardCoins(25)),
                CreateDay(5, "Day 5", RewardGems(2), RewardItem(10009, 1))
            }));

            catalog.Events.Add(CreateEvent("attendance-7", "7 Days", AttendancePresentationMode.Fixed, new[]
            {
                CreateDay(1, "Day 1", RewardCoins(10)),
                CreateDay(2, "Day 2", RewardCoins(20)),
                CreateDay(3, "Day 3", RewardGems(1)),
                CreateDay(4, "Day 4", RewardItem(10003, 2)),
                CreateDay(5, "Day 5", RewardCoins(30)),
                CreateDay(6, "Day 6", RewardGems(2)),
                CreateDay(7, "Day 7", RewardCoins(50), RewardItem(10010, 1))
            }));

            catalog.Events.Add(CreateEvent("attendance-14", "14 Days", AttendancePresentationMode.HorizontalScroll, new[]
            {
                CreateDay(1, "Day 1", RewardCoins(5)),
                CreateDay(2, "Day 2", RewardCoins(10)),
                CreateDay(3, "Day 3", RewardItem(10001, 1)),
                CreateDay(4, "Day 4", RewardCoins(15)),
                CreateDay(5, "Day 5", RewardGems(1)),
                CreateDay(6, "Day 6", RewardItem(10007, 2)),
                CreateDay(7, "Day 7", RewardCoins(25)),
                CreateDay(8, "Day 8", RewardCoins(30)),
                CreateDay(9, "Day 9", RewardItem(10008, 2)),
                CreateDay(10, "Day 10", RewardGems(2)),
                CreateDay(11, "Day 11", RewardCoins(35)),
                CreateDay(12, "Day 12", RewardItem(20001, 2)),
                CreateDay(13, "Day 13", RewardCoins(45)),
                CreateDay(14, "Day 14", RewardGems(3), RewardItem(10005, 3))
            }));
        }

        private static AttendanceEventDefinition CreateEvent(
            string eventId,
            string displayName,
            AttendancePresentationMode presentationMode,
            AttendanceDayDefinition[] days)
        {
            var definition = new AttendanceEventDefinition
            {
                EventId = eventId,
                DisplayName = displayName,
                PresentationMode = presentationMode
            };

            definition.Days.Clear();
            for (var i = 0; i < days.Length; i++)
            {
                definition.Days.Add(days[i]);
            }

            return definition;
        }

        private static AttendanceDayDefinition CreateDay(int dayNumber, string title, params AttendanceRewardDefinition[] rewards)
        {
            var day = new AttendanceDayDefinition
            {
                DayNumber = dayNumber,
                Title = title
            };

            day.Rewards.Clear();
            for (var i = 0; i < rewards.Length; i++)
            {
                day.Rewards.Add(rewards[i]);
            }

            return day;
        }

        private static AttendanceRewardDefinition RewardCoins(int amount)
        {
            return new AttendanceRewardDefinition
            {
                Kind = AttendanceRewardKind.Coins,
                Amount = amount
            };
        }

        private static AttendanceRewardDefinition RewardGems(int amount)
        {
            return new AttendanceRewardDefinition
            {
                Kind = AttendanceRewardKind.Gems,
                Amount = amount
            };
        }

        private static AttendanceRewardDefinition RewardItem(int itemId, int amount)
        {
            return new AttendanceRewardDefinition
            {
                Kind = AttendanceRewardKind.Item,
                ItemId = itemId,
                Amount = amount
            };
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets", "AttendanceDemo");
            EnsureFolder(RootFolder, "Prefabs");
        }

        private static void EnsureFolder(string parent, string child)
        {
            var fullPath = $"{parent}/{child}";
            if (!AssetDatabase.IsValidFolder(fullPath))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }
    }
}
