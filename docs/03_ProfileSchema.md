# データ構造 / プロファイル設定スキーマ — MidiToEverything

関連: [01_PRD.md](01_PRD.md) / [02_Architecture.md](02_Architecture.md)

保存形式は **JSON**（`System.Text.Json`）。保存先は `%APPDATA%\MidiToEverything\`。
`settings.json`（全体設定）＋ `profiles\<id>.json`（プロファイル単位）に分割するか、単一 `config.json` にまとめるかは実装選択（本書は単一束ねの論理スキーマを示す）。

---

## 1. シグナル識別子（マッピングのキー）

1 つの物理操作子は次の組で一意化する。文字列キー例: `dev:akai-mpkmini|ch:1|cc:74`。

| フィールド | 型 | 説明 |
|------|----|----|
| `device` | string | デバイス識別子（名前ベース。`"*"` で任意デバイス） |
| `channel` | int (1–16) or `"any"` | MIDI チャンネル |
| `type` | enum | `noteOn` / `noteOff` / `note`（On/Off 両方）/ `cc` / `pitchBend` / `programChange` |
| `number` | int (0–127) | Note 番号 or CC 番号（pitchBend では省略） |

---

## 2. トリガー（入力値の解釈）

連続値（CC/PitchBend）や Note の発火条件を定義。

| フィールド | 型 | 既定 | 説明 |
|------|----|----|----|
| `mode` | enum | `trigger` | `trigger`(しきい値で発火) / `hold`(On〜Off で押し続け) / `absolute`(値→連続量) / `relative`(増減=エンコーダ) |
| `threshold` | int | 1 | `trigger`/`hold` の発火しきい値（velocity/value がこれ以上で ON） |
| `range` | [int,int] | [0,127] | `absolute` の入力値域 |
| `deadzone` | int | 0 | 中央/境界の不感帯 |
| `invert` | bool | false | 値の反転 |
| `scale` | number | 1.0 | 感度（出力量の倍率） |
| `relativeFormat` | enum | `twosComplement` | 相対エンコーダの符号方式（`twosComplement`/`signedBit`/`binaryOffset`）|

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
- `key.keys` は修飾キー（`ctrl`/`shift`/`alt`/`win`）＋通常キーの配列。送信はスキャンコード既定。
- `hold:true` は Trigger.mode=`hold` と組み合わせ、Note On で押下／Note Off で解放。
- `useInputValue:true` は連続値（CC 等）を移動量/スクロール量へ写像（`scale`/`deadzone` 適用後）。

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

> ルール: 上位レイヤ（ピン留め > コンテキスト一致 > 基本）で最初に見つかった定義を採用。`none` はフォールバックを止める。詳細は [02_Architecture.md](02_Architecture.md) §3.2。

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
