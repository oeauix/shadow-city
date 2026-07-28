// ============================================================================
// SHADOW CITY — Entities/CharacterAnimator.cs
// Bridge between gameplay and Mixamo-rigged models. When an AI model has an
// Animator (after the Mixamo step), this drives Speed/IsDriving/Dead params;
// otherwise the procedural rig (or fake-bob) keeps working. Zero gameplay
// code changes: CharacterRig calls into this automatically.
// ============================================================================
using UnityEngine;

namespace ShadowCity
{
    public class CharacterAnimator : MonoBehaviour
    {
        Animator anim;
        bool hasParams;
        static readonly int SpeedHash = Animator.StringToHash("Speed");
        static readonly int DrivingHash = Animator.StringToHash("IsDriving");
        static readonly int DeadHash = Animator.StringToHash("Dead");

        public static CharacterAnimator TryAttach(GameObject aiModel)
        {
            var anim = aiModel.GetComponentInChildren<Animator>();
            if (anim == null || anim.runtimeAnimatorController == null) return null;
            var ca = aiModel.AddComponent<CharacterAnimator>();
            ca.anim = anim;
            ca.hasParams = true;
            return ca;
        }

        public void SetSpeed(float normalized)
        {
            if (hasParams) anim.SetFloat(SpeedHash, normalized);
        }
        public void SetDriving(bool v) { if (hasParams) anim.SetBool(DrivingHash, v); }
        public void SetDead(bool v) { if (hasParams) anim.SetBool(DeadHash, v); }
    }
}
