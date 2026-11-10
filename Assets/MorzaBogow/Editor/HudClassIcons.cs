using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace MorzaBogow.Editor
{
    /// <summary>
    /// Draws the class icons of the ship destroyer and the bomber (3c part 2) in the style of the interface package's ship
    /// icons: one line colour (measured from archer-ship-128.png), round-ended strokes of the same weight, the same hull
    /// below and the class's mark above it: a ballista with its torsion springs and a heavy bolt, a catapult's arm with a
    /// stone in flight. Writes Hud/Icons/{name}-64.png and -128.png; HudAssetImport sets their import.
    /// Run: -executeMethod MorzaBogow.Editor.HudClassIcons.Generate
    /// </summary>
    public static class HudClassIcons
    {
        const string Folder = "Assets/MorzaBogow/UI/Resources/Hud/Icons";

        /// <summary>Stroke half-width as a share of the icon's size (about 3 px at 64, 6 at 128, as the package draws).</summary>
        const float HalfStroke = 0.024f;

        abstract class Shape
        {
            public abstract float Distance(Vector2 p);
        }

        sealed class Segment : Shape
        {
            readonly Vector2 a, b;
            public Segment(float ax, float ay, float bx, float by) { a = new Vector2(ax, ay); b = new Vector2(bx, by); }
            public override float Distance(Vector2 p)
            {
                var ab = b - a;
                var t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-8f, ab.sqrMagnitude));
                return Vector2.Distance(p, a + ab * t) - HalfStroke;
            }
        }

        sealed class Ring : Shape
        {
            readonly Vector2 c;
            readonly float r;
            public Ring(float x, float y, float radius) { c = new Vector2(x, y); r = radius; }
            public override float Distance(Vector2 p) => Mathf.Abs(Vector2.Distance(p, c) - r) - HalfStroke;
        }

        sealed class Disc : Shape
        {
            readonly Vector2 c;
            readonly float r;
            public Disc(float x, float y, float radius) { c = new Vector2(x, y); r = radius; }
            public override float Distance(Vector2 p) => Vector2.Distance(p, c) - r;
        }

        /// <summary>The hull every ship icon of the package stands on: an open trapezoid with its deck line.</summary>
        static IEnumerable<Shape> Hull()
        {
            yield return new Segment(0.12f, 0.33f, 0.88f, 0.33f);
            yield return new Segment(0.88f, 0.33f, 0.74f, 0.17f);
            yield return new Segment(0.74f, 0.17f, 0.26f, 0.17f);
            yield return new Segment(0.26f, 0.17f, 0.12f, 0.33f);
        }

        /// <summary>Ship destroyer: a ballista on the deck, its springs in their frame, arms swept back and a heavy bolt laid.</summary>
        static IEnumerable<Shape> ShipKiller()
        {
            foreach (var part in Hull()) yield return part;
            // The stand on the deck.
            yield return new Segment(0.38f, 0.33f, 0.48f, 0.56f);
            yield return new Segment(0.58f, 0.33f, 0.48f, 0.56f);
            // The frame of the two spring bundles across the stock.
            yield return new Segment(0.57f, 0.44f, 0.57f, 0.70f);
            yield return new Segment(0.67f, 0.44f, 0.67f, 0.70f);
            yield return new Segment(0.57f, 0.70f, 0.67f, 0.70f);
            yield return new Segment(0.57f, 0.44f, 0.67f, 0.44f);
            // Short arms swept back from the springs.
            yield return new Segment(0.62f, 0.70f, 0.48f, 0.80f);
            yield return new Segment(0.62f, 0.44f, 0.50f, 0.38f);
            // The stock and the heavy bolt with its broad head.
            yield return new Segment(0.18f, 0.57f, 0.84f, 0.57f);
            yield return new Segment(0.92f, 0.57f, 0.82f, 0.66f);
            yield return new Segment(0.92f, 0.57f, 0.82f, 0.48f);
            yield return new Segment(0.82f, 0.66f, 0.82f, 0.48f);
        }

        /// <summary>Bomber: a heavy catapult's frame on the deck, its arm thrown up and the stone flying on its arc.</summary>
        static IEnumerable<Shape> Bomber()
        {
            foreach (var part in Hull()) yield return part;
            yield return new Segment(0.26f, 0.42f, 0.70f, 0.42f);
            yield return new Segment(0.30f, 0.33f, 0.30f, 0.42f);
            yield return new Segment(0.66f, 0.33f, 0.66f, 0.42f);
            // The A-frame and its cross beam where the arm stops.
            yield return new Segment(0.38f, 0.42f, 0.48f, 0.66f);
            yield return new Segment(0.58f, 0.42f, 0.48f, 0.66f);
            // The arm from its axle up past the beam to the cup.
            yield return new Segment(0.58f, 0.46f, 0.36f, 0.78f);
            yield return new Ring(0.33f, 0.82f, 0.05f);
            // The stone, and dots of its path.
            yield return new Disc(0.80f, 0.80f, 0.065f);
            foreach (var (x, y) in new[] { (0.47f, 0.88f), (0.58f, 0.90f), (0.69f, 0.87f) }) yield return new Disc(x, y, 0.022f);
        }

        [MenuItem("Morza Bogów/Interfejs/Ikony klas 3c")]
        public static void Generate()
        {
            var colour = MeasureLineColour();
            foreach (var (name, shapes) in new[] { ("ship-killer", new List<Shape>(ShipKiller())), ("bomber", new List<Shape>(Bomber())) })
            {
                foreach (var size in new[] { 64, 128 })
                {
                    var path = $"{Folder}/{name}-{size}.png";
                    File.WriteAllBytes(path, Draw(shapes, size, colour).EncodeToPNG());
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                    Debug.Log($"[HudClassIcons] {path}");
                }
            }
            AssetDatabase.SaveAssets();
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        /// <summary>The package's line colour: the mean of archer-ship-128's fully opaque pixels.</summary>
        static Color MeasureLineColour()
        {
            var source = new Texture2D(2, 2);
            source.LoadImage(File.ReadAllBytes($"{Folder}/archer-ship-128.png"));
            var sum = Vector3.zero;
            var count = 0;
            foreach (var pixel in source.GetPixels())
            {
                if (pixel.a < 0.98f) continue;
                sum += new Vector3(pixel.r, pixel.g, pixel.b);
                count++;
            }
            Object.DestroyImmediate(source);
            var mean = count > 0 ? sum / count : new Vector3(0.91f, 0.86f, 0.75f);
            Debug.Log($"[HudClassIcons] line colour {mean.x:0.000} {mean.y:0.000} {mean.z:0.000} from {count} pixels");
            return new Color(mean.x, mean.y, mean.z, 1f);
        }

        /// <summary>Coverage of every pixel from 4×4 samples against the strokes' distance; the colour is straight, alpha the coverage.</summary>
        static Texture2D Draw(List<Shape> shapes, int size, Color colour)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color[size * size];
            const int Samples = 4;
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var covered = 0;
                for (var sy = 0; sy < Samples; sy++)
                for (var sx = 0; sx < Samples; sx++)
                {
                    var p = new Vector2((x + (sx + 0.5f) / Samples) / size, (y + (sy + 0.5f) / Samples) / size);
                    foreach (var shape in shapes)
                    {
                        if (shape.Distance(p) > 0f) continue;
                        covered++;
                        break;
                    }
                }
                pixels[y * size + x] = new Color(colour.r, colour.g, colour.b, covered / (float)(Samples * Samples));
            }
            texture.SetPixels(pixels);
            texture.Apply(false);
            return texture;
        }
    }
}
