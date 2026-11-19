using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LLTacticalWar
{
    // A unit on the tactics board. It fights one on one through Combat. Ported from the 2015
    // Player / HumanPlayer / BotEnemy (the two subclasses were empty, so the side is now a Team field).
    public class Player : MonoBehaviour
    {
        public Team team;
        [System.NonSerialized] public BaseClass playerClass;
        public int tilePosition;
        public float moveSpeed = 6f;

        public bool isTurnOver;

        public Stats stats;
        public int currentHealth;
        public int MaxHP => stats.HP;
        public bool isAlive = true;
        public bool broken;   // hit with triangle advantage: no counters until its side's next phase
        public WeaponInfo Weapon { get; private set; }

        public UnitModel model;

        public string DisplayName => (team == Team.Player ? "" : "Enemy ") + playerClass.ClassName;
        public TileMapInfo Tile => TileMap.instance.tiles[tilePosition];

        public void Init(BaseClass cls, Team side)
        {
            team = side;
            playerClass = cls;
            playerClass.TerrainEffect(TileMap.instance.terrainType);
            stats = cls.Base;
            currentHealth = MaxHP;
            Weapon = Weapons.Of(cls.Weapon);
            model = UnitFactory.Create(cls, side, transform);
            model.footDust = true;
            model.maxSpeed = moveSpeed;
        }

        // Turns to the nearest of the four grid directions.
        public void FaceTowards(Vector3 point)
        {
            var d = point - transform.position;
            d.y = 0;
            if (d.sqrMagnitude < 0.001f) return;
            d = Mathf.Abs(d.x) > Mathf.Abs(d.z) ? new Vector3(Mathf.Sign(d.x), 0, 0) : new Vector3(0, 0, Mathf.Sign(d.z));
            transform.rotation = Quaternion.LookRotation(d);
        }

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
    }
}
