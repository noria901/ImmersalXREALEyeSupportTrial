# ImmersalXREALEyeSupportTrial

XREAL One Pro + Eye のRGBカメラ画像を Immersal SDK 2.x に食わせて
グラス単体でVPSリローカライズするための `IPlatformSupport` 実装トライアル。

**Status: 未実機検証のスキャフォールド**(コードレベル監査ベース)。
imdk-unity の公開インターフェース実シグネチャと、XREAL SDK 3.1 ドキュメントの
公開API面に合わせて記述。実機で潰すべき未確定要素は
[docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) の Open questions 参照。

## 構成

```
Runtime/
  XREALEyeSupport.cs      … 本体。Immersal IPlatformSupport 実装
  XREALSDKFrameSource.cs  … XREAL SDK カメラAPIのアダプタ(型名要確認のTODOあり)
  EyeCalibration.cs       … intrinsics + head→camera extrinsics 保持アセット
  Shaders/YUVToRGB.shader … YUV_420_888 → RGB 変換
tools/
  calibrate_intrinsics.py … チェッカーボードによる intrinsics 自前測定
docs/
  ARCHITECTURE.md         … パイプライン・座標系・Open questions・テスト計画
```

## インストール(UPM)

Unity の Package Manager > `+` > **Add package from git URL**:

```
https://github.com/noria901/ImmersalXREALEyeSupportTrial.git
```

依存として **Immersal SDK 2.x**(`com.immersal.core`、Developer Portal から)と
**XREAL SDK 3.1+**(Camera Features サンプル含む)を先に導入しておくこと。
XREAL SDK(`com.xreal.xr`)が存在すると asmdef の versionDefines により
`XREAL_SDK_PRESENT` が自動定義される(パッケージ名が異なる場合は asmdef を修正)。

## セットアップ手順(想定)

1. 上記2つのSDKを導入後、本パッケージを git URL で追加
2. `XREALSDKFrameSource.cs` の TODO(型名)をサンプルの実クラスに合わせて修正
4. `tools/calibrate_intrinsics.py` で Eye の intrinsics を測定し、
   EyeCalibration アセット(Create > ImmersalXREALEye > Eye Calibration)に入力
5. Immersal の Session 構成で PlatformSupport を `XREALEyeSupport` に差し替え
6. Immersal Mapper でマップ作成 → Beam Pro でローカライズ確認

## 前提・制約

- Eye は XREAL One シリーズ専用アクセサリ。SDK 3.0+ で RGB アクセス、3.1.0 で 6DoF
- Immersal Free は非商用 + ロゴ表示必須(商用は Pro $99/mo)
- 単眼SLAM(屋外・低照度で品質低下の公式注意あり)前提のため、
  再ローカライズ周期は環境に応じて要チューニング
