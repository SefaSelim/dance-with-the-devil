using System;
using System.Collections.Generic;
using UnityEngine;

namespace DanceWithTheDevil
{
    /// <summary>
    /// How a weapon sits in a humanoid's hands, as data the team tunes by hand (see <see cref="WeaponHold"/>).
    /// The grip is relative to the right-hand IK goal of the playing animation (so every clip carries the weapon
    /// along); the left hand is pulled by IK onto a point given relative to the weapon. Per-clip corrections are
    /// added on top, blended by how much each clip plays. It is an asset, so changes made in Play mode are kept.
    /// </summary>
    [CreateAssetMenu(menuName = "Dance/Weapon Hold Profile", fileName = "hold_weapon")]
    public sealed class WeaponHoldProfile : ScriptableObject
    {
        [Serializable]
        public struct ClipCorrection
        {
            [Tooltip("Animation clip name (as the Animator plays it, e.g. \"rifle run\").")]
            public string Clip;
            [Tooltip("Extra grip position for this clip, metres, in the hand's frame.")]
            public Vector3 Position;
            [Tooltip("Extra grip rotation for this clip, degrees.")]
            public Vector3 Rotation;
            [Tooltip("Left-hand IK weight for this clip (-1 = use the profile's weight).")]
            [Range(-1f, 1f)] public float LeftHandWeight;
        }

        [Tooltip("Set after the first automatic fit (WeaponHold). Clear it to fit again from the animation's hands.")]
        public bool Fitted;

        [Header("Weapon points (used by the automatic fit)")]
        [Tooltip("Where the right fist closes on the weapon, in its local frame (metres): the pistol grip.")]
        public Vector3 GripPoint = new Vector3(-0.015f, -0.015f, -0.06f);
        [Tooltip("Where the left fist closes on the weapon, in its local frame (metres): under the handguard.")]
        public Vector3 SupportPoint = new Vector3(-0.015f, 0.075f, 0.22f);

        [Header("Grip (right hand)")]
        [Tooltip("Weapon position relative to the right-hand IK goal, metres.")]
        public Vector3 GripPosition = new Vector3(0f, -0.03f, 0f);
        [Tooltip("Weapon rotation relative to the right-hand IK goal, degrees. The weapon's +Z is the barrel.")]
        public Vector3 GripRotation = Vector3.zero;

        [Header("Trigger hand (right, IK)")]
        [Tooltip("Turn the right hand onto the pistol grip with IK (the weapon still follows the animated hand).")]
        public bool UseRightHandIK = true;
        [Tooltip("Right-hand IK point on the weapon (its local frame, metres): the wrist behind the pistol grip.")]
        public Vector3 RightHandPosition = new Vector3(0.02f, -0.05f, -0.12f);
        [Tooltip("Right-hand IK rotation on the weapon (its local frame, degrees).")]
        public Vector3 RightHandRotation = Vector3.zero;

        [Header("Support hand (left, IK)")]
        [Tooltip("Pull the left hand onto the weapon with IK.")]
        public bool UseLeftHandIK = true;
        [Tooltip("Left-hand grip point on the weapon (its local frame, metres): the handguard of a rifle.")]
        public Vector3 LeftHandPosition = new Vector3(0f, -0.02f, 0.2f);
        [Tooltip("Left-hand rotation on the weapon (its local frame, degrees).")]
        public Vector3 LeftHandRotation = new Vector3(0f, 0f, 90f);
        [Tooltip("Left-hand IK weight (0 = the animation's hand, 1 = on the weapon).")]
        [Range(0f, 1f)] public float LeftHandWeight = 1f;

        [Header("Per-clip corrections")]
        [Tooltip("Added to the grip while a clip plays, weighted by that clip's blend weight.")]
        public List<ClipCorrection> Corrections = new List<ClipCorrection>();

        /// <summary>Index of the correction for <paramref name="clip"/>, or -1.</summary>
        public int IndexOf(string clip)
        {
            for (int i = 0; i < Corrections.Count; i++)
                if (Corrections[i].Clip == clip) return i;
            return -1;
        }
    }
}
