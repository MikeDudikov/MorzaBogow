using System.Collections.Generic;
using UnityEngine;

namespace MorzaBogow.Presentation
{
    /// <summary>
    /// Short-lived combat effects from a bounded pool. The caller decides visibility, so effects of
    /// hidden events are never created. Nothing here affects the simulation.
    /// </summary>
    public sealed class BattleEffects
    {
        const int MaxEffects = 160;

        enum Kind { Arrow, Flash, Rope, Ping }

        sealed class Effect
        {
            public Kind Kind;
            public Transform Item;
            public Vector3 From, To;
            public float Age, Life, Size;
        }

        readonly PlaceholderMaterials materials;
        readonly List<Effect> active = new List<Effect>();
        readonly Dictionary<Kind, Stack<Transform>> pool = new Dictionary<Kind, Stack<Transform>>();
        readonly List<(Transform item, float age)> sinking = new List<(Transform, float)>();

        public BattleEffects(PlaceholderMaterials materials)
        {
            this.materials = materials;
        }

        public int ActiveCount => active.Count;
        public int SinkingCount => sinking.Count;

        public void Arrows(Vector3 from, Vector3 to, int count)
        {
            for (var i = 0; i < Mathf.Min(count, 4); i++)
            {
                var spread = new Vector3(Mathf.Sin(i * 2.3f) * 2.5f, 0f, Mathf.Cos(i * 1.7f) * 2.5f);
                Add(Kind.Arrow, from + Vector3.up * 3f, to + spread + Vector3.up, 0.55f + i * 0.06f, 1f);
            }
        }

        public void Flash(Vector3 at, float size) => Add(Kind.Flash, at + Vector3.up * 2f, at, 0.6f, size);
        public void Rope(Vector3 from, Vector3 to, float life) => Add(Kind.Rope, from + Vector3.up * 2f, to + Vector3.up * 2f, life, 1f);
        public void Ping(Vector3 at) => Add(Kind.Ping, at + Vector3.up * 0.5f, at, 0.8f, 18f);

        /// <summary>Takes over a sunk ship's visual; the ship no longer exists in the simulation.</summary>
        public void Sink(Transform visual) => sinking.Add((visual, 0f));

        void Add(Kind kind, Vector3 from, Vector3 to, float life, float size)
        {
            if (active.Count >= MaxEffects) return;
            active.Add(new Effect { Kind = kind, Item = Rent(kind), From = from, To = to, Life = life, Size = size });
        }

        Transform Rent(Kind kind)
        {
            if (!pool.TryGetValue(kind, out var stack)) pool[kind] = stack = new Stack<Transform>();
            if (stack.Count > 0)
            {
                var reused = stack.Pop();
                reused.gameObject.SetActive(true);
                return reused;
            }

            var shape = kind == Kind.Flash ? PrimitiveType.Sphere : kind == Kind.Ping ? PrimitiveType.Cylinder : PrimitiveType.Cube;
            var item = GameObject.CreatePrimitive(shape);
            item.name = $"Effect {kind}";
            Object.Destroy(item.GetComponent<Collider>());
            var material = kind == Kind.Arrow ? materials?.Arrow : kind == Kind.Rope ? materials?.Rope : kind == Kind.Ping ? materials?.Selection : materials?.Flash;
            if (material != null) item.GetComponent<Renderer>().sharedMaterial = material;
            return item.transform;
        }

        public void Update(float delta)
        {
            for (var i = active.Count - 1; i >= 0; i--)
            {
                var effect = active[i];
                effect.Age += delta;
                if (effect.Age >= effect.Life)
                {
                    effect.Item.gameObject.SetActive(false);
                    pool[effect.Kind].Push(effect.Item);
                    active.RemoveAt(i);
                    continue;
                }
                Present(effect, effect.Age / effect.Life);
            }

            for (var i = sinking.Count - 1; i >= 0; i--)
            {
                var (item, age) = sinking[i];
                age += delta;
                if (item == null || age >= 4f)
                {
                    if (item != null) Object.Destroy(item.gameObject);
                    sinking.RemoveAt(i);
                    continue;
                }
                item.position += Vector3.down * (1.6f * delta);
                item.rotation *= Quaternion.Euler(9f * delta, 0f, 5f * delta);
                sinking[i] = (item, age);
            }
        }

        static void Present(Effect effect, float t)
        {
            switch (effect.Kind)
            {
                case Kind.Arrow:
                    var position = Vector3.Lerp(effect.From, effect.To, t) + Vector3.up * (Mathf.Sin(t * Mathf.PI) * 9f);
                    var next = Vector3.Lerp(effect.From, effect.To, Mathf.Min(1f, t + 0.05f)) + Vector3.up * (Mathf.Sin(Mathf.Min(1f, t + 0.05f) * Mathf.PI) * 9f);
                    effect.Item.position = position;
                    if ((next - position).sqrMagnitude > 1e-4f) effect.Item.rotation = Quaternion.LookRotation(next - position);
                    effect.Item.localScale = new Vector3(0.25f, 0.25f, 3.2f);
                    break;
                case Kind.Flash:
                    effect.Item.position = effect.From;
                    effect.Item.localScale = Vector3.one * (effect.Size * (0.3f + t * 1.2f) * (1f - t * 0.6f));
                    break;
                case Kind.Rope:
                    Stretch(effect.Item, effect.From, effect.To, 0.3f);
                    break;
                case Kind.Ping:
                    effect.Item.position = effect.From;
                    effect.Item.localScale = new Vector3(effect.Size * (1f - t * 0.75f), 0.05f, effect.Size * (1f - t * 0.75f));
                    break;
            }
        }

        public static void Stretch(Transform item, Vector3 from, Vector3 to, float width)
        {
            var direction = to - from;
            item.position = from + direction * 0.5f;
            if (direction.sqrMagnitude > 1e-4f) item.rotation = Quaternion.LookRotation(direction);
            item.localScale = new Vector3(width, width, direction.magnitude);
        }
    }
}
