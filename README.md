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

Unity 2022.3 以降。**依存パッケージ → 本体**の順で導入する。

### 1. 依存パッケージを先に導入

- **Immersal SDK 2.x**(`com.immersal.core` 2.3.0、Developer Portal から取得)
- **XREAL SDK 3.1+**(`com.xreal.xr`、Camera Features サンプルを含む)

XREAL SDK(`com.xreal.xr`)が存在すると asmdef の versionDefines により
`XREAL_SDK_PRESENT` が自動定義される(パッケージ名が異なる場合は asmdef を修正)。

### 2. 本パッケージを追加

**方法A — Package Manager GUI**

Window > Package Manager > `+` > **Add package from git URL** に以下を入力:

```
https://github.com/noria901/ImmersalXREALEyeSupportTrial.git
```

**方法B — manifest.json を直接編集**

`Packages/manifest.json` の `dependencies` に1行追記:

```json
{
  "dependencies": {
    "com.noria901.immersal-xreal-eye": "https://github.com/noria901/ImmersalXREALEyeSupportTrial.git"
  }
}
```

### バージョン固定(任意)

URL 末尾に `#<branch|tag|commit>` を付けると版を固定できる:

```
https://github.com/noria901/ImmersalXREALEyeSupportTrial.git#main
```

リリースタグは未作成のため、現状は `#main` かコミットSHAでの固定を推奨。

### ローカル開発(クローンして手元で編集する場合)

リポジトリをクローンし、Package Manager > `+` > **Add package from disk** で
クローン先の `package.json` を指定する。git URL 経由と違いその場で編集・再コンパイルできる。

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
