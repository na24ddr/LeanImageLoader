# Lean Image Loader

[English](README.md)

サムネイルやジャケット、バナーのような画像を何百枚も、縮小した Unity のテクスチャとして**速く**読み込むライブラリです。読み込み中の**ピークメモリは枚数が増えても一定**に保ちます。

もとはスマホ向け音楽ゲームの選曲画面のために作りました。選曲画面では全曲分のジャケットを読み込みます。最初は全画像をスレッドプールに一度に投げていましたが、メインスレッドのテクスチャ転送より元サイズのデコードのほうが速く進み、処理待ちの画像が溜まって実機がメモリ不足で落ちていました。このライブラリは同じ速度のまま、ピークメモリを約 3 分の 1 に抑えます。

## 特長

- **並列数に上限のある読み込み**: デコードするスレッドは最大 `MaxWorkers` 本です。デコード中とテクスチャ転送待ちを合わせた画像も最大 `MaxPending` 枚までにします。枚数が増えてもピークメモリは増えません。
- **元サイズのマネージド配列を作らない**: デコーダーのネイティブバッファから直接縮小し、ネイティブバッファはすぐ解放します。マネージドヒープに載るのは縮小後の小さな配列だけです（IL2CPP / Mono のヒープは一度広がると縮みません）。
- **速い面積平均の縮小**: 元画像を先頭から 1 回だけ順に読みます。
- **フレームごとの持ち時間**: メインスレッドがテクスチャを転送する時間は 1 フレームあたり `FrameBudgetSeconds` までです。読み込み中もロード画面のアニメーションが止まりません。
- **依存パッケージなし**: Unity 標準の `Awaitable` を使います。デコーダーの [StbImageSharp](https://github.com/StbSharp/StbImageSharp)（パブリックドメイン）は internal 型として同梱しているので、プロジェクトに別の StbImageSharp があっても衝突しません。
- **対応形式**: PNG、JPEG、BMP、TGA、PSD、GIF（先頭フレーム）、HDR。グレースケールは RGB に広げます。

## 動作環境

- Unity 2023.1 以降（Unity 6 対応）
- WebGL には対応していません（スレッドを使うため）

## 導入

Package Manager の **+** から **Add package from git URL...** を選び、次を入力します。

```
https://github.com/na24ddr/LeanImageLoader.git
```

または `Packages/manifest.json` に追加します。

```json
"com.na24ddr.lean-image-loader": "https://github.com/na24ddr/LeanImageLoader.git"
```

## 使い方

### 大量に読み込む

```csharp
using LeanImageLoader;

async Awaitable LoadJacketsAsync(string[] paths, CancellationToken ct)
{
    var options = new BatchLoadOptions { MaxSize = 640 };

    Texture2D[] textures = await BatchImageLoader.LoadAsync(
        paths,
        options,
        (finished, total) => progressBar.value = (float)finished / total,
        ct);

    // textures[i] は paths[i] に対応します。ファイルが無い・画像でない場合は null です。
}
```

メインスレッドから呼んでください。返したテクスチャは呼び出し側のものなので、不要になったら `Destroy` してください。キャンセルした場合は、それまでに作ったテクスチャを破棄してから `OperationCanceledException` を投げます。

| 設定 | 既定値 | 意味 |
|---|---|---|
| `MaxSize` | 640 | 長辺の上限 (px)。これより小さい画像はそのままの大きさで使います |
| `MaxWorkers` | 8 | デコードするスレッド数の上限。実際の数は `min(SystemInfo.processorCount, MaxWorkers)` |
| `MaxPending` | 16 | デコード中とテクスチャ転送待ちを合わせた画像の上限 |
| `FrameBudgetSeconds` | 0.025 | 1 フレームでテクスチャ転送に使う時間 |
| `Linear` | false | リニア（色でないデータ）のテクスチャにする |

### 1 枚だけメインスレッドで読む

```csharp
Texture2D texture = LeanTextureLoader.LoadDownscaled(path, 640);
```

Unity ネイティブの `ImageConversion.LoadImage`（PNG / JPEG）で読んでから縮小します。

### 自分のスレッドでデコードする

```csharp
// どのスレッドからでも呼べます（Unity の API を使いません）
DecodedImage image = LeanImageDecoder.Decode(path, 640);

// メインスレッドで
Texture2D texture = image.ToTexture2D();
```

縮小だけが必要な場合は、純粋な C# の `ImageDownscaler` も使えます。

## ベンチマーク

300 枚（500×500 の JPEG、1000×708 の PNG、2000×1500 の PNG）を、8 コアに制限したプロセスで 640px に縮小して読み込んだ結果です。

| | 時間 | ピーク作業セット |
|---|---|---|
| 全画像をスレッドプールに一度に投げ、元サイズでデコード | 1.66 秒 | 約 580MB |
| Lean Image Loader（8 ワーカー、待ち 16 枚） | 1.66〜1.73 秒 | 約 180MB |

## テストの実行

プロジェクトの `Packages/manifest.json` の `testables` にこのパッケージを追加し、Test Runner から実行します。

```json
"testables": ["com.na24ddr.lean-image-loader"]
```

## ライセンス

[MIT](LICENSE.md)。同梱しているサードパーティのコードは [Third Party Notices](Third%20Party%20Notices.md) にまとめています。
