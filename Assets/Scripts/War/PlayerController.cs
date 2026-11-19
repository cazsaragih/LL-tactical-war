using UnityEngine;

namespace LLTacticalWar
{
    // Ported from the 2015 PlayerController: you steer your general directly in the battle.
    // WASD / arrows move · left click or J attacks · right click or K casts the class skill ·
    // Space opens the command menu (time stops, as it did in 2015) with Advance / Cover Me /
    // Fall Back.
    public class PlayerController : MonoBehaviour
    {
        Soldier player;
        float nextSkill;
        public float turnSmoothing = 16f;

        public float SkillCooldownLeft => Mathf.Max(0f, nextSkill - Time.time);
        public float SkillCooldown01 => player.skill == null ? 1f : 1f - Mathf.Clamp01(SkillCooldownLeft / player.skill.SkillCooldown);

        void Awake()
        {
            player = GetComponent<Soldier>();
            player.agent.updateRotation = false;
            nextSkill = Time.time + 3f;
        }

        void Update()
        {
            var war = WarSceneManager.instance;
            if (war == null || war.battleState != WarSceneManager.BattleState.In_Battle || !player.isAlive) return;
            if (!GameManager.IsPlaying || war.CommandMenuOpen) return;

            var input = GameInput.Move;
            var move = new Vector3(input.x, 0f, input.y);
            if (move.sqrMagnitude > 0.01f && player.agent.enabled && player.agent.isOnNavMesh)
            {
                player.agent.isStopped = true;
                player.agent.Move(move * player.speed * Time.deltaTime);
                var targetRotation = Quaternion.LookRotation(move, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 1f - Mathf.Exp(-turnSmoothing * Time.deltaTime));
            }

            if (GameInput.Attack || (GameInput.AttackHeld && !player.weapon.IsSwinging))
            {
                AimAssist(player.weapon.Reach * 1.4f, 70f);
                player.weapon.Swing(player);
            }

            if (GameInput.Skill)
            {
                if (Time.time >= nextSkill && player.skill != null)
                {
                    AimAssist(14f, 35f);
                    nextSkill = Time.time + player.skill.SkillCooldown;
                    player.skill.Cast(player, transform.forward);
                }
                else AudioBank.I?.Play(Sfx.UiCancel, 0.5f);
            }

            if (GameInput.Command) war.OpenCommandMenu();
        }

        // Snap facing to the nearest enemy inside a forward cone, so attacks land where you mean.
        void AimAssist(float range, float halfAngle)
        {
            Soldier best = null;
            float bestD = range;
            foreach (var e in WarSceneManager.instance.Enemies(player.team))
            {
                if (e == null || !e.isAlive) continue;
                var d = e.transform.position - transform.position;
                d.y = 0f;
                float dist = d.magnitude;
                if (dist > bestD) continue;
                if (Vector3.Angle(transform.forward, d) > halfAngle) continue;
                bestD = dist;
                best = e;
            }
            if (best != null)
            {
                var d = best.transform.position - transform.position;
                d.y = 0f;
                if (d.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(d);
            }
        }
    }
}
