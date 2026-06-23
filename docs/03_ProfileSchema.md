# データ構造 / プロファイル設定スキーマ — MidiToEverything

関連: [01_PRD.md](01_PRD.md) / [02_Architecture.md](02_Architecture.md)

保存形式は **JSON**（`System.Text.Json`）。保存先は `%APPDATA%\MidiToEverything\`。
`settings.json`（全体設定）＋ `profiles\<id>.json`（プロファイル単位）に分割するか、単一 `config.json` にまとめるかは実装選択（本書は単一束ねの論理スキーマを示す）。

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

// プロファイル切替
{ "type": "switchProfile", "target": "next" }   // "next" | "prev" | "toggle" | プロファイルIdの文字列

// 何もしない＝基本プロファイルの割当を明示ブロック（FR-6.4）
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

## 4. プロファイル

| フィールド | 型 | 説明 |
|------|----|----|
| `id` | string | 一意 ID（kebab-case） |
| `name` | string | 表示名 |
| `enabled` | bool | 有効/無効 |
| `match` | object | コンテキスト自動切替条件（基本プロファイルでは無し） |
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
    "startWithWindows": false,          // ログオン時自動起動 FR-7.6
    "emergencyStopHotkey": "ctrl+alt+pause",
    "watchedDevices": ["*"],            // 監視対象（"*"=全デバイス）
    "monitor": { "maxLogLines": 500, "uiThrottleMs": 30 }
  },

  "activeContext": {                    // ランタイム状態（参考。保存は任意）
    "pinnedProfileId": null,            // 手動ピン留め中なら自動切替を無効化 FR-5.5
    "currentProfileId": "clip-studio"
  },

  // ── 基本（グローバル）プロファイル：常時最下層で適用 FR-6.1 ──
  "baseProfile": {
    "id": "base",
    "name": "基本プロファイル",
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
        "label": "プロファイル切替(次)"      // 手動切替を MIDI に割当 FR-5.4
      }
    ]
  },

  // ── 個別プロファイル群 ──
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
          // 基本プロファイルの「コピー(Note36...いや37)」を CSP では無効化する例（ブロック）FR-6.4
          "signal": { "device": "*", "channel": "any", "type": "noteOn", "number": 37 },
          "trigger": { "mode": "trigger" },
          "actions": [ { "type": "none" } ],
          "label": "（基本のコピーを無効化）"
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
          // 基本の Note36(Undo) を OBS ではシーン切替に上書き FR-6.2
          "actions": [ { "type": "key", "keys": ["ctrl", "shift", "1"] } ],
          "label": "シーン1へ"
        }
      ]
    }
  ]
}
```

---

## 6. 競合解決の具体例（この設定での挙動）

| 状況 | Note36 を叩く | Note37 を叩く |
|------|--------------|---------------|
| デスクトップ/ブラウザ（個別未一致） | 基本: **元に戻す(Ctrl+Z)** | 基本: **コピー(Ctrl+C)** |
| Clip Studio Paint 前面 | 個別に定義なし→基本へフォールバック: **元に戻す** | 個別が `none` で**明示ブロック→何もしない** |
| OBS 前面 | 個別が上書き: **シーン1へ(Ctrl+Shift+1)** | 個別に定義なし→基本: **コピー** |

> ルール: 上位レイヤ（ピン留め > コンテキスト一致 > 基本）で最初に定義が見つかったレイヤを採用。`none` はフォールバックを止める。詳細は [02_Architecture.md](02_Architecture.md) §3.2。
>
> 採用レイヤ内で**同一シグナルに同じ具体度のバインディングが複数ある場合は、それら全てが発火する**（より具体的なバインディングは曖昧なものを引き続き上書きする）。これにより1つのコントロールで複数アクションを駆動できる。例えば相対値ノブで `fireOnIncrease` の binding と `fireOnDecrease` の binding を別々に作れば、増加で前者・減少で後者がそれぞれ発火する。

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
    int[]? Range = null, int Deadzone = 0, bool Invert = false, double Scale = 1.0);

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(KeyAction), "key")]
[JsonDerivedType(typeof(MouseClickAction), "mouseClick")]
[JsonDerivedType(typeof(CursorMoveAction), "cursorMove")]
[JsonDerivedType(typeof(ScrollAction), "scroll")]
[JsonDerivedType(typeof(SwitchProfileAction), "switchProfile")]
[JsonDerivedType(typeof(NoneAction), "none")]
public abstract record GameAction;     // System.Text.Json のポリモーフィズムで union を表現

public sealed record Binding(Signal Signal, Trigger Trigger, GameAction[] Actions,
    string? Label = null, bool Enabled = true);

public sealed record Profile(string Id, string Name, bool Enabled,
    MatchRule? Match, IReadOnlyList<Binding> Bindings);
```
