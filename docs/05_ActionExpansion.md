# 05. アクション拡張 設計ドキュメント（ドラフト・要合意）

「MidiToEverything」の名にふさわしく、MIDI 入力をキーボード/マウス以外の**あらゆる操作**
（アプリ起動、特定ウィンドウのボタン押下、システム音量、Windows 設定トグル、外部連携 …）
にバインドできるようにするための基盤設計と、追加アクションのカタログ。

> 本ドキュメントは **実装前の合意用ドラフト**。§8 の未決事項に合意してから実装に入る。

---

## 1. 目的とスコープ

- **基盤**: 新しいアクションを「既存コードを触らず・型安全に・疎結合に」追加できる構造へ進化させる。
- **連続値の活用**: ノブ/フェーダー/ピッチベンドの連続値（`TriggerResult.Magnitude`）を
  音量・輝度・ズーム等のアナログ量にマッピングできるようにする（キーボードマクロとの差別化点）。
- **カタログ**: 追加アクションを Phase 分けで定義（§5）。

本ドラフトのスコープは **基盤（§3）＋カタログ定義（§5）** まで。個別アクションの実装は合意後に着手。

---

## 2. 現状の制約（出発点）

| 層 | 現状 | 制約 |
|----|------|------|
| ドメイン | `InputAction` 判別共用体（`KeyAction` 他6種, `src/Core/Domain/Actions.cs`） | 各アクションは型付きだが、**エディタは単一 `Detail` 文字列**で全アクションを表現（`EditMapper.DescribeAction`/`ToAction`） |
| 実行 | `ActionExecutor` が型 `switch` → `IInputSink` を呼ぶ | `IInputSink` は KB/マウス/スクロール/カーソル固定。**新カテゴリのたびに switch と Core を改変**する必要 |
| 永続化 | `ActionDto` の `[JsonDerivedType]` 多態（"key","scroll" 等, `ConfigDto.cs`） | DTO 追加は容易。スキーマ互換の方針が必要 |
| エディタ | `EditableActionKind` enum＋`Detail` 文字列＋候補/検証 | **複数パラメータを持つアクションを入力できない**（パス+引数、ウィンドウ+要素 等） |

→ ボトルネックは「**単一 Detail 文字列**」と「**IInputSink 固定＋executor switch**」の2点。

---

## 3. 設計方針

### 3.1 ドメイン: 型付きアクションの拡張（維持＋追加）

`InputAction` 判別共用体はそのまま活かし、新アクションを**型付き record** として追加する。
単一文字列に押し込めない（パラメータ）はレコードのフィールドで表現する。例:

```csharp
public sealed record LaunchAction(string Target, string? Arguments = null,
                                  string? WorkingDir = null) : InputAction;

public sealed record SetVolumeAction(VolumeTarget Target,   // Master | App | Mic
                                     string? AppName = null,
                                     ValueMap Map = default) : InputAction; // 0-127→0-100%

public sealed record UiaInvokeAction(WindowRef Window,      // プロセス/タイトル正規表現
                                     ElementRef Element,     // AutomationId/Name/ControlType
                                     UiaVerb Verb = UiaVerb.Invoke, // Invoke|Toggle|SetValue
                                     string? Value = null) : InputAction;

public sealed record HttpAction(string Url, HttpMethod Method = HttpMethod.Get,
                                string? Body = null, IReadOnlyList<Header>? Headers = null) : InputAction;

public sealed record RunCommandAction(string Command, bool Shell = true) : InputAction;
public sealed record WindowControlAction(WindowOp Op) : InputAction; // Minimize|Maximize|Close|TopMost|...
public sealed record MediaKeyAction(MediaKey Key) : InputAction;     // PlayPause|Next|Prev|...
public sealed record MidiOutAction(MidiOutMessage Message) : InputAction;
```

### 3.2 実行: ハンドラ registry ＋ 新ポート

`ActionExecutor` の巨大 switch を、**アクション型 → ハンドラ**のレジストリに置き換える。
Core は OS 非依存を維持し、OS 操作は **Core.Application.Ports のインターフェース**で抽象化、
実体は Infrastructure に置く（既存 `IInputSink` と同じ流儀）。

```csharp
public interface IActionHandler
{
    bool CanHandle(InputAction action);
    void Execute(InputAction action, TriggerResult trigger, MidiMessage message);
}
// 例: LaunchActionHandler(IShellLauncher), VolumeActionHandler(ISystemAudio),
//     UiaActionHandler(IUiaDriver), HttpActionHandler(IHttpSender) ...
```

- `ActionExecutor` は登録ハンドラを順に `CanHandle` で照合して委譲する薄いディスパッチャに。
- `SwitchProfileAction` は従来どおりイベントで ProfileManager に通知（OS には送らない）。
- 新ポート候補: `IShellLauncher` / `ISystemAudio` / `IWindowController` / `IUiaDriver`
  / `IHttpSender` / `IOscSender` / `IMidiOut`。
- **拡張性**: 新アクション = 「record 追加 ＋ DTO 追加 ＋ ハンドラ追加 ＋ ポート（必要なら）追加」。
  既存ハンドラ・executor は無改変（Open/Closed）。

### 3.3 連続値（Magnitude）の一級市民化

`TriggerResult` は既に `Phase`(Press/Release/Change) と `Magnitude` を持つ。
値駆動アクションは共通の **`ValueMap`**（入力 0–127 → 出力レンジ、反転・カーブ・デッドゾーン）を持ち、
Absolute（フェーダー＝絶対位置）/ Relative（エンコーダ＝増減）を宣言的に扱う。
音量・輝度・ズーム・UIA RangeValue・OSC/MIDI 値などはこの仕組みで統一する。

### 3.4 永続化（スキーマ v3・後方互換）

- `ActionDto` に新 DTO を追加し `[JsonDerivedType(typeof(...), "launch")]` 等で識別子を付与。
- **後方互換**: 既存アクション DTO は不変＝v2 設定はそのまま読める。`version` を 3 に上げるが、
  既知の型だけで構成された設定は v2/v3 双方を無改変ロード可能（追加は純粋に additive）。
- 未知の識別子に出会った場合の方針（§8-Q3）を決める：エラー停止 / 無視してスキップ / 警告ログ。

### 3.5 エディタ UI: パラメータ駆動フォーム

単一 `Detail` テキストボックスを廃し、**アクション種別ごとに必要な入力欄を動的に描画**する。
2案：

- **案A（推奨）: per-kind DataTemplate**。`EditableActionKind` ごとに XAML の `DataTemplate` を用意し
  `DataTemplateSelector` で切替。型安全・見た目を作り込みやすい。欄は既存の検証/候補/ツールチップ
  （`SignalValidation`・`ActionDetailCandidates`）の枠組みを各欄に再利用。
- 案B: メタ駆動の汎用フォーム（パラメータ記述子からラベル＋入力欄を生成）。汎用だが作り込み弱。

UIA 系には **「対象ウィンドウ/要素のピッカー」**（テスト自動化で使っている UI Automation を流用し、
カーソル下またはフォーカス要素の AutomationId/Name/ControlType を取り込む）を用意する。
これは MIDI Learn（最後の入力を取込）の "要素版"。

#### 3.5.1 複雑アクションの専用ダイアログ（ActionConfigWindow）

設定項目が多いアクション（Uia/Launch/Http/Osc/Obs/MidiOut/Macro/Toggle/Plugin）は、メイン編集画面が
煩雑になるため**専用ダイアログに分離**する。メイン側は「⚙ 詳細設定…」ボタンのみ表示し、ダイアログに以下を集約:

1. **設定項目本体** — per-kind テンプレート（App.xaml で共有、`ComplexActionContent` スタイルで種別ごとに切替）。
2. **項目の説明** — ヘッダーの一行説明（`ActionKindHelp`）＋各欄のツールチップ。
3. **設定方法のインストラクション** — `EditorHelp.Instructions` の手順テキスト。
4. **未保存設定の動作確認** — 現在の下書きを `ActionExecutor` で一度だけ実行（`EditMapper.ToAction`→
   value 連動は Change、他は Press）。ネットワーク/起動/OBS/MIDI 系は即時に検証可能。

ダイアログは下書き（`DraftBinding`）を直接編集するため、別途の適用は不要（自動下書きモデルと一貫）。
単純なアクションは従来どおりインライン編集。

---

## 4. データフロー（追加後）

```
MIDIメッセージ → MidiEventPipeline → (Profile解決/Trigger評価)
   → ActionExecutor(dispatcher) → 該当 IActionHandler
        → 各ポート(IShellLauncher / ISystemAudio / IUiaDriver / IHttpSender / IMidiOut ...)
        → OS / 対象アプリ / ネットワーク / コントローラLED
```

---

## 5. アクション・カタログ（Phase 別）

凡例 — トリガー適性: ⦿=単発(Trigger/Hold) ▥=連続(Absolute/Relative)　難度: ★(易)〜★★★(難)

### Phase 1 — OS 基本（基盤刷新と同時。実装容易・高価値）
| アクション | パラメータ | ポート | トリガー | 難度 |
|---|---|---|---|---|
| Launch（アプリ/ファイル/URL 起動） | target, args, workingDir | IShellLauncher (ShellExecute) | ⦿ | ★ |
| RunCommand（コマンド/PS 実行） | command, shell | IShellLauncher | ⦿ | ★ |
| SetVolume（マスター/アプリ/マイク） | target, appName, ValueMap | ISystemAudio (Core Audio) | ▥⦿ | ★★ |
| ToggleMute | target, appName | ISystemAudio | ⦿ | ★ |
| MediaKey（再生/停止/次/前） | key | IInputSink（拡張） | ⦿ | ★ |
| WindowControl（最小化/最大化/閉/最前面/別モニタ） | op | IWindowController (Win32) | ⦿ | ★★ |
| TypeText / PasteSnippet（定型文） | text | IInputSink | ⦿ | ★ |

### Phase 2 — ターゲット操作・デスクトップ
| アクション | パラメータ | ポート | 難度 |
|---|---|---|---|
| **UiaInvoke/Toggle/SetValue（特定ウィンドウの特定要素）** | window, element, verb, value | IUiaDriver (UI Automation) | ★★★ |
| ActivateWindow（指定ウィンドウを前面化） | window | IWindowController | ★★ |
| VirtualDesktop（next/prev/指定） | target | IDesktopController (COM) | ★★ |
| WindowsToggle（ダーク/ライト, 夜間, 集中, Wi-Fi/BT 等） | setting | ISystemToggle (レジストリ/WinRT) | ★★〜★★★ |
| Brightness（画面輝度） | ValueMap | IDisplay (WMI/DDC-CI) | ★★★ |

### Phase 3 — アプリ連携・ネットワーク
| アクション | パラメータ | ポート | 難度 |
|---|---|---|---|
| OBS（シーン切替/録画・配信/ソースミュート） | op, target | IObsClient (obs-websocket) | ★★ |
| Media（SMTC: アプリ非依存の再生制御） | op | ISmtcClient (WinRT) | ★★ |
| Http/Webhook | url, method, body, headers | IHttpSender | ★ |
| OSC 送信 | address, args | IOscSender | ★★ |
| MidiOut（MIDI→MIDI 変換/送出） | message, ValueMap | IMidiOut | ★★ |

### Phase 4 — 双方向・合成・拡張
| アクション | 内容 | 難度 |
|---|---|---|
| **LED フィードバック** | トグル状態/アクティブプロファイルをコントローラ LED に反映（MIDI 出力＋状態同期） | ★★★ |
| Macro（順次実行・遅延） | 複数アクションを順番に | ★★ |
| Toggle/State（押すたび A/B 交互） | 状態保持アクション | ★★ |
| Plugin SDK | IActionHandler を外部 DLL から登録 | ★★★ |

---

## 6. 安全性・権限

- **破壊的/外向き操作の確認**: RunCommand・Launch（実行ファイル）・Http は誤爆・悪用リスク。
  プロファイル import 時に「コマンド/起動を含む」旨を警告、初回実行時に確認、設定で無効化可能に。
- **緊急停止**: 既存の EmergencyStopHotkey を全アクションに効かせる（特に Hold/Macro/連続値）。
- **資格情報**: OBS/HTTP の認証情報は設定に平文保存しない方針（OS 資格情報ストア等）を別途検討。

---

## 7. 段階導入とマイルストーン対応

- **M-A 基盤刷新**: §3.1〜3.5（型付きパラメータ・ハンドラ registry・エディタ per-kind フォーム）。
  既存6アクションを新フォームへ移行（挙動・スキーマ後方互換を維持、テスト緑維持）。
- **M-B Phase 1 アクション群**を新基盤上に実装。
- **M-C 以降**: Phase 2 → 3 → 4 を順次。

各 M で「Core/Infra テスト緑 ＋ エディタ実機確認」をゲートにする（既存の進め方を踏襲）。

---

## 8. 決定事項（2026-06-22 合意）

- **Q1. エディタ UI 方式** → **案A: per-kind DataTemplate**（`DataTemplateSelector` でアクション種別ごとに切替）。
- **Q2. 基盤刷新の範囲** → **既存6アクションも全面移行**（挙動・スキーマ後方互換は維持）。
- **Q3. 未知アクション識別子** → **スキップ＋警告ログ**（該当アクションのみ無視、設定全体は読み込む）。
- **Q4. Phase 1 スコープ** → **アプリ/ファイル/URL 起動・システム音量（フェーダー）・ウィンドウ管理・
  メディアキー＋定型文**。
- **Q5. 安全方針** → **明示オプトイン**（RunCommand・任意実行ファイル起動は既定で無効。設定で ON、
  import 時に警告）。

## 9. 実装マイルストーン

- **M-A 基盤刷新** ✅ 完了: `ActionExecutor` をハンドラ registry 化（`IActionHandler`）、
  既存6アクションを per-kind DataTemplate へ全面移行。後方互換・テスト緑を維持。
- **M-B Phase 1** ✅ 完了: ウィンドウ管理 / メディアキー / 定型文 / 起動（Q5 オプトイン）/
  システム音量（value-driven, Core Audio）を新基盤上に実装。
  - 補足: SetVolume はフェーダー連動のため **トリガー=Absolute** で使う（ヘルプに明記）。
    Launch は **「外部起動を許可」ON** が必要（既定 OFF）。
- **M-C Phase 2** ✅ 完了:
  - UIA 要素操作（特定ウィンドウの特定ボタンを Invoke/Toggle/SetValue）＋ 要素ピッカー。
  - 仮想デスクトップ切替（Win+Ctrl+矢印）。
  - Windows 設定トグル（ダーク/ライト テーマ。レジストリ＋WM_SETTINGCHANGE）。
  - 画面輝度（value-driven, WMI。内蔵ディスプレイ対応）。
  - 補足: 夜間モード/集中モードは安定 API が無いため未実装（`ms-settings:` を Launch で開く運用を推奨）。
    外部モニタ輝度（DDC/CI）も未対応。
- **M-D Phase 3** ✅ 完了:
  - HTTP / Webhook（URL＋メソッド＋本文）✅ — Home Assistant / IFTTT / 自作API。実機 localhost で検証。
  - OSC 送信（UDP, host:port＋アドレス＋自動型付け引数）✅ — 照明/VJ/AV。実機 UDP で検証。
  - OBS（obs-websocket v5）✅ — シーン切替/録画・配信トグル/録画一時停止/ソースミュート等。
    接続設定(host/port/password)はメイン画面。SHA256認証は既知ベクトルで単体検証。
    ※実 OBS 接続は実機検証推奨（WebSocketプロトコルは仕様準拠で実装）。
  - MIDI 出力 ✅ — 出力デバイス(名前正規表現)へ Note/CC/PC を送信。CC は入力値連動も可
    （フェーダー→CC リマップ）。デバイス不在/不正正規表現は no-op で安全。
  - メディア(SMTC) — Phase 1 の MediaKey（再生/停止/次/前/ミュート/音量）で実質カバー済みのため見送り。
- **Phase 4**（進行中）:
  - マクロ ✅ — キー列を順次実行（ステップ間ディレイ ms）。
  - トグル状態 ✅ — 押すたびにキーA/B交互。**LEDフィードバック**として状態をコントローラLEDへ
    MIDI出力で反映（toggle に統合）。状態はプロセス内保持。
  - 補足: マクロ/トグルはキーチョード対象（任意アクションのネストは将来）。LEDフィードバックの
    プロファイル/緊急停止状態の常時反映は将来（コントローラ別LEDマップが必要）。
  - プラグイン SDK ✅ — `plugins/` フォルダの DLL から `IActionPlugin` を読み込み、`PluginAction`
    （プラグインID＋コマンド＋引数）でルーティング。分離 AssemblyLoadContext で読み込み、共有契約
    (Core)はホスト側を解決。永続化・既存アクションは無改変（§10）。

## 10. プラグイン SDK（作り方）

第三者が独自アクションを DLL で追加できる（docs §3.2 のハンドラ拡張をユーザ向けに開放）。

1. `MidiToEverything.Core` を参照する .NET 8 クラスライブラリを作成。
2. `IActionPlugin` を実装（public・引数なしコンストラクタ）:
   ```csharp
   using MidiToEverything.Core.Application.Ports;

   public sealed class HelloPlugin : IActionPlugin
   {
       public string Id => "hello";
       public void Execute(string command, string? arg)
       {
           // command/arg はバインディングのエディタで指定した値
       }
   }
   ```
3. ビルドした DLL を実行ファイル横の `plugins/` フォルダに置く（起動時に読み込み）。
4. プロファイル編集でアクション=`Plugin`、詳細にプラグインID（例 `hello`）、コマンド・引数を指定。

注意: プラグインはホストプロセス内で動作する（サンドボックス無し）。信頼できる DLL のみ配置すること。
プラグイン例外はホスト側で握りつぶし、入力パイプラインは継続する。
