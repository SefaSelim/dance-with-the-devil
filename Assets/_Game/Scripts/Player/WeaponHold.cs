using UnityEngine;

namespace DanceWithTheDevil
{
    /// <summary>
    /// Places the held weapon of an imported humanoid from a <see cref="WeaponHoldProfile"/> every frame and pulls the
    /// left hand onto it with IK. Put it on the GameObject with the <see cref="Animator"/> (the "Body"); the base
    /// layer needs IK Pass on (Dance/Character/Build Defne sets it).
    /// Frame order: the Animator plays the clips, <see cref="OnAnimatorIK"/> reads the right-hand goal of this frame and
    /// sets the left-hand IK onto the weapon's support point, then <see cref="LateUpdate"/> puts the weapon socket at
    /// goal × grip (before <see cref="CharacterAnimator"/> adds the recoil kick to the weapon inside the socket).
    /// Tuning: select the Body in Play mode; the inspector edits the active weapon's profile (kept after Play mode,
    /// it is an asset) and the Scene view shows move/rotate handles for the weapon and the left-hand point.
    /// A profile that was never fitted fits itself on the first frame: barrel from the right hand toward the left hand,
    /// support point where the left hand already is.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator))]
    public sealed class WeaponHold : MonoBehaviour
    {
        private const int MaxClips = 8;
        private const int FitAfterFrames = 5;
        private const float DefaultSupportReach = 0.2f;

        [Tooltip("Transform under the right hand that carries the weapons (\"WeaponSocket\").")]
        [SerializeField] private Transform _socket;
        [Tooltip("Rifle root under the socket (+Z = barrel).")]
        [SerializeField] private Transform _rifle;
        [Tooltip("Rocket launcher root under the socket (+Z = tube).")]
        [SerializeField] private Transform _launcher;
        [SerializeField] private WeaponHoldProfile _rifleProfile;
        [SerializeField] private WeaponHoldProfile _launcherProfile;
        [Tooltip("Animator layer whose clips drive the per-clip corrections (0 = locomotion).")]
        [SerializeField, Min(0)] private int _clipLayer;

        private Animator _animator;
        private readonly System.Collections.Generic.List<AnimatorClipInfo> _clipList =
            new System.Collections.Generic.List<AnimatorClipInfo>(MaxClips);
        private readonly System.Collections.Generic.List<AnimatorClipInfo> _nextClips =
            new System.Collections.Generic.List<AnimatorClipInfo>(MaxClips);

        private Vector3 _goalPosition;
        private Quaternion _goalRotation = Quaternion.identity;
        private bool _hasGoal;
        private int _ikFrames;
        private string _dominantClip = string.Empty;

        /// <summary>The profile of the weapon in the hands now (rifle, or the launcher while it is shown).</summary>
        public WeaponHoldProfile ActiveProfile =>
            _launcher != null && _launcher.gameObject.activeInHierarchy && _launcherProfile != null ? _launcherProfile : _rifleProfile;

        /// <summary>The weapon root in the hands now.</summary>
        public Transform ActiveWeapon =>
            _launcher != null && _launcher.gameObject.activeInHierarchy ? _launcher : _rifle;

        /// <summary>Name of the clip with the largest blend weight on the clip layer (for per-clip corrections).</summary>
        public string DominantClip => _dominantClip;

        /// <summary>True once the Animator has produced a right-hand goal this session.</summary>
        public bool HasGoal => _hasGoal;
        public Vector3 GoalPosition => _goalPosition;
        public Quaternion GoalRotation => _goalRotation;
        public Transform Socket => _socket;

        public void Configure(Transform socket, Transform rifle, Transform launcher, WeaponHoldProfile rifleProfile,
            WeaponHoldProfile launcherProfile)
        {
            _socket = socket;
            _rifle = rifle;
            _launcher = launcher;
            _rifleProfile = rifleProfile;
            _launcherProfile = launcherProfile;
        }

        // World size of the weapons, read before any body scaling (the Yoğunluk module widens the model in X/Z only).
        private float _socketWorldScale = 1f;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            if (_socket != null) _socketWorldScale = _socket.lossyScale.y;
        }

        private void OnAnimatorIK(int layerIndex)
        {
            if (_animator == null) return;
            // Called once per IK-pass layer; the last layer's call is the one that sticks, so every call sets the
            // left hand, and the right-hand goal is read from the base layer (the locomotion pose).
            if (layerIndex == 0)
            {
                _goalPosition = _animator.GetIKPosition(AvatarIKGoal.RightHand);
                _goalRotation = _animator.GetIKRotation(AvatarIKGoal.RightHand);
                _hasGoal = true;
                _ikFrames++;
            }

            WeaponHoldProfile profile = ActiveProfile;
            if (profile == null || _socket == null || !profile.Fitted) return; // unfitted: animation hands until LateUpdate fits it

            if (layerIndex == 0) UpdateDominantClip();
            GripFor(profile, out Vector3 gripPosition, out Quaternion gripRotation, out float leftWeight);
            Vector3 weaponPosition = _goalPosition + _goalRotation * gripPosition;
            Quaternion weaponRotation = _goalRotation * gripRotation;

            float weight = profile.UseLeftHandIK ? leftWeight : 0f;
            _animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, weight);
            _animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, weight);
            if (weight > 0f)
            {
                _animator.SetIKPosition(AvatarIKGoal.LeftHand, weaponPosition + weaponRotation * profile.LeftHandPosition);
                _animator.SetIKRotation(AvatarIKGoal.LeftHand, weaponRotation * Quaternion.Euler(profile.LeftHandRotation));
            }
        }

        private void LateUpdate()
        {
            WeaponHoldProfile profile = ActiveProfile;
            if (!_hasGoal || profile == null || _socket == null) return;
            if (!profile.Fitted)
            {
                // Bones are final for this frame here; wait a few frames so the clip has left the bind pose.
                if (_ikFrames < FitAfterFrames) return;
                FitProfile(profile);
            }
            GripFor(profile, out Vector3 gripPosition, out Quaternion gripRotation, out _);
            _socket.SetPositionAndRotation(_goalPosition + _goalRotation * gripPosition, _goalRotation * gripRotation);
            KeepUniformScale();
        }

        /// <summary>
        /// Undoes a non-uniform parent scale on the weapons: the Yoğunluk module widens the body 1.5× in X/Z, which
        /// stretched the gun along its barrel and pushed the muzzle (and its flash) past the drawn tip. Each socket
        /// axis is rescaled to the original world size; the gun lies close to the body's axes, so no visible shear is left.
        /// </summary>
        private void KeepUniformScale()
        {
            Transform parent = _socket.parent;
            if (parent == null) return;
            Matrix4x4 m = parent.localToWorldMatrix * Matrix4x4.Rotate(_socket.localRotation);
            float sx = m.MultiplyVector(Vector3.right).magnitude;
            float sy = m.MultiplyVector(Vector3.up).magnitude;
            float sz = m.MultiplyVector(Vector3.forward).magnitude;
            if (sx < 1e-5f || sy < 1e-5f || sz < 1e-5f) return;
            _socket.localScale = new Vector3(_socketWorldScale / sx, _socketWorldScale / sy, _socketWorldScale / sz);
        }

        /// <summary>The grip of this frame: the profile's base plus the per-clip corrections weighted by the clips playing.</summary>
        private void GripFor(WeaponHoldProfile profile, out Vector3 position, out Quaternion rotation, out float leftWeight)
        {
            position = profile.GripPosition;
            Vector3 euler = profile.GripRotation;
            leftWeight = profile.LeftHandWeight;
            if (profile.Corrections.Count > 0)
            {
                float overrideWeight = 0f;
                float overrideValue = 0f;
                for (int i = 0; i < _clipList.Count; i++)
                {
                    AnimatorClipInfo info = _clipList[i];
                    if (info.clip == null || info.weight <= 0f) continue;
                    int index = profile.IndexOf(info.clip.name);
                    if (index < 0) continue;
                    WeaponHoldProfile.ClipCorrection c = profile.Corrections[index];
                    position += c.Position * info.weight;
                    euler += c.Rotation * info.weight;
                    if (c.LeftHandWeight >= 0f)
                    {
                        overrideValue += c.LeftHandWeight * info.weight;
                        overrideWeight += info.weight;
                    }
                }
                if (overrideWeight > 0f)
                    leftWeight = Mathf.Lerp(leftWeight, overrideValue / overrideWeight, Mathf.Clamp01(overrideWeight));
            }
            rotation = Quaternion.Euler(euler);
        }

        private void UpdateDominantClip()
        {
            _clipList.Clear();
            _animator.GetCurrentAnimatorClipInfo(_clipLayer, _clipList);
            if (_animator.IsInTransition(_clipLayer))
            {
                _animator.GetNextAnimatorClipInfo(_clipLayer, _nextClips);
                _clipList.AddRange(_nextClips);
            }
            float best = -1f;
            for (int i = 0; i < _clipList.Count; i++)
            {
                if (_clipList[i].clip == null || _clipList[i].weight <= best) continue;
                best = _clipList[i].weight;
                _dominantClip = _clipList[i].clip.name;
            }
        }

        /// <summary>
        /// First fit of a fresh profile: barrel level along the body's facing (the body faces the aim, so shots and
        /// barrel agree), grip in the right palm, support point on the handguard <see cref="DefaultSupportReach"/>
        /// metres ahead of the grip, where the left hand is pulled by IK.
        /// </summary>
        private void FitProfile(WeaponHoldProfile profile)
        {
            Transform right = _animator.GetBoneTransform(HumanBodyBones.RightHand);
            if (right == null) return;
            Vector3 forward = transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 1e-4f) return;

            Quaternion weaponRotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
            Vector3 weaponPosition = right.position + Vector3.down * 0.03f;
            Quaternion inverseGoal = Quaternion.Inverse(_goalRotation);
            profile.GripPosition = inverseGoal * (weaponPosition - _goalPosition);
            profile.GripRotation = (inverseGoal * weaponRotation).eulerAngles;
            profile.LeftHandPosition = new Vector3(0f, -0.02f, DefaultSupportReach);
            profile.LeftHandRotation = new Vector3(0f, 0f, 90f); // palm up under the handguard
            profile.Fitted = true;
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(profile);
#endif
        }

        // ------------------------------------------------------------------ editor tuning

        /// <summary>Editor tuning: moves the weapon to a world pose; writes the base grip, or the dominant clip's correction.</summary>
        public void SetWeaponWorldPose(Vector3 position, Quaternion rotation, bool asClipCorrection)
        {
            WeaponHoldProfile profile = ActiveProfile;
            if (profile == null || !_hasGoal) return;
            Quaternion inverseGoal = Quaternion.Inverse(_goalRotation);
            Vector3 grip = inverseGoal * (position - _goalPosition);
            Vector3 euler = (inverseGoal * rotation).eulerAngles;
            if (!asClipCorrection || string.IsNullOrEmpty(_dominantClip))
            {
                profile.GripPosition = grip;
                profile.GripRotation = euler;
                return;
            }
            int index = profile.IndexOf(_dominantClip);
            WeaponHoldProfile.ClipCorrection c = index >= 0
                ? profile.Corrections[index]
                : new WeaponHoldProfile.ClipCorrection { Clip = _dominantClip, LeftHandWeight = -1f };
            c.Position = grip - profile.GripPosition;
            c.Rotation = DeltaEuler(profile.GripRotation, euler);
            if (index >= 0) profile.Corrections[index] = c;
            else profile.Corrections.Add(c);
        }

        /// <summary>Editor tuning: sets the left-hand point from a world pose (relative to the weapon in the hands now).</summary>
        public void SetLeftHandWorldPose(Vector3 position, Quaternion rotation)
        {
            WeaponHoldProfile profile = ActiveProfile;
            if (profile == null || _socket == null) return;
            Quaternion inverseWeapon = Quaternion.Inverse(_socket.rotation);
            profile.LeftHandPosition = inverseWeapon * (position - _socket.position);
            profile.LeftHandRotation = (inverseWeapon * rotation).eulerAngles;
        }

        private static Vector3 DeltaEuler(Vector3 from, Vector3 to) => new Vector3(
            Mathf.DeltaAngle(from.x, to.x), Mathf.DeltaAngle(from.y, to.y), Mathf.DeltaAngle(from.z, to.z));
    }
}
