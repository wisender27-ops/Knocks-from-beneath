using UnityEngine;

namespace KnocksFromBeneath
{
    public sealed class CornerPeekPose : MonoBehaviour
    {
        private struct BonePose
        {
            public Transform Bone;
            public Quaternion Neutral;
            public float Lean;
            public float Bend;
        }

        private BonePose[] _bones;
        private Transform _model;
        private float _side = 1f;

        public void Initialize(Animator animator, float leanSide = 1f)
        {
            Restore();
            _side = leanSide < 0f ? -1f : 1f;
            _model = animator != null ? animator.transform : transform;
            _bones = new[]
            {
                Capture(animator, HumanBodyBones.Spine, "Spine", -15f, 3f),
                Capture(animator, HumanBodyBones.Chest, "Spine1", -17f, 2f),
                Capture(animator, HumanBodyBones.UpperChest, "Spine2", -9f, 2f),
                Capture(animator, HumanBodyBones.Neck, "Neck", -4f, -2f),
                Capture(animator, HumanBodyBones.Head, "Head", 7f, -3f),
                Capture(animator, HumanBodyBones.LeftUpperArm, "LeftArm", -6f, 8f),
                Capture(animator, HumanBodyBones.RightUpperArm, "RightArm", 6f, 8f),
                Capture(animator, HumanBodyBones.LeftLowerArm, "LeftForeArm", 0f, 12f),
                Capture(animator, HumanBodyBones.RightLowerArm, "RightForeArm", 0f, 12f)
            };
        }

        public void Apply(float reveal, float elapsedSeconds)
        {
            if (_bones == null || _model == null) return;
            float amount = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(reveal));
            float breath = Mathf.Sin(elapsedSeconds * 2.4f) * 0.45f * amount;

            // Reset the full chain before applying world-axis rotations to avoid accumulated drift.
            Restore();
            for (int i = 0; i < _bones.Length; i++)
            {
                BonePose pose = _bones[i];
                if (pose.Bone == null) continue;
                Quaternion worldOffset =
                    Quaternion.AngleAxis((pose.Lean * amount + breath) * _side, _model.forward) *
                    Quaternion.AngleAxis(pose.Bend * amount, _model.right);
                pose.Bone.rotation = worldOffset * pose.Bone.rotation;
            }
        }

        public void Restore()
        {
            if (_bones == null) return;
            for (int i = 0; i < _bones.Length; i++)
                if (_bones[i].Bone != null)
                    _bones[i].Bone.localRotation = _bones[i].Neutral;
        }

        private BonePose Capture(Animator animator, HumanBodyBones humanoidBone,
            string fallbackName, float lean, float bend)
        {
            Transform bone = animator != null && animator.isHuman
                ? animator.GetBoneTransform(humanoidBone)
                : null;
            if (bone == null)
            {
                foreach (Transform candidate in _model.GetComponentsInChildren<Transform>(true))
                {
                    string candidateName = candidate.name;
                    int separator = candidateName.LastIndexOf(':');
                    if (separator >= 0) candidateName = candidateName.Substring(separator + 1);
                    if (candidateName != fallbackName && candidateName != "mixamorig" + fallbackName) continue;
                    bone = candidate;
                    break;
                }
            }
            return new BonePose
            {
                Bone = bone,
                Neutral = bone != null ? bone.localRotation : Quaternion.identity,
                Lean = lean,
                Bend = bend
            };
        }

        private void OnDisable() => Restore();
    }
}
