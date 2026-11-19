using System.Collections.Generic;
using UnityEngine;

namespace LLTacticalWar
{
    // Ported from the 2015 BaseSkill / BaseSkill_AuraThrust / AuraThrust_Projectile.
    // The projectile used to load "Skill Prefabs/AuraThrust" from Resources (never committed)
    // and only travelled along X; it is now built from primitives and flies the way the
    // general faces. Damage is the 2015 formula: attack + attack*1 - defense.
    public abstract class BaseSkill : MonoBehaviour
    {
        public int SkillID { get; protected set; }
        public string SkillName { get; protected set; }
        public string SkillDescription { get; protected set; }
        public float SkillCooldown { get; protected set; }

        public abstract void Cast(Soldier caster, Vector3 direction);
    }
}
