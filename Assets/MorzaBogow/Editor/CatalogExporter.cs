using System.IO;
using System.Text;
using MorzaBogow.Simulation.Catalog;
using UnityEditor;
using UnityEngine;

namespace MorzaBogow.Editor
{
    /// <summary>
    /// Writes the game's catalogue out for the tools outside the game (qa/runtime-catalog.json): the text of
    /// CatalogExport.ToJson, byte for byte, as UTF-8 without a mark and with LF. Nothing is decided here; a test keeps
    /// the file equal to the catalogue. Run it again after any change of the catalogue.
    /// Batch: -executeMethod MorzaBogow.Editor.CatalogExporter.Run
    /// </summary>
    public static class CatalogExporter
    {
        public static string ExportPath => Path.GetFullPath(Path.Combine(Application.dataPath, "..", CatalogExport.RepositoryPath));

        [MenuItem("Morza Bogów/Katalog/Zapisz eksport katalogu (qa)")]
        public static void Run()
        {
            var catalog = GameCatalog.Default;
            // Bytes, not text: no end of line is translated on the way to the disk.
            File.WriteAllBytes(ExportPath, new UTF8Encoding(false).GetBytes(CatalogExport.ToJson(catalog)));
            Debug.Log($"CatalogExporter: {ExportPath}, {catalog.Identity()}");
        }
    }
}
