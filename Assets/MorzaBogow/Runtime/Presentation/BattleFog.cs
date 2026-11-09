using MorzaBogow.Simulation;
using UnityEngine;

namespace MorzaBogow.Presentation
{
    /// <summary>
    /// Draws the logic fog of war as a dark layer over the map. It only reads simulation state. Hidden units
    /// are already deactivated, so this layer adds readability and never decides what the player may know.
    /// </summary>
    public sealed class BattleFog
    {
        public const byte UnexploredAlpha = 245;
        public const byte ExploredAlpha = 130;
        const float Height = 18f;
        const float FadePerSecond = 700f;

        readonly Texture2D texture;
        readonly Color32[] pixels = new Color32[World.FogCells * World.FogCells];
        readonly float[] alpha = new float[World.FogCells * World.FogCells];
        readonly bool[] visible = new bool[World.FogCells * World.FogCells];
        readonly Color32[] display = new Color32[World.FogCells * World.FogCells];
        readonly GameObject plane;
        readonly GameObject[] skirts = new GameObject[4];
        Texture2D skirtTexture;

        public BattleFog(Material material)
        {
            texture = new Texture2D(World.FogCells, World.FogCells, TextureFormat.RGBA32, false, true)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = "Fog of war",
            };
            for (var i = 0; i < alpha.Length; i++) alpha[i] = UnexploredAlpha;

            if (material == null) return;
            plane = GameObject.CreatePrimitive(PrimitiveType.Quad);
            plane.name = "Fog of war";
            Object.Destroy(plane.GetComponent<Collider>());
            var size = (float)(World.FogCells * World.FogCellSize);
            var center = (float)(World.FogOrigin + size * 0.5);
            plane.transform.SetPositionAndRotation(new Vector3(center, Height, center), Quaternion.Euler(90f, 0f, 0f));
            plane.transform.localScale = new Vector3(size, size, 1f);
            var renderer = plane.GetComponent<Renderer>();
            renderer.material = new Material(material);
            renderer.material.SetTexture("_UnlitColorMap", texture);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            // Beyond the grid the world stays dark, so the fog never ends in the middle of the view.
            skirtTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false, true);
            skirtTexture.SetPixel(0, 0, new Color32(6, 10, 18, UnexploredAlpha));
            skirtTexture.Apply(false);
            var reach = size * 3f;
            for (var i = 0; i < skirts.Length; i++)
            {
                var skirt = GameObject.CreatePrimitive(PrimitiveType.Quad);
                skirt.name = "Fog of war outside the map";
                Object.Destroy(skirt.GetComponent<Collider>());
                var horizontal = i < 2;
                var side = i % 2 == 0 ? -1f : 1f;
                var offset = (size + reach) * 0.5f * side;
                skirt.transform.SetPositionAndRotation(
                    horizontal ? new Vector3(center + offset, Height, center) : new Vector3(center, Height, center + offset),
                    Quaternion.Euler(90f, 0f, 0f));
                skirt.transform.localScale = horizontal ? new Vector3(reach, size + reach * 2f, 1f) : new Vector3(size, reach, 1f);
                var skirtRenderer = skirt.GetComponent<Renderer>();
                skirtRenderer.material = new Material(material);
                skirtRenderer.material.SetTexture("_UnlitColorMap", skirtTexture);
                skirtRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                skirts[i] = skirt;
            }
        }

        public static byte TargetAlpha(FogState state) =>
            state == FogState.Visible ? (byte)0 : state == FogState.Explored ? ExploredAlpha : UnexploredAlpha;

        public void Update(World world, int faction, float delta, bool snap = false)
        {
            world.FillVisible(faction, visible);
            var step = snap ? 255f : FadePerSecond * delta;
            for (var j = 0; j < World.FogCells; j++)
            {
                for (var i = 0; i < World.FogCells; i++)
                {
                    var index = j * World.FogCells + i;
                    var explored = (world.Explored[index] & (1 << faction)) != 0;
                    var state = visible[index] ? FogState.Visible : explored ? FogState.Explored : FogState.Unexplored;
                    alpha[index] = Mathf.MoveTowards(alpha[index], TargetAlpha(state), step);
                    pixels[index] = new Color32(6, 10, 18, (byte)alpha[index]);
                }
            }
            SoftenEdges();
            texture.SetPixels32(display);
            texture.Apply(false);
        }

        /// <summary>Display only: softens the cell grid. Game state and AlphaAt keep exact cell values.</summary>
        void SoftenEdges()
        {
            var size = World.FogCells;
            for (var j = 0; j < size; j++)
            {
                for (var i = 0; i < size; i++)
                {
                    var sum = 0f;
                    var weight = 0f;
                    for (var y = -1; y <= 1; y++)
                    {
                        for (var x = -1; x <= 1; x++)
                        {
                            var nx = Mathf.Clamp(i + x, 0, size - 1);
                            var ny = Mathf.Clamp(j + y, 0, size - 1);
                            var w = x == 0 && y == 0 ? 4f : x == 0 || y == 0 ? 2f : 1f;
                            sum += pixels[ny * size + nx].a * w;
                            weight += w;
                        }
                    }
                    display[j * size + i] = new Color32(6, 10, 18, (byte)(sum / weight));
                }
            }
        }

        public byte AlphaAt(int i, int j) => pixels[j * World.FogCells + i].a;

        public void Destroy()
        {
            if (plane != null) Object.Destroy(plane);
            foreach (var skirt in skirts) if (skirt != null) Object.Destroy(skirt);
            if (skirtTexture != null) Object.Destroy(skirtTexture);
            Object.Destroy(texture);
        }
    }
}
