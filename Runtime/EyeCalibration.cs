/*===============================================================================
Eye camera calibration container.

Until an intrinsics API is confirmed in XREAL SDK 3.x, values must be measured
once with a checkerboard (tools/calibrate_intrinsics.py) and entered here.
Eye is fixed-focus, so intrinsics are constant — EXCEPT if EIS warps the
stream (open question #3 in ARCHITECTURE.md).
===============================================================================*/

using UnityEngine;

namespace ImmersalXREALEye
{
    [CreateAssetMenu(menuName = "ImmersalXREALEye/Eye Calibration")]
    public class EyeCalibration : ScriptableObject
    {
        [Header("Intrinsics (at calibration resolution)")]
        public Vector2Int calibrationResolution = new Vector2Int(2016, 1512);
        public float fx = 1500f;
        public float fy = 1500f;
        public float cx = 1008f;
        public float cy = 756f;

        [Tooltip("k1,k2,p1,p2,k3 — currently unused by Immersal SDK, recorded for completeness.")]
        public double[] distortion = new double[5];

        [Header("Extrinsics: head (XR Origin camera) -> Eye camera")]
        [Tooltip("Position offset of the Eye camera relative to the head pose, in head-local meters.")]
        public Vector3 headToCameraPosition = Vector3.zero;
        [Tooltip("Rotation offset (euler, degrees) of the Eye camera relative to the head pose.")]
        public Vector3 headToCameraEuler = Vector3.zero;

        public double[] Distortion => distortion;

        /// <summary>
        /// Immersal layout: x = principal x, y = principal y, z = focal x, w = focal y.
        /// Scales calibration-resolution intrinsics to the actual readback resolution.
        /// </summary>
        public Vector4 ScaledIntrinsics(int width, int height)
        {
            float sx = (float)width / calibrationResolution.x;
            float sy = (float)height / calibrationResolution.y;
            return new Vector4(cx * sx, cy * sy, fx * sx, fy * sy);
        }

        public Pose ApplyHeadToCamera(Pose headPose)
        {
            Quaternion r = Quaternion.Euler(headToCameraEuler);
            return new Pose(
                headPose.position + headPose.rotation * headToCameraPosition,
                headPose.rotation * r);
        }
    }
}
