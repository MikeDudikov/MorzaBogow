using System.Collections.Generic;
using UnityEngine;

namespace MorzaBogow.Presentation.Landscape
{
    /// <summary>
    /// Dust and rock chips thrown from the face at each blow of a miner's pick: the ore was broken by hand at the face
    /// (report 4.x). One puff of pale dust drifting with the air and a few chips falling back, emitted by hand from two
    /// particle systems per site, from a stable sequence (no game random numbers). Presentation only.
    /// </summary>
    public sealed class OreDust
    {
        readonly ParticleSystem dust, chips;

        public int Blows { get; private set; }
        public ParticleSystem Dust => dust;
        public ParticleSystem Chips => chips;

        /// <summary>Pale dust of marble and schist, and grey chips of the face.</summary>
        public static readonly Color DustColour = new Color(0.8f, 0.75f, 0.66f, 0.5f), ChipColour = new Color(0.46f, 0.43f, 0.4f, 1f);

        public OreDust(Transform parent, Material spray)
        {
            dust = System(parent, "Ore dust", Tinted(spray, "ore dust", DustColour), -0.04f, 240);
            chips = System(parent, "Ore chips", Tinted(spray, "ore chips", ChipColour), 1f, 160);
        }

        static readonly Dictionary<(Material, string), Material> tints = new Dictionary<(Material, string), Material>();

        /// <summary>
        /// A copy of the scene's spray in the colour of the dust or of the chips, made once per source: its HDRP/Unlit
        /// shader ignores the particles' own colours (values only, the same shader variant; the spray stays as it is).
        /// </summary>
        static Material Tinted(Material source, string tag, Color colour)
        {
            if (source == null) return null;
            if (tints.TryGetValue((source, tag), out var made) && made != null) return made;
            made = new Material(source) { name = $"{source.name} ({tag})" };
            foreach (var property in new[] { "_UnlitColor", "_BaseColor", "_Color" })
                if (made.HasProperty(property)) made.SetColor(property, colour);
            tints[(source, tag)] = made;
            return made;
        }

        static ParticleSystem System(Transform parent, string name, Material material, float gravity, int most)
        {
            var item = new GameObject(name);
            item.transform.SetParent(parent, false);
            var system = item.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            // Every value of a particle is set by Blow; a fixed seed keeps anything left to the system the same too.
            system.useAutoRandomSeed = false;
            system.randomSeed = 0xD057u;
            var main = system.main;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = gravity;
            main.maxParticles = most;
            main.startSpeed = 0f;
            var emission = system.emission;
            emission.enabled = false;
            var size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(0.4f, 1f), new Keyframe(1f, gravity < 0f ? 1.6f : 0.8f)));
            var renderer = item.GetComponent<ParticleSystemRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (material != null) renderer.sharedMaterial = material;
            system.Play();
            return system;
        }

        /// <summary>One blow at a point of the face; toward points from the face to the miner.</summary>
        public void Blow(Vector3 face, Vector3 toward)
        {
            Blows++;
            var random = new StableHash.Sequence(StableHash.Mix((uint)Blows, 0xD057u));
            for (var i = 0; i < 6; i++)
            {
                dust.Emit(new ParticleSystem.EmitParams
                {
                    position = face + new Vector3(random.Range(-0.2f, 0.2f), random.Range(-0.15f, 0.15f), random.Range(-0.2f, 0.2f)),
                    velocity = toward * random.Range(0.2f, 0.6f) + Vector3.up * random.Range(0.3f, 0.7f),
                    startSize = random.Range(0.25f, 0.6f),
                    startLifetime = random.Range(1.4f, 2.2f),
                    startColor = DustColour,
                }, 1);
            }
            for (var i = 0; i < 5; i++)
            {
                chips.Emit(new ParticleSystem.EmitParams
                {
                    position = face,
                    velocity = toward * random.Range(1.2f, 2.4f) + Vector3.up * random.Range(0.8f, 1.8f) + new Vector3(random.Range(-0.6f, 0.6f), 0f, random.Range(-0.6f, 0.6f)),
                    startSize = random.Range(0.04f, 0.08f),
                    startLifetime = random.Range(0.5f, 0.8f),
                    startColor = ChipColour,
                }, 1);
            }
        }
    }
}
