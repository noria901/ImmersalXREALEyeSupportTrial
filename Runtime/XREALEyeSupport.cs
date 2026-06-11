/*===============================================================================
ImmersalXREALEyeSupportTrial
XREAL Eye (One series RGB camera accessory) -> Immersal SDK 2.x platform support.

STATUS: UNTESTED SCAFFOLD. Written against:
  - Immersal SDK 2.x (imdk-unity) public interfaces:
      IPlatformSupport / IPlatformUpdateResult / ICameraData / SimpleImageData
  - XREAL SDK 3.1.x RGB camera sample API (GetYUVFormatTextures)
Open questions are tracked in docs/ARCHITECTURE.md.

This file contains NO Immersal proprietary code; it only implements their
public C# interfaces. Immersal SDK itself must be installed separately.
===============================================================================*/

using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;
using Immersal.XR;

namespace ImmersalXREALEye
{
    /// <summary>
    /// IPlatformSupport implementation that feeds XREAL Eye RGB frames
    /// (+ head pose at capture, + intrinsics) into the Immersal localization
    /// pipeline. Drop this on the same GameObject where ARFoundationSupport
    /// would normally live, and reference it from ImmersalSDK/Session setup.
    /// </summary>
    public class XREALEyeSupport : MonoBehaviour, IPlatformSupport
    {
        [Header("Frame source")]
        [SerializeField, Tooltip("Adapter wrapping XREAL SDK RGBCameraTexture. If null, GetComponent is attempted.")]
        private MonoBehaviour m_FrameSourceBehaviour; // must implement IEyeFrameSource

        [Header("Pose source")]
        [SerializeField, Tooltip("Head (XR Origin main camera) transform. Falls back to Camera.main.")]
        private Transform m_HeadTransform;

        [Header("Calibration")]
        [SerializeField, Tooltip("Eye intrinsics + head->camera extrinsics. REQUIRED until an SDK intrinsics API is confirmed.")]
        private EyeCalibration m_Calibration;

        [Header("Readback")]
        [SerializeField, Tooltip("Resolution of the RGB readback sent to Immersal. Keep aspect ratio of the source stream.")]
        private Vector2Int m_ReadbackSize = new Vector2Int(1280, 720);

        [SerializeField, Tooltip("YUV->RGB conversion shader (Shaders/YUVToRGB.shader)")]
        private Shader m_YUVToRGBShader;

        private IEyeFrameSource m_FrameSource;
        private Material m_ConvertMaterial;
        private RenderTexture m_RGBTarget;
        private Texture2D m_ReadbackTexture;
        private bool m_Configured;

        #region IPlatformSupport

        public Task<IPlatformConfigureResult> ConfigurePlatform()
        {
            return ConfigurePlatform(new PlatformConfiguration { CameraDataFormat = CameraDataFormat.RGB });
        }

        public Task<IPlatformConfigureResult> ConfigurePlatform(IPlatformConfiguration configuration)
        {
            bool ok = true;

            if (configuration.CameraDataFormat != CameraDataFormat.RGB)
            {
                Debug.LogError("[XREALEyeSupport] Only CameraDataFormat.RGB is supported by this scaffold.");
                ok = false;
            }

            m_FrameSource ??= (m_FrameSourceBehaviour as IEyeFrameSource) ?? GetComponent<IEyeFrameSource>();
            if (m_FrameSource == null)
            {
                Debug.LogError("[XREALEyeSupport] No IEyeFrameSource found. Add XREALSDKFrameSource (or your own adapter).");
                ok = false;
            }

            if (m_Calibration == null)
            {
                Debug.LogError("[XREALEyeSupport] EyeCalibration asset is required (see tools/calibrate_intrinsics.py).");
                ok = false;
            }

            if (m_HeadTransform == null && Camera.main != null)
                m_HeadTransform = Camera.main.transform;
            if (m_HeadTransform == null)
            {
                Debug.LogError("[XREALEyeSupport] No head transform available.");
                ok = false;
            }

            if (ok && m_ConvertMaterial == null)
            {
                if (m_YUVToRGBShader == null)
                    m_YUVToRGBShader = Shader.Find("ImmersalXREALEye/YUVToRGB");
                m_ConvertMaterial = new Material(m_YUVToRGBShader);
                m_RGBTarget = new RenderTexture(m_ReadbackSize.x, m_ReadbackSize.y, 0, RenderTextureFormat.ARGB32);
                m_RGBTarget.Create();
            }

            m_Configured = ok;
            return Task.FromResult<IPlatformConfigureResult>(new SimplePlatformConfigureResult { Success = ok });
        }

        public Task<IPlatformUpdateResult> UpdatePlatform()
        {
            return UpdatePlatform(new PlatformConfiguration { CameraDataFormat = CameraDataFormat.RGB });
        }

        public async Task<IPlatformUpdateResult> UpdatePlatform(IPlatformConfiguration oneShotConfiguration)
        {
            if (!m_Configured || m_FrameSource == null || !m_FrameSource.IsReady)
                return Fail();

            // --- 1. Sample pose at (as close as possible to) capture time. ---------
            // NOTE: GetYUVFormatTextures gives us the *latest* GPU textures without an
            // explicit capture timestamp (open question #2 in ARCHITECTURE.md).
            // We therefore sample the head pose in the same frame we grab the textures
            // and accept up-to-one-frame skew for the PoC.
            Pose headPose = new Pose(m_HeadTransform.position, m_HeadTransform.rotation);
            Pose camPose = m_Calibration.ApplyHeadToCamera(headPose);

            // --- 2. YUV (GPU) -> RGB (GPU) ----------------------------------------
            Texture[] yuv = m_FrameSource.GetYUVTextures();
            if (yuv == null || yuv.Length < 3 || yuv[0] == null)
                return Fail();

            m_ConvertMaterial.SetTexture("_YTex", yuv[0]);
            m_ConvertMaterial.SetTexture("_UTex", yuv[1]);
            m_ConvertMaterial.SetTexture("_VTex", yuv[2]);
            Graphics.Blit(null, m_RGBTarget, m_ConvertMaterial);

            // --- 3. GPU -> CPU readback (async, no main-thread stall) -------------
            byte[] rgb = await ReadbackRGB24Async(m_RGBTarget);
            if (rgb == null)
                return Fail();

            // --- 4. Wrap into Immersal CameraData ---------------------------------
            // Intrinsics layout per imdk-unity ICameraData:
            //   x = principal point x, y = principal point y, z = focal x, w = focal y
            Vector4 intrinsics = m_Calibration.ScaledIntrinsics(m_ReadbackSize.x, m_ReadbackSize.y);

            var imageData = new SimpleImageData(rgb);
            var cameraData = new CameraData(imageData)
            {
                Width = m_ReadbackSize.x,
                Height = m_ReadbackSize.y,
                Channels = 3,
                Format = CameraDataFormat.RGB,
                Intrinsics = intrinsics,
                CameraPositionOnCapture = camPose.position,
                CameraRotationOnCapture = camPose.rotation,
                Distortion = m_Calibration.Distortion, // "not yet used" by SDK, kept for completeness
                ImageOrientation = 0
            };

            return new SimplePlatformUpdateResult
            {
                Success = true,
                Status = new SimplePlatformStatus { TrackingQuality = 1 },
                CameraData = cameraData
            };
        }

        public Task StopAndCleanUp()
        {
            if (m_RGBTarget != null) { m_RGBTarget.Release(); m_RGBTarget = null; }
            if (m_ConvertMaterial != null) { Destroy(m_ConvertMaterial); m_ConvertMaterial = null; }
            m_Configured = false;
            return Task.CompletedTask;
        }

        #endregion

        private static IPlatformUpdateResult Fail() =>
            new SimplePlatformUpdateResult
            {
                Success = false,
                Status = new SimplePlatformStatus { TrackingQuality = 0 },
                CameraData = null
            };

        private static Task<byte[]> ReadbackRGB24Async(RenderTexture rt)
        {
            var tcs = new TaskCompletionSource<byte[]>();
            AsyncGPUReadback.Request(rt, 0, TextureFormat.RGB24, req =>
            {
                if (req.hasError) { tcs.SetResult(null); return; }
                tcs.SetResult(req.GetData<byte>().ToArray());
            });
            return tcs.Task;
        }
    }

    /// <summary>
    /// Thin abstraction over the XREAL SDK camera class so that the unverified
    /// XREAL-side type names stay isolated in one adapter file.
    /// </summary>
    public interface IEyeFrameSource
    {
        bool IsReady { get; }
        /// <returns>Planar textures [Y, U, V] (YUV_420_888), GPU resident.</returns>
        Texture[] GetYUVTextures();
        int Width { get; }
        int Height { get; }
    }
}
