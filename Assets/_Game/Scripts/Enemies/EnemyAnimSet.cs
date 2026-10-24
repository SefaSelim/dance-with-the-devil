using UnityEngine;

namespace DanceWithTheDevil
{
    /// <summary>
    /// The animation clips of one enemy type (GDD §6, §13): swappable slots the enemy builder
    /// (Dance/Enemies/Build Enemy Models) turns into that type's AnimatorController. The slots start filled with the
    /// humanoid Basic Shooter Pack clips; drop a new pack's humanoid clips in and rebuild to swap them. Empty optional
    /// slots leave their state out of the controller (Tell and Attack then fall back to the procedural pose, Death is
    /// not played: the body bursts into cubes on death). Walk, Run and Idle are required.
    /// </summary>
    [CreateAssetMenu(fileName = "anim_enemy_", menuName = "Dance/Enemy Anim Set")]
    public sealed class EnemyAnimSet : ScriptableObject
    {
        [Header("Locomotion (required)")]
        [Tooltip("Standing still (loops).")]
        public AnimationClip Idle;
        [Tooltip("Walking forward (loops). Placed at 45% of Run Speed in the blend tree.")]
        public AnimationClip Walk;
        [Tooltip("Running forward (loops). Placed at Run Speed.")]
        public AnimationClip Run;

        [Header("Locomotion (optional; the body faces its aim, e.g. the Shooter backing off)")]
        public AnimationClip WalkBack;
        public AnimationClip RunBack;
        public AnimationClip StrafeLeft;
        public AnimationClip StrafeRight;
        public AnimationClip StrafeLeftRun;
        public AnimationClip StrafeRightRun;

        [Header("Actions (optional)")]
        [Tooltip("Upper-body hit reaction on every damaging hit.")]
        public AnimationClip Hit;
        [Tooltip("Attack tell (Chaser: opens its arms; Runner: crouches). Empty = procedural pose.")]
        public AnimationClip Tell;
        [Tooltip("Attack itself (Chaser/Runner lunge, Shooter shot). Empty = locomotion keeps playing.")]
        public AnimationClip Attack;
        [Tooltip("Death. Reserved: the model hides and bursts into cubes on death, so it is not played yet.")]
        public AnimationClip Death;

        [Header("Playback")]
        [Tooltip("Ground speed in m/s that plays the Run clip at full weight (CharacterAnimator Anim Run Speed).")]
        [Min(0.1f)] public float RunSpeed = 4.5f;
        [Tooltip("Animator playback speed (a fast Runner can play its run faster).")]
        [Range(0.25f, 3f)] public float PlaybackSpeed = 1f;
        [Tooltip("Playback speed of the Hit clip (a quick flinch, not a stagger).")]
        [Range(0.25f, 3f)] public float HitSpeed = 1.6f;
        [Tooltip("Playback speed of the Attack clip.")]
        [Range(0.25f, 3f)] public float AttackSpeed = 1f;
    }

    /// <summary>
    /// Runtime hooks on an enemy's humanoid body Animator (built from an <see cref="EnemyAnimSet"/>): the "Tell" bool
    /// and "Attack" trigger, used only when the controller has that state, and the humanoid bones for a procedural
    /// tell. Null-safe: cube enemies have no Animator and every call does nothing.
    /// </summary>
    public sealed class EnemyBodyAnimator
    {
        public const string TellParameter = "Tell";
        public const string AttackParameter = "Attack";
        public const string DieParameter = "Die";
        public const string TellState = "Tell";
        public const string AttackState = "Attack";

        private static readonly int TellId = Animator.StringToHash(TellParameter);
        private static readonly int AttackId = Animator.StringToHash(AttackParameter);
        private static readonly int TellStateId = Animator.StringToHash(TellState);
        private static readonly int AttackStateId = Animator.StringToHash(AttackState);
        private static readonly int TellPathId = Animator.StringToHash("Base Layer." + TellState);
        private static readonly int AttackPathId = Animator.StringToHash("Base Layer." + AttackState);

        private readonly Animator _animator;
        private bool _queried;
        private bool _hasTell;
        private bool _hasAttack;

        private EnemyBodyAnimator(Animator animator) => _animator = animator;

        /// <summary>The humanoid Animator under <paramref name="model"/>, or null for a cube model.</summary>
        public static EnemyBodyAnimator Find(Transform model)
        {
            if (model == null) return null;
            Animator animator = model.GetComponentInChildren<Animator>(true);
            return animator != null && animator.avatar != null && animator.avatar.isHuman ? new EnemyBodyAnimator(animator) : null;
        }

        public Animator Animator => _animator;
        private bool Ready => _animator != null && _animator.isActiveAndEnabled && _animator.runtimeAnimatorController != null;

        /// <summary>True when the controller plays a Tell clip (else the brain poses the tell procedurally).</summary>
        public bool HasTell
        {
            get
            {
                Query();
                return _hasTell;
            }
        }

        /// <summary>True when the controller plays an Attack clip.</summary>
        public bool HasAttack
        {
            get
            {
                Query();
                return _hasAttack;
            }
        }

        public void SetTell(bool on)
        {
            if (HasTell && Ready) _animator.SetBool(TellId, on);
        }

        public void TriggerAttack()
        {
            if (HasAttack && Ready) _animator.SetTrigger(AttackId);
        }

        public Transform Bone(HumanBodyBones bone) => _animator != null && _animator.isHuman ? _animator.GetBoneTransform(bone) : null;

        private void Query()
        {
            if (_queried || !Ready || !_animator.isInitialized) return;
            _queried = true;
            _hasTell = _animator.HasState(0, TellStateId) || _animator.HasState(0, TellPathId);
            _hasAttack = _animator.HasState(0, AttackStateId) || _animator.HasState(0, AttackPathId);
        }
    }
}
