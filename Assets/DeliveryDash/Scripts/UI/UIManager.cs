using System.Collections;
using System.Collections.Generic;
using DeliveryDash.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace DeliveryDash
{
    /// <summary>
    /// The whole interface, built in code at start-up (so there are no fragile serialized UI
    /// references): title, how to play, settings with rebinding, pause, HUD, feedback popups,
    /// results and the public scoreboard. Works with mouse, keyboard and gamepad.
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        public Camera Cam;

        Canvas canvas;
        RectTransform root;
        GameObject title, hud, pause, results, modal;
        CanvasGroup fade;
        Text scoreText, comboText, statsText, timeText, promptText, countdownText, bestText;
        RectTransform progressFill, progressRider, progressMarks;
        readonly List<Image> houseTicks = new List<Image>();
        Image promptBox;
        readonly List<(Text text, Vector3 world, float t, Vector3 drift)> popups = new List<(Text, Vector3, float, Vector3)>();
        readonly Queue<Text> popupPool = new Queue<Text>();
        RectTransform popupLayer;
        GameObject modalReturnFocus;
        bool practiceHud;

        public bool Busy => modal != null;
        GameFlow Game => GameFlow.Instance;

        void Awake()
        {
            UiKit.Init();
            var cgo = new GameObject("UI Canvas", typeof(RectTransform));
            cgo.transform.SetParent(transform, false);
            canvas = cgo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = cgo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.6f;
            cgo.AddComponent<GraphicRaycaster>();
            root = (RectTransform)cgo.transform;

            if (!FindAnyObjectByType<EventSystem>())
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                // created from code, so give it the standard UI actions explicitly (point, click, navigate, submit, cancel)
                var module = es.AddComponent<InputSystemUIInputModule>();
                module.AssignDefaultActions();
            }

            popupLayer = UiKit.Rect("Popups", root).Fill();
            BuildHud();
            BuildTitle();
            BuildPause();
            var f = UiKit.Box(root, "Fade", Color.black, UiKit.RoundedSmall);
            f.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(-50, -50), new Vector2(50, 50));
            f.raycastTarget = false;
            fade = f.gameObject.AddComponent<CanvasGroup>();
            fade.alpha = 1f;
            fade.blocksRaycasts = false;
            StartCoroutine(FadeTo(0f, 0.6f));
        }

        IEnumerator FadeTo(float a, float time)
        {
            float from = fade.alpha;
            for (float t = 0; t < time; t += Time.unscaledDeltaTime) { fade.alpha = Mathf.Lerp(from, a, t / time); yield return null; }
            fade.alpha = a;
        }

        // ================================================================== title

        void BuildTitle()
        {
            title = UiKit.Rect("Title", root).Fill().gameObject;
            var shade = UiKit.Box(title.transform, "Shade", new Color(0.1f, 0.08f, 0.2f, 0.35f), UiKit.RoundedSmall);
            shade.rectTransform.Place(Vector2.zero, new Vector2(0.48f, 1f), Vector2.zero, Vector2.zero);

            var logo = UiKit.Label(title.transform, "DELIVERY\nDASH", 150, UiKit.Sun, UiKit.Title);
            logo.rectTransform.Place(new Vector2(0.03f, 0.56f), new Vector2(0.45f, 0.96f), Vector2.zero, Vector2.zero);
            logo.lineSpacing = 0.8f;
            logo.Outlined(UiKit.Ink, 6f);
            logo.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(0, -12);
            var hook = UiKit.Label(title.transform, "Thirty deliveries. One bicycle.\nAn entire street having a bad morning.", 34, UiKit.Cream, UiKit.Bold);
            hook.rectTransform.Place(new Vector2(0.03f, 0.47f), new Vector2(0.45f, 0.57f), Vector2.zero, Vector2.zero);
            hook.Outlined(UiKit.Ink, 2f);

            var menu = UiKit.Rect("Menu", title.transform).Place(new Vector2(0.1f, 0.08f), new Vector2(0.38f, 0.46f), Vector2.zero, Vector2.zero);
            UiKit.Stack(menu, 16);
            var play = UiKit.Button(menu, "Play", UiKit.Sun, () => { Hide(title); Game.Play(); }, 44);
            play.Size(86);
            UiKit.Button(menu, "How to Play", UiKit.Cream, ShowHowTo).Size(64);
            UiKit.Button(menu, "Settings", UiKit.Cream, ShowSettings).Size(64);
            UiKit.Button(menu, "Scoreboard", UiKit.Cream, ShowScoreboard).Size(64);
            if (Application.platform != RuntimePlatform.WebGLPlayer) UiKit.Button(menu, "Quit", UiKit.Cream, Application.Quit).Size(64);

            bestText = UiKit.Label(title.transform, "", 30, UiKit.Cream, UiKit.Bold);
            bestText.rectTransform.Place(new Vector2(0.03f, 0.02f), new Vector2(0.45f, 0.07f), Vector2.zero, Vector2.zero);
            bestText.Outlined(UiKit.Ink, 2f);
            var credit = UiKit.Label(title.transform, "Developed by Host Hobbit · hosthobbit.com", 24, UiKit.Cream, UiKit.Bold, TextAnchor.LowerRight);
            credit.rectTransform.Place(new Vector2(0.5f, 0.015f), new Vector2(0.985f, 0.06f), Vector2.zero, Vector2.zero);
            credit.Outlined(UiKit.Ink, 2f);
            title.SetActive(false);
        }

        public void ShowTitle()
        {
            hud.SetActive(false);
            title.SetActive(true);
            int best = Settings.BestScore;
            bestText.text = best > 0 ? $"Your best: {best:N0}" : "";
            Select(title.GetComponentInChildren<Button>());
        }

        void Hide(GameObject g) { if (g) g.SetActive(false); }

        void Select(Selectable s)
        {
            if (!s || !EventSystem.current) return;
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(s.gameObject);
        }

        // ================================================================== modal panels (how to, settings, scoreboard)

        RectTransform OpenModal(string heading, float width = 1100, float height = 860)
        {
            CloseModal();
            modalReturnFocus = EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;
            modal = UiKit.Rect(heading, root).Fill().gameObject;
            var shade = UiKit.Box(modal.transform, "Shade", UiKit.Shade, UiKit.RoundedSmall);
            shade.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(-50, -50), new Vector2(50, 50));
            var panel = UiKit.Box(modal.transform, "Panel", UiKit.Panel);
            panel.rectTransform.Center(new Vector2(width, height));
            panel.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(0, -10);
            var h = UiKit.Label(panel.transform, heading, 64, UiKit.Tomato, UiKit.Title);
            h.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -100), new Vector2(0, -16));
            var body = UiKit.Rect("Body", panel.transform).Place(Vector2.zero, Vector2.one, new Vector2(50, 110), new Vector2(-50, -110));
            var close = UiKit.Button(panel.transform, "Done", UiKit.Sun, CloseModal, 32);
            close.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-130, 24), new Vector2(130, 92));
            Select(close);
            return body;
        }

        public void CloseModal()
        {
            if (!modal) return;
            Destroy(modal);
            modal = null;
            if (modalReturnFocus && modalReturnFocus.activeInHierarchy) Select(modalReturnFocus.GetComponent<Selectable>());
        }

        /// <summary>Escape / B: close whatever is open.</summary>
        public void Back()
        {
            if (rebinding != null) return;
            if (modal) CloseModal();
        }

        void ShowHowTo()
        {
            var body = OpenModal("How to Play", 1250, 900);
            bool pad = Game && Game.Input.UsingGamepad;
            string text =
                "<b>Deliver a paper to every house with a floating marker</b> before you ride past it. The whole front garden counts; " +
                "the closer to the doorstep the better, and <b>straight through the letterbox</b> is best of all.\n\n" +
                (pad
                    ? "<b>Ride</b>  left stick to steer · RT faster · LT brake\n<b>Throw</b>  hold A to aim (hold longer to reach further), let go to throw · LB / RB pick a side · right stick nudges the aim\n<b>Bell</b>  Y    <b>Pause</b>  Start\n\n"
                    : "<b>Ride</b>  A / D or ← → to steer · W faster · S brake\n<b>Throw</b>  point with the mouse, hold the left button to aim (hold longer to reach further), let go to throw\n<b>Keyboard only</b>  Q / E pick a side, hold Space, let go\n<b>Bell</b>  B    <b>Pause</b>  Esc\n\n") +
                "<b>Score</b>  Good 100 · Great 200 · Perfect 300 · Letterbox 400. Deliveries in a row build a combo multiplier (up to x3).\n" +
                "<b>Lose points</b> for missed houses (-50), crashes (-100), broken windows (-150) and bumping into people (-200). Any of these ends your combo.\n" +
                "<b>Bonuses</b> for finishing under par time, and +1000 for delivering to every single house.\n\n" +
                "Watch out for wheelie bins, potholes, an over-friendly dog, a car reversing out of its drive and people crossing the road. They always warn you first!";
            var t = UiKit.Label(body, text, 30, UiKit.Ink, UiKit.Bold, TextAnchor.UpperLeft);
            t.supportRichText = true;
            t.rectTransform.Fill();
        }

        // ------------------------------------------------------------------ settings (with rebinding)

        InputActionRebindingExtensions.RebindingOperation rebinding;

        void ShowSettings()
        {
            var body = OpenModal("Settings", 1300, 980);
            var cols = UiKit.Rect("Columns", body).Fill();
            var left = UiKit.Rect("Left", cols).Place(Vector2.zero, new Vector2(0.48f, 1f), Vector2.zero, Vector2.zero);
            var right = UiKit.Rect("Right", cols).Place(new Vector2(0.52f, 0f), Vector2.one, Vector2.zero, Vector2.zero);
            UiKit.Stack(left, 14);
            UiKit.Stack(right, 8);

            UiKit.Label(left, "Music", 30, UiKit.Ink, UiKit.Black, TextAnchor.MiddleLeft).Size(40);
            UiKit.Slider(left, Settings.Music, v => { Settings.Music = v; Game?.ApplySettings(); }).Size(44);
            UiKit.Label(left, "Sound effects", 30, UiKit.Ink, UiKit.Black, TextAnchor.MiddleLeft).Size(40);
            UiKit.Slider(left, Settings.Sound, v => { Settings.Sound = v; Game?.ApplySettings(); Sfx.Play(Sfx.Kind.Bell, 0.5f); }).Size(44);

            UiKit.Label(left, "Graphics", 30, UiKit.Ink, UiKit.Black, TextAnchor.MiddleLeft).Size(40);
            Button quality = null;
            quality = UiKit.Button(left, $"◀  {Settings.QualityNames[Settings.Quality]}  ▶", UiKit.Cream, () =>
            {
                Settings.Quality = (Settings.Quality + 1) % Settings.QualityNames.Length;
                quality.GetComponentInChildren<Text>().text = $"◀  {Settings.QualityNames[Settings.Quality]}  ▶";
                Game?.ApplySettings();
            }, 30);
            quality.Size(58);

            Button shake = null;
            shake = UiKit.Button(left, ShakeLabel(), UiKit.Cream, () =>
            {
                Settings.CameraShake = !Settings.CameraShake;
                shake.GetComponentInChildren<Text>().text = ShakeLabel();
                Game?.ApplySettings();
            }, 30);
            shake.Size(58);

            Button practice = null;
            practice = UiKit.Button(left, PracticeLabel(), UiKit.Cream, () =>
            {
                Settings.TutorialDone = !Settings.TutorialDone;
                practice.GetComponentInChildren<Text>().text = PracticeLabel();
            }, 30);
            practice.Size(58);

            UiKit.Label(right, "Keyboard controls", 30, UiKit.Ink, UiKit.Black, TextAnchor.MiddleLeft).Size(40);
            if (Game != null)
            {
                foreach (var (action, index, label) in Game.Input.Rebindable)
                {
                    var row = UiKit.Rect(label, right);
                    row.Size(52);
                    var l = UiKit.Label(row, label, 26, UiKit.Ink, UiKit.Bold, TextAnchor.MiddleLeft);
                    l.rectTransform.Place(Vector2.zero, new Vector2(0.55f, 1f), Vector2.zero, Vector2.zero);
                    Button b = null;
                    var a = action; int i = index;
                    b = UiKit.Button(row, GameInput.Describe(a, i), UiKit.Cream, () => Rebind(a, i, b), 26);
                    b.GetComponent<RectTransform>().Place(new Vector2(0.58f, 0.05f), new Vector2(1f, 0.95f), Vector2.zero, Vector2.zero);
                }
                UiKit.Button(right, "Reset controls", UiKit.Cream, () => { Game.Input.ResetOverrides(); ShowSettings(); }, 26).Size(50);
                UiKit.Label(right, "Gamepads are supported too (see How to Play).", 22, new Color(0.35f, 0.32f, 0.4f), UiKit.Bold, TextAnchor.MiddleLeft).Size(36);
            }
        }

        static string ShakeLabel() => "Camera shake: " + (Settings.CameraShake ? "On" : "Off");
        static string PracticeLabel() => "Practice run before playing: " + (Settings.TutorialDone ? "Off" : "On");

        void Rebind(InputAction action, int index, Button button)
        {
            if (rebinding != null) return;
            var label = button.GetComponentInChildren<Text>();
            label.text = "Press a key…";
            bool wasEnabled = action.enabled;
            action.Disable();
            rebinding = action.PerformInteractiveRebinding(index)
                .WithControlsHavingToMatchPath("<Keyboard>")
                .WithCancelingThrough("<Keyboard>/escape")
                .OnComplete(op => FinishRebind(action, index, label, wasEnabled))
                .OnCancel(op => FinishRebind(action, index, label, wasEnabled))
                .Start();
        }

        void FinishRebind(InputAction action, int index, Text label, bool reenable)
        {
            rebinding?.Dispose();
            rebinding = null;
            label.text = GameInput.Describe(action, index);
            if (reenable) action.Enable();
            Game?.Input.SaveOverrides();
        }

        // ------------------------------------------------------------------ scoreboard

        void ShowScoreboard()
        {
            var body = OpenModal("Scoreboard", 1200, 960);
            var status = UiKit.Label(body, "Loading the scores…", 30, UiKit.Ink, UiKit.Bold);
            status.rectTransform.Fill();
            StartCoroutine(Scoreboard.Fetch((list, error) =>
            {
                if (!body) return;
                if (error != null) { status.text = error; return; }
                if (list.Count == 0) { status.text = "No scores yet. Be the first on the board!"; return; }
                status.gameObject.SetActive(false);
                var col = UiKit.Rect("Rows", body).Fill();
                UiKit.Stack(col, 4);
                var header = UiKit.Label(col, "Best rounds from players everywhere", 24, new Color(0.35f, 0.32f, 0.4f), UiKit.Bold);
                header.Size(34);
                for (int i = 0; i < list.Count && i < 16; i++)
                {
                    var e = list[i];
                    var row = UiKit.Box(col, "Row", i < 3 ? new Color(1f, 0.86f, 0.5f) : new Color(1f, 1f, 1f, 0.6f), UiKit.RoundedSmall);
                    row.Size(40);
                    void Cell(string s, float a, float b, TextAnchor al, Font f) { var t = UiKit.Label(row.transform, s, 25, UiKit.Ink, f, al); t.rectTransform.Place(new Vector2(a, 0), new Vector2(b, 1), new Vector2(10, 0), new Vector2(-10, 0)); }
                    Cell((i + 1).ToString(), 0f, 0.08f, TextAnchor.MiddleCenter, UiKit.Black);
                    Cell(e.name, 0.08f, 0.45f, TextAnchor.MiddleLeft, UiKit.Black);
                    Cell(e.score.ToString("N0"), 0.45f, 0.62f, TextAnchor.MiddleRight, UiKit.Black);
                    Cell($"{e.delivered}/{e.houses} delivered", 0.62f, 0.84f, TextAnchor.MiddleLeft, UiKit.Bold);
                    Cell(FormatTime(e.seconds), 0.84f, 1f, TextAnchor.MiddleRight, UiKit.Bold);
                }
            }));
        }

        // ================================================================== pause

        void BuildPause()
        {
            pause = UiKit.Rect("Pause", root).Fill().gameObject;
            var shade = UiKit.Box(pause.transform, "Shade", UiKit.Shade, UiKit.RoundedSmall);
            shade.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(-50, -50), new Vector2(50, 50));
            var panel = UiKit.Box(pause.transform, "Panel", UiKit.Panel);
            panel.rectTransform.Center(new Vector2(640, 640));
            var h = UiKit.Label(panel.transform, "Paused", 72, UiKit.Tomato, UiKit.Title);
            h.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -110), new Vector2(0, -20));
            var col = UiKit.Rect("Buttons", panel.transform).Place(Vector2.zero, Vector2.one, new Vector2(60, 40), new Vector2(-60, -130));
            UiKit.Stack(col, 16);
            UiKit.Button(col, "Resume", UiKit.Sun, () => Game.SetPaused(false), 38).Size(80);
            UiKit.Button(col, "Restart route", UiKit.Cream, () => Game.Restart()).Size(66);
            UiKit.Button(col, "Settings", UiKit.Cream, ShowSettings).Size(66);
            UiKit.Button(col, "How to Play", UiKit.Cream, ShowHowTo).Size(66);
            UiKit.Button(col, "Quit to menu", UiKit.Cream, () => Game.QuitToMenu()).Size(66);
            pause.SetActive(false);
        }

        public void ShowPause(bool on)
        {
            pause.SetActive(on);
            if (!on) CloseModal();
            if (on) Select(pause.GetComponentInChildren<Button>());
        }

        // ================================================================== HUD

        void BuildHud()
        {
            hud = UiKit.Rect("HUD", root).Fill().gameObject;
            // score + combo (top left)
            var scoreBox = UiKit.Box(hud.transform, "Score", new Color(1f, 0.97f, 0.9f, 0.92f));
            scoreBox.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -138), new Vector2(404, -24));
            scoreText = UiKit.Label(scoreBox.transform, "0", 62, UiKit.Ink, UiKit.Title, TextAnchor.MiddleLeft);
            scoreText.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(24, 20), new Vector2(-10, 0));
            var scoreLabel = UiKit.Label(scoreBox.transform, "SCORE", 20, new Color(0.45f, 0.4f, 0.5f), UiKit.Black, TextAnchor.UpperLeft);
            scoreLabel.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(26, 0), new Vector2(0, -8));
            comboText = UiKit.Label(scoreBox.transform, "", 30, UiKit.Tomato, UiKit.Title, TextAnchor.LowerLeft);
            comboText.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(26, 8), new Vector2(0, 0));

            // time + deliveries (top right)
            var statBox = UiKit.Box(hud.transform, "Stats", new Color(1f, 0.97f, 0.9f, 0.92f));
            statBox.rectTransform.Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-404, -138), new Vector2(-24, -24));
            timeText = UiKit.Label(statBox.transform, "0:00", 54, UiKit.Ink, UiKit.Title, TextAnchor.UpperRight);
            timeText.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(10, 0), new Vector2(-24, -8));
            statsText = UiKit.Label(statBox.transform, "", 24, UiKit.Ink, UiKit.Black, TextAnchor.LowerRight);
            statsText.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(10, 12), new Vector2(-24, 0));

            // route progress (top centre): one tick per house
            var bar = UiKit.Box(hud.transform, "Progress", new Color(1f, 0.97f, 0.9f, 0.92f));
            bar.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-470, -78), new Vector2(470, -30));
            var track = UiKit.Box(bar.transform, "Track", new Color(0.78f, 0.74f, 0.68f));
            track.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(18, 18), new Vector2(-18, -18));
            var fillImg = UiKit.Box(track.transform, "Fill", UiKit.Sky);
            progressFill = fillImg.rectTransform;
            progressFill.Place(Vector2.zero, new Vector2(0, 1), Vector2.zero, Vector2.zero);
            progressMarks = UiKit.Rect("Marks", track.transform).Fill();
            var rider = UiKit.Box(track.transform, "Rider", UiKit.Tomato, UiKit.Circle);
            progressRider = rider.rectTransform;
            progressRider.anchorMin = progressRider.anchorMax = new Vector2(0, 0.5f);
            progressRider.sizeDelta = new Vector2(28, 28);
            rider.gameObject.AddComponent<Outline>().effectColor = UiKit.Ink;

            // prompt (bottom) and countdown (centre)
            promptBox = UiKit.Box(hud.transform, "Prompt", new Color(0.13f, 0.11f, 0.2f, 0.85f));
            promptBox.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-760, 40), new Vector2(760, 150));
            promptText = UiKit.Label(promptBox.transform, "", 34, UiKit.Cream, UiKit.Black);
            promptText.rectTransform.Fill();
            countdownText = UiKit.Label(hud.transform, "", 200, UiKit.Sun, UiKit.Title);
            countdownText.rectTransform.Center(new Vector2(1200, 300), new Vector2(0, 80));
            countdownText.Outlined(UiKit.Ink, 8f);
            hud.SetActive(false);
        }

        public void ShowHud(bool practice)
        {
            practiceHud = practice;
            title.SetActive(false);
            CloseModal();
            if (results) { Destroy(results); results = null; }
            hud.SetActive(true);
            foreach (Transform c in progressMarks) Destroy(c.gameObject);
            houseTicks.Clear();
            var route = RouteManager.Instance;
            bool showRoute = !practice && route;
            progressMarks.parent.parent.gameObject.SetActive(showRoute);
            scoreText.transform.parent.gameObject.SetActive(!practice);
            statsText.transform.parent.gameObject.SetActive(!practice);
            if (!showRoute) return;
            foreach (var h in route.Subscribers)
            {
                var tick = UiKit.Box(progressMarks, "House", Color.white, UiKit.Circle);
                float x = route.Progress(h.Doorstep.position.z);
                tick.rectTransform.anchorMin = tick.rectTransform.anchorMax = new Vector2(x, 0.5f);
                tick.rectTransform.sizeDelta = new Vector2(12, 12);
                houseTicks.Add(tick);
            }
        }

        public void UpdateHud(GameFlow g)
        {
            if (!hud.activeSelf) return;
            if (!practiceHud)
            {
                var s = g.Score;
                scoreText.text = s.Score.ToString("N0");
                comboText.text = s.Streak >= 2 ? $"COMBO x{s.Multiplier:0.##}" : "";
                timeText.text = FormatTime(g.RoundTime);
                statsText.text = $"{s.Delivered} delivered   {s.Missed} missed";
                var route = g.Route;
                float p = route.Progress(g.Bike.transform.position.z);
                progressFill.anchorMax = new Vector2(p, 1);
                progressRider.anchorMin = progressRider.anchorMax = new Vector2(p, 0.5f);
                for (int i = 0; i < houseTicks.Count && i < route.Subscribers.Count; i++)
                {
                    var h = route.Subscribers[i];
                    houseTicks[i].color = h.Delivered ? UiKit.Leaf : h.Missed ? UiKit.Tomato : Color.white;
                }
            }
        }

        public void Prompt(string text)
        {
            promptText.text = text;
            promptBox.gameObject.SetActive(!string.IsNullOrEmpty(text));
        }

        public void Countdown(string text)
        {
            countdownText.text = text;
            countdownText.transform.localScale = Vector3.one * 1.4f;
        }

        // ================================================================== feedback popups

        public void Popup(string text, Vector3 world, FeedbackKind kind)
        {
            Text t = popupPool.Count > 0 ? popupPool.Dequeue() : null;
            if (!t)
            {
                t = UiKit.Label(popupLayer, "", 40, Color.white, UiKit.Title);
                t.rectTransform.sizeDelta = new Vector2(900, 80);
                t.Outlined(UiKit.Ink, 4f);
                t.horizontalOverflow = HorizontalWrapMode.Overflow;
            }
            t.gameObject.SetActive(true);
            t.text = text;
            t.fontSize = kind == FeedbackKind.Great ? 50 : 40;
            t.color = kind switch
            {
                FeedbackKind.Great => UiKit.Sun,
                FeedbackKind.Good => new Color(0.6f, 1f, 0.6f),
                FeedbackKind.Bad => new Color(1f, 0.5f, 0.45f),
                FeedbackKind.Warning => new Color(1f, 0.85f, 0.4f),
                _ => UiKit.Cream,
            };
            // keep popups from stacking on top of each other
            popups.Add((t, world, 0f, new Vector3(0f, popups.Count * 0.6f, 0f)));
        }

        void LateUpdate()
        {
            if (countdownText && countdownText.transform.localScale.x > 1f)
                countdownText.transform.localScale = Vector3.one * Mathf.MoveTowards(countdownText.transform.localScale.x, 1f, Time.unscaledDeltaTime * 2.5f);
            if (!Cam) Cam = Camera.main;
            for (int i = popups.Count - 1; i >= 0; i--)
            {
                var (t, world, age, drift) = popups[i];
                age += Time.deltaTime;
                if (age > 1.5f) { t.gameObject.SetActive(false); popupPool.Enqueue(t); popups.RemoveAt(i); continue; }
                popups[i] = (t, world, age, drift);
                var p = Cam.WorldToScreenPoint(world + drift + Vector3.up * age * 0.9f);
                if (p.z < 0) { t.enabled = false; continue; }
                t.enabled = true;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(popupLayer, p, null, out var local);
                t.rectTransform.anchoredPosition = local;
                float pop = age < 0.15f ? Mathf.Lerp(0.5f, 1.15f, age / 0.15f) : Mathf.Lerp(1.15f, 1f, Mathf.Clamp01((age - 0.15f) * 5f));
                t.transform.localScale = Vector3.one * pop;
                var c = t.color; c.a = Mathf.Clamp01((1.5f - age) / 0.4f); t.color = c;
            }
        }

        // ================================================================== results

        public void ShowResults(ScoreSheet s, float seconds, int houses, int bonus, bool newBest)
        {
            hud.SetActive(false);
            results = UiKit.Rect("Results", root).Fill().gameObject;
            var shade = UiKit.Box(results.transform, "Shade", UiKit.Shade, UiKit.RoundedSmall);
            shade.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(-50, -50), new Vector2(50, 50));
            var panel = UiKit.Box(results.transform, "Panel", UiKit.Panel);
            panel.rectTransform.Center(new Vector2(1380, 1000));
            var h = UiKit.Label(panel.transform, s.Delivered == houses ? "Every House Delivered!" : "Round Complete!", 70, UiKit.Tomato, UiKit.Title);
            h.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -110), new Vector2(0, -16));

            // left: big score + key stats; right: breakdown
            var left = UiKit.Rect("Left", panel.transform).Place(Vector2.zero, new Vector2(0.45f, 1f), new Vector2(50, 200), new Vector2(0, -120));
            var big = UiKit.Label(left, s.Score.ToString("N0"), 130, UiKit.Ink, UiKit.Title);
            big.rectTransform.Place(new Vector2(0, 0.62f), Vector2.one, Vector2.zero, Vector2.zero);
            var bestLine = UiKit.Label(left, newBest ? "NEW PERSONAL BEST!" : $"Your best: {Settings.BestScore:N0}", 34, newBest ? UiKit.Leaf : new Color(0.4f, 0.36f, 0.45f), UiKit.Black);
            bestLine.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(1, 0.62f), Vector2.zero, Vector2.zero);
            var keys = UiKit.Label(left,
                $"Delivered  <b>{s.Delivered} / {houses}</b>\nAccuracy  <b>{Mathf.RoundToInt(s.Accuracy * 100)}%</b>  ({s.Throws} throws)\nTime  <b>{FormatTime(seconds)}</b>   Best combo  <b>{s.BestStreak}</b>",
                32, UiKit.Ink, UiKit.Bold);
            keys.supportRichText = true;
            keys.rectTransform.Place(Vector2.zero, new Vector2(1, 0.5f), Vector2.zero, Vector2.zero);

            var right = UiKit.Rect("Breakdown", panel.transform).Place(new Vector2(0.48f, 0f), Vector2.one, new Vector2(0, 200), new Vector2(-50, -120));
            var lines = new List<string>
            {
                $"Letterbox x{s.GradeCounts[(int)Grade.Letterbox]}   Perfect x{s.GradeCounts[(int)Grade.Perfect]}",
                $"Great x{s.GradeCounts[(int)Grade.Great]}   Good x{s.GradeCounts[(int)Grade.Good]}",
                $"Delivery points (with combos)  <b>+{s.DeliveryPoints:N0}</b>",
                $"Missed houses x{s.Missed}  ·  Windows x{s.Windows}",
                $"People bumped x{s.Pedestrians}  ·  Crashes x{s.Crashes}",
                $"Penalties  <b><color=#d9433a>-{s.PenaltyPoints:N0}</color></b>",
                $"Time & full-round bonus  <b>+{bonus:N0}</b>",
            };
            var breakdown = UiKit.Label(right, string.Join("\n", lines), 30, UiKit.Ink, UiKit.Bold, TextAnchor.MiddleLeft);
            breakdown.supportRichText = true;
            breakdown.lineSpacing = 1.15f;
            breakdown.rectTransform.Fill();

            // public scoreboard submission
            var submit = UiKit.Rect("Submit", panel.transform).Place(new Vector2(0, 0), new Vector2(1, 0), new Vector2(50, 112), new Vector2(-50, 186));
            var inputBg = UiKit.Box(submit, "Name", Color.white);
            inputBg.rectTransform.Place(Vector2.zero, new Vector2(0.4f, 1f), Vector2.zero, Vector2.zero);
            var input = inputBg.gameObject.AddComponent<InputField>();
            var inputText = UiKit.Label(inputBg.transform, "", 30, UiKit.Ink, UiKit.Black, TextAnchor.MiddleLeft);
            inputText.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(18, 0), new Vector2(-18, 0));
            inputText.supportRichText = false;
            var placeholder = UiKit.Label(inputBg.transform, "Your name", 30, new Color(0.6f, 0.58f, 0.62f), UiKit.Bold, TextAnchor.MiddleLeft);
            placeholder.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(18, 0), new Vector2(-18, 0));
            input.textComponent = inputText;
            input.placeholder = placeholder;
            input.characterLimit = 16;
            input.text = Settings.PlayerName;
            var status = UiKit.Label(submit, "Put your round on the public scoreboard", 26, new Color(0.35f, 0.32f, 0.4f), UiKit.Bold, TextAnchor.MiddleLeft);
            status.rectTransform.Place(new Vector2(0.68f, 0f), Vector2.one, new Vector2(16, 0), Vector2.zero);
            Button send = null;
            send = UiKit.Button(submit, "Add to scoreboard", UiKit.Sky, () =>
            {
                string name = input.text.Trim();
                if (name.Length < 2) { status.text = "Type a name first (2–16 letters)."; return; }
                Settings.PlayerName = name; Settings.Save();
                send.interactable = false;
                status.text = "Sending…";
                StartCoroutine(Scoreboard.Submit(name, s, seconds, houses, (ok, msg) =>
                {
                    if (!status) return;
                    status.text = msg;
                    if (!ok) send.interactable = true;
                }));
            }, 28, Color.white);
            send.GetComponent<RectTransform>().Place(new Vector2(0.41f, 0f), new Vector2(0.67f, 1f), Vector2.zero, Vector2.zero);

            var buttons = UiKit.Rect("Buttons", panel.transform).Place(new Vector2(0, 0), new Vector2(1, 0), new Vector2(50, 24), new Vector2(-50, 96));
            var row = buttons.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.spacing = 20; row.childForceExpandWidth = true; row.childControlWidth = true; row.childControlHeight = true;
            var again = UiKit.Button(buttons, "Ride Again", UiKit.Sun, () => Game.Restart(), 36);
            UiKit.Button(buttons, "Scoreboard", UiKit.Cream, ShowScoreboard, 32);
            UiKit.Button(buttons, "Main Menu", UiKit.Cream, () => Game.QuitToMenu(), 32);
            Select(again);
        }

        public static string FormatTime(float seconds)
        {
            int s = Mathf.Max(0, Mathf.FloorToInt(seconds));
            return $"{s / 60}:{s % 60:00}";
        }
    }
}
