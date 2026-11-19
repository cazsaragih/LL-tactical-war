using System.Collections.Generic;
using UnityEngine;

namespace LLTacticalWar
{
    public class AuraThrust_Projectile : MonoBehaviour
    {
        Soldier caster;
        Vector3 velocity;
        float life = 1.1f;
        readonly HashSet<Soldier> hit = new HashSet<Soldier>();
        static readonly Collider[] overlaps = new Collider[64];

        public void Launch(Soldier pCaster, Vector3 v) { caster = pCaster; velocity = v; }

        void Update()
        {
            transform.position += velocity * Time.deltaTime;
            float pulse = 1f + Mathf.Sin(Time.time * 30f) * 0.08f;
            transform.localScale = new Vector3(pulse, pulse, 1f);
            int n = Physics.OverlapSphereNonAlloc(transform.position, 1.4f, overlaps, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var s = overlaps[i].GetComponent<Soldier>();
                if (s == null || caster == null || s.team == caster.team || !s.isAlive || hit.Contains(s)) continue;
                hit.Add(s);
                int damage = (caster.modifiedAttack + caster.modifiedAttack * 1) - s.modifiedDefense;
                s.TakeDamage(damage, caster);
            }
            life -= Time.deltaTime;
            if (life <= 0f) Destroy(gameObject);
        }
    }
}
