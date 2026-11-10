using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using MorzaBogow.Simulation;
using UnityEngine;

namespace MorzaBogow.Presentation
{
    /// <summary>
    /// The looks of one match's ships: composed once per ship id when the ship first appears, then kept, so a ship that is
    /// struck, repaired, taken or drawn again keeps its look. A quick save writes them beside the save file together with
    /// the save's hash; loading takes them back only for that very save. Without them (an older save, another file) the
    /// looks are composed again from the ids, which gives the same ships except that a ship taken from the other side
    /// would get its new owner's workshop.
    /// </summary>
    public sealed class ShipLookBook
    {
        public const string Format = "morza-bogow/ship-looks@1";
        readonly Dictionary<int, ShipLook> looks = new Dictionary<int, ShipLook>();

        public int Count => looks.Count;

        /// <summary>The ship's look, composed the first time its id is seen, by the side that has it then.</summary>
        public ShipLook For(Ship ship)
        {
            if (!looks.TryGetValue(ship.Id, out var look) || look == null) looks[ship.Id] = look = ShipLookComposer.Compose(ship.Id, ship.Class, ship.Owner);
            return look;
        }

        public bool TryGet(int shipId, out ShipLook look) => looks.TryGetValue(shipId, out look);

        [Serializable]
        sealed class Stored
        {
            public string Format;
            public string SaveHash;
            public List<ShipLook> Looks = new List<ShipLook>();
        }

        /// <summary>The looks of the ships still afloat, by id, tied to the save they belong to.</summary>
        public string ToJson(ulong saveHash, IEnumerable<int> alive)
        {
            var keep = new HashSet<int>(alive);
            var stored = new Stored
            {
                Format = Format,
                SaveHash = saveHash.ToString("x16"),
                Looks = looks.Where(pair => keep.Contains(pair.Key) && pair.Value != null).OrderBy(pair => pair.Key).Select(pair => pair.Value).ToList(),
            };
            return JsonUtility.ToJson(stored, true);
        }

        /// <summary>The book written for this save, or null for another save, another format or a damaged file.</summary>
        public static ShipLookBook FromJson(string json, ulong saveHash)
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
            if (stored == null || stored.Format != Format || stored.SaveHash != saveHash.ToString("x16") || stored.Looks == null) return null;
            var book = new ShipLookBook();
            foreach (var look in stored.Looks)
                if (look != null && !string.IsNullOrEmpty(look.Profile)) book.looks[look.ShipId] = look;
            return book;
        }

        public static string PathBeside(string savePath) => savePath + ".looks.json";

        public void WriteBeside(string savePath, byte[] saveData, IEnumerable<int> alive) =>
            SaveGame.WriteFile(PathBeside(savePath), Encoding.UTF8.GetBytes(ToJson(SaveGame.Hash(saveData), alive)));

        public static ShipLookBook ReadBeside(string savePath, byte[] saveData)
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
