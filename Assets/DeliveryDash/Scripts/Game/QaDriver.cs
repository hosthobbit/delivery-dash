using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace DeliveryDash
{
    /// <summary>
    /// Automated testing only. Enabled by command-line flags (desktop) or URL parameters (browser):
    ///   --autoplay / ?autoplay   a bot rides the route, steering round obstacles and throwing at houses
    ///   --autoshot DIR           also saves screenshots at key moments, then quits
    ///   --practice               the bot does the first-time practice run (then quits)
    ///   --inputtest              presses real keys and gamepad buttons through the Input System
    ///                            (virtual devices) and checks the game responds; pause, restart, menus
    ///   --quality N              force a graphics preset
    /// Results are logged as DD_RESULT / DD_PERF / DD_PRACTICE / DD_INPUT lines.
    /// </summary>
    public class QaDriver : MonoBehaviour
    {
        public GameFlow Game;
        static QaDriver persistent;   // the input test survives scene reloads (restarts)
        bool autoplay, practice, inputTest;
        string shotDir;
        float thrownFor = -1f;
        int frames;
        float frameTimeSum, worstFrame;
        readonly float[] laneProbe = { -2.2f, -1.1f, 0f, 1.1f, 2.2f };
        float targetX;
        int passes, fails;

        GameFlow G => GameFlow.Instance ? GameFlow.Instance : Game;

        void Start()
        {
            if (persistent && persistent != this) { Destroy(this); return; }
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--autoplay") autoplay = true;
                if (args[i] == "--autoshot" && i + 1 < args.Length) { autoplay = true; shotDir = args[i + 1]; }
                if (args[i] == "--practice") practice = true;
                if (args[i] == "--inputtest") inputTest = true;
                if (args[i] == "--quality" && i + 1 < args.Length && int.TryParse(args[i + 1], out int q)) Settings.Quality = q;
            }
            string url = Application.absoluteURL ?? "";
            if (url.Contains("autoplay")) autoplay = true;
            if (!autoplay && !practice && !inputTest) { enabled = false; return; }
            AudioListener.volume = 0f;           // automated runs stay silent
            G.ApplySettings();
            if (inputTest)
            {
                persistent = this;
                transform.SetParent(null);
                DontDestroyOnLoad(gameObject);
                StartCoroutine(InputTest());
                return;
            }
            G.AutoPilot = true;
            if (practice) { Settings.TutorialDone = false; GameFlow.QaPractice = true; StartCoroutine(Practice()); return; }
            Debug.Log("DD_QA autoplay" + (shotDir != null ? " autoshot " + shotDir : ""));
            StartCoroutine(Run());
        }

        // ------------------------------------------------------------------ full route with screenshots

        IEnumerator Run()
        {
            yield return new WaitForSeconds(2.5f);
            yield return Shot("01_title");
            G.Play();
            yield return new WaitForSeconds(1.2f);
            yield return Shot("02_countdown");
            while (G.Current != GameFlow.Phase.Playing) yield return null;
            float t0 = Time.realtimeSinceStartup;
            bool shotArc = false, shotMid = false, shotHazard = false, shotDog = false, shotCar = false;
            while (G.Current == GameFlow.Phase.Playing)
            {
                float z = G.Bike.transform.position.z;
                if (!shotArc && z > 40f)
                {
                    // show the aiming preview for a screenshot
                    G.Thrower.ChooseSide(1);
                    G.Thrower.BeginCharge();
                    yield return new WaitForSeconds(0.45f);
                    yield return Shot("03_aiming");
                    G.Thrower.Release();
                    shotArc = true;
                }
                if (!shotMid && z > 120f) { yield return Shot("04_riding"); shotMid = true; }
                if (!shotHazard && z > 246f) { yield return Shot("05_crossing"); shotHazard = true; }
                if (!shotCar && z > 404f) { yield return Shot("06_reversing_car"); shotCar = true; }
                if (!shotDog && z > 549f) { yield return Shot("07_dog"); shotDog = true; }
                yield return null;
            }
            var s = G.Score;
            Debug.Log($"DD_RESULT score={s.Score} delivered={s.Delivered}/{G.Route.Subscribers.Count} missed={s.Missed} crashes={s.Crashes} windows={s.Windows} people={s.Pedestrians} throws={s.Throws} time={G.RoundTime:0.0}s real={Time.realtimeSinceStartup - t0:0.0}s");
            Debug.Log($"DD_EVENTS seconds={G.RoundTime.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)} score={s.Score} events={Scoreboard.Encode(s.Events)}");
            LogPerf();
            while (G.Current != GameFlow.Phase.Results) yield return null;
            yield return new WaitForSeconds(1.0f);
            yield return Shot("08_results");
            Debug.Log("DD_QA_DONE");
            if (shotDir != null) Application.Quit();
        }

        void LogPerf()
        {
            float avg = frames > 0 ? frameTimeSum / frames : 0f;
            Debug.Log($"DD_PERF frames={frames} avg_ms={avg * 1000f:0.0} avg_fps={(avg > 0 ? 1f / avg : 0):0.0} worst_ms={worstFrame * 1000f:0.0} quality={QualitySettings.names[QualitySettings.GetQualityLevel()]} res={Screen.width}x{Screen.height} gpu={SystemInfo.graphicsDeviceName}");
        }

        IEnumerator Shot(string name)
        {
            if (shotDir == null) yield break;
            Directory.CreateDirectory(shotDir);
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(shotDir, name + ".png"));
            Debug.Log("DD_SHOT " + name);
            yield return null;  // let the capture finish before anything changes
        }

        // ------------------------------------------------------------------ practice run

        IEnumerator Practice()
        {
            yield return new WaitForSeconds(2f);
            G.Play();
            float t = 0f;
            int lastStep = -1;
            while (G.Current == GameFlow.Phase.Practice && t < 120f)
            {
                if (G.PracticeStep != lastStep) { lastStep = G.PracticeStep; Debug.Log($"DD_PRACTICE step {lastStep} at z={G.Bike.transform.position.z:0.0}"); }
                if (Mathf.Repeat(t, 5f) < Time.deltaTime) Debug.Log($"DD_PRACTICE t={t:0} z={G.Bike.transform.position.z:0.0} x={G.Bike.transform.position.x:0.0} state={G.Bike.Current} speed={G.Bike.Speed:0.0}");
                t += Time.deltaTime;
                yield return null;
            }
            bool ok = G.Current == GameFlow.Phase.Countdown || G.Current == GameFlow.Phase.Playing;
            Debug.Log($"DD_PRACTICE {(ok ? "PASS" : "FAIL")} reached={G.Current} tutorialSaved={Settings.TutorialDone} time={t:0.0}s");
            yield return new WaitForSeconds(1f);
            Application.Quit();
        }

        // ------------------------------------------------------------------ input test (virtual keyboard + gamepad)

        void Check(string name, bool ok, string detail = "")
        {
            if (ok) passes++; else fails++;
            Debug.Log($"DD_INPUT {name} {(ok ? "PASS" : "FAIL")} {detail}");
        }

        static void Keys(Keyboard kb, params Key[] keys) => InputSystem.QueueStateEvent(kb, new KeyboardState(keys));

        IEnumerator Hold(Keyboard kb, Key key, float seconds)
        {
            Keys(kb, key);
            yield return new WaitForSecondsRealtime(seconds);
            Keys(kb);
            yield return null;
        }

        IEnumerator Tap(Keyboard kb, Key key)
        {
            Keys(kb, key);
            yield return null; yield return null;
            Keys(kb);
            yield return null; yield return null;
        }

        IEnumerator InputTest()
        {
            Settings.TutorialDone = true;
            // the test window may not have keyboard focus (virtual screen): don't drop its input
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            var kb = InputSystem.AddDevice<Keyboard>("QA Keyboard");
            var pad = InputSystem.AddDevice<Gamepad>("QA Gamepad");
            yield return new WaitForSecondsRealtime(2.5f);

            // menus with the keyboard: Play is focused; Enter presses it
            var selected = EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;
            Check("menu_focus", selected && selected.name.StartsWith("Play"), selected ? selected.name : "nothing selected");
            yield return Tap(kb, Key.Enter);
            yield return new WaitForSecondsRealtime(0.5f);
            Check("menu_enter_starts", G.Current == GameFlow.Phase.Countdown || G.Current == GameFlow.Phase.Playing, G.Current.ToString());
            if (G.Current == GameFlow.Phase.Title) G.Play();   // carry on testing the rest
            for (float w = 0; w < 10f && G.Current != GameFlow.Phase.Playing; w += Time.unscaledDeltaTime) yield return null;
            yield return new WaitForSecondsRealtime(0.5f);

            // keyboard riding
            float x0 = G.Bike.transform.position.x;
            yield return Hold(kb, Key.D, 0.6f);
            Check("steer_right_D", G.Bike.transform.position.x > x0 + 0.3f, $"x {x0:0.00} -> {G.Bike.transform.position.x:0.00}");
            x0 = G.Bike.transform.position.x;
            yield return Hold(kb, Key.LeftArrow, 0.6f);
            Check("steer_left_arrow", G.Bike.transform.position.x < x0 - 0.3f, $"x {x0:0.00} -> {G.Bike.transform.position.x:0.00}");
            Keys(kb, Key.W);
            yield return new WaitForSecondsRealtime(1.5f);
            Check("accelerate_W", G.Bike.Speed > G.Bike.CruiseSpeed + 0.5f, $"speed {G.Bike.Speed:0.0}");
            Keys(kb, Key.S);
            yield return new WaitForSecondsRealtime(1.0f);
            Check("brake_S", G.Bike.Speed < G.Bike.CruiseSpeed - 1f, $"speed {G.Bike.Speed:0.0}");
            Keys(kb);
            yield return new WaitForSecondsRealtime(0.5f);

            int throws = G.Score.Throws;
            yield return Tap(kb, Key.E);
            yield return Hold(kb, Key.Space, 0.4f);
            yield return new WaitForSecondsRealtime(0.3f);
            Check("throw_space", G.Score.Throws == throws + 1, $"throws {throws} -> {G.Score.Throws}");

            // pause stops the game; resume restores input
            yield return Tap(kb, Key.Escape);
            bool paused = G.Paused && Time.timeScale == 0f;
            float z0 = G.Bike.transform.position.z;
            yield return new WaitForSecondsRealtime(0.8f);
            Check("pause_escape", paused && Mathf.Approximately(z0, G.Bike.transform.position.z), $"paused={G.Paused} z {z0:0.00} -> {G.Bike.transform.position.z:0.00}");
            Keys(kb, Key.D);   // keys pressed while paused don't move the bike
            yield return new WaitForSecondsRealtime(0.3f);
            Keys(kb);
            Check("no_input_while_paused", Mathf.Approximately(z0, G.Bike.transform.position.z));
            yield return Tap(kb, Key.Escape);
            yield return new WaitForSecondsRealtime(0.6f);
            Check("resume_escape", !G.Paused && G.Bike.transform.position.z > z0 + 1f, $"z {z0:0.00} -> {G.Bike.transform.position.z:0.00}");

            // gamepad
            x0 = G.Bike.transform.position.x;
            InputSystem.QueueStateEvent(pad, new GamepadState { leftStick = new Vector2(-1f, 0f) });
            yield return new WaitForSecondsRealtime(0.6f);
            InputSystem.QueueStateEvent(pad, new GamepadState());
            yield return null;
            Check("steer_gamepad", G.Bike.transform.position.x < x0 - 0.3f, $"x {x0:0.00} -> {G.Bike.transform.position.x:0.00}");
            throws = G.Score.Throws;
            InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.South));
            yield return new WaitForSecondsRealtime(0.4f);
            InputSystem.QueueStateEvent(pad, new GamepadState());
            yield return new WaitForSecondsRealtime(0.3f);
            Check("throw_gamepad", G.Score.Throws == throws + 1, $"throws {throws} -> {G.Score.Throws}");
            InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.Start));
            yield return null; yield return null;
            InputSystem.QueueStateEvent(pad, new GamepadState());
            yield return null;
            Check("pause_gamepad", G.Paused);
            InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.Start));
            yield return null; yield return null;
            InputSystem.QueueStateEvent(pad, new GamepadState());
            yield return new WaitForSecondsRealtime(0.3f);
            Check("resume_gamepad", !G.Paused);

            // restart three times: everything resets each time
            for (int i = 0; i < 3; i++)
            {
                G.Restart();
                yield return new WaitForSecondsRealtime(0.5f);
                for (float w = 0; w < 10f && (G == null || G.Current == GameFlow.Phase.Title); w += Time.unscaledDeltaTime) yield return null;
                bool reset = G.Score.Score == 0 && G.Score.Throws == 0 && G.RoundTime < 0.5f && G.Bike.transform.position.z < 0f;
                for (float w = 0; w < 10f && G.Current != GameFlow.Phase.Playing; w += Time.unscaledDeltaTime) yield return null;
                yield return new WaitForSecondsRealtime(1.5f);
                Check($"restart_{i + 1}", reset && G.Bike.transform.position.z > 2f, $"z={G.Bike.transform.position.z:0.0}");
            }
            G.QuitToMenu();
            yield return new WaitForSecondsRealtime(1f);
            Check("quit_to_menu", G.Current == GameFlow.Phase.Title && !G.Paused && Time.timeScale == 1f);

            Debug.Log($"DD_INPUT_DONE pass={passes} fail={fails}");
            yield return new WaitForSecondsRealtime(0.5f);
            Application.Quit();
        }

        // ------------------------------------------------------------------ autopilot

        void Update()
        {
            var g = G;
            if (!g || inputTest) return;
            if (g.Current == GameFlow.Phase.Playing && Time.unscaledDeltaTime > 0f)
            {
                frames++;
                frameTimeSum += Time.unscaledDeltaTime;
                if (frames > 30) worstFrame = Mathf.Max(worstFrame, Time.unscaledDeltaTime);
            }
            bool riding = g.Current == GameFlow.Phase.Playing || g.Current == GameFlow.Phase.Practice;
            if (!riding || g.Paused) return;
            Drive(g);
            ThrowAtHouses(g);
        }

        /// <summary>Pick the lane with the longest clear run ahead and steer for it; brake if boxed in.</summary>
        void Drive(GameFlow g)
        {
            var bike = g.Bike;
            var p = bike.transform.position;
            float best = -1f;
            foreach (float lane in laneProbe)
            {
                var origin = new Vector3(lane, 0.6f, p.z + 0.5f);
                float clear = Physics.SphereCast(origin, 0.6f, Vector3.forward, out var hit, 18f, bike.Obstacles, QueryTriggerInteraction.Collide) ? hit.distance : 18f;
                foreach (var hole in g.Route.Potholes)
                {
                    var hp = hole.transform.position;
                    if (Mathf.Abs(hp.x - lane) < 0.9f && hp.z > p.z && hp.z - p.z < clear) clear = hp.z - p.z;
                }
                float score = clear - Mathf.Abs(lane - p.x) * 0.6f;
                if (score > best) { best = score; targetX = lane; }
            }
            float steer = Mathf.Clamp((targetX - p.x) * 0.9f - bike.Heading / 25f, -1f, 1f);
            var ahead = new Vector3(p.x, 0.6f, p.z + 0.3f);
            bool blocked = Physics.SphereCast(ahead, 0.5f, Vector3.forward, out _, 4f, bike.Obstacles, QueryTriggerInteraction.Collide);
            // ease off when something is right ahead (but never stop dead: keep steering out of it)
            bike.SetInput(steer, 0f, blocked && bike.Speed > 3f ? 1f : 0f);
        }

        void ThrowAtHouses(GameFlow g)
        {
            var bike = g.Bike;
            var next = g.Route.NextTarget(bike.transform.position.z, 0, 16f);
            if (!next || thrownFor == next.Doorstep.position.z) return;
            float dz = next.Doorstep.position.z - bike.transform.position.z;
            if (dz < 7f && dz > -1f)
            {
                var target = next.Doorstep.position;
                target.y = g.Thrower.GroundHeight;
                if (g.Thrower.ThrowAt(target)) thrownFor = next.Doorstep.position.z;
            }
        }
    }
}
