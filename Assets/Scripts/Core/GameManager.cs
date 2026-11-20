using System.Collections;
using Unity.Cinemachine;
using Unity.Cinemachine.TargetTracking;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace LLTacticalWar
{
    // Boots the whole game from one GameObject in the Main scene and owns the shell:
    // Title -> Playing <-> Paused -> Ended. The tactics map and the battle stage are two
    // scene objects toggled on and off (as the 2015 "TacticScene" / "WarScene" were). The turn
    // and battle logic sit on an always-active Systems object, because a deactivated
    // GameObject stops its coroutines.
    public class GameManager : MonoBehaviour
    {
        public static GameManager I;

        public enum GameState { Title, Playing, Paused, Ended }
        public GameState State { get; private set; } = GameState.Title;
        public static bool IsPlaying => I != null && I.State == GameState.Playing;

        public bool InBattle;
        public Transform TacticRoot { get; private set; }
        public Transform BattleRoot { get; private set; }

        Camera cam;
        CinemachineBrain brain;
        CinemachineCamera tacticCam, titleCam;
        Transform boardCenter;
        PointerScript pointer;
        TileMap map;
        float titleAngle = 200f;
        static float baseTimeScale = 1f;

        public static void SetTimeScale(float s) { baseTimeScale = s; ApplyTimeScale(); }
        static void ApplyTimeScale() { Time.timeScale = (I != null && I.State == GameState.Paused) ? 0f : baseTimeScale; }

        void Awake()
        {
            I = this;
            baseTimeScale = 1f;
            Time.timeScale = 1f;

            SetupRendering();

            new GameObject("Audio").AddComponent<AudioBank>().transform.SetParent(transform, false);
            new GameObject("Fx").AddComponent<Fx>().transform.SetParent(transform, false);
            var ui = new GameObject("UI").AddComponent<GameUI>();
            ui.transform.SetParent(transform, false);

            // tactics scene (visuals)
            TacticRoot = new GameObject("TacticScene").transform;
            TacticRoot.SetParent(transform, false);
            map = new GameObject("TileMap").AddComponent<TileMap>();
            map.transform.SetParent(TacticRoot, false);
            pointer = new GameObject("Pointer").AddComponent<PointerScript>();
            pointer.transform.SetParent(TacticRoot, false);
            pointer.SetTile(map.tileX / 2, 1, true);
            boardCenter = new GameObject("BoardCenter").transform;
            boardCenter.SetParent(TacticRoot, false);
            boardCenter.position = map.Center;
            Fx.I.CreateMotes(TacticRoot, map.Center + Vector3.up * 3f, new Vector3(map.tileX * map.tileSize, 4f, map.tileZ * map.tileSize));

            // battle stage (visuals) - far away from the board
            BattleRoot = new GameObject("BattleScene").transform;
            BattleRoot.SetParent(transform, false);
            BattleRoot.position = new Vector3(600f, 0f, 0f);

            // systems (always active)
            var systems = new GameObject("Systems");
            systems.transform.SetParent(transform, false);
            systems.AddComponent<TurnManager>();
            systems.AddComponent<BattleScene>().Build(BattleRoot);
            BattleRoot.gameObject.SetActive(false);

            BuildCameras();
            TurnManager.instance.SpawnArmies();

            State = GameState.Title;
            GameUI.I.ShowTitle(true);
            GameUI.I.SetTacticHud(false);
            StartCoroutine(GameUI.I.Fade(0f, 0.8f));
        }

        void SetupRendering()
        {
            // key light: warm, from the south-west, angled across the board
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.transform.SetParent(transform, false);
            sun.type = LightType.Directional;
            sun.color = Palette.SunKey;
            sun.intensity = 1.25f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.82f;
            sun.transform.rotation = Quaternion.Euler(44f, 38f, 0f);
            RenderSettings.sun = sun;

            // the ambient answer: cool sky, neutral equator, dark warm ground
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Palette.AmbientSky;
            RenderSettings.ambientEquatorColor = Palette.AmbientEq;
            RenderSettings.ambientGroundColor = Palette.AmbientGnd;
            RenderSettings.skybox = null;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = Palette.SkyHorizon;
            RenderSettings.fogStartDistance = 55f;
            RenderSettings.fogEndDistance = 190f;

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(transform, false);
            cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Palette.SkyTop;
            cam.fieldOfView = 40f;
            cam.farClipPlane = 400f;
            cam.allowHDR = true;
            camGo.AddComponent<AudioListener>();
            var data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            brain = camGo.AddComponent<CinemachineBrain>();
            brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, 0.9f);

            // one global volume
            var volGo = new GameObject("GlobalVolume");
            volGo.transform.SetParent(transform, false);
            var vol = volGo.AddComponent<Volume>();
            vol.isGlobal = true;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            var tone = profile.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.ACES);
            var bloom = profile.Add<Bloom>(true);
            bloom.intensity.Override(0.35f);
            bloom.threshold.Override(1.1f);
            bloom.tint.Override(Palette.SunKey);
            var vig = profile.Add<Vignette>(true);
            vig.intensity.Override(0.22f);
            vig.color.Override(Palette.Ink);
            var adj = profile.Add<ColorAdjustments>(true);
            adj.saturation.Override(10f);
            adj.contrast.Override(6f);
            adj.postExposure.Override(0.15f);
            var wb = profile.Add<WhiteBalance>(true);
            wb.temperature.Override(6f);
            vol.sharedProfile = profile;
        }

        void BuildCameras()
        {
            // tactics: looks down at the board, follows the cursor with damping
            tacticCam = new GameObject("CM_Tactic").AddComponent<CinemachineCamera>();
            tacticCam.transform.SetParent(TacticRoot, false);
            // narrow lens from further back: less edge distortion on the figures
            tacticCam.transform.rotation = Quaternion.Euler(54f, 0f, 0f);
            tacticCam.Lens.FieldOfView = 30f;
            tacticCam.Lens.FarClipPlane = 400f;
            var follow = tacticCam.gameObject.AddComponent<CinemachineFollow>();
            follow.FollowOffset = new Vector3(0f, 21f, -15.2f);
            follow.TrackerSettings.BindingMode = BindingMode.WorldSpace;
            follow.TrackerSettings.PositionDamping = new Vector3(0.45f, 0.45f, 0.45f);
            tacticCam.Follow = pointer.transform;
            tacticCam.Priority = 10;

            // title backdrop: a slow orbit of the live board
            titleCam = new GameObject("CM_Title").AddComponent<CinemachineCamera>();
            titleCam.transform.SetParent(TacticRoot, false);
            titleCam.Lens.FieldOfView = 40f;
            titleCam.Lens.FarClipPlane = 400f;
            var aim = titleCam.gameObject.AddComponent<CinemachineRotationComposer>();
            aim.Composition.ScreenPosition = new Vector2(0.12f, -0.05f);
            titleCam.LookAt = boardCenter;
            titleCam.Priority = 50;
            UpdateTitleCam(0f);
        }

        void UpdateTitleCam(float dt)
        {
            titleAngle += dt * 5f;
            float a = titleAngle * Mathf.Deg2Rad;
            titleCam.transform.position = map.Center + new Vector3(Mathf.Sin(a) * 34f, 13f, Mathf.Cos(a) * 34f);   // low enough to see the horizon past the castles
        }

        void Update()
        {
            switch (State)
            {
                case GameState.Title:
                    UpdateTitleCam(Time.unscaledDeltaTime);
                    if (GameInput.AnyStart && GameUI.I.FadeAlpha < 0.05f) StartCoroutine(StartGame());
                    break;
                case GameState.Playing:
                    if (GameInput.Pause && GameUI.I.FadeAlpha < 0.05f) Pause();
                    break;
            }
        }

        IEnumerator StartGame()
        {
            State = GameState.Playing;
            AudioBank.I.Play(Sfx.UiConfirm);
            GameUI.I.ShowTitle(false);
            titleCam.Priority = 0;
            yield return Ease.WaitUnscaled(0.9f);
            GameUI.I.SetTacticHud(true);
            TurnManager.instance.Begin();
        }

        // ---------------- pause ----------------

        void Pause()
        {
            State = GameState.Paused;
            ApplyTimeScale();
            AudioBank.I.DuckMusic(true);
            AudioBank.I.Play(Sfx.UiConfirm, 0.7f);
            var menu = new KeyMenu();
            menu.Add("Resume", Resume);
            menu.Add("Restart", Restart);
            menu.AddSlider(() => "Music   <   " + Mathf.RoundToInt(AudioBank.MusicVolume * 100) + "%   >", d => AudioBank.MusicVolume = Mathf.Clamp01(AudioBank.MusicVolume + d * 0.1f));
            menu.AddSlider(() => "Sound   <   " + Mathf.RoundToInt(AudioBank.SfxVolume * 100) + "%   >", d => AudioBank.SfxVolume = Mathf.Clamp01(AudioBank.SfxVolume + d * 0.1f));
            menu.Add("Quit to Title", Restart);
            menu.Add("Quit Game", QuitGame);
            menu.onCancel = Resume;
            GameUI.I.ShowPause(menu);
        }

        void Resume()
        {
            GameUI.I.HidePause();
            State = GameState.Playing;
            ApplyTimeScale();
            AudioBank.I.DuckMusic(false);
        }

        public void Restart() { StartCoroutine(RestartRoutine()); }

        IEnumerator RestartRoutine()
        {
            yield return GameUI.I.Fade(1f, 0.3f);
            Time.timeScale = 1f;
            baseTimeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ---------------- scene toggle + ending ----------------

        public void ShowBattle(bool on)
        {
            brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f);
            TacticRoot.gameObject.SetActive(!on);
            BattleRoot.gameObject.SetActive(on);
            StartCoroutine(RestoreBlend());
        }

        IEnumerator RestoreBlend()
        {
            yield return null;
            yield return null;
            brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, 0.9f);
        }

        public void EndGame(bool victory) { StartCoroutine(EndRoutine(victory)); }

        IEnumerator EndRoutine(bool victory)
        {
            yield return Ease.WaitUnscaled(1.2f);
            State = GameState.Ended;
            GameUI.I.HideContextMenu();
            GameUI.I.HideUnitCard();
            GameUI.I.SetHint("");
            AudioBank.I.Play(victory ? Sfx.Victory : Sfx.Defeat);
            if (victory) Fx.I.Confetti(cam.transform.position + cam.transform.forward * 8f - Vector3.up * 2f);
            var t = TurnManager.instance;
            string stats = "ROUNDS " + t.round + "     BATTLES " + t.battlesFought + "     ENEMIES DEFEATED " + t.enemiesDefeated + "     UNITS LOST " + t.unitsLost;
            var menu = new KeyMenu();
            menu.Add("Play Again", Restart);
            menu.Add("Quit Game", QuitGame);
            GameUI.I.ShowEnded(victory, stats, menu);
        }
    }
}
