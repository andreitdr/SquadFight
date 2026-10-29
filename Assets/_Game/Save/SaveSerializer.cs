using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace Game.Save
{
    public enum DecodeStatus
    {
        Ok,
        /// <summary>Damaged/partial file (bad JSON or checksum): safe to quarantine.</summary>
        Corrupt,
        /// <summary>Intact file from a newer app version.</summary>
        FutureVersion,
        /// <summary>Intact file (checksum OK or foreign format) that this build cannot read: must never be overwritten.</summary>
        Incompatible,
    }

    public readonly struct DecodeResult
    {
        public readonly DecodeStatus Status;
        public readonly SaveData Data;
        public readonly long Counter;
        public readonly string Error;

        public DecodeResult(DecodeStatus status, SaveData data, long counter, string error)
        {
            Status = status;
            Data = data;
            Counter = counter;
            Error = error;
        }
    }

    /// <summary>
    /// Wraps the save payload in an envelope with a SHA-256 checksum (corruption detection, not security) and a
    /// monotonic save counter used to pick the newest intact file after a crash.
    /// Envelope: {"format","version","counter","savedUtc","sha256" (of counter + payload),"payload": "&lt;json string&gt;"}.
    /// </summary>
    public sealed class SaveSerializer
    {
        public const string Format = "squadfight-save";

        public static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            ContractResolver = new DefaultContractResolver { NamingStrategy = new CamelCaseNamingStrategy { ProcessDictionaryKeys = false, OverrideSpecifiedNames = false } },
            Converters = new List<JsonConverter> { new StringEnumConverter() },
            ObjectCreationHandling = ObjectCreationHandling.Replace,
            MissingMemberHandling = MissingMemberHandling.Ignore,
            DateTimeZoneHandling = DateTimeZoneHandling.Utc,
            NullValueHandling = NullValueHandling.Include,
            Formatting = Formatting.None,
            FloatParseHandling = FloatParseHandling.Double,
            DateParseHandling = DateParseHandling.None,
        };

        private static readonly JsonSerializer Json = JsonSerializer.Create(Settings);
        private readonly MigrationRunner _migrations;

        public SaveSerializer(MigrationRunner migrations = null) => _migrations = migrations ?? MigrationRunner.Default();

        public string Encode(SaveData data, long counter, DateTime savedUtc)
        {
            var payload = JsonConvert.SerializeObject(data, Settings);
            var envelope = new JObject
            {
                ["format"] = Format,
                ["version"] = data.Version,
                ["counter"] = counter,
                ["savedUtc"] = savedUtc.ToString("o"),
                ["sha256"] = Checksum(counter, payload),
                ["payload"] = payload,
            };
            return envelope.ToString(Formatting.Indented);
        }

        /// <summary>Checksum covers the counter too, so a flipped counter cannot make a stale file win.</summary>
        public static string Checksum(long counter, string payload) => Sha256(counter.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" + payload);

        /// <summary>Never throws.</summary>
        public DecodeResult Decode(string text)
        {
            try { return DecodeUnsafe(text); }
            catch (Exception e) { return Fail("unexpected decode error: " + e.Message); }
        }

        private static JObject ParseObject(string json)
        {
            using (var reader = new JsonTextReader(new System.IO.StringReader(json)) { DateParseHandling = DateParseHandling.None, FloatParseHandling = FloatParseHandling.Double })
            {
                var obj = JObject.Load(reader);
                if (reader.Read() && reader.TokenType != JsonToken.Comment) throw new JsonReaderException("trailing content");
                return obj;
            }
        }

        private static string Str(JObject o, string key) => o[key]?.Type == JTokenType.String ? (string)o[key] : null;

        private static bool TryLong(JObject o, string key, out long value)
        {
            value = 0;
            var t = o[key];
            if (t == null || t.Type != JTokenType.Integer) return false;
            if (!(((JValue)t).Value is long l)) return false; // BigInteger etc.
            value = l;
            return true;
        }

        private DecodeResult DecodeUnsafe(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return Fail("empty file");
            JObject envelope;
            try { envelope = ParseObject(text); }
            catch (Exception e) { return Fail("envelope is not valid JSON: " + e.Message); }

            var format = Str(envelope, "format");
            if (format == null) return Fail("missing format");
            if (format != Format) return new DecodeResult(DecodeStatus.Incompatible, null, 0, $"unknown save format '{format}'");
            var payload = Str(envelope, "payload");
            if (payload == null) return Fail("missing payload");
            if (!TryLong(envelope, "counter", out var counter)) return Fail("missing counter");
            var sha = Str(envelope, "sha256");
            if (sha == null || !string.Equals(sha, Checksum(counter, payload), StringComparison.OrdinalIgnoreCase)) return Fail("checksum mismatch");

            // From here the file is intact: any failure means this build cannot read it, not that it is damaged.
            JObject obj;
            try { obj = ParseObject(payload); }
            catch (Exception e) { return Incompatible(counter, "payload is not valid JSON: " + e.Message); }

            if (!TryLong(obj, "version", out var version) || version <= 0) return Incompatible(counter, "missing version");
            if (version > SaveVersions.Current) return new DecodeResult(DecodeStatus.FutureVersion, null, counter, $"save version {version} is newer than supported {SaveVersions.Current}");

            try
            {
                _migrations.Migrate(obj, (int)version, SaveVersions.Current);
                var data = obj.ToObject<SaveData>(Json);
                if (data == null) return Incompatible(counter, "payload deserialized to null");
                Normalize(data);
                return new DecodeResult(DecodeStatus.Ok, data, counter, null);
            }
            catch (Exception e)
            {
                return Incompatible(counter, "payload could not be read: " + e.Message);
            }
        }

        private static DecodeResult Incompatible(long counter, string error) => new DecodeResult(DecodeStatus.Incompatible, null, counter, error);

        /// <summary>Replaces nulls left by hand-edited or old saves with empty collections.</summary>
        private static void Normalize(SaveData d)
        {
            d.Currencies ??= new Dictionary<string, long>();
            d.Heroes ??= new List<HeroSave>();
            d.Party ??= new List<string>();
            d.Equipment ??= new List<EquipmentSave>();
            d.Progress ??= new ProgressSave();
            d.Progress.Fights ??= new List<FightProgressSave>();
            d.Progress.ClaimedChapterRewards ??= new List<string>();
            d.Progress.SeenStoryBeats ??= new List<string>();
            d.Settings ??= new SettingsSave();
            d.Heroes.RemoveAll(h => h == null || string.IsNullOrEmpty(h.HeroId));
            d.Equipment.RemoveAll(e => e == null || string.IsNullOrEmpty(e.InstanceId));
            d.Progress.Fights.RemoveAll(f => f == null || string.IsNullOrEmpty(f.FightId));
        }

        public static SaveData Clone(SaveData data) =>
            JsonConvert.DeserializeObject<SaveData>(JsonConvert.SerializeObject(data, Settings), Settings);

        /// <summary>Canonical JSON of the payload (used by tests to compare states).</summary>
        public static string ToCanonicalJson(SaveData data) => JsonConvert.SerializeObject(data, Settings);

        public static string Sha256(string text)
        {
            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(text));
                var sb = new StringBuilder(hash.Length * 2);
                foreach (var b in hash) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        private static DecodeResult Fail(string error) => new DecodeResult(DecodeStatus.Corrupt, null, 0, error);
    }
}
