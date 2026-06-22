# MidiToEverything

任意の **MIDI コントローラー**（鍵盤・パッド・ノブ・フェーダー）を、Windows の汎用「左手デバイス」＝キーボードショートカット／マウス操作に変換する常駐型ソフトウェア。アクティブウィンドウに応じてプロファイルを自動切替し、基本（グローバル）プロファイルとの継承・上書きをサポートする。

> ステータス: M0–M9 実装完了（コア＋実機 MIDI/キー送信/窓監視＋WPF アプリ＋エディタ＋配布）。

## 主要機能
- MIDI デバイスの自動認識（ホットプラグ検知・複数デバイス・同名再接続）
- リアルタイム入力モニター＋ラーン機能
- ノブ/フェーダー/キーの視覚的フィードバック（ゲージ・押下ハイライト）
- マッピングエンジン（キー/マウス/カーソル/スクロール、ホールド・相対エンコーダ対応）
- アクティブウィンドウ連動＋手動プロファイル切替＋ピン留め
- 基本プロファイルの継承・オーバーライド（明示ブロック対応）
- プロファイルエディタ（ラーン・起動中プロセス候補・正規表現自動生成・インポート/エクスポート）
- システムトレイ常駐・緊急停止ホットキー（Ctrl+Alt+Pause）・Windows 起動時自動実行

## 技術スタック（要約）
C# / .NET 8 ・ WPF(MVVM, CommunityToolkit.Mvvm) ・ Melanchall.DryWetMIDI ・ Win32 P/Invoke（SetWinEventHook / SendInput / RegisterHotKey）・ System.Text.Json。
選定理由は [docs/02_Architecture.md](docs/02_Architecture.md) §1。

## 構成
- `src/Core` — ドメイン・マッピング解決・永続化（OS/UI 非依存、テスト対象）
- `src/Infrastructure` — MIDI(DryWetMIDI)・Win32 アダプタ（窓監視・入力送信・自動起動）
- `src/App` — WPF アプリ（常駐・モニター・ビジュアライザ・エディタ）
- `tests/` — Core.Tests / Infrastructure.Tests（xUnit）

## ビルドと実行（要 .NET 8 SDK）
```sh
dotnet build MidiToEverything.sln -c Release      # ビルド
dotnet test  MidiToEverything.sln -c Release      # テスト
dotnet run   --project src/App/App.csproj -c Release   # アプリ起動
```

## 配布（self-contained 単一ファイル）
公開リリースは **GitHub Releases** に自動添付される（バージョンタグの push で CI が生成）。
リリース手順は [CONTRIBUTING.md](CONTRIBUTING.md) §6。

ローカルで単一 exe を作る場合:
```sh
dotnet publish src/App/App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish
```
→ `publish/MidiToEverything.exe`（.NET ランタイム不要の単一 exe）。

## 設定ファイル
`%APPDATA%\MidiToEverything\config.json`（初回起動時に既定生成）。スキーマは [docs/03_ProfileSchema.md](docs/03_ProfileSchema.md)、サンプルは [samples/config.sample.json](samples/config.sample.json)。
ログは `%APPDATA%\MidiToEverything\logs\`。

## ウィンドウ判別（プロファイルのマッチ）
各プロファイルは **単一の正規表現**を持ち、`"<プロセス名>\n<ウィンドウタイトル>"` の2行文字列に複数行モードで照合する。
エディタの「候補を追加」は、起動中プロセスから選ぶ／名前を入力したプロセスを表す節 `^name$` を正規表現へ自動統合する（失敗時は通知）。正規表現は手動編集可能。

## ドキュメント
| # | ドキュメント | 内容 |
|---|------|------|
| 1 | [docs/01_PRD.md](docs/01_PRD.md) | 要件定義書（機能/非機能要件、ユースケース、リスク） |
| 2 | [docs/02_Architecture.md](docs/02_Architecture.md) | 技術選定・アーキテクチャ・モジュール・スレッド設計 |
| 3 | [docs/03_ProfileSchema.md](docs/03_ProfileSchema.md) | プロファイル設定スキーマ（JSON 例・C# 型） |
| 4 | [docs/04_Roadmap.md](docs/04_Roadmap.md) | タスク分解・マイルストーン・着手手順 |

開発の進め方・コミット規約は [CONTRIBUTING.md](CONTRIBUTING.md)。

## 既知の制約
- Windows 専用（初期リリース）。x64。
- UIPI により、管理者権限で動くアプリへキー送信するには本体も昇格が必要。
- 一部 MIDI ドライバはデバイスを占有し、DAW との同時併用ができない場合がある（占有時は警告ログ）。
- 物理 USB の抜き差し検知は環境により `DevicesWatcher` が発火しないため、既定で 1 秒間隔のポーリングを併用（[docs/04_Roadmap.md](docs/04_Roadmap.md) B-1 で切替式を予定）。
- アンチチート等で `SendInput` がブロックされるゲームは対象外。
