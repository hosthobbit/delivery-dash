using System.Collections;
using System.Collections.Generic;
using DeliveryDash.Core;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace DeliveryDash
{
    public enum FeedbackKind { Good, Great, Bad, Warning, Info }

    /// <summary>
    /// The game loop: title → (first time) practice → countdown → the round → results. Owns the score
    /// sheet and turns events from the bike, papers and hazards into points and feedback.
    /// </summary>
    public class GameFlow : MonoBehaviour
    {
        public enum Phase { Title, Practice, Countdown, Playing, Finished, Results }

        public static GameFlow Instance { get; private set; }
        /// <summary>Set before reloading the scene to go straight back into a round.</summary>
        public static bool QuickRestart;

        [Header("Scene references (assigned by the builder)")]
        public BikeController Bike;
        public ThrowController Thrower;
        public PaperPool Pool;
        public RouteManager Route;
        public Hazard[] Hazards = new Hazard[0];
        public UIManager Ui;
        public Sfx Audio;
        public Effects Fx;
        public CinemachineImpulseSource Impulse;
        public CinemachineImpulseListener ImpulseListener;
        public Transform TitleCameraTarget;

        [Header("Practice")]
        public float PracticeGateZ1 = -56f, PracticeGateZ2 = -44f;
        public float PracticeSpeedZ = -36f;
        public float PracticeRetryZ = -6f;

        public Phase Current { get; private set; } = Phase.Title;
        public bool Paused { get; private set; }
        public ScoreSheet Score { get; private set; } = new ScoreSheet();
        public float RoundTime { get; private set; }
        public GameInput Input { get; private set; }
        public int PracticeStep { get; private set; }
        public bool AutoPilot { get; set; }

        readonly List<int> missBuffer = new List<int>();
        float lastMouseMove = -10f;
        bool throwFromMouse;

        void Awake()
        {
            Instance = this;
            Input = new GameInput();
            ValidateReferences();
            Application.targetFrameRate = Application.platform == RuntimePlatform.WebGLPlayer ? -1 : 120;
        }

        void ValidateReferences()
        {
            if (!Bike || !Thrower || !Pool || !Route || !Ui || !Audio)
                Debug.LogError("GameFlow: a scene reference is missing. Rebuild the scene with Delivery Dash > Build Everything.");
        }

        void Start()
        {
            ApplySettings();
            Bike.Hit += OnBikeHit;
            foreach (var h in Hazards) if (h) h.Bind(Bike);
            if (QuickRestart) { QuickRestart = false; StartRound(); }
            else ShowTitle();
        }

        void OnDestroy()
        {
            if (Input != null) { Input.Ride.Disable(); Input.UI.Disable(); }
            if (Instance == this) Instance = null;
            Time.timeScale = 1f;
        }

        public void ApplySettings()
        {
            Audio.MusicVolume = Settings.Music;
            Audio.SoundVolume = Settings.Sound;
            QualitySettings.SetQualityLevel(Mathf.Clamp(Settings.Quality, 0, QualitySettings.names.Length - 1), true);
            if (Fx) Fx.Reduced = Settings.Quality == 0;
            if (ImpulseListener) ImpulseListener.Gain = Settings.CameraShake ? 1f : 0f;
        }

        // ------------------------------------------------------------------ flow

        void ShowTitle()
        {
            Current = Phase.Title;
            Bike.PlaceAt(new Vector3(1.6f, 0f, Route.StartZ - 3f));
            Bike.Freeze(true);
            Input.Ride.Disable();
            Ui.ShowTitle();
        }

        /// <summary>Play: the practice run the first time, straight to the round after that.</summary>
        /// <summary>Automated tests: let the autopilot do the practice run too.</summary>
        public static bool QaPractice;

        public void Play()
        {
            if (!Settings.TutorialDone && (!AutoPilot || QaPractice)) StartPractice();
            else StartRound();
        }

        public void StartPractice()
        {
            Current = Phase.Practice;
            PracticeStep = 0;
            Score = new ScoreSheet();
            Bike.PlaceAt(new Vector3(0f, 0f, Route.PracticeStartZ));
            Bike.Freeze(false);
            Input.Ride.Enable();
            Ui.ShowHud(practice: true);
            Ui.Prompt(Input.UsingGamepad ? "Steer with the left stick: ride through the cones" : "Steer with A / D (or ← →): ride through the cones");
        }

        public void SkipPractice()
        {
            Settings.TutorialDone = true;
            Settings.Save();
            StartRound();
        }

        public void StartRound()
        {
            StopAllCoroutines();
            Pool.ReturnAll();
            Score = new ScoreSheet();
            RoundTime = 0f;
            foreach (var h in Hazards) if (h) h.ResetHazard();
            Bike.PlaceAt(new Vector3(0f, 0f, Route.StartZ - 3f));
            Bike.Freeze(true);
            Input.Ride.Disable();
            Ui.ShowHud(practice: false);
            Ui.Prompt("");
            StartCoroutine(Countdown());
        }

        IEnumerator Countdown()
        {
            Current = Phase.Countdown;
            for (int i = 3; i > 0; i--)
            {
                Ui.Countdown(i.ToString());
                Sfx.Play(Sfx.Kind.Count);
                yield return new WaitForSeconds(0.8f);
            }
            Ui.Countdown("GO!");
            Sfx.Play(Sfx.Kind.Go);
            Sfx.Play(Sfx.Kind.Bell, 0.6f);
            Current = Phase.Playing;
            Bike.Freeze(false);
            Input.Ride.Enable();
            yield return new WaitForSeconds(0.7f);
            Ui.Countdown("");
        }

        void Finish()
        {
            Current = Phase.Finished;
            Thrower.CancelCharge();
            foreach (int i in Route.CloseOut()) MissedHouse(i);
            int bonus = Score.Finish(RoundTime, Route.ParSeconds, Route.Subscribers.Count);
            Input.Ride.Disable();
            Bike.SetInput(0f, 0f, 1f);
            bool newBest = Score.Score > Settings.BestScore;
            if (newBest) { Settings.BestScore = Score.Score; Settings.BestTime = RoundTime; Settings.Save(); }
            Sfx.Play(Sfx.Kind.Perfect);
            Sfx.Play(Sfx.Kind.Bell, 0.7f);
            Ui.Countdown("FINISH!");
            StartCoroutine(ShowResultsSoon(bonus, newBest));
        }

        IEnumerator ShowResultsSoon(int bonus, bool newBest)
        {
            yield return new WaitForSeconds(1.6f);
            Bike.Freeze(true);
            Current = Phase.Results;
            Ui.Countdown("");
            Ui.ShowResults(Score, RoundTime, Route.Subscribers.Count, bonus, newBest);
        }

        public void Restart()
        {
            Time.timeScale = 1f;
            QuickRestart = true;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        public void QuitToMenu()
        {
            Time.timeScale = 1f;
            QuickRestart = false;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        public void SetPaused(bool on)
        {
            if (Current != Phase.Playing && Current != Phase.Practice && Current != Phase.Countdown) return;
            Paused = on;
            Time.timeScale = on ? 0f : 1f;
            AudioListener.pause = on;
            if (on) { Input.Ride.Disable(); Thrower.CancelCharge(); }
            else if (Current != Phase.Countdown) Input.Ride.Enable();
            Ui.ShowPause(on);
        }

        // ------------------------------------------------------------------ per frame

        void Update()
        {
            Input.TrackDevice();
            if (Input.Pause.WasPressedThisFrame()) { if (Paused) SetPaused(false); else if (!Ui.Busy) SetPaused(true); else Ui.Back(); }
            if (Paused) return;

            bool riding = Current == Phase.Playing || Current == Phase.Practice;
            if (riding && !AutoPilot) ReadRideInput();
            if (Current == Phase.Playing)
            {
                RoundTime += Time.deltaTime;
                foreach (int i in Route.CollectMisses(Bike.transform.position.z)) MissedHouse(i);
                if (Route.Ledger.Finished(Bike.transform.position.z)) Finish();
            }
            if (Current == Phase.Practice) PracticeUpdate();
            Ui.UpdateHud(this);
        }

        void ReadRideInput()
        {
            float steer = Input.Steer.ReadValue<float>();
            float accel = Input.Accelerate.ReadValue<float>();
            float brake = Input.Brake.ReadValue<float>();
            Bike.SetInput(steer, accel, brake);

            var mouse = Mouse.current;
            if (mouse != null && mouse.delta.ReadValue().sqrMagnitude > 2f) lastMouseMove = Time.unscaledTime;
            if (Input.SideLeft.WasPressedThisFrame()) Thrower.ChooseSide(-1);
            if (Input.SideRight.WasPressedThisFrame()) Thrower.ChooseSide(1);
            Thrower.NudgeAim(Input.AimStick.ReadValue<Vector2>());
            if (Input.Bell.WasPressedThisFrame()) Sfx.Play(Sfx.Kind.Bell);

            if (Input.Throw.WasPressedThisFrame())
            {
                var device = Input.Throw.activeControl?.device;
                throwFromMouse = device is Mouse;
                // a click on a button is for the button, never a throw
                if (throwFromMouse && EventSystem.current && EventSystem.current.IsPointerOverGameObject()) return;
                if (throwFromMouse) Thrower.UseMouse();
                else if (Thrower.Mode == ThrowController.AimMode.Mouse || Time.unscaledTime - lastMouseMove > 3f)
                {
                    // keyboard / pad throw: aim at the next waiting house (its side, unless the player picked one)
                    var next = Route.NextTarget(Bike.transform.position.z, 0, Thrower.MaxRange + 4f);
                    Thrower.ChooseSide(next ? next.Side : Thrower.Side);
                }
                Thrower.BeginCharge();
            }
            if (Input.Throw.WasReleasedThisFrame()) Thrower.Release();
        }

        // ------------------------------------------------------------------ practice

        void PracticeUpdate()
        {
            float z = Bike.transform.position.z;
            switch (PracticeStep)
            {
                case 0:
                    if (z > PracticeGateZ2 + 2f)
                    {
                        PracticeStep = 1;
                        Sfx.Play(Sfx.Kind.Deliver, 0.5f);
                        Ui.Prompt(Input.UsingGamepad ? "Right trigger to speed up, left trigger to brake" : "Hold W to speed up, S to brake");
                    }
                    break;
                case 1:
                    if (z > PracticeSpeedZ)
                    {
                        PracticeStep = 2;
                        Ui.Prompt(Input.UsingGamepad
                            ? "Hold A to aim, let go to throw: get the paper into the garden with the marker!"
                            : "Hold the LEFT MOUSE BUTTON to aim, let go to throw: get the paper into the garden with the marker!  (Keyboard only: E, then hold Space)");
                    }
                    break;
                case 2:
                    if (z > PracticeRetryZ && Route.PracticeHouse && !Route.PracticeHouse.Delivered)
                    {
                        // missed it: back up for another go
                        Bike.PlaceAt(new Vector3(0f, 0f, PracticeSpeedZ));
                        Ui.Prompt("Have another go: hold to aim at the garden with the marker, then let go");
                    }
                    break;
            }
        }

        void PracticeDone()
        {
            PracticeStep = 3;
            Settings.TutorialDone = true;
            Settings.Save();
            Ui.Prompt("Lovely! That's all there is to it. Now for the real round…");
            StartCoroutine(AfterPractice());
        }

        IEnumerator AfterPractice()
        {
            yield return new WaitForSeconds(2.2f);
            StartRound();
        }

        // ------------------------------------------------------------------ events

        public void OnThrow()
        {
            if (Current == Phase.Playing) Score.CountThrow();
        }

        /// <summary>A paper reached a garden (or a letterbox). True if it counted as a delivery.</summary>
        public bool TryDeliver(House house, Grade grade, Vector3 at, Newspaper paper)
        {
            if (!house || grade == Grade.None) return false;
            if (house == Route.PracticeHouse)
            {
                if (house.Delivered || Current != Phase.Practice) return false;
                house.MarkDelivered();
                paper.MarkScored();
                Celebrate(grade, at, 0);
                PracticeDone();
                return true;
            }
            if (!house.Subscriber || Current != Phase.Playing) return false;
            if (!Route.Ledger.Deliver(house.RouteIndex)) return false;   // already delivered or missed: one paper per house
            house.MarkDelivered();
            paper.MarkScored();
            int before = Score.Streak;
            int points = Score.Apply(new RoundEvent(RoundEventKind.Delivery, grade));
            Celebrate(grade, at, points);
            if (Score.Streak >= 3 && Score.Streak > before && (Score.Streak == 3 || Score.Streak % 5 == 0))
            {
                Feedback($"Combo x{Score.Multiplier:0.##}!", at + Vector3.up * 2.4f, FeedbackKind.Great);
                Sfx.Play(Sfx.Kind.Combo, 0.8f);
            }
            return true;
        }

        void Celebrate(Grade grade, Vector3 at, int points)
        {
            bool top = grade >= Grade.Perfect;
            Sfx.Play(top ? Sfx.Kind.Perfect : Sfx.Kind.Deliver);
            Fx?.DeliveryBurst(at, top);
            string label = DeliveryJudge.Label(grade);
            Feedback(points > 0 ? $"{label}  +{points}" : label, at + Vector3.up * 1.5f, top ? FeedbackKind.Great : FeedbackKind.Good);
        }

        public void OnPaperLanded(Newspaper paper, Vector3 at)
        {
            var house = Route.HouseAt(at);
            if (house)
            {
                var grade = DeliveryJudge.Judge(at, house.Plot, house.Doorstep.position);
                if ((house.Subscriber || house == Route.PracticeHouse) && !house.Delivered && TryDeliver(house, grade, at, paper)) return;
                if (!house.Subscriber && house != Route.PracticeHouse && Current == Phase.Playing) Feedback("Not a subscriber!", at + Vector3.up, FeedbackKind.Info);
            }
        }

        public void OnWindowBroken(Vector3 at)
        {
            Sfx.Play(Sfx.Kind.Glass);
            Shake(0.35f);
            if (Current != Phase.Playing) { Feedback("Window Broken!", at + Vector3.up * 0.5f, FeedbackKind.Bad); return; }
            int d = Score.Apply(new RoundEvent(RoundEventKind.WindowBroken));
            Feedback($"Window Broken!  {d}", at + Vector3.up * 0.5f, FeedbackKind.Bad);
        }

        public void OnPedestrianHit(Vector3 at)
        {
            if (Current != Phase.Playing) { Feedback("Oi! Watch it!", at, FeedbackKind.Bad); return; }
            int d = Score.Apply(new RoundEvent(RoundEventKind.PedestrianHit));
            Feedback($"Oi! Watch it!  {d}", at, FeedbackKind.Bad);
        }

        public void OnPaperHitPerson(Vector3 at) => OnPedestrianHit(at + Vector3.up * 0.6f);

        void MissedHouse(int index)
        {
            var house = Route.Subscribers[index];
            int d = Score.Apply(new RoundEvent(RoundEventKind.MissedHouse));
            Sfx.Play(Sfx.Kind.Miss, 0.6f);
            Feedback($"Missed House  {d}", house.Doorstep.position + Vector3.up * 2.2f, FeedbackKind.Bad);
        }

        void OnBikeHit(string kind, Vector3 at)
        {
            switch (kind)
            {
                case "crash":
                    Sfx.Play(Sfx.Kind.Crash);
                    Fx?.DustBurst(at);
                    Shake(1f);
                    Thrower.CancelCharge();
                    if (Current == Phase.Playing)
                    {
                        int d = Score.Apply(new RoundEvent(RoundEventKind.Crash));
                        Feedback($"Crash!  {d}", Bike.transform.position + Vector3.up * 2f, FeedbackKind.Bad);
                    }
                    else Feedback("Whoops!", Bike.transform.position + Vector3.up * 2f, FeedbackKind.Bad);
                    break;
                case "pothole":
                    Sfx.Play(Sfx.Kind.Bump, 0.9f);
                    Shake(0.4f);
                    Fx?.DustBurst(at);
                    Feedback("Pothole!", Bike.transform.position + Vector3.up * 2f, FeedbackKind.Warning);
                    break;
                default:
                    Sfx.Play(Sfx.Kind.Bump, 0.7f);
                    Shake(0.25f);
                    break;
            }
        }

        void Shake(float strength)
        {
            if (Impulse && Settings.CameraShake) Impulse.GenerateImpulse(Vector3.one * 0.25f * strength);
        }

        public void Feedback(string text, Vector3 at, FeedbackKind kind) => Ui?.Popup(text, at, kind);
    }
}
