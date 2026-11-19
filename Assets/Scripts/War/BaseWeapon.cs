using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LLTacticalWar
{
    // Ported from the 2015 BaseWeapon: a forward cast along the weapon's length during the
    // swing's "hit frames", hitting each enemy at most once per swing. The hit window used to
    // be toggled by animation events; there is no Animator now, so the swing times it.
    public class BaseWeapon : MonoBehaviour
    {
        public float weaponLength = 2f;
        public float swingDuration = 0.55f;
        public float hitStart = 0.32f, hitEnd = 0.55f;   // fraction of the swing
        public bool ranged;

        protected bool inHitFrame;
        readonly List<Soldier> hitActors = new List<Soldier>();
        static readonly RaycastHit[] hits = new RaycastHit[16];

        public bool IsSwinging { get; private set; }
        public float Reach => weaponLength;

        public static BaseWeapon AddFor(GameObject go, WeaponKind kind)
        {
            switch (kind)
            {
                case WeaponKind.Dagger: return go.AddComponent<BaseWeapon_Dagger>();
                case WeaponKind.Spear:  return go.AddComponent<BaseWeapon_Spear>();
                case WeaponKind.Staff:  return go.AddComponent<BaseWeapon_Staff>();
                case WeaponKind.Bow:    return go.AddComponent<BaseWeapon_Bow>();
                case WeaponKind.Club:   return go.AddComponent<BaseWeapon_Club>();
                default:                return go.AddComponent<BaseWeapon_Sword>();
            }
        }

        public bool Swing(Soldier hitter)
        {
            if (IsSwinging || !hitter.isAlive) return false;
            StartCoroutine(SwingRoutine(hitter));
            return true;
        }

        IEnumerator SwingRoutine(Soldier hitter)
        {
            IsSwinging = true;
            hitter.model.Swing(swingDuration);
            float t = 0f;
            bool fired = false;
            ResetHitActorsList();
            while (t < swingDuration)
            {
                float k = t / swingDuration;
                inHitFrame = k >= hitStart && k <= hitEnd;
                if (inHitFrame && hitter.isAlive)
                {
                    if (!ranged) TraceAttack(hitter);
                    else if (!fired) { fired = true; FireArrow(hitter); }
                }
                t += Time.deltaTime;
                yield return null;
            }
            inHitFrame = false;
            IsSwinging = false;
        }

        public void TraceAttack(Soldier hitter)
        {
            var origin = hitter.transform.position + Vector3.up * 1f;
            var direction = hitter.transform.forward;
            int n = Physics.SphereCastNonAlloc(origin, 0.45f, direction, hits, weaponLength, ~0, QueryTriggerInteraction.Ignore);
            Soldier best = null;
            float bestDist = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                var s = hits[i].collider.GetComponent<Soldier>();
                if (s == null || s.team == hitter.team || !s.isAlive || hitActors.Contains(s)) continue;
                if (hits[i].distance < bestDist) { bestDist = hits[i].distance; best = s; }
            }
            if (best != null)
            {
                hitActors.Add(best);
                best.TakeDamage(hitter.modifiedAttack - best.modifiedDefense, hitter);
            }
        }

        void FireArrow(Soldier hitter)
        {
            var origin = hitter.transform.position + Vector3.up * 1.1f;
            var direction = hitter.transform.forward;
            var end = origin + direction * weaponLength;
            int n = Physics.SphereCastNonAlloc(origin, 0.35f, direction, hits, weaponLength, ~0, QueryTriggerInteraction.Ignore);
            Soldier best = null;
            float bestDist = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                var s = hits[i].collider.GetComponent<Soldier>();
                if (s == null || s.team == hitter.team || !s.isAlive) continue;
                if (hits[i].distance < bestDist) { bestDist = hits[i].distance; best = s; }
            }
            if (best != null) end = best.Center;
            ArrowTracer.Fire(origin, end, Palette.TeamLight(hitter.team));
            AudioBank.I?.Play(Sfx.Arrow, hitter.isGeneral ? 0.8f : 0.3f, hitter.isGeneral ? 0f : 0.05f);
            if (best != null) best.TakeDamage(hitter.modifiedAttack - best.modifiedDefense, hitter);
        }

        public bool ToggleHitFrame()
        {
            ResetHitActorsList();
            return inHitFrame = !inHitFrame;
        }

        public void ResetHitActorsList() => hitActors.Clear();
    }
}
