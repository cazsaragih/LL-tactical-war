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
        Transform boardCenter, mapFocus;
        PointerScript pointer;
        TileMap map;
        float titleTime;
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
            // key: warm late-morning sun from the south-west, angled across the board, with slow cloud
            // shadows drifting through it. The only light: the toon shader's shadow tone stands in for
            // fill and bounce. Its shadows are filtered soft at full strength so the shader can cut the
            // penumbra at its middle into a clean, hard edge.
            var sun = NewLight("Sun", Palette.SunKey, 1.1f, Quaternion.Euler(44f, 38f, 0f), LightShadows.Soft);
            sun.shadowStrength = 1f;
            RenderSettings.sun = sun;
            CloudShadows.Attach(sun);

            // the ambient answer: cool sky (the toon shader's flat lift on the lit tone reads its zenith)
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Palette.AmbientSky;
            RenderSettings.ambientEquatorColor = Palette.AmbientEq;
            RenderSettings.ambientGroundColor = Palette.AmbientGnd;
            // the gradient sky's horizon is the fog colour, so the far world dissolves into blue-grey haze
            RenderSettings.skybox = Palette.Sky();
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = Palette.Haze;
            SetFog(FieldFog);

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(transform, false);
            cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.fieldOfView = 40f;
            cam.farClipPlane = 400f;
            cam.allowHDR = true;
            camGo.AddComponent<AudioListener>();
            var data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            brain = camGo.AddComponent<CinemachineBrain>();
            brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, 0.9f);

            // one global volume: the grade. Depth of field rides each camera (see Lens).
            var volGo = new GameObject("GlobalVolume");
            volGo.transform.SetParent(transform, false);
            var vol = volGo.AddComponent<Volume>();
            vol.isGlobal = true;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.Add<Tonemapping>(true).mode.Override(TonemappingMode.ACES);
            var bloom = profile.Add<Bloom>(true);   // true highlights only: the glow gem, sparks, flashes; tight, so cel edges stay sharp
            bloom.intensity.Override(0.2f);
            bloom.threshold.Override(1.1f);
            bloom.scatter.Override(0.5f);
            bloom.tint.Override(Palette.SunKey);
            var vig = profile.Add<Vignette>(true);
            vig.intensity.Override(0.2f);
            vig.smoothness.Override(0.45f);
            vig.color.Override(Palette.Ink);
            var adj = profile.Add<ColorAdjustments>(true);
            adj.saturation.Override(8f);
            adj.contrast.Override(8f);
            adj.postExposure.Override(0f);
            profile.Add<WhiteBalance>(true).temperature.Override(4f);
            var split = profile.Add<SplitToning>(true);   // warm highlights, cool shadows
            split.shadows.Override(Palette.GradeShadows);
            split.highlights.Override(Palette.GradeHighlights);
            split.balance.Override(-10f);
            vol.sharedProfile = profile;
        }

        // Haze: the field's distance fades into the horizon colour; the battle stage keeps its backdrop
        // hills and keep clear of it, so the duel stands against a blue sky.
        static readonly Vector2 FieldFog = new Vector2(80f, 320f), StageFog = new Vector2(120f, 420f);

        static void SetFog(Vector2 range) { RenderSettings.fogStartDistance = range.x; RenderSettings.fogEndDistance = range.y; }

        Light NewLight(string name, Color color, float intensity, Quaternion rotation, LightShadows shadows)
        {
            var light = new GameObject(name).AddComponent<Light>();
            light.transform.SetParent(transform, false);
            light.transform.rotation = rotation;
            light.type = LightType.Directional;
            light.color = color;
            light.intensity = intensity;
            light.shadows = shadows;
            return light;
        }

        // Depth of field for one camera, through Cinemachine so it blends across camera cuts and
        // tracks its focus every frame. Bokeh, so near and far both soften: the title's orbit reads like a
        // photographed miniature, and the duel's backdrop falls back behind its two actors. Strength is
        // focalLength (mm) over aperture (f-stop); see URP's Bokeh CoC: blur px = 14 * (1 - focus / depth) * maxCoC.
        public static void Lens(CinemachineCamera vcam, float focalLength, float aperture, CinemachineVolumeSettings.FocusTrackingMode focus, Transform target = null)
        {
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            var dof = profile.Add<DepthOfField>(true);
            dof.mode.Override(DepthOfFieldMode.Bokeh);
            dof.focusDistance.Override(10f);
            dof.focalLength.Override(focalLength);
            dof.aperture.Override(aperture);
            var settings = vcam.gameObject.AddComponent<CinemachineVolumeSettings>();
            settings.Profile = profile;
            settings.FocusTracking = focus;
            settings.FocusTarget = target;
        }

        // The map camera: high and far enough that the board's full width fits and an edge unit
        // isn't oversized, steep enough that obstacles hide no figure (verify.sh sightlines).
        public static readonly Vector3 MapOffset = new Vector3(0f, 24.5f, -17f);

        void BuildCameras()
        {
            // tactics: looks down at the board, follows the cursor (through a framing clamp) with damping
            mapFocus = new GameObject("MapFocus").transform;
            mapFocus.SetParent(TacticRoot, false);
            UpdateMapFocus(true);
            tacticCam = new GameObject("CM_Tactic").AddComponent<CinemachineCamera>();
            tacticCam.transform.SetParent(TacticRoot, false);
            tacticCam.transform.rotation = Quaternion.LookRotation(-MapOffset);
            tacticCam.Lens.FieldOfView = 30f;
            tacticCam.Lens.FarClipPlane = 400f;
            var follow = tacticCam.gameObject.AddComponent<CinemachineFollow>();
            follow.FollowOffset = MapOffset;
            follow.TrackerSettings.BindingMode = BindingMode.WorldSpace;
            follow.TrackerSettings.PositionDamping = new Vector3(0.45f, 0.45f, 0.45f);
            tacticCam.Follow = mapFocus;
            tacticCam.Priority = 10;
            // no depth of field on the map: every row, the enemy's far one included, keeps crisp cel edges

            // title backdrop: a slow orbit of the live board, in light miniature focus
            titleCam = new GameObject("CM_Title").AddComponent<CinemachineCamera>();
            titleCam.transform.SetParent(TacticRoot, false);
            titleCam.Lens.FieldOfView = 40f;
            titleCam.Lens.FarClipPlane = 400f;
            var aim = titleCam.gameObject.AddComponent<CinemachineRotationComposer>();
            aim.Composition.ScreenPosition = new Vector2(0.12f, -0.05f);
            titleCam.LookAt = boardCenter;
            titleCam.Priority = 50;
            Lens(titleCam, 300f, 8f, CinemachineVolumeSettings.FocusTrackingMode.LookAtTarget);
            UpdateTitleCam(0f);
        }

        void UpdateTitleCam(float dt)
        {
            // A slow swing over the open east meadow, the board side-on with a camp at each end: a full
            // orbit passes over the camps, where a tower or the keep fills the frame. Low enough to see
            // the horizon past the castles.
            titleTime += dt;
            float a = (90f + 30f * Mathf.Sin(titleTime * 0.08f)) * Mathf.Deg2Rad;
            titleCam.transform.position = map.Center + new Vector3(Mathf.Sin(a) * 46f, 13f, Mathf.Cos(a) * 46f);
        }

        // The map camera frames the board, not the meadow round it: it follows the pin, clamped so the
        // frame's bottom edge never falls more than BoardMargin.x below the board's near edge, nor its
        // top past BoardMargin.y beyond the far one, nor its near corners past the board's sides. At the
        // default lens the whole field stays in view with the near rows above the HUD.
        static readonly Vector3 BoardMargin = new Vector3(2.4f, 4.6f, 1.2f);   // near, far (clear of the top plates), sides

        void UpdateMapFocus(bool snap = false)
        {
            float fov = (tacticCam != null ? tacticCam.Lens.FieldOfView : 30f) * 0.5f * Mathf.Deg2Rad;
            float h = MapOffset.y, back = -MapOffset.z, pitch = Mathf.Atan2(h, back);
            float near = h / Mathf.Tan(pitch + fov) - back, far = h / Mathf.Tan(pitch - fov) - back;
            float halfW = h / Mathf.Sin(pitch + fov) * Mathf.Cos(fov) * Mathf.Tan(fov) * cam.aspect;
            float w = map.tileX * map.tileSize, d = map.tileZ * map.tileSize;
            float zMin = -BoardMargin.x - near, zMax = d + BoardMargin.y - far;
            float xMin = halfW - BoardMargin.z, xMax = w - halfW + BoardMargin.z;
            var p = pointer.transform.position;
            mapFocus.position = new Vector3(xMin <= xMax ? Mathf.Clamp(p.x, xMin, xMax) : w / 2f, 0f,
                                            zMin <= zMax ? Mathf.Clamp(p.z, zMin, zMax) : (zMin + zMax) / 2f);
        }

        void LateUpdate()
        {
            if (mapFocus != null && TacticRoot.gameObject.activeInHierarchy) UpdateMapFocus();
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
            SetFog(on ? StageFog : FieldFog);
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
            var ledger = new[] { ("Rounds", t.round), ("Battles", t.battlesFought), ("Enemies Defeated", t.enemiesDefeated), ("Units Lost", t.unitsLost) };
            var menu = new KeyMenu();
            menu.Add("Play Again", Restart);
            menu.Add("Quit Game", QuitGame);
            GameUI.I.ShowEnded(victory, ledger, menu);
        }
    }
}
