using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VFX;
using Object = UnityEngine.Object;

namespace MorzaBogow.Presentation
{
    /// <summary>
    /// A bounded pool of one kind of presentation object: a missile, a splash, a flash, a fire. Renting switches an
    /// object on and starts its particle systems and visual effects from the beginning; returning stops and clears them
    /// and switches the object off for the next use. Below the limit nothing is made or destroyed while the pool is in
    /// use, so a long battle leaves no garbage for the collector. Presentation only: the simulation decides what
    /// happens, the pool only shows it, and an effect it cannot lend is simply not shown.
    /// </summary>
    public sealed class EffectPool
    {
        readonly Func<GameObject> create;
        readonly Stack<GameObject> free = new Stack<GameObject>();
        readonly HashSet<GameObject> lent = new HashSet<GameObject>();
        readonly List<ParticleSystem> particleScratch = new List<ParticleSystem>();
        readonly List<VisualEffect> visualScratch = new List<VisualEffect>();

        /// <summary>A pool that makes its objects with <paramref name="create"/> and lends at most <paramref name="limit"/> at once.</summary>
        public EffectPool(Func<GameObject> create, int limit)
        {
            this.create = create ?? throw new ArgumentNullException(nameof(create));
            Limit = Math.Max(1, limit);
        }

        /// <summary>The most objects out at the same time; a rent beyond it gets nothing.</summary>
        public int Limit { get; }

        /// <summary>Objects out of the pool now.</summary>
        public int ActiveCount => lent.Count;

        /// <summary>Objects waiting in the pool, switched off.</summary>
        public int FreeCount => free.Count;

        /// <summary>Objects this pool has made; it stops growing once the pool holds what a battle needs.</summary>
        public int CreatedCount { get; private set; }

        /// <summary>
        /// Makes objects ahead of need until <paramref name="count"/> wait in the pool, within the limit, so the first
        /// volley of a battle does not stutter.
        /// </summary>
        public void Prewarm(int count)
        {
            var target = Math.Min(count, Limit - lent.Count);
            while (free.Count < target) free.Push(Make());
        }

        /// <summary>
        /// An object switched on with its effects playing from the start, or null when the limit is reached. The caller
        /// places it and gives it back with <see cref="Return"/>.
        /// </summary>
        public GameObject Rent()
        {
            if (lent.Count >= Limit) return null;
            GameObject item = null;
            // An object destroyed with its scene is skipped, never lent.
            while (item == null && free.Count > 0) item = free.Pop();
            if (item == null) item = Make();
            lent.Add(item);
            item.SetActive(true);
            Restart(item);
            return item;
        }

        /// <summary>
        /// Stops, clears and switches off an object this pool lent; false for anything else. An object destroyed while
        /// it was out is only forgotten.
        /// </summary>
        public bool Return(GameObject item)
        {
            if (ReferenceEquals(item, null) || !lent.Remove(item)) return false;
            if (item == null) return true;
            Halt(item);
            item.SetActive(false);
            free.Push(item);
            return true;
        }

        /// <summary>Destroys every object of the pool, out or waiting. The pool stays usable and makes new ones.</summary>
        public void Clear()
        {
            foreach (var item in lent) if (item != null) Object.Destroy(item);
            foreach (var item in free) if (item != null) Object.Destroy(item);
            lent.Clear();
            free.Clear();
        }

        GameObject Make()
        {
            var item = create();
            CreatedCount++;
            Halt(item);
            item.SetActive(false);
            return item;
        }

        /// <summary>
        /// Particle systems and visual effects start again from nothing, as the visual effect's own Restart control does.
        /// Particles are driven from the top systems of the object with their children, so sub-emitters still fire only
        /// when their parent's particles call them and never on their own at the effect's position.
        /// </summary>
        void Restart(GameObject item)
        {
            TopSystems(item);
            foreach (var system in particleScratch)
            {
                system.Clear(true);
                system.Play(true);
            }
            item.GetComponentsInChildren(true, visualScratch);
            foreach (var effect in visualScratch)
            {
                effect.Reinit();
                effect.pause = false;
            }
        }

        /// <summary>Particle systems stop and lose their particles; visual effects are reinitialised and paused, as by their Stop control.</summary>
        void Halt(GameObject item)
        {
            TopSystems(item);
            foreach (var system in particleScratch) system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            item.GetComponentsInChildren(true, visualScratch);
            foreach (var effect in visualScratch)
            {
                effect.Reinit();
                effect.pause = true;
            }
        }

        /// <summary>Fills the scratch list with the object's particle systems that have no other particle system above them in it.</summary>
        void TopSystems(GameObject item)
        {
            item.GetComponentsInChildren(true, particleScratch);
            var top = item.transform;
            for (var i = particleScratch.Count - 1; i >= 0; i--)
            {
                for (var above = particleScratch[i].transform.parent; above != null && above != top.parent; above = above.parent)
                {
                    if (!above.TryGetComponent(out ParticleSystem _)) continue;
                    particleScratch.RemoveAt(i);
                    break;
                }
            }
        }
    }
}
