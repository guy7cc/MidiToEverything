# データ構造 / ルール設定スキーマ — MidiToEverything

関連: [01_PRD.md](01_PRD.md) / [02_Architecture.md](02_Architecture.md)

保存形式は **JSON**（`System.Text.Json`）。保存先は `%APPDATA%\MidiToEverything\`。
`settings.json`（全体設定）＋ `profiles\<id>.json`（ルール単位）に分割するか、単一 `config.json` にまとめるかは実装選択（本書は単一束ねの論理スキーマを示す）。

---

## 1. シグナル識別子（マッピングのキー）

1 つの物理操作子は次の組で一意化する。文字列キー例: `dev:akai-mpkmini|ch:1|cc:74`。

| フィールド | 型 | 説明 |
|------|----|----|
| `device` | string | デバイス判別の**正規表現**（大文字小文字を無視・部分一致）。`"*"` または空で任意デバイス。例: `"^MPK"` |
| `channel` | int (1–16) or `"any"` | MIDI チャンネル |
| `type` | enum | `noteOn` / `noteOff` / `note`（On/Off 両方）/ `cc` / `pitchBend` / `programChange` |
| `number` | int (0–127) | Note 番号 or CC 番号（pitchBend では省略） |

---

## 2. トリガー（入力値の解釈）

連続値（CC/PitchBend）や Note の発火条件を定義。

| フィールド | 型 | 既定 | 説明 |
|------|----|----|----|
| `mode` | enum | `trigger` | `trigger`(しきい値で発火) / `hold`(On〜Off で押し続け) / `absolute`(値→連続量) / `relative`(増減として扱う) |
| `threshold` | int | 1 | `trigger`/`hold` の発火しきい値（velocity/value がこれ以上で ON） |
| `range` | [int,int] | [0,127] | `absolute` の入力値域（=有効ウィンドウ。0..1 へ正規化される） |
| `outOfRange` | enum | `clamp` | `absolute` でウィンドウ外の値の扱い。`clamp`=端に丸めて発火し続ける（従来）／`gate`=**ウィンドウ内に入った時だけ発火**、外は発火しない |
| `deadzone` | int | 0 | 中央/境界の不感帯 |
| `invert` | bool | false | 値の反転 |
| `scale` | number | 1.0 | 感度（出力量の倍率） |
| `relativeFormat` | enum | `twosComplement` | `relative` の増減の読み取り方。エンコーダ符号方式（`twosComplement`/`signedBit`/`binaryOffset`）または `absoluteDelta`（絶対値の前回との差分を増減として扱う＝絶対値デバイスを相対化）|
| `relativeOutput` | enum | `amount` | `relative` の発火の向き。`amount`(増減量をアクション量として送る) / `fireOnIncrease`(増加=エンコーダなら右回り) / `fireOnDecrease`(減少=左回り) / `fireOnEither`(どちらでも発火)。fire 系は発火トリガー。エディタのラベルは `relativeFormat` に応じて回転方向（エンコーダ）か値の増減（`absoluteDelta`）で表示される |
| `edge` | bool | false | `trigger`/`absolute`(gate) のエッジ発火。**ゾーンに入った瞬間に1回だけ**発火し、出てから再度入るまで再発火しない。CC/フェーダをボタンのように使う場合に有効（`hold`/`relative` では無視） |
| `wrap` | bool | false | `relative` + `absoluteDelta` 専用。値が一周する無限ノブ（…126,127,0,1…）で `127→0` を `+1` と解釈。境界のあるフェーダでは off（大きなスイープを誤検出しないため）|

> `gate` の例: フェーダが特定ゾーン（例 値 100〜127）に来た時だけアクションを発火させたい場合、`{ "mode": "absolute", "range": [100, 127], "outOfRange": "gate", ... }`。ウィンドウ内では値が 0..1 に正規化され（離散アクションは在圏中に発火、連続アクションはウィンドウ内で 0..1 に写像）、ウィンドウ外では何も起きない。
>
> 注: `gate` 単体では在圏中に CC が届くたび再発火する（しきい値 `trigger` モードと同じ挙動）。「入った瞬間に1回だけ」発火したい場合は `"edge": true` を併用する。

---

## 3. アクション

`type` による discriminated union。`actions` 配列にすると順次実行（マクロ, FR-4.6）。

```jsonc
// キーストローク（ホールド対応）
{ "type": "key", "keys": ["ctrl", "z"], "hold": false, "repeat": false }

// マウスクリック
{ "type": "mouseClick", "button": "left", "double": false }

// カーソル移動（相対 / 絶対）
{ "type": "cursorMove", "mode": "relative", "dx": 0, "dy": 0, "useInputValue": true }

// スクロール
{ "type": "scroll", "axis": "vertical", "amount": 120, "useInputValue": true }

// ルール切替
{ "type": "switchProfile", "target": "next" }   // "next" | "prev" | "toggle" | ルールIdの文字列

// 何もしない（no-op）。合算モードでは他ルールを遮断しない（旧「ブロック」は廃止）
{ "type": "none" }
```

アクション共通補足:
- `key.keys` は修飾キー（`ctrl`/`shift`/`alt`/`win`、左右別の `lctrl`/`rctrl`/`lshift`/`rshift`/`lalt`/`ralt`/`altgr` も可）＋通常キーの配列。送信はスキャンコード既定。
  - 通常キーは標準キーボードを概ね網羅: 英数字 `a`–`z` / `0`–`9`、ファンクション `f1`–`f24`、移動・編集 `enter`/`tab`/`esc`/`space`/`backspace`/`delete`/`insert`/`home`/`end`/`pageup`/`pagedown`/`up`/`down`/`left`/`right`、ロック `capslock`/`numlock`/`scrolllock`、`printscreen`/`pause`/`apps`(コンテキストメニュー)。
  - 記号キーは記号そのもの（`-` `=` `[` `]` `\` `;` `'` `.` `/` `` ` ``）か語名（`minus`/`equals`/`plus`/`semicolon`/`backslash`/`period`/`slash`/`backtick` 等）。`,`（カンマ）と空白は区切り文字なので語名 `comma`/`space` を使う。記号はスキャンコード送信のため、実際の文字は送信先のキーボード配列に従う。
  - テンキー: `numpad0`–`numpad9`、`add`/`subtract`/`multiply`/`divide`/`decimal`、`numpadenter`（数字・小数点は NumLock 有効時のみ数字を生成）。
  - 上記キー名に無い**1文字**（`^` `~` `!` `@` や非ASCII 等）は、その文字を Unicode で直接送信する（配列非依存）。ただし Unicode 送信は修飾キーと組み合わせられない（`ctrl+^` のような組合せは不可）。修飾キー付きや純粋なテキスト入力には `typeText` を使う。
- `hold:true` は Trigger.mode=`hold` と組み合わせ、Note On で押下／Note Off で解放。
- `useInputValue:true` は連続値（CC 等）を移動量/スクロール量へ写像（`scale`/`deadzone` 適用後）。

`relative` で「ノブの増減を量として送る」例と「増えたら発火する」例:
```jsonc
// 増減をアクション量として送る（スクロール量に直結）
{ "signal": { "type": "cc", "number": 74 },
  "trigger": { "mode": "relative", "relativeFormat": "twosComplement", "relativeOutput": "amount" },
  "actions": [ { "type": "scroll", "useInputValue": true } ] }

// 絶対値デバイスでも、増えたときだけキーを発火（絶対値の差分を相対化）
{ "signal": { "type": "cc", "number": 74 },
  "trigger": { "mode": "relative", "relativeFormat": "absoluteDelta", "relativeOutput": "fireOnIncrease" },
  "actions": [ { "type": "key", "keys": ["ctrl", "plus"] } ] }
```
> 増加→A・減少→B のように1ノブで2アクションへ分けたい場合は、`fireOnIncrease` の binding と `fireOnDecrease` の binding を別々に作る（旧 action 単位の `direction` は廃止し、トリガー側の `relativeOutput` に統一）。

---

## 4. ルール

| フィールド | 型 | 説明 |
|------|----|----|
| `id` | string | 一意 ID（kebab-case） |
| `name` | string | 表示名 |
| `enabled` | bool | 有効/無効 |
| `match` | object | コンテキスト自動切替条件（基本ルールでは無し） |
| `match.pattern` | string(regex) | **単一の正規表現**。`"<プロセス名>\n<ウィンドウタイトル>"` の2行文字列に対し**複数行モード**で評価。例: `^chrome\.exe$`（プロセス名行に一致）/ `Google Chrome$`（タイトル行に一致）/ 両方を `(?:…)|(?:…)` でOR結合。空文字は不一致。不正な正規表現は例外を投げず不一致扱い |
| `match.priority` | int | 複数一致時の優先度（大きいほど優先） |
| `bindings` | Binding[] | シグナル→アクションの対応 |

> プロセス名とタイトル判別を1つの編集可能な正規表現に統合（schema v2）。エディタの「候補を追加」は、選択/入力したプロセスを表す節 `^name$` を既存パターンへ OR 結合し、失敗時（空入力・手編集で再構成不能）はユーザに通知する。v1 の `processNames`/`titlePattern` はロード時に `pattern` へ自動移行。

`Binding`:
```jsonc
{
  "signal": { "device": "*", "channel": "any", "type": "cc", "number": 74 },
  "trigger": { "mode": "relative", "scale": 1.0 },
  "actions": [ { "type": "scroll", "axis": "vertical", "useInputValue": true } ],
  "label": "ズーム",
  "enabled": true
}
```

---

## 5. ルート設定ファイル例（`config.json`）

```jsonc
{
  "version": 2,                         // スキーマバージョン（マイグレーション用 FR-7.5）
  "settings": {
    // 起動・常駐
    "startWithWindows": false,          // ログオン時自動起動 FR-7.6
    "startMinimized": false,            // トレイに最小化した状態で起動
    "closeToTray": true,                // 閉じるボタンでトレイへ（false=終了）
    "startEmissionEnabled": true,       // 起動時に発行（セーフティゲート）を有効化
    "emergencyStopHotkey": "ctrl+alt+pause", // 緊急停止ホットキー（設定で変更可）
    // 入力・連携
    "allowExternalLaunch": false,       // launch/command 系アクションの許可
    "language": "ja",                   // UI 言語（ja/en …）docs/07
    "autoDetectDevices": true,          // MIDI を自動ポーリング検出（false=手動）
    "watchedDevices": ["*"],            // 監視対象（"*"=全デバイス）
    "obsHost": "localhost",             // obs-websocket 接続
    "obsPort": 4455,
    "obsPassword": "",
    // アップデート（自動チェックは公式ビルドのみ）
    "autoUpdate": true,                 // GitHub Releases を起動時/定期に確認
    "updateChannel": "stable",          // stable / prerelease
    "updateCheckHours": 24,             // 自動確認の間隔（時間）
    // 通知・外観
    "trayNotifications": true,          // トレイ通知（ルール切替・更新あり）
    "theme": "dark",                    // dark / light（即時反映）
    "accentColor": "blue",              // blue / green / purple / orange
    "uiScale": 1.0,                     // UI 拡大率（1.0=100%）
    // ログ・診断
    "logLevel": "Debug",                // Serilog 最小レベル（即時反映）
    "logRetentionDays": 7,              // 日次ログの保持数
    "crashAutoRestart": true,           // 未処理例外で自動再起動
    "monitor": { "maxLogLines": 500, "uiThrottleMs": 30 } // 入力モニターのUI調整
  },

  "activeContext": {                    // ランタイム状態（参考。保存は任意）
    "pinnedProfileId": null,            // 手動ピン留め中なら自動切替を無効化 FR-5.5
    "currentProfileId": "clip-studio"
  },

  // ── 基本（グローバル）ルール：常時最下層で適用 FR-6.1 ──
  "baseProfile": {
    "id": "base",
    "name": "基本ルール",
    "enabled": true,
    "bindings": [
      {
        "signal": { "device": "*", "channel": "any", "type": "noteOn", "number": 36 },
        "trigger": { "mode": "trigger", "threshold": 1 },
        "actions": [ { "type": "key", "keys": ["ctrl", "z"] } ],
        "label": "元に戻す"
      },
      {
        "signal": { "device": "*", "channel": "any", "type": "noteOn", "number": 37 },
        "trigger": { "mode": "trigger", "threshold": 1 },
        "actions": [ { "type": "key", "keys": ["ctrl", "c"] } ],
        "label": "コピー"
      },
      {
        "signal": { "device": "*", "channel": "any", "type": "noteOn", "number": 51 },
        "trigger": { "mode": "trigger", "threshold": 1 },
        "actions": [ { "type": "switchProfile", "target": "next" } ],
        "label": "ルール切替(次)"      // 手動切替を MIDI に割当 FR-5.4
      }
    ]
  },

  // ── 個別ルール群 ──
  "profiles": [
    {
      "id": "clip-studio",
      "name": "Clip Studio Paint",
      "enabled": true,
      "match": {
        "pattern": "^CLIPStudioPaint\\.exe$",
        "priority": 10
      },
      "bindings": [
        {
          // ノブ(CC74) でブラシサイズ：[ と ] 相当を相対で（例ではキー連打に写像）
          "signal": { "device": "*", "channel": "any", "type": "cc", "number": 74 },
          "trigger": { "mode": "absolute", "range": [0,127], "scale": 1.0 },
          "actions": [ { "type": "scroll", "axis": "vertical", "useInputValue": true } ],
          "label": "ブラシサイズ"
        },
        {
          // フェーダー(CC7) を絶対値で不透明度などへ
          "signal": { "device": "*", "channel": "any", "type": "cc", "number": 7 },
          "trigger": { "mode": "absolute", "range": [0,127] },
          "actions": [ { "type": "cursorMove", "mode": "absolute", "useInputValue": true } ],
          "label": "不透明度スライダ"
        },
        {
          // ホールド型：パッド押下中だけ Space（手のひらツール）
          "signal": { "device": "*", "channel": "any", "type": "note", "number": 40 },
          "trigger": { "mode": "hold", "threshold": 1 },
          "actions": [ { "type": "key", "keys": ["space"], "hold": true } ],
          "label": "手のひらツール(押下中)"
        },
        {
          // no-op の例（合算モードでは基本のコピーは遮断されず発火する点に注意）
          "signal": { "device": "*", "channel": "any", "type": "noteOn", "number": 37 },
          "trigger": { "mode": "trigger" },
          "actions": [ { "type": "none" } ],
          "label": "（何もしない）"
        }
      ]
    },

    {
      "id": "obs",
      "name": "OBS Studio",
      "enabled": true,
      "match": { "pattern": "^obs64\\.exe$", "priority": 5 },
      "bindings": [
        {
          "signal": { "device": "*", "channel": "any", "type": "noteOn", "number": 36 },
          "trigger": { "mode": "trigger" },
          // OBS では Note36 にシーン切替を割り当て。合算のため基本の Undo と「両方」発火する
          "actions": [ { "type": "key", "keys": ["ctrl", "shift", "1"] } ],
          "label": "シーン1へ"
        }
      ]
    }
  ]
}
```

---

## 6. 解決（合算モデルでの挙動）

前面アプリにマッチした**全ルール＋基本ルール**を常に同時評価し、各ルールのバインディングを**合算して発火**する（優先度による上書きは無い）。この設定（基本＝Note36:元に戻す / Note37:コピー、clip-studio＝Note37:`none`、obs＝Note36:シーン1）での例:

| 前面アプリ | Note36 を叩く | Note37 を叩く |
|------|--------------|---------------|
| デスクトップ/ブラウザ（アプリ別ルール未一致） | 基本: **元に戻す(Ctrl+Z)** | 基本: **コピー(Ctrl+C)** |
| Clip Studio Paint（基本＋CSP が有効） | 基本: **元に戻す** | 基本: **コピー**（CSP の `none` は不活性＝遮断しない） |
| OBS（基本＋OBS が有効） | 基本: **元に戻す** ＋ OBS: **シーン1へ(Ctrl+Shift+1)**（**両方発火**） | 基本: **コピー** |

> ルール: マッチした全ルール（基本含む）のバインディングが合算で発火する。**上書きは無い**ため、同一シグナルを複数ルールが持てば全て発火する。アプリ別に挙動を変えたいときは、各ルールの正規表現で**重ならないようにスコープ**を切る（例: 基本側の regex で対象アプリを除外）。`none` は「何もしない」no-op で、他ルールを遮断しない。詳細は [02_Architecture.md](02_Architecture.md) §3.2。
>
> ルール内では特異度が効く: より具体的なバインディングが同一ルール内の曖昧なものを上書きし、同じ具体度のバインディングが複数あれば全て発火する（例: 相対値ノブの `fireOnIncrease`/`fireOnDecrease` を別々に作れば増加で前者・減少で後者）。手動の強制 ON/OFF（ピン/トグル）で regex に関係なくルールを有効/無効化できる。

---

## 7. C# 型（参考スケッチ）

```csharp
public enum MessageType { NoteOn, NoteOff, Note, Cc, PitchBend, ProgramChange }
public enum TriggerMode { Trigger, Hold, Absolute, Relative }

public sealed record Signal(string Device, string Channel, MessageType Type, int? Number)
{
    public string Key => $"dev:{Device}|ch:{Channel}|{Type}:{Number}";
}

public sealed record Trigger(TriggerMode Mode = TriggerMode.Trigger, int Threshold = 1,
    int[]? Range = null, int Deadzone = 0, bool Invert = false, double Scale = 1.0,
    RelativeFormat RelativeFormat = RelativeFormat.TwosComplement,
    RelativeOutput RelativeOutput = RelativeOutput.Amount,
    OutOfRangeBehavior OutOfRange = OutOfRangeBehavior.Clamp,
    bool Edge = false, bool Wrap = false);

// アクションは type 判別子の union。下記は判別子の一覧（フィールドの詳細は §3 と
// docs/05_ActionExpansion.md のカタログを参照。実体は src/Core/Persistence/ConfigDto.cs の ActionDto）。
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(KeyAction), "key")]
[JsonDerivedType(typeof(MouseClickAction), "mouseClick")]
[JsonDerivedType(typeof(CursorMoveAction), "cursorMove")]
[JsonDerivedType(typeof(ScrollAction), "scroll")]
[JsonDerivedType(typeof(SwitchProfileAction), "switchProfile")]
[JsonDerivedType(typeof(WindowControlAction), "windowControl")]
[JsonDerivedType(typeof(MediaKeyAction), "mediaKey")]
[JsonDerivedType(typeof(TypeTextAction), "typeText")]
[JsonDerivedType(typeof(LaunchAction), "launch")]
[JsonDerivedType(typeof(SetVolumeAction), "setVolume")]
[JsonDerivedType(typeof(UiaAction), "uia")]
[JsonDerivedType(typeof(VirtualDesktopAction), "virtualDesktop")]
[JsonDerivedType(typeof(WindowsToggleAction), "windowsToggle")]
[JsonDerivedType(typeof(BrightnessAction), "brightness")]
[JsonDerivedType(typeof(HttpAction), "http")]
[JsonDerivedType(typeof(OscAction), "osc")]
[JsonDerivedType(typeof(ObsAction), "obs")]
[JsonDerivedType(typeof(MidiOutAction), "midiOut")]
[JsonDerivedType(typeof(MacroAction), "macro")]
[JsonDerivedType(typeof(ToggleAction), "toggle")]
[JsonDerivedType(typeof(PluginAction), "plugin")]
[JsonDerivedType(typeof(NoneAction), "none")]
public abstract record GameAction;     // System.Text.Json のポリモーフィズムで union を表現

public sealed record Binding(Signal Signal, Trigger Trigger, GameAction[] Actions,
    string? Label = null, bool Enabled = true);

public sealed record Profile(string Id, string Name, bool Enabled,
    MatchRule? Match, IReadOnlyList<Binding> Bindings);
```
