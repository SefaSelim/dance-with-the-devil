using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace DanceWithTheDevil.Tests
{
    /// <summary>
    /// The game music carries on through a scene load (main menu → arena): the arena's GameAudio adopts the playing
    /// track instead of starting it over from the top.
    /// </summary>
    public sealed class MusicContinuityTests
    {
        private const string MenuPath = "Assets/_Game/Scenes/MainMenu.unity";
        private const string ArenaPath = "Assets/_Game/Scenes/Arena.unity";

        [UnityTearDown]
        public IEnumerator Leave() => TestScenes.LeaveLoadedScenes();

        [UnityTest]
        public IEnumerator MenuToArena_MusicKeepsItsPlace()
        {
#if UNITY_EDITOR
            // Arrange: the menu music has been playing for a while.
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                MenuPath, new LoadSceneParameters(LoadSceneMode.Single));
            float end = Time.realtimeSinceStartup + 3f;
            while (Time.realtimeSinceStartup < end) yield return null;
            Assert.IsNotNull(GameAudio.Instance, "GameAudio in the menu");
            AudioSource menuTrack = GameAudio.Instance.MusicTrackSource;
            if (menuTrack == null) Assert.Ignore("No single music track in the bank.");
            Assert.IsTrue(menuTrack.isPlaying, "menu music playing");
            float before = menuTrack.time;
            Assert.Greater(before, 1f, "menu music has advanced");

            // Act: load the arena.
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                ArenaPath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            yield return null;

            // Assert: same source, still playing, not back at the start.
            AudioSource arenaTrack = GameAudio.Instance != null ? GameAudio.Instance.MusicTrackSource : null;
            Assert.AreSame(menuTrack, arenaTrack, "the arena adopts the menu's track");
            Assert.IsTrue(arenaTrack.isPlaying);
            Assert.GreaterOrEqual(arenaTrack.time, before, "music did not restart from the top");
#else
            Assert.Ignore("Editor only.");
            yield break;
#endif
        }
    }
}
