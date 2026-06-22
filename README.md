# MidiToEverything

任意の **MIDI コントローラー**（鍵盤・パッド・ノブ・フェーダー）を、Windows の汎用「左手デバイス」＝キーボードショートカット／マウス操作に変換する常駐型ソフトウェア。アクティブウィンドウに応じてプロファイルを自動切替し、基本（グローバル）プロファイルとの継承・上書きをサポートする。

> ステータス: 計画・設計フェーズ（ドキュメント先行）。実装は [docs/04_Roadmap.md](docs/04_Roadmap.md) の M1 から。

## 主要機能
- MIDI デバイスの自動認識（ホットプラグ検知）
- リアルタイム入力モニター＋ラーン機能
- ノブ/フェーダー/キーの視覚的フィードバック
- マッピングエンジン（キー/マウス/カーソル/スクロール、ホールド・相対エンコーダ対応）
- アクティブウィンドウ連動＋手動プロファイル切替
- 基本プロファイルの継承・オーバーライド（明示ブロック対応）

## 技術スタック（要約）
C# / .NET 8 ・ WPF(MVVM) ・ Melanchall.DryWetMIDI ・ Win32 P/Invoke（SetWinEventHook / SendInput）・ System.Text.Json。
選定理由は [docs/02_Architecture.md](docs/02_Architecture.md) §1。

## ドキュメント
| # | ドキュメント | 内容 |
|---|------|------|
| 1 | [docs/01_PRD.md](docs/01_PRD.md) | 要件定義書（機能/非機能要件、ユースケース、リスク） |
| 2 | [docs/02_Architecture.md](docs/02_Architecture.md) | 技術選定・アーキテクチャ・モジュール・スレッド設計 |
| 3 | [docs/03_ProfileSchema.md](docs/03_ProfileSchema.md) | プロファイル設定スキーマ（JSON 例・C# 型） |
| 4 | [docs/04_Roadmap.md](docs/04_Roadmap.md) | タスク分解・マイルストーン・着手手順 |

## 動作確認（M4: MIDI入力）
`run-midi-monitor.bat` をダブルクリックすると、MIDI入力モニタ（コンソール）が起動します。
接続済みデバイスの一覧、抜き差し（ホットプラグ）の検知、Note/CC/Pitch Bend などの
受信イベントをリアルタイム表示します（要 .NET 8 SDK）。

## 既知の制約
- Windows 専用（初期リリース）。
- UIPI により、管理者権限で動くアプリへ送信するには本体も昇格が必要。
- 一部 MIDI ドライバはデバイスを占有し、DAW との同時併用ができない場合がある。

詳細は各ドキュメント参照。
