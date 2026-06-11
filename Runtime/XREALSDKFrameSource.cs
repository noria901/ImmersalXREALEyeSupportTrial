/*===============================================================================
Adapter wrapping the XREAL SDK 3.1.x RGB camera sample API.

!! The exact class/namespace of the camera texture provider in XREAL SDK 3.1
!! is UNVERIFIED (docs only show `m_RGBCameraTexture.GetYUVFormatTextures()`).
!! Fix the marked lines after importing the Camera Features sample.
===============================================================================*/

using UnityEngine;

namespace ImmersalXREALEye
{
    public class XREALSDKFrameSource : MonoBehaviour, IEyeFrameSource
    {
#if XREAL_SDK_PRESENT
        // TODO(verify): replace with the actual type from the imported
        // "Camera Features" sample, e.g.:
        //   private Unity.XR.XREAL.Samples.RGBCameraTexture m_CameraTexture;
        private RGBCameraTexture m_CameraTexture;

        private void Awake()
        {
            m_CameraTexture = GetComponent<RGBCameraTexture>();
            // TODO(verify): start/Play call if the sample class requires it.
        }

        public bool IsReady => m_CameraTexture != null /* && m_CameraTexture.IsPlaying */;
        public Texture[] GetYUVTextures() => m_CameraTexture.GetYUVFormatTextures();
        public int Width  => m_CameraTexture != null ? m_CameraTexture.Width  : 0; // TODO(verify)
        public int Height => m_CameraTexture != null ? m_CameraTexture.Height : 0; // TODO(verify)
#else
        public bool IsReady => false;
        public Texture[] GetYUVTextures() => null;
        public int Width => 0;
        public int Height => 0;

        private void Awake()
        {
            Debug.LogWarning("[XREALSDKFrameSource] Define XREAL_SDK_PRESENT in " +
                "Player Settings > Scripting Define Symbols after importing the XREAL SDK.");
        }
#endif
    }
}
