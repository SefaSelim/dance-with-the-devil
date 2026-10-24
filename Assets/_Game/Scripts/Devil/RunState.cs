using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DanceWithTheDevil
{
    /// <summary>Where the Devil flow is (exposed for UI and tests).</summary>
    public enum DevilState
    {
        /// <summary>Normal play.</summary>
        Playing,
        /// <summary>Death: time frozen, screen desaturating, the Devil sliding in.</summary>
        Freezing,
        /// <summary>The Devil says a line (first appearance, NO line, always-yes line).</summary>
        Speaking,
        /// <summary>Contract card with EVET / HAYIR.</summary>
        Offer,
        /// <summary>Contract card without a choice (contract 6).</summary>
        Forced,
        /// <summary>Always-yes ending: the grey shell crumbles.</summary>
        Crumbling,
        /// <summary>Results screen is up.</summary>
        Ended,
    }

    /// <summary>Which GDD §11 ending the run reached.</summary>
    public enum EndingKind
    {
        None,
        /// <summary>HAYIR at death 1: "Kaybettin ama kendin kaldın."</summary>
        KeptYourself,
        /// <summary>HAYIR at deaths 2-5: "Eksildin ama ayaktasın."</summary>
        DiminishedButStanding,
        /// <summary>Every contract taken: "Bir şey olmadı, değil mi?"</summary>
        AlwaysYes,
    }

    /// <summary>Everything the results screen shows.</summary>
    public readonly struct RunSummary
    {
        public readonly EndingKind Ending;
        public readonly IReadOnlyList<DealDefinition> Taken;
        public readonly int SmallNoCount;
        public readonly int Score;

        public RunSummary(EndingKind ending, IReadOnlyList<DealDefinition> taken, int smallNoCount, int score)
        {
            Ending = ending;
            Taken = taken;
            SmallNoCount = smallNoCount;
            Score = score;
        }

        public int YesCount => Taken != null ? Taken.Count : 0;
    }

    /// <summary>
    /// Contract 2 "Sesin" cost flags (K-8: Defne's voice and one music layer go silent; game cues stay).
    /// There is no audio yet; the Phase 6 audio layer reads these.
    /// </summary>
    public static class AudioCosts
    {
        public static bool VoiceMuted;
        public static bool MusicLayerMuted;

        public static void Reset()
        {
            VoiceMuted = false;
            MusicLayerMuted = false;
        }
    }

    /// <summary>
    /// Mid-run pause (Esc overlay, jam safety net so a build can always be left): freezes time, pauses gameplay
    /// sounds (music keeps playing) and disables the player's input actions so no click fires behind the overlay.
    /// Only the caller decides when pausing is allowed (normal play, never on the Devil or ending screens).
    /// </summary>
    public static class RunPause
    {
        private static float _savedTimeScale = 1f;
        private static PlayerInputReader _input;
        private static bool _inputWasEnabled;

        public static bool IsPaused { get; private set; }

        public static void Pause(PlayerInputReader input)
        {
            if (IsPaused) return;
            if (HitStop.Instance != null) HitStop.Instance.CancelAll();
            _savedTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
            Time.timeScale = 0f;
            AudioListener.pause = true;
            _input = input;
            _inputWasEnabled = input != null && input.enabled;
            if (input != null) input.enabled = false;
            IsPaused = true;
        }

        public static void Resume()
        {
            if (!IsPaused) return;
            IsPaused = false;
            Time.timeScale = _savedTimeScale;
            AudioListener.pause = false;
            if (_input != null && _inputWasEnabled) _input.enabled = true;
            _input = null;
        }

        /// <summary>Back to the main menu when the build has one; otherwise leaves the game (stops play mode in the Editor).</summary>
        public static void Quit()
        {
            Resume();
            if (UnityEngine.Application.CanStreamedLevelBeLoaded(GameSettings.MainMenuScene))
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene(GameSettings.MainMenuScene);
                return;
            }
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        /// <summary>Clears the pause without touching time (scene reload, overlay destroyed).</summary>
        public static void Reset()
        {
            if (IsPaused) Resume();
            IsPaused = false;
            _input = null;
        }
    }

    /// <summary>Starts a fresh run: resets run-wide statics and reloads the active scene.</summary>
    public static class RunRestart
    {
        /// <summary>Static run modifiers back to defaults (contract costs live in statics) and time running.</summary>
        public static void ResetStatics()
        {
            if (HitStop.Instance != null) HitStop.Instance.CancelAll();
            Enemy.SpeedMultiplier = 1f;
            AudioCosts.Reset();
            RunPause.Reset();
            Time.timeScale = 1f;
        }

        public static void ReloadActiveScene()
        {
            ResetStatics();
            Scene scene = SceneManager.GetActiveScene();
            if (scene.buildIndex >= 0) SceneManager.LoadScene(scene.buildIndex);
            else SceneManager.LoadScene(scene.name);
        }
    }
}
