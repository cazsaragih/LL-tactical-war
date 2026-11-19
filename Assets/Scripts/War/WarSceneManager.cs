using System;
using System.Collections;
using System.Collections.Generic;
using Unity.AI.Navigation;
using Unity.Cinemachine;
using Unity.Cinemachine.TargetTracking;
using UnityEngine;
using UnityEngine.AI;

namespace LLTacticalWar
{
    // Ported from the 2015 WarSceneManager. An attack on the tactics map cuts to this real-time
    // battle: both generals take the field with every troop they have left. You pick a
    // formation (Line / Wedge / Phalanx, with the 2015 attack/defense modifiers), then fight
    // until a general falls or the 60-second limit runs out. Survivors carry back to the map.
    public class WarSceneManager : MonoBehaviour
    {
        public static WarSceneManager instance;

        public enum BattleState { Idle, Preview, Pre_Battle, In_Battle, Halt_Battle, Post_Battle }
        public BattleState battleState = BattleState.Idle;

        public enum Formation { Line, Wedge, Phalanx }
        public Formation formation = Formation.Line;

        public int limitTime = 60;
        public float TimeLeft { get; private set; }
        public bool CommandMenuOpen { get; private set; }

        public Soldier playerGeneral, enemyGeneral;
        public readonly List<Soldier> playerTroops = new List<Soldier>();   // alive, general included
        public readonly List<Soldier> enemyTroops = new List<Soldier>();

        Transform warRoot, troopsRoot, centerMark;
        CinemachineCamera followCam, previewCam;
        CinemachineImpulseSource impulse;
        PlayerController controller;
        readonly List<Vector3> formationSlots = new List<Vector3>();
        bool startPressed;
        string pName, eName;

        const float FieldX = 90f, FieldZ = 56f;
        static readonly Vector3 PlayerSpawner = new Vector3(-16f, 0f, 0f);
        static readonly Vector3 EnemySpawner = new Vector3(16f, 0f, 0f);

        void Awake() { instance = this; }

        public void Build(Transform root)
        {
            warRoot = root;
            Scenery.BuildBattlefield(warRoot, FieldX, FieldZ);
            troopsRoot = new GameObject("Troops").transform;
            troopsRoot.SetParent(warRoot, false);
            centerMark = new GameObject("FieldCenter").transform;
            centerMark.SetParent(warRoot, false);
            centerMark.localPosition = new Vector3(0, 1f, 0);

            var surface = warRoot.gameObject.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.BuildNavMesh();

            // follow camera: south of the field looking north, armies left vs right (the 2015 view)
            followCam = new GameObject("CM_WarFollow").AddComponent<CinemachineCamera>();
            followCam.transform.SetParent(warRoot, false);
            followCam.Lens.FieldOfView = 30f;
            followCam.Lens.FarClipPlane = 400f;
            var follow = followCam.gameObject.AddComponent<CinemachineFollow>();
            follow.FollowOffset = new Vector3(0f, 21f, -20f);
            follow.TrackerSettings.BindingMode = BindingMode.WorldSpace;
            follow.TrackerSettings.PositionDamping = new Vector3(0.6f, 0.6f, 0.6f);
            var aim = followCam.gameObject.AddComponent<CinemachineRotationComposer>();
            aim.Damping = new Vector2(0.4f, 0.4f);
            followCam.gameObject.AddComponent<CinemachineImpulseListener>();
            followCam.Priority = 10;

            previewCam = new GameObject("CM_WarPreview").AddComponent<CinemachineCamera>();
            previewCam.transform.SetParent(warRoot, false);
            previewCam.Lens.FieldOfView = 50f;
            previewCam.Lens.FarClipPlane = 400f;
            previewCam.gameObject.AddComponent<CinemachineRotationComposer>();
            previewCam.LookAt = centerMark;
            previewCam.Priority = 0;

            impulse = gameObject.AddComponent<CinemachineImpulseSource>();
        }

        void Update()
        {
            // keep the HUD's troop counts live before the charge as well
            if (battleState == BattleState.Preview || battleState == BattleState.Pre_Battle)
                GameUI.I.UpdateWar(limitTime, TroopCount(playerTroops, playerGeneral), TroopCount(enemyTroops, enemyGeneral), controller != null ? controller.SkillCooldown01 : 0f);
        }

        public List<Soldier> Enemies(Team t) => t == Team.Player ? enemyTroops : playerTroops;
        public Soldier General(Team t) => t == Team.Player ? playerGeneral : enemyGeneral;

        public void Shake(float force) { if (impulse != null && battleState == BattleState.In_Battle) impulse.GenerateImpulseWithForce(force); }

        public void OnSoldierDied(Soldier s)
        {
            (s.team == Team.Player ? playerTroops : enemyTroops).Remove(s);
            if (s.isGeneral)
            {
                Fx.I?.TeamBurst(s.Center, Palette.Team(s.team));
                impulse?.GenerateImpulseWithForce(1.2f);
            }
        }

        // ---------------- the battle ----------------

        public void StartBattle(Player playerSide, Player enemySide, Team attacker, int flankBonus, string flankLabel, Action onDone)
        {
            StartCoroutine(BattleRoutine(playerSide, enemySide, attacker, flankBonus, flankLabel, onDone));
        }

        IEnumerator BattleRoutine(Player playerSide, Player enemySide, Team attacker, int flankBonus, string flankLabel, Action onDone)
        {
            GameManager.I.InBattle = true;
            yield return GameUI.I.Fade(1f, 0.35f);

            GameManager.I.ShowWar(true);
            pName = playerSide.playerClass.ClassName;
            eName = enemySide.playerClass.ClassName;
            InitializeWar(playerSide, enemySide, attacker, flankBonus);
            formation = Formation.Line;
            ApplyFormation(formation);

            battleState = BattleState.Preview;
            previewCam.Priority = 30;
            previewCam.transform.localPosition = new Vector3(-26f, 17f, -34f);
            followCam.Follow = playerGeneral.transform;
            followCam.LookAt = playerGeneral.transform;
            GameUI.I.SetTacticHud(false);
            GameUI.I.SetWarHud(true);
            GameUI.I.BindWar(playerGeneral, enemyGeneral, pName, "Enemy " + eName);
            GameUI.I.SetHint("");
            AudioBank.I.PlayWarMusic();
            AudioBank.I.Play(Sfx.Horn);
            string sub = attacker == Team.Player ? "YOU ATTACK" : "YOU ARE ATTACKED";
            if (flankLabel != null) sub += "  -  " + flankLabel;
            GameUI.I.Banner(pName.ToUpper() + "  VS  " + eName.ToUpper(), sub, Palette.Gold);
            yield return GameUI.I.Fade(0f, 0.35f);

            // preview: a slow dolly across the field (the 2015 AnimateCamera wait)
            float t = 0f;
            while (t < 2.6f)
            {
                t += Time.deltaTime;
                previewCam.transform.localPosition = Vector3.Lerp(new Vector3(-26f, 17f, -34f), new Vector3(6f, 15f, -30f), Ease.InOutSine(t / 2.6f));
                yield return null;
            }
            previewCam.Priority = 0;

            // pre-battle: formation menu
            battleState = BattleState.Pre_Battle;
            startPressed = false;
            var menu = new KeyMenu("FORMATION");
            menu.Add("Line          balanced", () => ApplyFormation(Formation.Line), null);
            menu.Add("Wedge        ATK +10   DEF -10", () => ApplyFormation(Formation.Wedge), null);
            menu.Add("Phalanx      ATK -10   DEF +10", () => ApplyFormation(Formation.Phalanx), null);
            menu.Add("START BATTLE", () => startPressed = true, null);
            menu.selected = 3;
            GameUI.I.ShowSideMenu(menu, () => (int)formation);
            GameUI.I.SetHint("UP/DOWN  choose     Z  apply formation / start");
            while (!startPressed) yield return null;
            GameUI.I.HideSideMenu();

            // in battle
            battleState = BattleState.In_Battle;
            GameUI.I.Banner("CHARGE!", null, Palette.Gold);
            AudioBank.I.Play(Sfx.Horn, 0.8f);
            GameUI.I.SetHint("WASD  move     LMB / J  attack     RMB / K  Aura Thrust     SPACE  commands");
            float startingTime = Time.time;
            foreach (var s in enemyTroops)
            {
                var ai = s.GetComponent<TroopAI>();
                if (ai == null) continue;
                if (s == enemyGeneral) ai.Command(); else ai.Advance();
            }
            AdvanceCommand();

            while (true)
            {
                TimeLeft = Mathf.Max(0f, limitTime - (Time.time - startingTime));
                GameUI.I.UpdateWar(TimeLeft, TroopCount(playerTroops, playerGeneral), TroopCount(enemyTroops, enemyGeneral), controller != null ? controller.SkillCooldown01 : 0f);
                if (!playerGeneral.isAlive || !enemyGeneral.isAlive || TimeLeft <= 0f) break;
                yield return null;
            }

            // post battle: a beat of slow motion, the verdict, then back to the board
            battleState = BattleState.Post_Battle;
            CloseCommandMenu();
            GameUI.I.SetHint("");
            string verdict; Color vc;
            if (!enemyGeneral.isAlive) { verdict = "ENEMY GENERAL FALLS"; vc = Palette.Gold; AudioBank.I.Play(Sfx.Victory); }
            else if (!playerGeneral.isAlive) { verdict = "YOUR GENERAL HAS FALLEN"; vc = Palette.CrimsonLight; AudioBank.I.Play(Sfx.Defeat); }
            else { verdict = "THE ARMIES WITHDRAW"; vc = Palette.Parchment; AudioBank.I.Play(Sfx.Horn, 0.7f); }
            GameManager.SetTimeScale(0.3f);
            GameUI.I.Banner(verdict, "TIME " + Mathf.CeilToInt(limitTime - TimeLeft) + "s", vc);
            yield return Ease.WaitUnscaled(1.1f);
            GameManager.SetTimeScale(1f);
            yield return Ease.WaitUnscaled(1.2f);

            int pTroops = TroopCount(playerTroops, playerGeneral);
            int eTroops = TroopCount(enemyTroops, enemyGeneral);
            int pLost = playerSide.currentUnits - pTroops, eLost = enemySide.currentUnits - eTroops;
            playerSide.TakeBattleResult(playerGeneral.isAlive ? playerGeneral.currentHealth : 0, pTroops);
            enemySide.TakeBattleResult(enemyGeneral.isAlive ? enemyGeneral.currentHealth : 0, eTroops);

            yield return GameUI.I.Fade(1f, 0.35f);
            EndWar();
            GameManager.I.ShowWar(false);
            GameUI.I.SetWarHud(false);
            GameUI.I.SetTacticHud(true);
            AudioBank.I.PlayTacticMusic();
            yield return GameUI.I.Fade(0f, 0.35f);

            if (playerSide != null && pLost > 0) GameUI.I.FloatText(playerSide.transform.position + Vector3.up * 2.2f, "-" + pLost + " troops", Palette.AzureLight, 0.9f);
            if (enemySide != null && eLost > 0) GameUI.I.FloatText(enemySide.transform.position + Vector3.up * 2.2f, "-" + eLost + " troops", Palette.CrimsonLight, 0.9f);

            GameManager.I.InBattle = false;
            battleState = BattleState.Idle;
            onDone?.Invoke();
        }

        static int TroopCount(List<Soldier> list, Soldier general) => list.Count - (general != null && general.isAlive ? 1 : 0);

        // Spawn both armies. The 2015 version cloned the tactics prefab for every troop and then
        // copied stats into the clone's class; troops now get a fresh, un-boosted class instance.
        void InitializeWar(Player player, Player enemy, Team attacker, int flankBonus)
        {
            playerTroops.Clear();
            enemyTroops.Clear();

            playerGeneral = SpawnSoldier(player.playerClass, Team.Player, true, PlayerSpawner, 90f,
                player.currentHealth, player.modifiedAttack + (attacker == Team.Player ? flankBonus : 0), player.modifiedDefense);
            controller = playerGeneral.gameObject.AddComponent<PlayerController>();
            playerTroops.Add(playerGeneral);

            for (int i = 0; i < player.currentUnits; i++)
            {
                var cls = player.playerClass.Fresh();
                var s = SpawnSoldier(cls, Team.Player, false, PlayerSpawner + new Vector3(-3f, 0, 0), 90f, cls.BaseHP, cls.BaseAttack, cls.BaseDefense);
                s.gameObject.AddComponent<TroopAI>();
                playerTroops.Add(s);
            }

            enemyGeneral = SpawnSoldier(enemy.playerClass, Team.Enemy, true, EnemySpawner, 270f,
                enemy.currentHealth, enemy.modifiedAttack + (attacker == Team.Enemy ? flankBonus : 0), enemy.modifiedDefense);
            enemyGeneral.gameObject.AddComponent<TroopAI>();
            enemyTroops.Add(enemyGeneral);

            int n = enemy.currentUnits;
            int cols = Mathf.Min(10, Mathf.Max(1, n));
            for (int i = 0; i < n; i++)
            {
                int row = i / cols, col = i % cols;
                var p = EnemySpawner + new Vector3(3f + row * 1.6f, 0f, (col - (cols - 1) / 2f) * 2.2f);
                var cls = enemy.playerClass.Fresh();
                var s = SpawnSoldier(cls, Team.Enemy, false, p, 270f, cls.BaseHP, cls.BaseAttack, cls.BaseDefense);
                s.gameObject.AddComponent<TroopAI>();
                enemyTroops.Add(s);
            }
        }

        Soldier SpawnSoldier(BaseClass cls, Team team, bool general, Vector3 localPos, float yaw, int hp, int atk, int def)
        {
            var go = new GameObject((general ? "General_" : "Troop_") + cls.ClassName);
            go.transform.SetParent(troopsRoot, false);
            go.transform.localPosition = localPos;
            go.transform.rotation = Quaternion.Euler(0, yaw, 0);
            var s = go.AddComponent<Soldier>();
            s.Setup(cls, team, general, hp, atk, def);
            if (s.agent.isOnNavMesh == false) s.agent.Warp(go.transform.position);
            return s;
        }

        // ---------------- formations (2015 GeneralFormation / OffensiveFormation / DefensiveFormation) ----------------

        public void ApplyFormation(Formation f)
        {
            formation = f;
            int atkMod = f == Formation.Wedge ? 10 : f == Formation.Phalanx ? -10 : 0;
            int defMod = f == Formation.Wedge ? -10 : f == Formation.Phalanx ? 10 : 0;
            foreach (var s in playerTroops)
            {
                s.modifiedAttack = s.baseAttack + atkMod;
                s.modifiedDefense = s.baseDefense + defMod;
            }

            int count = playerTroops.Count - 1;
            BuildSlots(f, count);
            int k = 0;
            foreach (var s in playerTroops)
            {
                if (s == playerGeneral) continue;
                var world = warRoot.TransformPoint(formationSlots[k]);
                s.agent.Warp(world);
                s.transform.rotation = Quaternion.Euler(0, 90f, 0);
                s.model.ResetMotionAnchor();
                s.GetComponent<TroopAI>()?.Hold(world);
                k++;
            }
            if (battleState == BattleState.Pre_Battle) AudioBank.I.Play(Sfx.Step, 0.8f);
        }

        void BuildSlots(Formation f, int count)
        {
            formationSlots.Clear();
            var center = PlayerSpawner + new Vector3(-3f, 0f, 0f);
            switch (f)
            {
                case Formation.Line:      // 20 per row, pairs spreading out from the centre
                    for (int i = 0; i < count; i++)
                    {
                        int row = i / 20, col = i % 20;
                        float z = (1f + (col / 2) * 2f) * (col % 2 == 0 ? 1f : -1f);
                        formationSlots.Add(center + new Vector3(-row * 2f, 0f, z));
                    }
                    break;
                case Formation.Wedge:     // row r holds r soldiers: a point aimed at the enemy
                    {
                        int row = 1, col = 0;
                        for (int i = 0; i < count; i++)
                        {
                            float z;
                            if (row % 2 == 1) z = col == 0 ? 0f : ((col + 1) / 2) * 2f * (col % 2 == 1 ? 1f : -1f);
                            else z = (1f + (col / 2) * 2f) * (col % 2 == 0 ? 1f : -1f);
                            formationSlots.Add(center + new Vector3(-(row - 1) * 1.5f, 0f, z));
                            col++;
                            if (col >= row) { row++; col = 0; }
                        }
                    }
                    break;
                case Formation.Phalanx:   // 10 per row, tight ranks
                    for (int i = 0; i < count; i++)
                    {
                        int row = i / 10, col = i % 10;
                        float z = (0.75f + (col / 2) * 1.5f) * (col % 2 == 0 ? 1f : -1f);
                        formationSlots.Add(center + new Vector3(-row * 1.5f, 0f, z));
                    }
                    break;
            }
        }

        // ---------------- commands ----------------

        public void AdvanceCommand()
        {
            foreach (var s in playerTroops) if (s != playerGeneral) s.GetComponent<TroopAI>()?.Advance();
        }

        public void CoverCommand()
        {
            foreach (var s in playerTroops) if (s != playerGeneral) s.GetComponent<TroopAI>()?.Cover();
        }

        public void FallBackCommand()
        {
            BuildSlots(formation, playerTroops.Count - 1);
            int k = 0;
            foreach (var s in playerTroops)
            {
                if (s == playerGeneral) continue;
                s.GetComponent<TroopAI>()?.FallBack(warRoot.TransformPoint(formationSlots[k]));
                k++;
            }
        }

        public void OpenCommandMenu()
        {
            if (CommandMenuOpen || battleState != BattleState.In_Battle) return;
            CommandMenuOpen = true;
            GameManager.SetTimeScale(0f);
            AudioBank.I.DuckMusic(true);
            var menu = new KeyMenu("COMMAND");
            menu.Add("Advance", () => { AdvanceCommand(); Announce("ADVANCE!"); CloseCommandMenu(); });
            menu.Add("Cover Me", () => { CoverCommand(); Announce("COVER ME!"); CloseCommandMenu(); });
            menu.Add("Fall Back", () => { FallBackCommand(); Announce("FALL BACK!"); CloseCommandMenu(); });
            menu.Add("Resume", CloseCommandMenu);
            menu.onCancel = CloseCommandMenu;
            GameUI.I.ShowSideMenu(menu, null);
            GameUI.I.SetHint("UP/DOWN  choose     Z  give order     X  resume");
        }

        void Announce(string order)
        {
            if (playerGeneral != null) GameUI.I.FloatText(playerGeneral.Center + Vector3.up * 1.4f, order, Palette.Gold, 1.1f);
        }

        void CloseCommandMenu()
        {
            if (!CommandMenuOpen) return;
            CommandMenuOpen = false;
            GameUI.I.HideSideMenu();
            GameManager.SetTimeScale(1f);
            AudioBank.I.DuckMusic(false);
            if (battleState == BattleState.In_Battle)
                GameUI.I.SetHint("WASD  move     LMB / J  attack     RMB / K  Aura Thrust     SPACE  commands");
        }

        void EndWar()
        {
            for (int i = troopsRoot.childCount - 1; i >= 0; i--) Destroy(troopsRoot.GetChild(i).gameObject);
            playerTroops.Clear();
            enemyTroops.Clear();
            controller = null;
        }
    }
}
