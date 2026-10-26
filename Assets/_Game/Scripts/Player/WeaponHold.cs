using UnityEngine;

namespace DanceWithTheDevil
{
    /// <summary>
    /// Places the held weapon of an imported humanoid from a <see cref="WeaponHoldProfile"/> every frame and pulls the
    /// left hand onto it with IK. Put it on the GameObject with the <see cref="Animator"/> (the "Body"); the base
    /// layer needs IK Pass on (Dance/Character/Build Defne sets it).
    /// A shouldered profile (the rifle) sets the weapon against the right shoulder instead, level along the facing.
    /// Frame order: the Animator plays the clips, <see cref="OnAnimatorIK"/> reads the right-hand goal of this frame, then
    /// <see cref="LateUpdate"/> puts both hands on the weapon (a two-bone arm solve on the bones: right hand round the
    /// pistol grip, left hand under the handguard) and the weapon socket at goal × grip (before
    /// <see cref="CharacterAnimator"/> adds the recoil kick to the weapon inside the socket).
    /// Tuning: select the Body in Play mode; the inspector edits the active weapon's profile (kept after Play mode,
    /// it is an asset) and the Scene view shows move/rotate handles for the weapon and both hands.
    /// A profile that was never fitted (or "Refit from animation") fits itself on the first frames: barrel level along
    /// the facing, pistol grip in the animated right fist, both hands then held on the weapon (right on the trigger,
    /// left under the handguard).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator))]
    public sealed class WeaponHold : MonoBehaviour
    {
        private const int MaxClips = 8;
        private const int FitAfterFrames = 5;
        // Hand poses on the weapon, in its frame (+Z barrel, +Y up, +X right), that the fit puts the hands in:
        // trigger hand along the barrel, tipped up a little, palm to the left against the pistol grip; support hand
        // palm up under the handguard, fingers across to the right and forward.
        private static readonly Vector3 TriggerHandDirection = new Vector3(0f, 0.35f, 1f);
        private static readonly Vector3 TriggerPalm = Vector3.left;
        private static readonly Vector3 SupportHandDirection = new Vector3(0.75f, 0f, 0.65f);
        private static readonly Vector3 SupportPalm = Vector3.up;
        // The support palm sits this far below the handguard's centre line.
        private const float SupportPalmDrop = 0.04f;

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

            if (layerIndex == 0 && ActiveProfile != null && ActiveProfile.Fitted) UpdateDominantClip();
            // The hands are put on the weapon by HoldHand in LateUpdate (on the bones), not by the humanoid IK: on this
            // retargeted rig the IK goals sit ~20 cm off the hand bones and do not follow them rigidly.
            _animator.SetIKPositionWeight(AvatarIKGoal.RightHand, 0f);
            _animator.SetIKRotationWeight(AvatarIKGoal.RightHand, 0f);
            _animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, 0f);
            _animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, 0f);
        }

        private void LateUpdate()
        {
            WeaponHoldProfile profile = ActiveProfile;
            if (!_hasGoal || profile == null || _socket == null) return;
            if (!profile.Fitted && _ikFrames < FitAfterFrames) return; // wait until the clip has left the bind pose
            GripFor(profile, out Vector3 gripPosition, out Quaternion gripRotation, out float leftWeight);
            // Fingers first: the curl is relative to the hand, and the fit measures the closed fist.
            if (profile.UseRightHandIK) CurlFingers(true, profile.GripFingerCurl, profile.TriggerFingerCurl, 1f);
            if (profile.UseLeftHandIK && leftWeight > 0f)
                CurlFingers(false, profile.SupportFingerCurl, profile.SupportFingerCurl, leftWeight);
            if (!profile.Fitted)
            {
                // Bones are final for this frame here.
                FitProfile(profile);
                GripFor(profile, out gripPosition, out gripRotation, out leftWeight);
            }
            Vector3 weaponPosition;
            Quaternion weaponRotation;
            if (!profile.Shouldered || !ShoulderPose(profile, out weaponPosition, out weaponRotation))
            {
                weaponPosition = _goalPosition + _goalRotation * gripPosition;
                weaponRotation = _goalRotation * gripRotation;
            }

            // Hands first (the socket hangs under the right hand), then the weapon where the grip says. Elbows: the
            // right one out to the side and down (a shouldered rifle), the left one down under the handguard.
            Vector3 right = transform.right, down = Vector3.down, forward = transform.forward;
            if (profile.UseRightHandIK)
                HoldHand(true, weaponPosition + weaponRotation * profile.RightHandPosition,
                    weaponRotation * Quaternion.Euler(profile.RightHandRotation), 1f,
                    right * 0.9f + down - forward * 0.2f);
            if (profile.UseLeftHandIK && leftWeight > 0f)
                HoldHand(false, weaponPosition + weaponRotation * profile.LeftHandPosition,
                    weaponRotation * Quaternion.Euler(profile.LeftHandRotation), leftWeight,
                    down - right * 0.35f);
            _socket.SetPositionAndRotation(weaponPosition, weaponRotation);
            KeepUniformScale();
        }

        /// <summary>
        /// Puts a hand bone at a world pose (blended by <paramref name="weight"/> from the animated one) with a two-bone
        /// arm solve: the elbow stays in the arm's animated bending plane, the upper arm and forearm keep their lengths,
        /// the hand then takes the rotation. Fingers keep their animated curl. The arm is then turned about the
        /// shoulder-to-hand line so the elbow points toward <paramref name="elbowPole"/> (world direction), which keeps it
        /// out of the chest.
        /// </summary>
        private void HoldHand(bool right, Vector3 position, Quaternion rotation, float weight, Vector3 elbowPole)
        {
            Transform upper = _animator.GetBoneTransform(right ? HumanBodyBones.RightUpperArm : HumanBodyBones.LeftUpperArm);
            Transform lower = _animator.GetBoneTransform(right ? HumanBodyBones.RightLowerArm : HumanBodyBones.LeftLowerArm);
            Transform hand = _animator.GetBoneTransform(right ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand);
            if (upper == null || lower == null || hand == null) return;
            weight = Mathf.Clamp01(weight);
            Vector3 target = Vector3.Lerp(hand.position, position, weight);
            Quaternion handRotation = Quaternion.Slerp(hand.rotation, rotation, weight);

            Vector3 a = upper.position, b = lower.position, c = hand.position;
            float upperLength = (b - a).magnitude, lowerLength = (c - b).magnitude;
            if (upperLength < 1e-4f || lowerLength < 1e-4f) return;
            float reach = Mathf.Clamp((target - a).magnitude, 0.01f, (upperLength + lowerLength) * 0.999f);

            Vector3 bendAxis = Vector3.Cross(c - a, b - a);
            if (bendAxis.sqrMagnitude < 1e-8f) bendAxis = Vector3.Cross(c - a, Vector3.down);
            if (bendAxis.sqrMagnitude < 1e-8f) return;
            bendAxis.Normalize();

            // Interior angles now and wanted (law of cosines), applied about the bending plane's normal.
            float shoulderNow = Vector3.Angle(c - a, b - a);
            float elbowNow = Vector3.Angle(a - b, c - b);
            float shoulderWanted = Mathf.Acos(Mathf.Clamp(
                (lowerLength * lowerLength - upperLength * upperLength - reach * reach) / (-2f * upperLength * reach), -1f, 1f)) * Mathf.Rad2Deg;
            float elbowWanted = Mathf.Acos(Mathf.Clamp(
                (reach * reach - upperLength * upperLength - lowerLength * lowerLength) / (-2f * upperLength * lowerLength), -1f, 1f)) * Mathf.Rad2Deg;
            upper.rotation = Quaternion.AngleAxis(shoulderWanted - shoulderNow, bendAxis) * upper.rotation;
            lower.rotation = Quaternion.AngleAxis(elbowWanted - elbowNow, bendAxis) * lower.rotation;
            upper.rotation = Quaternion.FromToRotation(hand.position - a, target - a) * upper.rotation;

            Vector3 reachAxis = hand.position - a;
            if (reachAxis.sqrMagnitude > 1e-8f && elbowPole.sqrMagnitude > 1e-8f)
            {
                Vector3 elbowNowDir = Vector3.ProjectOnPlane(lower.position - a, reachAxis);
                Vector3 elbowWantDir = Vector3.ProjectOnPlane(elbowPole, reachAxis);
                if (elbowNowDir.sqrMagnitude > 1e-8f && elbowWantDir.sqrMagnitude > 1e-8f)
                {
                    float swing = Vector3.SignedAngle(elbowNowDir, elbowWantDir, reachAxis) * weight;
                    upper.rotation = Quaternion.AngleAxis(swing, reachAxis) * upper.rotation;
                }
            }
            hand.rotation = handRotation;
        }

        // Share of the finger curl taken by the knuckle, middle and end joints.
        private static readonly float[] CurlPerJoint = { 1f, 1.1f, 0.7f };

        /// <summary>
        /// Closes a hand's four fingers toward its palm: each joint is bent to <paramref name="grip"/> degrees (the
        /// index finger to <paramref name="index"/>) from the bone before it, scaled per joint, blended by
        /// <paramref name="weight"/> from the animated curl. The thumb keeps the animation.
        /// </summary>
        private void CurlFingers(bool right, float grip, float index, float weight)
        {
            Transform hand = _animator.GetBoneTransform(right ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand);
            if (hand == null || !HandAxes(right, Quaternion.identity, out _, out Vector3 palm)) return;
            HumanBodyBones first = right ? HumanBodyBones.RightIndexProximal : HumanBodyBones.LeftIndexProximal;
            for (int finger = 0; finger < 4; finger++)
            {
                float degrees = finger == 0 ? index : grip;
                Transform before = hand;
                for (int joint = 0; joint < 3; joint++)
                {
                    Transform bone = _animator.GetBoneTransform(first + finger * 3 + joint);
                    if (bone == null) break;
                    Transform next = joint < 2 ? _animator.GetBoneTransform(first + finger * 3 + joint + 1)
                        : bone.childCount > 0 ? bone.GetChild(0) : null;
                    if (next == null) break;
                    Vector3 previous = bone.position - before.position;
                    Vector3 axis = Vector3.Cross(previous, palm); // turning about it moves the finger toward the palm
                    if (axis.sqrMagnitude < 1e-10f) break;
                    float now = Vector3.SignedAngle(previous, next.position - bone.position, axis);
                    float wanted = degrees * CurlPerJoint[joint];
                    bone.rotation = Quaternion.AngleAxis((wanted - now) * weight, axis) * bone.rotation;
                    before = bone;
                }
            }
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

        /// <summary>
        /// The shouldered weapon pose: butt (<see cref="WeaponHoldProfile.StockPoint"/>) at the right shoulder joint plus
        /// <see cref="WeaponHoldProfile.ShoulderOffset"/> in the body's frame, barrel along the body's facing, tilted up by
        /// <see cref="WeaponHoldProfile.ShoulderPitch"/>. The shoulder bone carries the clips' breathing and run bob.
        /// </summary>
        private bool ShoulderPose(WeaponHoldProfile profile, out Vector3 position, out Quaternion rotation)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            Transform shoulder = _animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            Vector3 forward = transform.forward;
            forward.y = 0f;
            if (shoulder == null || forward.sqrMagnitude < 1e-4f) return false;
            Quaternion body = Quaternion.LookRotation(forward.normalized, Vector3.up);
            rotation = body * Quaternion.Euler(-profile.ShoulderPitch, 0f, 0f);
            position = shoulder.position + body * profile.ShoulderOffset - rotation * profile.StockPoint;
            return true;
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
        /// barrel agree) with the weapon's <see cref="WeaponHoldProfile.GripPoint"/> (pistol grip) in the animated right
        /// fist; both hands then go onto the weapon (<see cref="HandPoseFor"/>), the right one round the pistol grip
        /// (on the trigger), the left one palm up under the handguard (<see cref="WeaponHoldProfile.SupportPoint"/>). The animation's own
        /// left hand is not used: the Basic Shooter clips stand bladed (chest ~45° right) with the left hand low at the
        /// side, which would point the barrel down and sideways.
        /// </summary>
        private void FitProfile(WeaponHoldProfile profile)
        {
            if (!FistCentre(true, out Vector3 rightFist)) return;
            Vector3 forward = transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 1e-4f) return;

            Quaternion weaponRotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
            Vector3 weaponPosition = rightFist - weaponRotation * profile.GripPoint;
            if (profile.Shouldered) ShoulderPose(profile, out weaponPosition, out weaponRotation);
            Quaternion inverseGoal = Quaternion.Inverse(_goalRotation);
            profile.GripPosition = inverseGoal * (weaponPosition - _goalPosition);
            profile.GripRotation = (inverseGoal * weaponRotation).eulerAngles;

            Quaternion inverseWeapon = Quaternion.Inverse(weaponRotation);
            if (HandPoseFor(true, weaponPosition, weaponRotation, profile.GripPoint, TriggerHandDirection, TriggerPalm,
                    out Vector3 handPosition, out Quaternion handRotation))
            {
                profile.RightHandPosition = inverseWeapon * (handPosition - weaponPosition);
                profile.RightHandRotation = (inverseWeapon * handRotation).eulerAngles;
            }
            if (HandPoseFor(false, weaponPosition, weaponRotation, profile.SupportPoint + Vector3.down * SupportPalmDrop,
                    SupportHandDirection, SupportPalm, out handPosition, out handRotation))
            {
                profile.LeftHandPosition = inverseWeapon * (handPosition - weaponPosition);
                profile.LeftHandRotation = (inverseWeapon * handRotation).eulerAngles;
            }
            profile.Corrections.Clear(); // they were relative to the old grip
            profile.Fitted = true;
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(profile);
#endif
        }

        /// <summary>
        /// The hand bone pose that puts a hand on the weapon, from this frame's animated hand: the bone is turned so the
        /// hand direction and palm become <paramref name="direction"/> / <paramref name="palm"/> (weapon frame), and
        /// placed so the fist (right) or palm (left) lands on <paramref name="point"/>.
        /// </summary>
        private bool HandPoseFor(bool right, Vector3 weaponPosition, Quaternion weaponRotation, Vector3 point,
            Vector3 direction, Vector3 palm, out Vector3 handPosition, out Quaternion handRotation)
        {
            handPosition = Vector3.zero;
            handRotation = Quaternion.identity;
            Transform hand = _animator.GetBoneTransform(right ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand);
            if (hand == null || !HandAxes(right, Quaternion.identity, out Vector3 handDirection, out Vector3 handPalm)) return false;
            Vector3 grip;
            if (right)
            {
                if (!FistCentre(true, out grip)) return false;
            }
            else
            {
                Transform knuckle = _animator.GetBoneTransform(HumanBodyBones.LeftMiddleProximal);
                if (knuckle == null) return false;
                grip = (hand.position + knuckle.position) * 0.5f; // palm centre
            }
            Vector3 gripInBone = Quaternion.Inverse(hand.rotation) * (grip - hand.position);

            Quaternion now = Quaternion.LookRotation(handDirection, handPalm);
            Quaternion wanted = weaponRotation * Quaternion.LookRotation(direction.normalized, palm);
            handRotation = wanted * Quaternion.Inverse(now) * hand.rotation;
            handPosition = weaponPosition + weaponRotation * point - handRotation * gripInBone;
            return true;
        }

        /// <summary>
        /// A hand's direction (wrist to middle knuckle) and palm normal in the weapon's frame. The palm comes from the
        /// index and little knuckles, its side set by the way the middle finger curls.
        /// </summary>
        private bool HandAxes(bool right, Quaternion inverseWeapon, out Vector3 direction, out Vector3 palm)
        {
            direction = palm = Vector3.zero;
            Transform hand = _animator.GetBoneTransform(right ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand);
            Transform middle = _animator.GetBoneTransform(right ? HumanBodyBones.RightMiddleProximal : HumanBodyBones.LeftMiddleProximal);
            Transform index = _animator.GetBoneTransform(right ? HumanBodyBones.RightIndexProximal : HumanBodyBones.LeftIndexProximal);
            Transform little = _animator.GetBoneTransform(right ? HumanBodyBones.RightLittleProximal : HumanBodyBones.LeftLittleProximal);
            if (hand == null || middle == null || index == null || little == null) return false;
            direction = inverseWeapon * (middle.position - hand.position);
            Vector3 across = inverseWeapon * (index.position - little.position);
            palm = Vector3.Cross(direction, across);
            // The knuckle order is not mirrored the same way on every rig: the fingers curl toward the palm, so the
            // middle finger's bend settles the side.
            Transform tip = _animator.GetBoneTransform(right ? HumanBodyBones.RightMiddleDistal : HumanBodyBones.LeftMiddleDistal);
            if (tip != null)
            {
                Vector3 curl = Vector3.ProjectOnPlane(inverseWeapon * (tip.position - middle.position), direction);
                if (curl.sqrMagnitude > 1e-6f && Vector3.Dot(palm, curl) < 0f) palm = -palm;
            }
            if (direction.sqrMagnitude < 1e-8f || palm.sqrMagnitude < 1e-8f) return false;
            direction.Normalize();
            palm.Normalize();
            return true;
        }

        /// <summary>Centre of a closed fist: the mean of the middle finger's three joints, else 8 cm past the wrist.</summary>
        private bool FistCentre(bool right, out Vector3 centre)
        {
            Transform hand = _animator.GetBoneTransform(right ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand);
            Transform a = _animator.GetBoneTransform(right ? HumanBodyBones.RightMiddleProximal : HumanBodyBones.LeftMiddleProximal);
            Transform b = _animator.GetBoneTransform(right ? HumanBodyBones.RightMiddleIntermediate : HumanBodyBones.LeftMiddleIntermediate);
            Transform c = _animator.GetBoneTransform(right ? HumanBodyBones.RightMiddleDistal : HumanBodyBones.LeftMiddleDistal);
            centre = Vector3.zero;
            if (hand == null) return false;
            if (a != null && b != null && c != null) centre = (a.position + b.position + c.position) / 3f;
            else if (a != null) centre = hand.position + (a.position - hand.position) * 1.3f;
            else centre = hand.position + hand.up * 0.08f;
            return true;
        }

        // ------------------------------------------------------------------ editor tuning

        /// <summary>Editor tuning: moves the weapon to a world pose; writes the base grip, or the dominant clip's correction.</summary>
        public void SetWeaponWorldPose(Vector3 position, Quaternion rotation, bool asClipCorrection)
        {
            WeaponHoldProfile profile = ActiveProfile;
            if (profile == null || !_hasGoal) return;
            if (profile.Shouldered)
            {
                Transform shoulder = _animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
                Vector3 forward = transform.forward;
                forward.y = 0f;
                if (shoulder == null || forward.sqrMagnitude < 1e-4f) return;
                Quaternion body = Quaternion.LookRotation(forward.normalized, Vector3.up);
                profile.ShoulderOffset = Quaternion.Inverse(body) * (position + rotation * profile.StockPoint - shoulder.position);
                Vector3 barrel = Quaternion.Inverse(body) * (rotation * Vector3.forward);
                profile.ShoulderPitch = Mathf.Asin(Mathf.Clamp(barrel.y, -1f, 1f)) * Mathf.Rad2Deg;
                return;
            }
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

        /// <summary>Editor tuning: sets a hand bone's pose from a world pose (relative to the weapon in the hands now).</summary>
        public void SetHandWorldPose(bool right, Vector3 position, Quaternion rotation)
        {
            WeaponHoldProfile profile = ActiveProfile;
            if (profile == null || _socket == null) return;
            Quaternion inverseWeapon = Quaternion.Inverse(_socket.rotation);
            Vector3 local = inverseWeapon * (position - _socket.position);
            Vector3 euler = (inverseWeapon * rotation).eulerAngles;
            if (right)
            {
                profile.RightHandPosition = local;
                profile.RightHandRotation = euler;
            }
            else
            {
                profile.LeftHandPosition = local;
                profile.LeftHandRotation = euler;
            }
        }

        private static Vector3 DeltaEuler(Vector3 from, Vector3 to) => new Vector3(
            Mathf.DeltaAngle(from.x, to.x), Mathf.DeltaAngle(from.y, to.y), Mathf.DeltaAngle(from.z, to.z));
    }
}
