using System.IO;
using MorzaBogow.Presentation;
using UnityEditor;
using UnityEngine;

namespace MorzaBogow.Editor
{
    /// <summary>
    /// Writes the crews' documents from the code that draws them: the matrix of classes and crews
    /// (docs/art/ShipCrewCoverage.json) and the manifest of roles (docs/art/CrewRoles.json). Tests keep both equal to
    /// the code. Batch: -executeMethod MorzaBogow.Editor.CrewExport.Run
    /// </summary>
    public static class CrewExport
    {
        public static string CoveragePath => Path.Combine(Application.dataPath, "..", "docs", "art", "ShipCrewCoverage.json");
        public static string RolesPath => Path.Combine(Application.dataPath, "..", "docs", "art", "CrewRoles.json");

        [MenuItem("Morza Bogów/Załogi/Zapisz macierz i manifest ról")]
        public static void Run()
        {
            File.WriteAllText(CoveragePath, CrewCoverage.Json(), new System.Text.UTF8Encoding(false));
            File.WriteAllText(RolesPath, CrewRoles.Json(), new System.Text.UTF8Encoding(false));
            Debug.Log($"CrewExport: {CoveragePath}, {RolesPath}");
        }
    }
}
