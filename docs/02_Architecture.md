# システムアーキテクチャ / モジュール設計 — MidiToEverything

関連: [01_PRD.md](01_PRD.md) / [03_ProfileSchema.md](03_ProfileSchema.md) / [04_Roadmap.md](04_Roadmap.md)

---

## 1. 技術スタックの選定

### 1.1 結論

| 領域 | 採用 | 主な代替案 |
|------|------|-----------|
| 言語 / ランタイム | **C# / .NET 8 (LTS)** | Rust, Node.js(Electron), Python |
| UI フレームワーク | **WPF + CommunityToolkit.Mvvm** | WinUI 3, Avalonia |
| MIDI ライブラリ | **Melanchall.DryWetMIDI** | RtMidi.Core, NAudio.Midi, managed-midi |
| OS 連携 | **Win32 P/Invoke**（自前薄ラッパ） + Vanara（補助） | — |
| 設定永続化 | **System.Text.Json**（JSON） | YAML(YamlDotNet) |
| DI / ロギング | Microsoft.Extensions.DependencyInjection / Serilog | — |
| 配布 | self-contained 単一 exe（ポータブル zip）＋ per-machine MSI（WiX）。GitHub Releases で配布、自動アップデート対応 | — |

### 1.2 選定理由

**言語: C# / .NET 8**
- Windows ネイティブ API（SetWinEventHook, SendInput, WinMM）への P/Invoke が容易で資料も豊富。最も摩擦が少ない。
- `async/await`・`System.Threading.Channels`・`Task` による高品質な非同期/並行処理が標準で揃う（本要件のスレッド分離に直結）。
- LTS で長期保守でき、単一ファイル self-contained 配布が可能（ユーザー環境にランタイム不要）。
- 代替: Rust+Tauri は配布が軽く高速だが OS 連携の実装コスト大。Electron は OS 連携が `robotjs`/`active-win` 等のネイティブ依存になり、メモリ重く保守性に難。Python は GIL・パッケージングがリアルタイム常駐に不利。→ **Windows 専用・常駐・低レイテンシ・保守性**の重みで C# が最適。

**UI: WPF**
- 成熟しており、MVVM・データバインディング・カスタム描画（ノブ/フェーダーのリアルタイム可視化）に強い。トレイ常駐の実績も豊富。
- WinUI 3 はモダンだが周辺（トレイ常駐、長期安定性）に粗さが残るため、初期は WPF を選ぶ。将来 UI を差し替えても**コアは UI 非依存**なので影響を局所化できる（後述レイヤ分割）。
- Avalonia はクロスプラットフォーム余地があるが、初期 Windows 専用では WPF の枯れた安定性を優先。

**MIDI: Melanchall.DryWetMIDI**
- `InputDevice` による受信、イベント型（NoteOnEvent / ControlChangeEvent / PitchBendEvent 等）への自動パースを提供。
- **`DevicesWatcher`** によりデバイスの接続/切断イベント（ホットプラグ, FR-1.2）をネイティブに検知できる点が決め手。
- 代替: NAudio.Midi は軽量だがホットプラグ検知が弱い。RtMidi.Core はクロスプラットフォームだが API が低水準。→ ホットプラグ要件のため DryWetMIDI。

**永続化: JSON (System.Text.Json)**
- 人間可読・手編集/共有が容易（FR-7.1）。.NET 標準で高速・AOT 親和。スキーマ進化に `version` フィールドで対応。
- YAML はコメントが書ける利点があるが、依存追加・パース曖昧性を避け JSON を既定とする（必要なら将来 YAML インポータを追加）。

---

## 2. アーキテクチャ全体像

クリーンアーキテクチャ的なレイヤ分割。**ドメイン/アプリケーション層は UI・OS から独立**させ、テスト可能にする。
OS 依存と MIDI 依存は「ポート（インターフェース）」の裏に隔離する。

```
┌──────────────────────────────────────────────────────────────────┐
│  Presentation (WPF / MVVM)                                         │
│  ・MainWindow(デバイス一覧/モニター/可視化)  ・ProfileEditor       │
│  ・TrayIcon/通知  ・ViewModels(INotifyPropertyChanged)            │
└───────────────▲───────────────────────────┬──────────────────────┘
                │ bind/observe               │ commands
┌───────────────┴───────────────────────────▼──────────────────────┐
│  Application (オーケストレーション層)                              │
│  ・EngineCoordinator(常駐ライフサイクル)                           │
│  ・MidiEventPipeline(Channel ベースの非同期パイプライン)           │
│  ・ProfileManager(アクティブプロファイル決定/切替・窓監視も購読)   │
│  ・FiringEvaluator/MappingResolver(合成・競合解決・発火判定)       │
└──────▲────────────▲────────────────▲───────────────▲──────────────┘
       │ ports      │ ports          │ ports         │ ports
┌──────┴───┐ ┌──────┴──────┐ ┌───────┴──────┐ ┌──────┴──────────────┐
│ Domain   │ │ Infra: MIDI │ │ Infra: OS    │ │ Infra: Persistence  │
│ (モデル) │ │ IMidiSource │ │ IWindowWatch │ │ IProfileRepository  │
│ Signal   │ │ DryWetMIDI  │ │ IInputSink   │ │ JsonProfileRepo     │
│ Binding  │ │ 実装        │ │ Win32 実装   │ │ ConfigMigrator      │
│ Profile  │ └─────────────┘ └──────────────┘ └─────────────────────┘
│ Action   │
└──────────┘
```

### データフロー（ホットパス: 入力 → 発行）

```
[MIDIデバイス]
   │ ドライバコールバックスレッド
   ▼
IMidiSource.EventReceived ──(MidiEvent)──▶ Channel<MidiEnvelope> (バウンド, ドロップなし)
                                                   │
                                  ┌────────────────┴───────────────┐
                                  ▼ (UI用 分岐, 間引きOK)            ▼ (発行用 ワーカー)
                         MonitorBroadcaster              MappingWorker (専用Task)
                         ・ログ/可視化へ throttle           1. ContextSnapshot 取得(キャッシュ)
                         ・Dispatcher で UI 反映             2. MappingResolver で有効アクション解決
                                                            3. ActionExecutor が IInputSink へ発行
                                                                   │
                                                            IInputSink.SendInput(Win32)
                                                                   ▼
                                                              [OSへキー/マウス]
```

> 図中の概念名と実型の対応: **MonitorBroadcaster** = 入力モニター（`MainViewModel` が `IMidiSource` を購読し throttle 表示）、**MappingWorker** = `MidiEventPipeline` 内の単一ワーカー（解決・発火判定は `FiringEvaluator`/`MappingResolver`、発行は `ActionExecutor` 経由 `IInputSink`）。

ポイント:
- MIDI コールバックスレッドでは**重い処理をしない**。エンベロープ化して Channel に積むだけ（< 数 µs）。
- 発行ワーカーは単一の長寿命 `Task`。順序保証とロック最小化のため**シングルライター**にする。
- UI 更新は別系統に分岐し throttle（例: 60fps / 30ms バッチ）して、高頻度 CC でも UI を凍結させない（FR-2.4）。

---

## 3. モジュール詳細

### 3.1 Domain（純粋・依存なし）
- `Signal`: `DeviceId, Channel, MessageType(NoteOn/Off, CC, PitchBend, ProgramChange), Number, [ValueMode]`。シグナルの**一意キー**を生成（辞書キー）。
- `Action`: 抽象。実装は `KeyStrokeAction`（修飾＋キー, ホールド可）, `MouseClickAction`, `CursorMoveAction`, `ScrollAction`, `SwitchProfileAction`, `MacroAction`, `NoneAction`(=ブロック)。
- `Binding`: `Signal` ＋ `Trigger`（しきい値/値域/デッドゾーン/相対 or 絶対）＋ `Action[]`。
- `Profile`: `Id, Name, MatchRules(process/title regex), Bindings, Enabled`。
- `BaseProfile`: 特別扱いの Profile（常時最下層）。

### 3.2 MappingResolver（競合解決の中核, 純粋ロジック）
有効マッピングをレイヤ合成で決定する。優先度（高→低）:

```
1. 手動ピン留めプロファイル（ユーザーが固定中の場合のみ）
2. コンテキスト一致プロファイル（アクティブウィンドウにマッチ）
3. 基本（グローバル）プロファイル
```

解決アルゴリズム（シグナル単位）:
```
resolve(signal, context):
    layers = [pinnedProfile?, contextProfile?, baseProfile]   # 上位優先
    for layer in layers (上位から):
        if layer defines binding for signal.key:
            if binding.action is NoneAction:   # 明示ブロック(FR-6.4)
                return NO_OP                    # 下位へフォールバックしない
            return binding                      # 上書き成立
    return NO_OP                                # どこにも無ければ無視
```
- アクティブプロファイルが定義 → 上書き（FR-6.2）。未定義 → 基本へフォールバック（FR-6.3）。
- `NoneAction` は「基本の割当を無効化」を意味し、フォールバックを止める（FR-6.4）。
- 解決結果は `(profileVersion, contextHash)` 単位でキャッシュし、ホットパスを高速化。
- **完全に純粋関数**なのでユニットテスト対象（入力: profiles + context、出力: effective binding）。

### 3.3 Infra: MIDI（`IMidiSource`）
- DryWetMIDI の `InputDevice` をラップ。受信イベントを Domain の `Signal` ＋値へ正規化。
- ホットプラグ検知（FR-1.2）: `DevicesWatcher` は物理 USB MIDI で発火しない環境があるため、
  **`InputDevice.GetAll()` の定期ポーリング（既定 1s）＋差分リコンサイル**を確実な土台とし、
  watcher 購読は即応用の補助として併用する。切断後の同名再接続で自動再 Attach（FR-1.4）。
  → 将来、自動ポーリング ⇔ 手動認識を切り替え可能にする（[04_Roadmap.md](04_Roadmap.md) B-1）。
- 複数デバイスを `DeviceId` で識別し並行監視（FR-1.3）。

### 3.4 Infra: OS 連携
**アクティブウィンドウ監視（`IWindowWatcher`）**
- 推奨: **`SetWinEventHook(EVENT_SYSTEM_FOREGROUND)`** によるイベント駆動でフォアグラウンド変化を検知（ポーリング不要・低負荷・低遅延）。
- フォールバック: 一部の全画面/特殊ケース対策に `GetForegroundWindow()` の低頻度ポーリング（例: 500ms）を併用可能。
- プロセス特定: `GetWindowThreadProcessId` → `QueryFullProcessImageName`（実行ファイル名）、`GetWindowText`（タイトル）。
- 注意: WinEventHook のコールバックは呼び出し元スレッドのメッセージループ上で動く。**専用 STA スレッド＋メッセージポンプ**を立てて受け、結果を `ContextSnapshot` としてアプリ層へ送る。

**入力発行（`IInputSink`）**
- **`SendInput`**（Win32, user32.dll）を使用。
  - キーボード: `INPUT_KEYBOARD`。ゲーム/一部アプリ互換のため **`KEYEVENTF_SCANCODE`（スキャンコード送信）** を既定。修飾キーは down/up を明示し、押下順・解放順を保証。ホールド型（FR-4.5）は Note On で down、Note Off で up。
  - マウスクリック: `INPUT_MOUSE`（LEFTDOWN/UP 等）。
  - カーソル移動: 相対は `MOUSEEVENTF_MOVE`、絶対は `MOUSEEVENTF_ABSOLUTE`（0–65535 正規化, マルチモニタは仮想デスクトップ座標）。
  - スクロール: `MOUSEEVENTF_WHEEL`。
- 制約: UIPI により**昇格アプリへは非昇格プロセスから送信不可**。起動時に管理者起動オプションを提示（PRD 6 章）。
- 緊急停止: グローバルホットキー（`RegisterHotKey`）で全発行を即停止（安全要件）。ホットキーは設定で変更可（§3.7）。

### 3.5 Infra: Persistence（`IProfileRepository`）
- `System.Text.Json` で設定・プロファイル群を JSON 保存/ロード（スキーマは [03_ProfileSchema.md](03_ProfileSchema.md)）。
- 保存先: `%APPDATA%\MidiToEverything\` の**単一 `config.json`**（設定＋基本プロファイル＋全プロファイル＋ActiveContext を1ファイルに集約）。ログは同フォルダの `logs\`。
- 外部編集の反映: 常駐中は再読込しないが、2回目の起動で既存インスタンスが前面化される際にディスクから再読込する（`App.ReloadConfigFromDisk`。プロファイルエディタを開いている間はスキップ）。エディタからの保存は即時に `ProfileManager.Reload` で反映（FR-7.2）。※ `FileSystemWatcher` は未使用。
- 書き込みはテンポラリ→アトミック置換（破損防止）。`version` でマイグレーション（現行 v2、v1→v2 の自動移行あり）。

### 3.6 Presentation（WPF / MVVM）
- `DeviceListView`（一覧/接続状態）, `InputMonitorView`（ログ＋ラーン）, `VisualizerView`（ノブ/フェーダー/パッドのリアルタイム表示）, `ProfileEditorView`（バインディング編集）, `StatusBar/Tray`（有効プロファイル・コンテキスト表示, FR-5.6）。
- ViewModel は `IObservable`/`INotifyPropertyChanged`。MIDI/コンテキストイベントは `SynchronizationContext`（Dispatcher）でマーシャリング。
- 可視化は `WriteableBitmap`/カスタム `Canvas` 描画で高頻度更新を捌く。
- **設定ウィンドウ**（`SettingsWindow`、タイトルバーの ⚙ から開く）: タブ構成（一般／外観／デバイス／更新／診断／管理）で全設定を集約。エクスポート/インポート/初期化も提供。

### 3.7 App シェル / 運用（`App.xaml.cs` 他）
- **多重起動防止**: 名前付き `Mutex` で1インスタンスに限定。2回目の起動は既存インスタンスを前面化（その際ディスクから config 再読込）して終了。
- **常駐**: トレイアイコン常駐。閉じる挙動（トレイへ最小化 / 終了）と起動時最小化は設定可（`CloseToTray`/`StartMinimized`）。
- **緊急停止ホットキー**: グローバルホットキー（`RegisterHotKey`）。スペックは設定で変更可（`EmergencyStopHotkey`、`HotkeyParser` で解析・即時再登録）。
- **テーマ**: ライト/ダーク＋アクセント色＋UI拡大率。`Theme.xaml` のブラシを `DynamicResource Pal.*` 化し、`ThemeManager` が色を差し替えて即時反映。
- **クラッシュ耐性**: `CrashReporter` が未処理例外をログ＋レポート化し自動再起動（連続クラッシュ時は停止、`CrashAutoRestart` でトグル）。
- **自動アップデート**: GitHub Releases を確認（stable / prerelease）。MSI を取得（SHA-256検証）→ `msiexec /passive` で更新→再起動。自動チェックは公式ビルドのみ（`App.IsOfficialBuild`）。詳細は [04_Roadmap.md](04_Roadmap.md)。

---

## 4. 並行・スレッド設計

| スレッド/コンテキスト | 役割 | 備考 |
|----|----|----|
| MIDI ドライバコールバック | 受信イベントを `MidiEnvelope` 化し `Channel` へ enqueue | 重処理禁止。ロックフリーに近づける |
| MappingWorker（`MidiEventPipeline` の単一 長寿命 Task） | Channel 消費→解決→発行 | シングルライターで順序保証 |
| WinEvent STA スレッド（メッセージポンプ。`WinEventWindowWatcher`） | フォアグラウンド変化検知 | `ContextSnapshot` を volatile/`Interlocked` で共有 |
| UI スレッド（Dispatcher） | 描画・操作 | 入力処理は載せない。更新は throttle/バッチ |
| FileWatcher / タイマ | 設定再読込・周期タスク | デバウンス |

- スレッド間連携は **`System.Threading.Channels`（producer/consumer）** と不変メッセージ（`record`）中心で、共有可変状態とロックを最小化。
- バックプレッシャ: Channel は bounded。ただし入力取りこぼしは不可なので容量を大きめ＋満杯時は最古を捨てず警告ログ（高頻度 CC は発行側でデッドゾーン/レート制限）。
- キャンセルは `CancellationToken` で一括停止（アプリ終了・緊急停止）。

---

## 5. エラーハンドリング / 可観測性
- デバイス切断/例外は監視ループを殺さず復旧（リトライ＋指数バックオフ）。
- Serilog でファイルログ（ローテーション）。レイテンシ計測（受信→発行）を内部メトリクス化。
- 「発行に失敗した（UIPI 等）」をユーザーに通知し、原因（昇格不足等）を提示。

---

## 6. テスト戦略
- **ユニット**: `MappingResolver`（競合解決・ブロック・フォールバック）、`Signal` キー、`Trigger`（しきい値/デッドゾーン/相対）。OS 非依存で完全カバー。
- **統合（フェイク）**: `IMidiSource`/`IWindowWatcher`/`IInputSink` のフェイク実装で、擬似 MIDI 列→期待アクション列を検証（実機・実発行なし）。
- **手動 E2E**: 実機 MIDI ＋ メモ帳/CSP で受け入れ基準（PRD 8 章）を確認。
- ポート（インターフェース）境界のおかげで、OS へ実発行せずに大半をテスト可能。
