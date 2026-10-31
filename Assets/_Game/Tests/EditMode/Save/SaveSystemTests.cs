using System;
using System.IO;
using System.Linq;
using Game.Core;
using Game.Save;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public class SaveSystemTests
    {
        private ManualClock _clock;
        private InMemorySaveStorage _storage;
        private SaveStore _store;

        [SetUp]
        public void SetUp()
        {
            _clock = new ManualClock();
            _storage = new InMemorySaveStorage();
            _store = new SaveStore(_storage, _clock);
        }

        public static SaveData Sample(long gold = 123)
        {
            var d = new SaveData { CreatedUtc = new DateTime(2026, 2, 3, 4, 5, 6, DateTimeKind.Utc) };
            d.Currencies["gold"] = gold;
            d.Heroes.Add(new HeroSave { HeroId = "hero_a", Unlocked = true, Stars = 2, Shards = 5 });
            d.Heroes.Add(new HeroSave { HeroId = "hero_c", Unlocked = false, Shards = 17 });
            d.Party.AddRange(new[] { "hero_a", null, null, null, null });
            d.Equipment.Add(new EquipmentSave
            {
                InstanceId = Guid.NewGuid().ToString(), TemplateId = "eq_sword", Rarity = Rarity.Epic, EquippedByHeroId = "hero_a", EquippedSlotIndex = 0, AcquiredOrder = 3,
                RolledStats = new System.Collections.Generic.List<StatModifier> { new StatModifier(StatType.Attack, 12.5f), new StatModifier(StatType.MaxHp, 0.05f, ModifierKind.Percent) },
            });
            d.Progress.Fights.Add(new FightProgressSave { FightId = "fight_1", Cleared = true, BestStars = 2, Clears = 3, FirstClearClaimed = true });
            d.Progress.ClaimedChapterRewards.Add("chapter_1");
            d.Progress.SeenStoryBeats.Add("beat_1");
            d.Settings.MusicVolume = 0.25f;
            d.Settings.Language = "ro";
            d.RngState = new ulong[] { 1, ulong.MaxValue };
            return d;
        }

        private static string Canon(SaveData d) => SaveSerializer.ToCanonicalJson(d);

        // ---------- serializer ----------

        [Test]
        public void RoundTrip_PreservesEverything()
        {
            var original = Sample();
            var s = new SaveSerializer();
            var decoded = s.Decode(s.Encode(original, 1, _clock.UtcNow));
            Assert.AreEqual(DecodeStatus.Ok, decoded.Status, decoded.Error);
            Assert.AreEqual(Canon(original), Canon(decoded.Data));
            Assert.AreEqual(Rarity.Epic, decoded.Data.Equipment[0].Rarity);
            Assert.AreEqual(ulong.MaxValue, decoded.Data.RngState[1]);
            Assert.IsNull(decoded.Data.Party[1]);
        }

        [Test]
        public void Encode_UsesReadableEnumNames()
        {
            var text = new SaveSerializer().Encode(Sample(), 1, _clock.UtcNow);
            StringAssert.Contains("Epic", text);
        }

        [Test]
        public void Decode_ChecksumMismatch_IsCorrupt()
        {
            var s = new SaveSerializer();
            var env = JObject.Parse(s.Encode(Sample(), 1, _clock.UtcNow));
            env["payload"] = ((string)env["payload"]).Replace("123", "999");
            var r = s.Decode(env.ToString());
            Assert.AreEqual(DecodeStatus.Corrupt, r.Status);
            StringAssert.Contains("checksum", r.Error);
        }

        [TestCase("")]
        [TestCase("{")]
        [TestCase("not json at all")]
        [TestCase("{\"format\":\"squadfight-save\",\"sha256\":\"x\"}")]
        public void Decode_Garbage_IsCorruptNotException(string text) =>
            Assert.AreEqual(DecodeStatus.Corrupt, new SaveSerializer().Decode(text).Status);

        [Test]
        public void Decode_TruncatedFile_IsCorrupt()
        {
            var text = new SaveSerializer().Encode(Sample(), 1, _clock.UtcNow);
            for (var cut = 1; cut < text.Length; cut += Math.Max(1, text.Length / 40))
                Assert.AreEqual(DecodeStatus.Corrupt, new SaveSerializer().Decode(text.Substring(0, cut)).Status, $"cut at {cut}");
        }

        // ---------- migrations ----------

        [Test]
        public void V1Fixture_MigratesToCurrent()
        {
            var r = new SaveSerializer().Decode(Fixtures.Read("save_v1.json"));
            Assert.AreEqual(DecodeStatus.Ok, r.Status, r.Error);
            Assert.AreEqual(SaveVersions.Current, r.Data.Version);
            Assert.AreEqual(2, r.Data.Heroes.Single(h => h.HeroId == "hero_knight").Stars, "v1 'star' must become 'stars'");
            Assert.AreEqual(12, r.Data.Heroes.Single(h => h.HeroId == "hero_cleric").Shards);
            Assert.AreEqual(345, r.Data.Currencies["gold"]);
            Assert.AreEqual(7, r.Counter);
            Assert.AreEqual(0f, r.Data.Settings.MusicVolume, "v5 mutes audio");
            Assert.AreEqual(1, r.Data.Player.Level, "v6 starts everyone at player level 1");
        }

        [Test]
        public void V3Fixture_MigratesToMutedAudio_KeepsOtherSettings()
        {
            var r = new SaveSerializer().Decode(Fixtures.Read("save_v3.json"));
            Assert.AreEqual(DecodeStatus.Ok, r.Status, r.Error);
            Assert.AreEqual(0f, r.Data.Settings.MusicVolume);
            Assert.AreEqual(0f, r.Data.Settings.SfxVolume);
            Assert.AreEqual("en", r.Data.Settings.Language);
            Assert.AreEqual(2f, r.Data.Settings.BattleSpeed);
        }

        [Test]
        public void MigrationRunner_RejectsGapsAndBadSteps()
        {
            Assert.Throws<ArgumentException>(() => new MigrationRunner(new ISaveMigration[] { new FakeMigration(1, 3) }));
            var runner = new MigrationRunner(new ISaveMigration[] { new FakeMigration(1, 2), new FakeMigration(2, 3) });
            Assert.IsTrue(runner.CanMigrate(1, 3));
            Assert.IsFalse(runner.CanMigrate(1, 4));
            var obj = new JObject { ["version"] = 1 };
            runner.Migrate(obj, 1, 3);
            Assert.AreEqual(3, (int)obj["version"]);
            CollectionAssert.AreEqual(new[] { "1>2", "2>3" }, ((JArray)obj["trail"]).Select(t => (string)t));
            Assert.Throws<InvalidOperationException>(() => runner.Migrate(new JObject(), 3, 4));
        }

        private sealed class FakeMigration : ISaveMigration
        {
            public FakeMigration(int from, int to) { From = from; To = to; }
            public int From { get; }
            public int To { get; }
            public void Apply(JObject payload)
            {
                if (!(payload["trail"] is JArray a)) payload["trail"] = a = new JArray();
                a.Add($"{From}>{To}");
            }
        }

        // ---------- store: recovery ----------

        [Test]
        public void NoFiles_StartsNewGame()
        {
            var r = _store.Load(() => Sample(1));
            Assert.AreEqual(LoadSource.NewGame, r.Source);
            Assert.AreEqual(1, r.Data.Currencies["gold"]);
        }

        [Test]
        public void Write_KeepsPreviousAsBackup()
        {
            _store.Write(Sample(1));
            _store.Write(Sample(2));
            Assert.IsTrue(_storage.Exists(SaveStore.MainKey));
            Assert.IsTrue(_storage.Exists(SaveStore.BackupKey));
            Assert.IsFalse(_storage.Exists(SaveStore.TempKey));
            var r = new SaveStore(_storage, _clock).Load(() => null);
            Assert.AreEqual(LoadSource.Main, r.Source);
            Assert.AreEqual(2, r.Data.Currencies["gold"]);
        }

        [Test]
        public void CorruptMain_FallsBackToBackup_AndQuarantinesMain()
        {
            _store.Write(Sample(1));
            _store.Write(Sample(2));
            _storage.Files[SaveStore.MainKey] = _storage.Files[SaveStore.MainKey].Substring(0, 50);
            var r = new SaveStore(_storage, _clock).Load(() => Sample(0));
            Assert.AreEqual(LoadSource.Backup, r.Source);
            Assert.AreEqual(1, r.Data.Currencies["gold"]);
            Assert.IsTrue(r.Recovered);
            Assert.IsTrue(_storage.Files.Keys.Any(k => k.StartsWith("save.corrupt.") && k.EndsWith(SaveStore.MainKey)), "corrupt main kept for inspection");
        }

        [Test]
        public void MainAndBackupCorrupt_NewGame_KeepsBothCorruptCopies()
        {
            _store.Write(Sample(1));
            _store.Write(Sample(2));
            _storage.Files[SaveStore.MainKey] = "garbage";
            _storage.Files[SaveStore.BackupKey] = "{\"format\":\"squadfight-save\"}";
            LoadResult r = null;
            Assert.DoesNotThrow(() => r = new SaveStore(_storage, _clock).Load(() => Sample(0)));
            Assert.AreEqual(LoadSource.NewGame, r.Source);
            Assert.AreEqual(0, r.Data.Currencies["gold"]);
            Assert.AreEqual(2, _storage.Files.Keys.Count(k => k.StartsWith("save.corrupt.")));
        }

        [Test]
        public void CrashDuringTempWrite_MainUntouched_PartialTempDiscarded()
        {
            _store.Write(Sample(1));
            _storage.TruncateNextWriteAt = 40;
            Assert.Throws<IOException>(() => _store.Write(Sample(2)));
            Assert.IsTrue(_storage.Exists(SaveStore.TempKey), "partial tmp left behind by the crash");
            var r = new SaveStore(_storage, _clock).Load(() => null);
            Assert.AreEqual(LoadSource.Main, r.Source);
            Assert.AreEqual(1, r.Data.Currencies["gold"]);
            Assert.IsFalse(_storage.Exists(SaveStore.TempKey), "partial tmp removed");
        }

        [Test]
        public void CrashAfterTempWrite_BeforeReplace_LoadsNewerTemp()
        {
            _store.Write(Sample(1));
            _storage.FailNextReplace = true;
            Assert.Throws<IOException>(() => _store.Write(Sample(2)));
            var store = new SaveStore(_storage, _clock);
            var r = store.Load(() => null);
            Assert.AreEqual(LoadSource.Temp, r.Source);
            Assert.AreEqual(2, r.Data.Currencies["gold"]);
            store.Write(r.Data);
            var again = new SaveStore(_storage, _clock).Load(() => null);
            Assert.AreEqual(LoadSource.Main, again.Source);
            Assert.AreEqual(2, again.Data.Currencies["gold"]);
        }

        [Test]
        public void MainMissing_TempAndBackupPresent_LoadsTemp()
        {
            // Manual-replace fallback crashed after moving main to bak.
            _store.Write(Sample(1));
            _store.Write(Sample(2));
            _storage.FailNextReplace = true;
            Assert.Throws<IOException>(() => _store.Write(Sample(3)));
            _storage.Move(SaveStore.MainKey, SaveStore.BackupKey);
            var r = new SaveStore(_storage, _clock).Load(() => null);
            Assert.AreEqual(3, r.Data.Currencies["gold"]);
        }

        [Test]
        public void Counter_ContinuesAfterReload()
        {
            _store.Write(Sample(1));
            _store.Write(Sample(2));
            var s2 = new SaveStore(_storage, _clock);
            s2.Load(() => null);
            Assert.AreEqual(2, s2.Counter);
            s2.Write(Sample(3));
            Assert.AreEqual(3, s2.Counter);
        }

        private string FutureEnvelope()
        {
            var payload = "{\"version\":99,\"currencies\":{\"gold\":5}}";
            return new JObject
            {
                ["format"] = SaveSerializer.Format, ["version"] = 99, ["counter"] = 50, ["savedUtc"] = "2030-01-01T00:00:00Z",
                ["sha256"] = SaveSerializer.Checksum(50, payload), ["payload"] = payload,
            }.ToString();
        }

        [Test]
        public void FutureVersionSave_IsNeverOverwritten()
        {
            _storage.Files[SaveStore.MainKey] = FutureEnvelope();
            var manager = new SaveManager(_store, _clock);
            var r = manager.Load(() => Sample(0));
            Assert.IsTrue(r.FutureVersion);
            Assert.IsTrue(r.ReadOnly);
            Assert.IsTrue(manager.ReadOnly);
            manager.Bind(() => r.Data);
            manager.MarkDirty();
            Assert.IsFalse(manager.SaveNow());
            StringAssert.Contains("\"version\": 99", _storage.Files[SaveStore.MainKey]);
            Assert.IsFalse(_storage.Files.Keys.Any(k => k.StartsWith("save.corrupt.")));
        }

        // ---------- manager ----------

        private SaveManager Manager(out SaveData state)
        {
            var m = new SaveManager(_store, _clock);
            var r = m.Load(() => Sample(10));
            var live = r.Data;
            state = live;
            m.Bind(() => live);
            m.SaveNow();
            return m;
        }

        [Test]
        public void Debounce_SavesAtMostOneSecondAfterFirstChange()
        {
            var m = Manager(out var state);
            var writes = _storage.WriteCount;
            state.Currencies["gold"] = 11;
            m.MarkDirty();
            _clock.Advance(0.5);
            state.Currencies["gold"] = 12;
            m.MarkDirty();
            m.Tick();
            Assert.AreEqual(writes, _storage.WriteCount, "no write before the debounce interval");
            _clock.Advance(0.5);
            m.Tick();
            Assert.AreEqual(writes + 1, _storage.WriteCount, "write 1 s after the first change even if changes keep coming");
            Assert.IsFalse(m.IsDirty);
            m.Tick();
            Assert.AreEqual(writes + 1, _storage.WriteCount);
            Assert.AreEqual(12, new SaveStore(_storage, _clock).Load(() => null).Data.Currencies["gold"]);
        }

        [Test]
        public void Flush_WritesImmediately_OnlyWhenDirty()
        {
            var m = Manager(out var state);
            var writes = _storage.WriteCount;
            m.Flush();
            Assert.AreEqual(writes, _storage.WriteCount);
            state.Currencies["gold"] = 99;
            m.MarkDirty();
            m.Flush();
            Assert.AreEqual(writes + 1, _storage.WriteCount);
        }

        [Test]
        public void WriteFailure_DoesNotThrow_StaysDirty_RetriesLater()
        {
            var m = Manager(out var state);
            string failure = null;
            m.SaveFailed += e => failure = e;
            _storage.FailAllWrites = true;
            state.Currencies["gold"] = 77;
            m.MarkDirty();
            _clock.Advance(1);
            Assert.DoesNotThrow(() => m.Tick());
            Assert.IsTrue(m.IsDirty);
            Assert.IsNotNull(failure);
            _storage.FailAllWrites = false;
            _clock.Advance(1);
            m.Tick();
            Assert.IsFalse(m.IsDirty);
            Assert.AreEqual(77, new SaveStore(_storage, _clock).Load(() => null).Data.Currencies["gold"]);
        }

        [Test]
        public void RecoveredLoad_RewritesMainOnFirstTick()
        {
            _store.Write(Sample(1));
            _store.Write(Sample(2));
            _storage.Files[SaveStore.MainKey] = "garbage";
            var m = new SaveManager(new SaveStore(_storage, _clock), _clock);
            var r = m.Load(() => null);
            m.Bind(() => r.Data);
            Assert.IsTrue(m.IsDirty);
            m.Tick();
            Assert.AreEqual(LoadSource.Main, new SaveStore(_storage, _clock).Load(() => null).Source);
        }

        // ---------- real files ----------

        [Test]
        public void FileStorage_AtomicReplaceWithBackup()
        {
            var dir = Path.Combine(Path.GetTempPath(), "squadfight-save-test-" + Guid.NewGuid().ToString("N"));
            try
            {
                var storage = new FileSaveStorage(dir);
                var store = new SaveStore(storage, _clock);
                store.Write(Sample(1));
                store.Write(Sample(2));
                Assert.IsTrue(File.Exists(Path.Combine(dir, SaveStore.MainKey)));
                Assert.IsTrue(File.Exists(Path.Combine(dir, SaveStore.BackupKey)));
                Assert.IsFalse(File.Exists(Path.Combine(dir, SaveStore.TempKey)));
                File.WriteAllText(Path.Combine(dir, SaveStore.MainKey), "{broken");
                var r = new SaveStore(new FileSaveStorage(dir), _clock).Load(() => null);
                Assert.AreEqual(LoadSource.Backup, r.Source);
                Assert.AreEqual(1, r.Data.Currencies["gold"]);
                Assert.AreEqual(1, Directory.GetFiles(dir, "save.corrupt.*").Length);
                Assert.Throws<ArgumentException>(() => storage.Read("../escape"));
            }
            finally
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
            }
        }

        // ---------- review findings (step 03 second pass) ----------

        private string Envelope(string payload, long counter = 5, string format = SaveSerializer.Format) => new JObject
        {
            ["format"] = format, ["version"] = 2, ["counter"] = counter, ["savedUtc"] = "2026-01-01T00:00:00Z",
            ["sha256"] = SaveSerializer.Checksum(counter, payload), ["payload"] = payload,
        }.ToString();

        [Test]
        public void IntactButUnreadableSave_IsKept_AndSessionReadOnly()
        {
            var payload = SaveSerializer.ToCanonicalJson(Sample()).Replace("\"Epic\"", "\"Mythic\"");
            _storage.Files[SaveStore.MainKey] = Envelope(payload);
            var m = new SaveManager(_store, _clock);
            var r = m.Load(() => Sample(0));
            Assert.AreEqual(DecodeStatus.Incompatible, new SaveSerializer().Decode(_storage.Files[SaveStore.MainKey]).Status);
            Assert.IsTrue(r.ReadOnly);
            Assert.IsTrue(m.ReadOnly);
            m.Bind(() => r.Data);
            Assert.IsFalse(m.SaveNow());
            StringAssert.Contains("Mythic", _storage.Files[SaveStore.MainKey]);
            Assert.IsFalse(_storage.Files.Keys.Any(k => k.StartsWith(SaveStore.CorruptPrefix)));
        }

        [Test]
        public void UnknownFormat_IsIncompatibleNotCorrupt()
        {
            Assert.AreEqual(DecodeStatus.Incompatible, new SaveSerializer().Decode("{\"format\":\"other\"}").Status);
        }

        [Test]
        public void ReadError_IsNotCorruption_NothingMovedOrOverwritten()
        {
            _store.Write(Sample(1));
            _store.Write(Sample(2));
            _storage.UnreadableKeys.Add(SaveStore.MainKey);
            var m = new SaveManager(new SaveStore(_storage, _clock), _clock);
            var r = m.Load(() => Sample(0));
            Assert.IsTrue(r.ReadOnly, "unknown-content main must not be overwritten");
            Assert.AreEqual(1, r.Data.Currencies["gold"], "plays from the backup meanwhile");
            Assert.IsTrue(_storage.Exists(SaveStore.MainKey));
            Assert.IsFalse(_storage.Files.Keys.Any(k => k.StartsWith(SaveStore.CorruptPrefix)));
            m.Bind(() => r.Data);
            Assert.IsFalse(m.SaveNow());
        }

        [TestCase("{\"format\":5,\"payload\":[],\"sha256\":{},\"counter\":\"x\"}")]
        [TestCase("{\"format\":\"squadfight-save\",\"payload\":\"{}\",\"sha256\":\"a\",\"counter\":99999999999999999999999}")]
        [TestCase("[1,2,3]")]
        [TestCase("{\"format\":\"squadfight-save\"} trailing")]
        public void Decode_HostileInput_NeverThrows(string text)
        {
            DecodeResult r = default;
            Assert.DoesNotThrow(() => r = new SaveSerializer().Decode(text));
            Assert.AreNotEqual(DecodeStatus.Ok, r.Status);
        }

        [Test]
        public void HugeVersion_InIntactFile_DoesNotThrow()
        {
            var r = new SaveSerializer().Decode(Envelope("{\"version\":99999999999999999999}"));
            Assert.AreEqual(DecodeStatus.Incompatible, r.Status);
        }

        [Test]
        public void SaveNow_BeforeLoad_NeverWrites()
        {
            _store.Write(Sample(7));
            var before = _storage.Files[SaveStore.MainKey];
            var m = new SaveManager(new SaveStore(_storage, _clock), _clock);
            m.Bind(() => Sample(0));
            m.MarkDirty();
            Assert.IsFalse(m.SaveNow());
            m.Flush();
            _clock.Advance(5);
            m.Tick();
            Assert.AreEqual(before, _storage.Files[SaveStore.MainKey]);
        }

        [Test]
        public void ThrowingEventHandlers_DoNotBreakSaving()
        {
            var m = Manager(out _);
            m.Saved += () => throw new Exception("ui bug");
            m.SaveFailed += _ => throw new Exception("ui bug");
            m.MarkDirty();
            Assert.IsTrue(m.SaveNow());
            Assert.IsFalse(m.IsDirty);
            _storage.FailAllWrites = true;
            m.MarkDirty();
            Assert.DoesNotThrow(() => m.SaveNow());
        }

        [Test]
        public void ChosenTemp_IsPromotedToMainOnLoad()
        {
            _store.Write(Sample(1));
            _storage.FailNextReplace = true;
            Assert.Throws<IOException>(() => _store.Write(Sample(2)));
            new SaveStore(_storage, _clock).Load(() => null);
            Assert.IsFalse(_storage.Exists(SaveStore.TempKey));
            Assert.AreEqual(2, new SaveSerializer().Decode(_storage.Files[SaveStore.MainKey]).Data.Currencies["gold"]);
        }

        [Test]
        public void DateLikeStrings_ArePreserved()
        {
            var d = Sample();
            d.Settings.Language = "2026-10-29T10:00:00Z";
            var s = new SaveSerializer();
            Assert.AreEqual("2026-10-29T10:00:00Z", s.Decode(s.Encode(d, 1, _clock.UtcNow)).Data.Settings.Language);
        }

        [Test]
        public void TamperedCounter_FailsChecksum()
        {
            var s = new SaveSerializer();
            var env = JObject.Parse(s.Encode(Sample(), 3, _clock.UtcNow));
            env["counter"] = 1000;
            Assert.AreEqual(DecodeStatus.Corrupt, s.Decode(env.ToString()).Status);
        }

        [Test]
        public void CorruptCopies_AreUniqueAndCapped()
        {
            for (var i = 0; i < SaveStore.MaxCorruptCopies + 3; i++)
            {
                _storage.Files[SaveStore.MainKey] = "garbage " + i;
                new SaveStore(_storage, _clock).Load(() => Sample(0));
            }
            Assert.AreEqual(SaveStore.MaxCorruptCopies, _storage.Files.Keys.Count(k => k.StartsWith(SaveStore.CorruptPrefix)));
        }
    }
}
