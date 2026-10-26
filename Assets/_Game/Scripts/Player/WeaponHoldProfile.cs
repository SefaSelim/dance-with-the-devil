using System;
using System.Collections.Generic;
using UnityEngine;

namespace DanceWithTheDevil
{
    /// <summary>
    /// How a weapon sits in a humanoid's hands, as data the team tunes by hand (see <see cref="WeaponHold"/>).
    /// The grip is relative to the right-hand IK goal of the playing animation (so every clip carries the weapon
    /// along); both hand bones are then put on poses given relative to the weapon (right on the trigger, left under
    /// the handguard). Per-clip corrections are added on top, blended by how much each clip plays. It is an asset,
    /// so changes made in Play mode are kept.
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
            [Tooltip("Left hand on the weapon for this clip (-1 = use the profile's weight).")]
            [Range(-1f, 1f)] public float LeftHandWeight;
        }

        [Tooltip("Set after the first automatic fit (WeaponHold). Clear it to fit again from the animation's hands.")]
        public bool Fitted;

        [Header("Weapon points (used by the automatic fit)")]
        [Tooltip("Where the right fist closes on the weapon, in its local frame (metres): the pistol grip.")]
        public Vector3 GripPoint = new Vector3(-0.015f, -0.015f, -0.06f);
        [Tooltip("Where the left fist closes on the weapon, in its local frame (metres): under the handguard.")]
        public Vector3 SupportPoint = new Vector3(-0.015f, 0.075f, 0.22f);

        [Header("Shouldered (rifle)")]
        [Tooltip("Hold the weapon with its butt against the right shoulder, level along the facing, instead of following "
                 + "the animated right hand: the right arm then comes from the side and never crosses the chest.")]
        public bool Shouldered;
        [Tooltip("The butt of the weapon in its local frame (metres): the point set against the shoulder.")]
        public Vector3 StockPoint = new Vector3(-0.015f, 0.03f, -0.27f);
        [Tooltip("Where the butt sits from the right shoulder joint, in the body's frame (x right, y up, z forward), metres.")]
        public Vector3 ShoulderOffset = new Vector3(-0.07f, -0.06f, 0.1f);
        [Tooltip("Barrel tilt while shouldered, degrees up.")]
        [Range(-20f, 20f)] public float ShoulderPitch = 5f;

        [Header("Grip (right hand, when not shouldered)")]
        [Tooltip("Weapon position relative to the right-hand IK goal, metres.")]
        public Vector3 GripPosition = new Vector3(0f, -0.03f, 0f);
        [Tooltip("Weapon rotation relative to the right-hand IK goal, degrees. The weapon's +Z is the barrel.")]
        public Vector3 GripRotation = Vector3.zero;

        [Header("Trigger hand (right)")]
        [Tooltip("Put the right hand on the pistol grip (the weapon still follows the animated hand).")]
        public bool UseRightHandIK = true;
        [Tooltip("Right hand bone (wrist) position on the weapon, in its local frame (metres): behind the pistol grip.")]
        public Vector3 RightHandPosition = new Vector3(0.02f, -0.05f, -0.12f);
        [Tooltip("Right hand bone rotation on the weapon, in its local frame (degrees).")]
        public Vector3 RightHandRotation = Vector3.zero;

        [Header("Support hand (left)")]
        [Tooltip("Put the left hand on the weapon.")]
        public bool UseLeftHandIK = true;
        [Tooltip("Left hand bone (wrist) position on the weapon, in its local frame (metres): under the handguard of a rifle.")]
        public Vector3 LeftHandPosition = new Vector3(0f, -0.02f, 0.2f);
        [Tooltip("Left hand bone rotation on the weapon, in its local frame (degrees).")]
        public Vector3 LeftHandRotation = new Vector3(0f, 0f, 90f);
        [Tooltip("Left hand blend (0 = the animation's hand, 1 = on the weapon).")]
        [Range(0f, 1f)] public float LeftHandWeight = 1f;

        [Header("Fingers (degrees per joint, from the knuckle)")]
        [Tooltip("How far the right middle, ring and little fingers close round the pistol grip.")]
        [Range(0f, 110f)] public float GripFingerCurl = 75f;
        [Tooltip("How far the right index finger bends onto the trigger.")]
        [Range(0f, 110f)] public float TriggerFingerCurl = 30f;
        [Tooltip("How far the left fingers close round the handguard.")]
        [Range(0f, 110f)] public float SupportFingerCurl = 35f;

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
