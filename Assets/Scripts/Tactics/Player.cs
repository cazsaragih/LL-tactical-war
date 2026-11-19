using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LLTacticalWar
{
    // A unit on the tactics board: one general plus the troop count they bring to battle.
    // Ported from the 2015 Player / HumanPlayer / BotEnemy (the two subclasses were empty, so the
    // side is now a Team field).
    public class Player : MonoBehaviour
    {
        public Team team;
        [System.NonSerialized] public BaseClass playerClass;
        public int tilePosition;
        public float moveSpeed = 6f;

        public bool isTurnOver;
        public bool moveEnabled = true;
        public bool attackEnabled = true;
        public bool waitEnabled = true;

        public int maxHealth;
        public int currentHealth;
        public int startUnits;
        public int currentUnits;
        public int modifiedAttack;
        public int modifiedDefense;
        public bool isAlive = true;

        public enum Direction { North, East, South, West }
        public Direction playerDirection;

        public UnitModel model;

        public string DisplayName => (team == Team.Player ? "" : "Enemy ") + playerClass.ClassName;

        public void Init(BaseClass cls, Team side)
        {
            team = side;
            playerClass = cls;
            playerClass.BoostStats();   // the general is the boosted version of the class
            playerClass.TerrainEffect(TileMap.instance.terrainType);
            maxHealth = currentHealth = playerClass.BaseHP;
            startUnits = currentUnits = playerClass.TotalUnits;
            modifiedAttack = playerClass.BaseAttack;
            modifiedDefense = playerClass.BaseDefense;
            model = UnitFactory.Create(cls, side, true, transform);
            model.footDust = true;
            model.maxSpeed = moveSpeed;
        }

        public void Rotate(Direction dir)
        {
            playerDirection = dir;
            transform.rotation = Quaternion.Euler(0, (int)dir * 90f, 0);
        }

        public void FaceTowards(Vector3 point)
        {
            var d = point - transform.position;
            d.y = 0;
            if (d.sqrMagnitude < 0.001f) return;
            if (Mathf.Abs(d.x) > Mathf.Abs(d.z)) Rotate(d.x > 0 ? Direction.East : Direction.West);
            else Rotate(d.z > 0 ? Direction.North : Direction.South);
        }

        public Vector3 Forward => Quaternion.Euler(0, (int)playerDirection * 90f, 0) * Vector3.forward;

        // Walks the path tile by tile, turning to face each step (as the 2015 Move did).
        public IEnumerator MoveAlong(List<TileMapInfo> path)
        {
            for (int i = 1; i < path.Count; i++)
            {
                var from = path[i - 1].position;
                var to = path[i].position;
                FaceTowards(to);
                float dur = Vector3.Distance(from, to) / moveSpeed;
                yield return Ease.Tween(dur, k => transform.position = Vector3.Lerp(from, to, k), Ease.Linear, false);
                AudioBank.I?.Play(Sfx.Step, 0.5f);
            }
        }

        public void SetTurnOver(bool over)
        {
            isTurnOver = over;
            model.SetDimmed(over);
        }

        public void TakeBattleResult(int health, int units)
        {
            currentHealth = Mathf.Max(0, health);
            currentUnits = Mathf.Max(0, units);
            isAlive = currentHealth > 0;
        }
    }
}
