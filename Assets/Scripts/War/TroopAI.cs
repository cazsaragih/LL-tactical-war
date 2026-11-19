using System.Collections.Generic;
using UnityEngine;

namespace LLTacticalWar
{
    // Merges the 2015 AIController, GeneralAIController and PlayerAIController (three near-copies)
    // into one brain used by both sides. States follow the 2015 PlayerAIController: Idle,
    // Defense, Targeting/Chasing (Advancing), Attacking, Covering, FallingBack, Dead.
    // Fixes: targets used to be chosen with Random.Range(0, Count - 1), which never picked the
    // last soldier; targets are now the nearest few with some spread, so the battle reads as
    // lines meeting instead of a brawl. "Cover Me" (an empty button in 2015) now works.
    public class TroopAI : MonoBehaviour
    {
        // Commanding: an AI general marches behind its own army's centre and only fights what
        // reaches it. (In 2015 the enemy general charged alone, got mobbed, and battles were over
        // in seconds.)
        public enum BotState { Idle, Advancing, Attacking, FallingBack, Defense, Covering, Commanding, Dead }
        public BotState botState = BotState.Idle;

        Soldier self;
        Soldier target;
        BotState returnState = BotState.Advancing;
        Vector3 holdPosition;
        Vector3 coverOffset;
        float thinkTimer, pathTimer;

        const float DefenseRadius = 9f;
        const float CoverRadius = 9f;

        void Awake()
        {
            self = GetComponent<Soldier>();
            thinkTimer = Random.value * 0.5f;
            coverOffset = new Vector3(Random.Range(-4f, -1f), 0, Random.Range(-4f, 4f));
        }

        public void Advance() { if (botState != BotState.Dead) { botState = BotState.Advancing; target = null; } }
        public void Command() { if (botState != BotState.Dead) { botState = BotState.Commanding; target = null; } }
        public void Cover() { if (botState != BotState.Dead) { botState = BotState.Covering; target = null; } }
        public void FallBack(Vector3 slot) { if (botState != BotState.Dead) { holdPosition = slot; botState = BotState.FallingBack; target = null; } }
        public void Hold(Vector3 slot) { holdPosition = slot; }

        float AttackDistance => self.weapon.Reach * (self.weapon.ranged ? 0.85f : 0.75f);

        void Update()
        {
            if (!self.isAlive) { botState = BotState.Dead; return; }
            var war = WarSceneManager.instance;
            if (war == null || war.battleState != WarSceneManager.BattleState.In_Battle)
            {
                if (self.agent.enabled && self.agent.isOnNavMesh) self.agent.isStopped = true;
                return;
            }

            thinkTimer -= Time.deltaTime;
            pathTimer -= Time.deltaTime;

            switch (botState)
            {
                case BotState.Idle: Stop(); break;
                case BotState.Advancing: Advancing(war); break;
                case BotState.Attacking: Attacking(); break;
                case BotState.FallingBack: FallingBack(); break;
                case BotState.Defense: Defense(war); break;
                case BotState.Covering: Covering(war); break;
                case BotState.Commanding: Commanding(war); break;
            }
        }

        Vector3 armyCentre;
        void Commanding(WarSceneManager war)
        {
            if (thinkTimer <= 0f)
            {
                thinkTimer = 0.4f + Random.value * 0.2f;
                target = FindTarget(war.Enemies(self.team), transform.position, 7f);
                var own = self.team == Team.Player ? war.playerTroops : war.enemyTroops;
                Vector3 sum = Vector3.zero; int n = 0;
                foreach (var s in own) if (s != self && s.isAlive) { sum += s.transform.position; n++; }
                // no army left: the general fights in person
                if (n == 0) { botState = BotState.Advancing; return; }
                var back = self.team == Team.Player ? Vector3.left : Vector3.right;
                armyCentre = sum / n + back * 6f;
            }
            if (target != null && target.isAlive) { Engage(BotState.Commanding); return; }
            if (Vector3.Distance(transform.position, armyCentre) > 2f) MoveTo(armyCentre, 0.9f);
            else
            {
                Stop();
                var enemyGen = war.General(self.team == Team.Player ? Team.Enemy : Team.Player);
                if (enemyGen != null) Face(enemyGen.transform.position);
            }
        }

        void Advancing(WarSceneManager war)
        {
            if (target == null || !target.isAlive || thinkTimer <= 0f)
            {
                thinkTimer = 0.6f + Random.value * 0.3f;
                target = FindTarget(war.Enemies(self.team), transform.position, float.MaxValue);
            }
            if (target == null) { Stop(); return; }
            Engage(BotState.Advancing);
        }

        void Engage(BotState from)
        {
            float d = Vector3.Distance(transform.position, target.transform.position);
            if (d <= AttackDistance) { returnState = from; botState = BotState.Attacking; return; }
            MoveTo(target.transform.position, 1f);
        }

        void Attacking()
        {
            if (target == null || !target.isAlive)
            {
                target = null;
                botState = returnState == BotState.Defense ? BotState.FallingBack : returnState;
                return;
            }
            float d = Vector3.Distance(transform.position, target.transform.position);
            if (d > AttackDistance * 1.25f)
            {
                botState = returnState;
                return;
            }
            Stop();
            Face(target.transform.position);
            if (Time.time >= nextSwing && self.weapon.Swing(self))
                nextSwing = Time.time + self.weapon.swingDuration + Random.Range(0.25f, 0.8f);
        }
        float nextSwing;

        void FallingBack()
        {
            MoveTo(holdPosition, 1.4f);
            if (Vector3.Distance(transform.position, holdPosition) <= 0.8f)
            {
                Stop();
                botState = BotState.Defense;
            }
        }

        void Defense(WarSceneManager war)
        {
            Stop();
            Face(transform.position + (self.team == Team.Player ? Vector3.right : Vector3.left));
            if (thinkTimer > 0f) return;
            thinkTimer = 0.4f + Random.value * 0.2f;
            target = FindTarget(war.Enemies(self.team), holdPosition, DefenseRadius);
            if (target != null) { returnState = BotState.Defense; botState = BotState.Attacking; MoveTo(target.transform.position, 1f); }
        }

        void Covering(WarSceneManager war)
        {
            var general = war.General(self.team);
            if (general == null || !general.isAlive) { botState = BotState.Advancing; return; }
            if (thinkTimer <= 0f)
            {
                thinkTimer = 0.4f + Random.value * 0.2f;
                target = FindTarget(war.Enemies(self.team), general.transform.position, CoverRadius);
            }
            if (target != null && target.isAlive) { Engage(BotState.Covering); return; }
            var spot = general.transform.position + general.transform.rotation * coverOffset;
            if (Vector3.Distance(transform.position, spot) > 1.5f) MoveTo(spot, 1.2f);
            else Stop();
        }

        // nearest few enemies, pick one at random so a whole line doesn't mob a single soldier
        static readonly List<Soldier> candidates = new List<Soldier>();
        Soldier FindTarget(List<Soldier> enemies, Vector3 from, float radius)
        {
            candidates.Clear();
            float r2 = radius * radius;
            Soldier n1 = null, n2 = null, n3 = null;
            float d1 = float.MaxValue, d2 = float.MaxValue, d3 = float.MaxValue;
            foreach (var e in enemies)
            {
                if (e == null || !e.isAlive) continue;
                float d = (e.transform.position - from).sqrMagnitude;
                if (d > r2) continue;
                if (d < d1) { n3 = n2; d3 = d2; n2 = n1; d2 = d1; n1 = e; d1 = d; }
                else if (d < d2) { n3 = n2; d3 = d2; n2 = e; d2 = d; }
                else if (d < d3) { n3 = e; d3 = d; }
            }
            if (n1 != null) candidates.Add(n1);
            if (n2 != null && d2 < d1 * 2.5f + 4f) candidates.Add(n2);
            if (n3 != null && d3 < d1 * 2.5f + 4f) candidates.Add(n3);
            return candidates.Count == 0 ? null : candidates[Random.Range(0, candidates.Count)];
        }

        void MoveTo(Vector3 p, float speedMul)
        {
            var a = self.agent;
            if (!a.enabled || !a.isOnNavMesh) return;
            a.isStopped = false;
            a.speed = self.speed * speedMul;
            if (pathTimer <= 0f || (a.destination - p).sqrMagnitude > 4f)
            {
                pathTimer = 0.25f + Random.value * 0.15f;
                a.SetDestination(p);
            }
        }

        void Stop()
        {
            var a = self.agent;
            if (a.enabled && a.isOnNavMesh && !a.isStopped) { a.isStopped = true; a.velocity = Vector3.zero; }
        }

        void Face(Vector3 p)
        {
            var d = p - transform.position;
            d.y = 0;
            if (d.sqrMagnitude < 0.0001f) return;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(d), 1f - Mathf.Exp(-Time.deltaTime * 14f));
        }
    }
}
