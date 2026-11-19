using UnityEngine;
using UnityEngine.AI;

namespace LLTacticalWar
{
    // One figure in the real-time battle: a general or a troop. Holds the stats the 2015
    // Player carried into the war scene (currentHealth / modifiedAttack / modifiedDefense).
    public class Soldier : MonoBehaviour
    {
        public Team team;
        public bool isGeneral;
        [System.NonSerialized] public BaseClass playerClass;
        public int maxHealth, currentHealth, modifiedAttack, modifiedDefense;
        public int baseAttack, baseDefense;   // before formation modifiers
        public bool isAlive = true;

        public BaseWeapon weapon;
        public BaseSkill skill;
        public UnitModel model;
        public NavMeshAgent agent;
        public float speed = 5f;

        public void Setup(BaseClass cls, Team side, bool general, int hp, int atk, int def)
        {
            team = side;
            isGeneral = general;
            playerClass = cls;
            maxHealth = currentHealth = hp;
            modifiedAttack = baseAttack = atk;
            modifiedDefense = baseDefense = def;
            model = UnitFactory.Create(cls, side, general, transform);
            model.footDust = general;

            var col = gameObject.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0, 0.8f, 0);
            col.height = 1.6f;
            col.radius = cls.Mounted ? 0.45f : 0.32f;

            weapon = BaseWeapon.AddFor(gameObject, cls.Weapon);
            skill = gameObject.AddComponent<BaseSkill_AuraThrust>();

            speed = (general ? 6.5f : 5.2f) * cls.WarSpeed;
            agent = gameObject.AddComponent<NavMeshAgent>();
            agent.speed = speed;
            agent.acceleration = 30f;
            agent.angularSpeed = 540f;
            agent.radius = cls.Mounted ? 0.45f : 0.34f;
            agent.height = 1.6f;
            agent.stoppingDistance = 0.2f;
            agent.autoBraking = true;
            agent.obstacleAvoidanceType = general ? ObstacleAvoidanceType.HighQualityObstacleAvoidance : ObstacleAvoidanceType.LowQualityObstacleAvoidance;
            agent.avoidancePriority = general ? 30 : Random.Range(40, 70);
            model.maxSpeed = speed;
        }

        public Vector3 Center => transform.position + Vector3.up * 1f;

        public void TakeDamage(int damage, Soldier from)
        {
            if (!isAlive) return;
            // Generals are hardened: rank-and-file blows land at a quarter strength, so a general
            // can fight inside the line instead of dying to the first mob (balance change from 2015).
            if (isGeneral && from != null && !from.isGeneral) damage = Mathf.CeilToInt(damage * 0.25f);
            damage = Mathf.Max(1, damage);
            currentHealth -= damage;
            model.Punch();
            Fx.I?.HitSparks(Center, Palette.Team(team));
            AudioBank.I?.Play(Sfx.Hit, isGeneral ? 0.9f : 0.45f, isGeneral ? 0f : 0.04f);
            if (isGeneral && (from == null || from.isGeneral || damage >= 8))
            {
                GameUI.I?.FloatText(Center + Vector3.up * 0.9f, "-" + damage, team == Team.Player ? Palette.CrimsonLight : Palette.Gold, 0.9f);
                WarSceneManager.instance?.Shake(0.25f);
            }
            if (currentHealth <= 0) Dead();
        }

        void Dead()
        {
            isAlive = false;
            currentHealth = 0;
            if (agent != null && agent.enabled) { agent.isStopped = true; agent.enabled = false; }
            var col = GetComponent<Collider>();
            if (col != null) col.enabled = false;
            Fx.I?.DeathPuff(transform.position);
            AudioBank.I?.Play(Sfx.Death, isGeneral ? 1f : 0.35f, isGeneral ? 0f : 0.06f);
            model.Die();
            WarSceneManager.instance?.OnSoldierDied(this);
        }
    }
}
