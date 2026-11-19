using UnityEngine;

namespace LLTacticalWar
{
    public class BaseSkill_AuraThrust : BaseSkill
    {
        void Awake()
        {
            SkillID = 1;
            SkillName = "Aura Thrust";
            SkillDescription = "One straight line wave normal damage";
            SkillCooldown = 15f;
        }

        public override void Cast(Soldier caster, Vector3 direction)
        {
            direction.y = 0f;
            direction.Normalize();
            var go = new GameObject("AuraThrust");
            go.transform.SetParent(caster.transform.parent, false);
            go.transform.position = caster.transform.position + Vector3.up * 0.9f + direction * 1.2f;
            go.transform.rotation = Quaternion.LookRotation(direction);
            var c = Palette.TeamLight(caster.team);
            Prim.GlowPart("Wave", PrimitiveType.Cube, go.transform, Vector3.zero, new Vector3(2.6f, 1.2f, 0.25f), c);
            Prim.GlowPart("Core", PrimitiveType.Cube, go.transform, new Vector3(0, 0, -0.2f), new Vector3(1.6f, 0.7f, 0.2f), Palette.Gold);
            var p = go.AddComponent<AuraThrust_Projectile>();
            p.Launch(caster, direction * 22f);
            AudioBank.I?.Play(Sfx.Skill);
            WarSceneManager.instance?.Shake(0.5f);
        }
    }
}
