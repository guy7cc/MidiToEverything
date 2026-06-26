# 実装ロードマップ / タスク分解 — MidiToEverything

関連: [01_PRD.md](01_PRD.md) / [02_Architecture.md](02_Architecture.md) / [03_ProfileSchema.md](03_ProfileSchema.md)

Claude Code を用いた**段階的・検証可能**な実装手順。各マイルストーンは「動く成果物（デモ可能）」で区切る。
依存の少ない**コア（純粋ロジック）から着手**し、OS 連携・UI を後から被せる（リスク前倒し＋テスト容易性）。

> **進捗**: M0〜M10 ✅ 完了。コア＋実機 Infra＋WPF アプリ＋エディタ＋配布＋**1.0.0 リリース整備**まで実装・検証済み。
> 起動: `dotnet run --project src/App/App.csproj -c Release`。配布: タグ push → GitHub Releases（CI）、またはローカル `dotnet publish`（self-contained 単一 exe）。
> 検証: 自動起動(レジストリ登録)・発行 exe の起動・各機能をスクリーンショット/テストで確認済み（`Core.Tests` + `Infrastructure.Tests` の全テスト緑、現在 277 件）。
> 以降は設定ウィンドウ拡充（[08_SettingsRoadmap.md](08_SettingsRoadmap.md)）・自動アップデート・多重起動防止・テーマ等の運用機能を追加済み。B-1（MIDI 検出モード切替）は設定として永続化済み。
> **次の節目: v1.0.0**（最初の安定版）。整備項目は [M10](#マイルストーン-m10--100-リリース整備05日) を参照。

---

## 0. 実装順の原則

1. **ドメイン → ポート定義 → コア（解決ロジック）→ Infra 実装 → UI** の順。
2. OS 依存（MIDI/ウィンドウ/入力発行）はすべて**インターフェース（ポート）**の裏。まずフェイクで通し、後で実装を差し替える。
3. 各 PR/コミットは「テストが緑」かつ「手で確認できる小さなデモ」を伴う。
4. レイテンシ要件（< 10ms）に関わるホットパスは早期に骨組みを作り、計測ログを仕込む。

---

## マイルストーン M0 — プロジェクト基盤（0.5〜1日）

**目的:** ソリューション骨格と CI、DI、ロギングが動く。

- [ ] ソリューション作成（`MidiToEverything.sln`）。プロジェクト分割:
  - `Core`（Domain＋Application、UI/OS 非依存, netstandard/net8）
  - `Infrastructure`（MIDI/OS/Persistence 実装, net8-windows）
  - `App`（WPF, net8-windows）
  - `Core.Tests`（xUnit）
- [ ] DI（`Microsoft.Extensions.DependencyInjection`）・Serilog・設定読込の土台。
- [ ] `dotnet test` が走る最小 CI（GitHub Actions, windows-latest）。

**完了条件:** 空アプリが起動し、ログが出る。テストが 1 件緑。

---

## マイルストーン M1 — ドメイン＋マッピング解決ロジック（コア, 1〜2日）★最初に書く

**目的:** OS なしで「シグナル＋ルール → 有効アクション」を確定できる。**ここがアプリの心臓**。

- [ ] Domain 型: `Signal` / `Trigger` / `Action`(union) / `Binding` / `Profile`（[03_ProfileSchema.md](03_ProfileSchema.md) §7）。
- [ ] `Signal.Key` の正規化（`*`/`any` のワイルドカード一致を含む）。
- [ ] `MappingResolver`（[02_Architecture.md](02_Architecture.md) §3.2）:
  - レイヤ優先（pinned > context > base）、上書き、フォールバック、`none` によるブロック。
  - 解決キャッシュ（profileVersion + contextHash）。
- [ ] `TriggerEvaluator`: しきい値/デッドゾーン/反転/スケール、`absolute`/`relative`(エンコーダ符号方式)/`hold` の値→出力量変換。
- [ ] **ユニットテスト**: [03_ProfileSchema.md](03_ProfileSchema.md) §6 の競合解決表をそのままテストケース化。

**完了条件:** 競合解決・ブロック・フォールバック・相対値変換が全て緑。OS 一切不要。

> 👉 **Claude Code はここから着手するのが最適**。純粋ロジックで外部依存ゼロ、テストで正しさを固定でき、以降の土台になる。

---

## マイルストーン M2 — 永続化（JSON）（0.5〜1日）

**目的:** [03_ProfileSchema.md](03_ProfileSchema.md) の JSON を読み書きできる。

- [ ] `IProfileRepository` と `JsonProfileRepository`（`System.Text.Json`, ポリモーフィック Action）。
- [ ] `%APPDATA%\MidiToEverything\config.json` のロード/保存（アトミック書込）。
- [ ] `version` フィールド＋マイグレーション骨組み。
- [ ] サンプル `config.json` をリソース同梱（初回生成）。
- [ ] ラウンドトリップテスト（保存→ロードで等価）。

**完了条件:** サンプル設定をロードして M1 の Resolver に渡し、期待アクションが出る（統合テスト, フェイク入力）。

---

## マイルストーン M3 — ポート＋フェイクでパイプライン貫通（1日）

**目的:** UI/実機なしで「擬似 MIDI 列 → 実発行直前まで」を通す。ホットパスの骨格確立。

- [ ] ポート定義: `IMidiSource`(イベント発行) / `IWindowWatcher`(ContextSnapshot) / `IInputSink`(発行)。
- [ ] `MidiEventPipeline`: `System.Threading.Channels` の producer/consumer、単一 `MappingWorker`。
- [ ] フェイク実装: `FakeMidiSource`（スクリプト再生）/ `FakeWindowWatcher` / `RecordingInputSink`（発行内容を記録）。
- [ ] レイテンシ計測ログ（受信→発行のスタンプ）。
- [ ] 統合テスト: 「CSP コンテキスト＋Note40 押下保持→Space down、離す→Space up」等を `RecordingInputSink` で検証。

**完了条件:** エンドツーエンド（発行直前まで）がフェイクで緑。スレッド分離が機能。

---

## マイルストーン M4 — MIDI 実装（ホットプラグ）（1〜2日）

**目的:** 実機 MIDI を受信し、接続/切断を検知。

- [ ] `DryWetMidiSource`: `InputDevice` 受信→`Signal`＋値へ正規化。
- [ ] `DevicesWatcher` 購読で接続/切断イベント（FR-1.2）、同名再接続の自動 Attach（FR-1.4）、複数デバイス（FR-1.3）。
- [ ] コールバックスレッドは enqueue のみ（重処理排除）。
- [ ] コンソール/ログで受信を確認（UI 前の暫定）。

**完了条件:** 実機を抜き差しすると検知ログが出て、操作すると Signal が流れる。

---

## マイルストーン M5 — OS 入力発行（SendInput）（1〜2日）

**目的:** 実際に OS へキー/マウスを発行。

- [ ] `Win32InputSink`: `SendInput` の P/Invoke（キーボード=スキャンコード既定, マウスクリック/相対・絶対移動/ホイール）。
- [ ] ホールド型（Note On=down / Note Off=up）、修飾キーの押下/解放順保証、オートリピート。
- [ ] 緊急停止ホットキー（`RegisterHotKey`）で全発行停止。
- [ ] 手動確認: メモ帳で Undo/コピー、ブラウザでスクロール/ズーム。

**完了条件:** 実機パッド→メモ帳で受け入れ基準（PRD 8 章「ラーンで Ctrl+Z」）が成立。UIPI 制約を README に明記。

---

## マイルストーン M6 — アクティブウィンドウ監視＋ルール自動切替（1〜2日）

**目的:** コンテキスト連動を実現。

- [ ] `WinEventWindowWatcher`: 専用 STA スレッド＋メッセージポンプで `SetWinEventHook(EVENT_SYSTEM_FOREGROUND)`、プロセス名/タイトル取得。
- [ ] `ProfileManager`: ContextSnapshot＋match ルールでアクティブルール決定、`priority` 解決、ピン留め（FR-5.5）。
- [ ] `switchProfile` アクション（next/prev/toggle/指定）を MIDI から（FR-5.4）。
- [ ] 手動確認: CSP↔ブラウザ切替で自動ルール変更、トレイ表示更新。

**完了条件:** PRD 8 章「CSP 前面で自動切替」「基本＋上書き」が成立。

---

## マイルストーン M7 — WPF UI: 常駐＋モニター＋可視化（2〜3日）

**目的:** ユーザーが見て操作できる。

- [ ] アプリシェル＋トレイ常駐（最小化でバックグラウンド, FR-7.3）、有効ルール/コンテキスト表示（FR-5.6）。
- [ ] デバイス一覧ビュー（接続状態, FR-1.1）。
- [ ] 入力モニター（ログ＋フィルタ＋一時停止、UI throttle/バッチ, FR-2.1/2.2/2.4）。
- [ ] 視覚フィードバック（ノブ/フェーダーのゲージ、Note 押下ハイライト, FR-3.1〜3.4）。
- [ ] MIDI/コンテキストイベントの Dispatcher マーシャリング。

**完了条件:** 操作すると UI のインジケーターが動き、ログが流れる。高頻度 CC でも凍結しない。

---

## マイルストーン M8 — ルールエディタ＋ラーン（2〜3日）

**目的:** GUI だけで設定を完結。

- [ ] ルール CRUD、match ルール編集、バインディング編集（アクション union のフォーム）。
- [ ] 「ラーン」: 最後に受信した Signal を取り込みバインディング作成（FR-2.3）。
- [ ] 基本/上書きの区別表示（FR-6.5）、`none` ブロックの UI。
- [ ] 変更の即時反映（FileWatcher/直接反映, FR-7.2）、インポート/エクスポート（FR-7.4）。

**完了条件:** インストール直後から、GUI のみで CSP ルールを作って動かせる。

---

## マイルストーン M9 — 仕上げ・配布（1〜2日）

- [ ] 自動起動オプション（FR-7.6）、設定永続、エラー通知（UIPI/発行失敗）。
- [ ] 24h 常駐の安定性確認（リーク/ハンドル）。
- [ ] self-contained single-file 発行、README（制約・既知事項）、サンプルルール。
- [ ] （任意）コードサイニング、MSIX 化検討。

**完了条件:** 配布物単体でインストール→運用できる。

---

## マイルストーン M10 — 1.0.0 リリース整備（0.5日）

**目的:** 0.x の機能群を「最初の安定版（v1.0.0）」として固める。0.6.0 まででアプリは機能完備のため、
本マイルストーンは**運用上の頑健性・配布物の体裁**に絞る。ユーザー操作・実機を要しない範囲は自動テスト化する。

- [x] **設定ファイルの破損耐性**（リリース必須）: 起動時の `LoadOrCreateDefault()` が、破損・部分書き込み・
  未知スキーマの `config.json` でクラッシュしないこと。破損ファイルを `config.corrupt[.N].json` に退避して
  既定で起動し、警告ログを残す。`JsonProfileRepository` のユニットテストで自動検証（破損 JSON / 新スキーマ /
  連続破損のバックアップ非破壊）。
- [x] **バージョン番号を 1.0.0 へ**（[Directory.Build.props](../Directory.Build.props) の dev 既定）。
  リリースは従来どおりタグ（`v1.0.0`）から `-p:Version=` で上書き。
- [x] **CHANGELOG.md** を追加（Keep a Changelog 形式）。1.0.0 の Added/Changed/Fixed と既知制約を記載。
- [x] 全テスト緑（`Core.Tests` + `Infrastructure.Tests`）を確認のうえコミット＆プッシュ。

**完了条件:** 壊れた設定でも起動でき、バージョン/変更履歴が 1.0.0 として整い、`dotnet test` が緑。
リリースは `git tag v1.0.0 && git push origin v1.0.0` で CI が ZIP/MSI を発行する。

> **残課題（1.0.0 後でも可）**: 破損時のトレイ通知（GUI のため別途・要手動確認）、
> [08_SettingsRoadmap.md](08_SettingsRoadmap.md) の `[ ]` 項目（ポーリング間隔 UI 等、いずれも任意）。

---

## 依存関係（要約）

```
M0 ─▶ M1 ─▶ M2 ─▶ M3 ─┬─▶ M4(MIDI実機) ─┐
                        ├─▶ M5(発行)      ├─▶ M7(UI) ─▶ M8(エディタ) ─▶ M9
                        └─▶ M6(窓監視/切替)┘
```
- M1→M2→M3 は一直線（コア確立）。M4/M5/M6 は M3 の後なら並行可能。
- UI（M7/M8）はコアと Infra が揃ってから。

---

## Claude Code への着手指示テンプレ（コピー用）

> 「`Core` プロジェクトに [03_ProfileSchema.md](03_ProfileSchema.md) §7 の Domain 型を実装し、[02_Architecture.md](02_Architecture.md) §3.2 の `MappingResolver` と `TriggerEvaluator` を作成。[03_ProfileSchema.md](03_ProfileSchema.md) §6 の競合解決表を xUnit テストとして実装し、全て緑にして。OS/MIDI/UI には一切触れないこと（ポートは未定義のままで良い）。」

これが M1。緑になったら M2（JSON 永続化）→ M3（ポート＋フェイク貫通）と進める。

---

## 将来拡張・バックログ

実装順は未定。マイルストーン進行の合間、または該当 UI 着手時（M7/M8）に取り込む。

### B-1. MIDI デバイス検出モードの切り替え（自動ポーリング ⇔ 手動認識）
- **状態**: ✅ 基本実装済み（UI overhaul で対応）。`IMidiSource.DetectionMode`（`AutoPolling`/`Manual`）と
  `Rescan()` を追加。`DryWetMidiSource` は Manual 時にポーリングタイマーを停止。メインウィンドウの
  デバイスパネルに「今すぐ更新」ボタン、検出モードのトグルは設定ウィンドウ（デバイスタブ）へ集約。
- **永続化**: ✅ 設定 `autoDetectDevices`（[03_ProfileSchema.md](03_ProfileSchema.md) §5）として保存し起動時に適用。
- **残り（任意）**: ポーリング間隔の設定 UI、フォアグラウンド復帰時の自動再スキャン。
- **背景**: `DryWetMidiSource` は `DevicesWatcher` が物理 USB で発火しないため、1 秒間隔の
  `InputDevice.GetAll()` ポーリングでホットプラグを検知している（[02_Architecture.md](02_Architecture.md) §3.3）。
- **設計案**:
  - `MidiDetectionMode { AutoPolling, Manual }` を導入。検出のプリミティブは既存の
    リコンサイル処理を公開した `Rescan()`（= 現在の `Reconcile()`）。
  - **AutoPolling**: タイマー間隔を設定可能に（既定を 1s→2s 程度へ緩和も検討）。
    `DevicesWatcher` が効く環境では即応し、タイマーはフォールバック。
  - **Manual**: タイマー無し。`Rescan()` の発火契機を用意する:
    - UI の「デバイス再スキャン」ボタン（M7/M8）
    - アプリのフォアグラウンド復帰時に 1 回スキャン（M6 の窓監視と連動）
    - 特定 MIDI 入力に「再スキャン」アクションを割当（任意）
  - **設定スキーマ**（[03_ProfileSchema.md](03_ProfileSchema.md) §5 `settings` に追加予定、未実装）:
    ```jsonc
    "midi": { "detectionMode": "autoPolling", "pollIntervalMs": 2000 }
    ```
  - 後方互換: 既定は AutoPolling。`DryWetMidiSource` のコンストラクタは既に `pollInterval` を
    受け取るため、モード分岐とタイマー有無の制御を足すだけで移行できる。
