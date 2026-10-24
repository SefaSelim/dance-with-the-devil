using UnityEngine;

namespace DanceWithTheDevil
{
    /// <summary>
    /// The settings menu's values (design/ux/main-menu.md §2): master, music and effects volume, screen shake and
    /// fullscreen. Saved in PlayerPrefs, applied when the game starts and whenever a value changes.
    /// </summary>
    public static class GameSettings
    {
        public const string MainMenuScene = "MainMenu";
        public const string ArenaScene = "Arena";

        private const string MasterKey = "settings.master";
        private const string MusicKey = "settings.music";
        private const string SfxKey = "settings.sfx";
        private const string ShakeKey = "settings.shake";
        private const string FullscreenKey = "settings.fullscreen";

        public static float Master { get; private set; } = 1f;
        public static float Music { get; private set; } = 1f;
        public static float Sfx { get; private set; } = 1f;
        public static float Shake { get; private set; } = 1f;
        public static bool Fullscreen { get; private set; } = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void LoadOnStart()
        {
            Master = PlayerPrefs.GetFloat(MasterKey, 1f);
            Music = PlayerPrefs.GetFloat(MusicKey, 1f);
            Sfx = PlayerPrefs.GetFloat(SfxKey, 1f);
            Shake = PlayerPrefs.GetFloat(ShakeKey, 1f);
            Fullscreen = PlayerPrefs.GetInt(FullscreenKey, Screen.fullScreen ? 1 : 0) == 1;
            Apply(false);
        }

        public static void SetMaster(float value) { Master = Mathf.Clamp01(value); Save(MasterKey, Master); }
        public static void SetMusic(float value) { Music = Mathf.Clamp01(value); Save(MusicKey, Music); }
        public static void SetSfx(float value) { Sfx = Mathf.Clamp01(value); Save(SfxKey, Sfx); }
        public static void SetShake(float value) { Shake = Mathf.Clamp01(value); Save(ShakeKey, Shake); }

        public static void SetFullscreen(bool value)
        {
            Fullscreen = value;
            PlayerPrefs.SetInt(FullscreenKey, value ? 1 : 0);
            PlayerPrefs.Save();
            Apply(true);
        }

        private static void Save(string key, float value)
        {
            PlayerPrefs.SetFloat(key, value);
            PlayerPrefs.Save();
            Apply(false);
        }

        private static void Apply(bool screen)
        {
            AudioListener.volume = Master;
            GameAudio.MusicVolume = Music;
            GameAudio.SfxVolume = Sfx;
            PixelCameraRig.GlobalShakeScale = Shake;
            // The Editor's Game view ignores fullscreen; only touch it in a player, and only when asked.
            if (screen && !Application.isEditor) Screen.fullScreen = Fullscreen;
        }
    }
}
