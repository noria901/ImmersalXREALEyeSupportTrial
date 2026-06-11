# Architecture

## Pipeline

```
XREAL Eye (YUV_420_888, GPU textures)
        │  XREALSDKFrameSource (adapter, IEyeFrameSource)
        ▼
XREALEyeSupport : IPlatformSupport          … this repo
  1. head pose sampled at frame grab
     + head→camera extrinsics (EyeCalibration)
  2. YUV→RGB blit (Shaders/YUVToRGB)
  3. AsyncGPUReadback → byte[] RGB24
  4. CameraData {bytes, intrinsics, pose-on-capture}
        ▼
Immersal SDK 2.x (unmodified)
  Localizer (on-device .aar / REST) → map-space camera pose
  SceneUpdater → applies map↔session alignment
        ▼
XREAL SLAM keeps tracking between localizations
```

座標系の貼り合わせ(T_session→map の算出と適用)は **Immersal SDK 側が
`CameraPositionOnCapture/RotationOnCapture` を使って行う**。本リポジトリの責務は
「正直な CameraData を渡す」ことだけ。

## Why IPlatformSupport

imdk-unity の組み込みプラットフォームは `ARFoundationSupport`(468行)のみで、
カメラ供給は最初から差し替え前提の抽象化になっている:

- `IPlatformSupport`: `ConfigurePlatform` / `UpdatePlatform` → `IPlatformUpdateResult`
- `ICameraData`: 画像バイト列 + `Intrinsics(Vector4: cx, cy, fx, fy)` +
  `CameraPositionOnCapture` / `CameraRotationOnCapture`

つまり撮影時ポーズとのペアリング(レイテンシ対策)は SDK のデータモデルに
組み込み済み。

## Open questions(実機で潰す)

| # | 問い | 影響 | フォールバック |
|---|------|------|----------------|
| 1 | SDK 3.x に Eye の intrinsics API はあるか(旧 NRSDK の `NRFrame.GetDeviceIntrinsicMatrix` 相当) | 高 | チェッカーボード自前測定(tools/) |
| 2 | `GetYUVFormatTextures` にフレームタイムスタンプは付くか | 中 | 同フレームのヘッドポーズで近似(±1フレームのズレ許容) |
| 3 | EIS は無効化できるか / かかっていると intrinsics が揺れる | 高 | EIS込みでキャリブレーションし残差を許容 |
| 4 | 旧 `GetDevicePoseFromHead(RGB_CAMERA)` 相当の head→Eye 外部パラメータ API は 3.x にあるか | 中 | 手測 + 再投影誤差で微調整(EyeCalibration) |
| 5 | サンプルのカメラクラスの正確な型名・名前空間(`RGBCameraTexture`?) | 低 | サンプルimport後に Adapter の TODO を修正 |
| 6 | ストリーム実解像度(動画 600×1200 説 vs 静止画 2016×1512)と縦横比 | 中 | ReadbackSize を実測値に合わせる |

## Test plan

1. **Editor**: ダミーの IEyeFrameSource(静止画3枚)で UpdatePlatform → CameraData 生成までをユニット確認
2. **Beam Pro 単体**: XREAL_SDK_PRESENT を定義、RGBCamera サンプルと共存させてフレーム取得 → PNG 保存で画質・解像度・EIS 確認
3. **キャリブレーション**: tools/calibrate_intrinsics.py、RMS < 1px を目標
4. **Immersal Free アカウント**: Mapper で小空間をマップ → 本実装でローカライズ成功率・残差を計測
5. 成功したら再ローカライズ周期と lerp なじませ(Tracking analyzer)のチューニング

## Licensing notes

- 本リポジトリは Immersal の公開 C# インターフェースに対する実装のみを含み、
  Immersal 製コードは含まない(SDK は各自 Developer Portal から導入)
- Immersal Free ライセンスは非商用・ロゴ表示必須。商用化時は Pro($99/mo)
- XREAL SDK は XREAL の利用規約に従う
