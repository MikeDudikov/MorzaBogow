using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using MorzaBogow.Simulation;
using UnityEngine;

namespace MorzaBogow.Presentation
{
    /// <summary>A shot that struck a structure: where (in the settlement's frame), from where, when, with what.</summary>
    [Serializable]
    public sealed class DestructionImpact
    {
        public int Event;
        public long Tick;
        public float X, Y, Z, DirX, DirZ;
        public string Category;
    }

    /// <summary>An element that failed in its own right, the event that broke it and the way it fell.</summary>
    [Serializable]
    public sealed class DestructionFailure
    {
        public string Element;
        public int Event;
        public float FallX, FallZ;
    }

    /// <summary>A scar on an element's face: plaster or whitewash knocked off, a broken tile.</summary>
    [Serializable]
    public sealed class DestructionScar
    {
        public string Element;
        public float X, Y, Z, NX, NY, NZ, Size;
        public int Seed;
    }

    /// <summary>
    /// One destruction event as the presentation saw it: its id, the object struck (a settlement's id), the game tick,
    /// where and from which way, the kind of shot, the stage the object was left at and the seed its look is drawn with.
    /// </summary>
    [Serializable]
    public sealed class DestructionEvent
    {
        public int Id;
        public int ObjectId;
        public long Tick;
        public float X, Y, Z, DirX, DirZ;
        public string Category;
        public string Stage;
        public int Seed;
    }

    /// <summary>
    /// What one object's destruction looks like so far, kept so that it looks the same after a load: the latest shots that
    /// struck it, the elements that failed in their own right in the order they failed, the scars. The hull itself is the
    /// game's; this only says which elements show the damage the hull already decides.
    /// </summary>
    [Serializable]
    public sealed class DestructionRecord
    {
        public const int MaxImpacts = 24, MaxEvents = 48;

        public int ObjectId;
        public int NextEvent = 1;
        public List<DestructionImpact> Impacts = new List<DestructionImpact>();
        public List<DestructionFailure> Failures = new List<DestructionFailure>();
        public List<DestructionScar> Scars = new List<DestructionScar>();
        public List<DestructionEvent> Events = new List<DestructionEvent>();

        /// <summary>Notes a shot that struck the object: an impact to break near, and an event.</summary>
        public DestructionEvent Strike(long tick, Vector3 local, Vector3 direction, string category, DestructionStage stage)
        {
            var id = NextEvent++;
            var flat = new Vector2(direction.x, direction.z);
            if (flat.sqrMagnitude > 1e-8f) flat.Normalize();
            Impacts.Add(new DestructionImpact { Event = id, Tick = tick, X = local.x, Y = local.y, Z = local.z, DirX = flat.x, DirZ = flat.y, Category = category });
            if (Impacts.Count > MaxImpacts) Impacts.RemoveAt(0);
            var happened = new DestructionEvent
            {
                Id = id, ObjectId = ObjectId, Tick = tick, X = local.x, Y = local.y, Z = local.z, DirX = flat.x, DirZ = flat.y, Category = category,
                Stage = stage.ToString(), Seed = (int)DestructionHash.Mix((uint)ObjectId, id),
            };
            Events.Add(happened);
            if (Events.Count > MaxEvents) Events.RemoveAt(0);
            return happened;
        }

        public void Clear()
        {
            Impacts.Clear();
            Failures.Clear();
            Scars.Clear();
            Events.Clear();
        }
    }

    /// <summary>
    /// The destruction records of one match's structures, by object id. A quick save writes them beside the save file with
    /// the save's hash, as the ships' looks are written (ShipLookBook); loading takes them back only for that very save.
    /// Without them every structure shows the stage its hull gives, with its failures chosen afresh.
    /// </summary>
    public sealed class DestructionBook
    {
        public const string Format = "morza-bogow/destruction@1";
        readonly Dictionary<int, DestructionRecord> records = new Dictionary<int, DestructionRecord>();

        public int Count => records.Count;

        public DestructionRecord For(int objectId)
        {
            if (!records.TryGetValue(objectId, out var record) || record == null) records[objectId] = record = new DestructionRecord { ObjectId = objectId };
            return record;
        }

        public bool TryGet(int objectId, out DestructionRecord record) => records.TryGetValue(objectId, out record);

        [Serializable]
        sealed class Stored
        {
            public string Format;
            public string SaveHash;
            public List<DestructionRecord> Records = new List<DestructionRecord>();
        }

        /// <summary>The records of the objects still in the game, tied to the save they belong to.</summary>
        public string ToJson(ulong saveHash, IEnumerable<int> present)
        {
            var keep = new HashSet<int>(present);
            var stored = new Stored
            {
                Format = Format,
                SaveHash = saveHash.ToString("x16"),
                Records = records.Where(pair => keep.Contains(pair.Key) && pair.Value != null).OrderBy(pair => pair.Key).Select(pair => pair.Value).ToList(),
            };
            return JsonUtility.ToJson(stored, true);
        }

        /// <summary>The book written for this save, or null for another save, another format or a damaged file.</summary>
        public static DestructionBook FromJson(string json, ulong saveHash)
        {
            Stored stored;
            try
            {
                stored = JsonUtility.FromJson<Stored>(json);
            }
            catch (ArgumentException)
            {
                return null;
            }
            if (stored == null || stored.Format != Format || stored.SaveHash != saveHash.ToString("x16") || stored.Records == null) return null;
            var book = new DestructionBook();
            foreach (var record in stored.Records)
                if (record != null) book.records[record.ObjectId] = record;
            return book;
        }

        public static string PathBeside(string savePath) => savePath + ".destruction.json";

        public void WriteBeside(string savePath, byte[] saveData, IEnumerable<int> present) =>
            SaveGame.WriteFile(PathBeside(savePath), Encoding.UTF8.GetBytes(ToJson(SaveGame.Hash(saveData), present)));

        public static DestructionBook ReadBeside(string savePath, byte[] saveData)
        {
            var path = PathBeside(savePath);
            if (!File.Exists(path)) return null;
            try
            {
                return FromJson(File.ReadAllText(path, Encoding.UTF8), SaveGame.Hash(saveData));
            }
            catch (IOException)
            {
                return null;
            }
        }
    }
}
