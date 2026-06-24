# MidiToEverything

任意の **MIDI コントローラー**（鍵盤・パッド・ノブ・フェーダー）を、Windows の汎用「左手デバイス」＝キーボードショートカット／マウス操作に変換する常駐型ソフトウェア。アクティブウィンドウに応じてプロファイルを自動切替し、基本（グローバル）プロファイルとの継承・上書きをサポートする。

> ステータス: M0–M9 実装完了（コア＋実機 MIDI/キー送信/窓監視＋WPF アプリ＋エディタ＋配布）。

## 主要機能
- MIDI デバイスの自動認識（ホットプラグ検知・複数デバイス・同名再接続。自動ポーリング/手動検出を切替可）
- リアルタイム入力モニター（実際に発火したアクションも表示）＋ラーン機能
- ノブ/フェーダー/キーの視覚的フィードバック（ゲージ・押下ハイライト）
- 豊富なアクション: キー入力/テキスト入力・マウス/カーソル/スクロール・メディアキー・音量・ウィンドウ/仮想デスクトップ操作・UI Automation・画面の明るさ・HTTP/OSC・OBS 連携・MIDI 出力・マクロ/トグル・プラグイン（ホールド・絶対値/相対エンコーダ対応）
- アクティブウィンドウ連動＋手動プロファイル切替＋ピン留め
- 基本プロファイルの継承・オーバーライド（明示ブロック対応）
- プロファイルエディタ（ラーン・起動中プロセス候補・正規表現自動生成・インポート/エクスポート）
- 設定ウィンドウ（タブ構成: 一般／外観／デバイス／更新／診断／管理）: 言語・起動/常駐・緊急停止ホットキー（変更可）・OBS 接続・ログ/診断・更新設定・設定のインポート/エクスポート/初期化
- 外観のカスタマイズ: ライト/ダークテーマ・アクセント色・UI 拡大率（いずれも即時反映）
- システムトレイ常駐（閉じる挙動・起動時最小化を設定可）・多重起動防止・Windows 起動時自動実行
- 自動アップデート（GitHub Releases を確認、stable/prerelease、MSI 取得→更新→再起動。公式ビルドのみ）
- クラッシュ時の自動再起動・ログ記録

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
2 種類を配布:

- **ポータブル zip** — 展開して `MidiToEverything.exe` を実行するだけ（インストール・管理者権限不要）。
- **MSI インストーラ** — `Program Files` へのマシン単位インストール（インストール時に管理者権限/UAC が必要）。
  ウィザード形式で、⓪**最初に表示言語を選択**（日本語/English/中文/Español/Deutsch/Français/한국어 の7言語。
  以降の画面が選んだ言語で表示される）①インストール先の選択 ②インストール進捗の表示 ③デスクトップ
  ショートカット作成の確認 ④Windows スタートアップ時に起動するかの確認 ⑤インストール完了後に起動するかの
  確認 を行う。スタートメニューにショートカットを作成し、「プログラムの追加と削除」に登録される。WiX で生成。
  ウィザードは翻訳表からの生成スクリプト（[installer/build-wxs.ps1](installer/build-wxs.ps1)）で
  [installer/Package.wxs](installer/Package.wxs) を出力している。

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
| 5 | [docs/05_ActionExpansion.md](docs/05_ActionExpansion.md) | アクション拡張の設計・カタログ（Phase 別） |
| 6 | [docs/06_BuiltinVsPluginActions.md](docs/06_BuiltinVsPluginActions.md) | 組み込みアクション vs プラグインの線引き |
| 7 | [docs/07_Localization.md](docs/07_Localization.md) | UI ローカライズの仕組み |
| 8 | [docs/08_SettingsRoadmap.md](docs/08_SettingsRoadmap.md) | 設定ウィンドウ項目のバックログ |

開発の進め方・コミット規約は [CONTRIBUTING.md](CONTRIBUTING.md)。

## 既知の制約
- Windows 専用（初期リリース）。x64。
- UIPI により、管理者権限で動くアプリへキー送信するには本体も昇格が必要。
- 一部 MIDI ドライバはデバイスを占有し、DAW との同時併用ができない場合がある（占有時は警告ログ）。
- 物理 USB の抜き差し検知は環境により `DevicesWatcher` が発火しないため、既定で 1 秒間隔のポーリングを併用（設定の「MIDIデバイスを自動検出」で手動検出に切替可）。
- アンチチート等で `SendInput` がブロックされるゲームは対象外。
