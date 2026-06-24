---
name: test-actions
description: Action acceptance runbook for MidiToEverything (WPF MIDI→action mapper). Use to verify every action type works end-to-end before a release — injects MIDI via a loopMIDI virtual port + the midisend tool, auto-asserts OS side-effects where possible, and prints clear 👤 steps for app launch / UIA / OBS / visual checks the user must confirm. Covers various target-app shapes.
---

# MidiToEverything — 全アクション受け入れテスト手順書

MidiToEverything は **MIDI 入力 → OS 操作** に変換する常駐アプリ。このスキルは「**MIDI を注入 → 入力モニターで発火を確認 → OS 側の効果を確認**」を全アクションに対して行い、リリース前に**期待動作を安定して証明**するための手順書。

凡例: **🤖 自動**（このスキルがコマンドで実行・判定）／ **👤 ユーザー確認**（あなたに指示を出すので、画面を見て確認・操作）。

> 進め方の原則: まず §1 準備（一度だけ）。以降は §3 のアクションを上から順に。各アクションは
> **(a) 入力モニターに発火が出るか**（全アクション共通の証拠）＋ **(b) OS 側の効果**（自動 or 目視）で判定する。
> MIDI 信号は重複しない Note/CC に割り当て済み（[test-config.json](test-config.json)）。

---

## 1. 事前準備（初回のみ）

### 1.1 仮想 MIDI ループバック 〔👤 必須〕
注入を自動化するには、アプリの「入力」として見える仮想ポートが要る（物理デバイスの出力はアプリ入力に戻らないため）。

1. [loopMIDI](https://www.tobias-erichsen.de/software/loopmidi.html) をインストール。
2. ポートを **2 つ** 作成（名前は厳密に）:
   - `MIDIToEverything-In` … テスト信号の**注入用**（アプリがこれを入力として認識する）
   - `MIDIToEverything-Out` … **midiOut アクションの検証用**ループバック
3. 作成後、確認（🤖）:
   ```
   tools/MidiTestSender/bin/Release/net8.0-windows/midisend.exe list
   ```
   出力ポート/入力ポートの両方に上記2つが出れば OK。出なければ loopMIDI で作り直す。

> 実機（例: MPD218）を持っている場合、§3 は実機のパッド/ノブを叩いて**手動**でも実施できる
> （その場合 MIDI 注入コマンドは飛ばし、入力モニターで発火を見る）。自動化したいなら loopMIDI を使う。

### 1.2 ビルド 〔🤖〕
```
dotnet build MidiToEverything.sln -c Release -nologo -clp:ErrorsOnly
dotnet build tools/MidiTestSender/MidiTestSender.csproj -c Release -nologo -clp:ErrorsOnly
```

### 1.3 テスト設定の投入（バックアップ必須）〔🤖〕
ユーザーの本番設定を壊さないため、**必ず退避してから差し替え、テスト後に戻す**。
```bash
cfg="$APPDATA/MidiToEverything/config.json"   # = %APPDATA%\MidiToEverything\config.json
cp "$cfg" "$cfg.acceptance-bak" 2>/dev/null || true
cp ".claude/skills/test-actions/test-config.json" "$cfg"
```
（テスト終了後に必ず: `mv "$cfg.acceptance-bak" "$cfg"` で復元。）

### 1.4 アプリ起動 〔🤖〕
多重起動防止が有効なので既存インスタンスを止めてから起動:
```
Get-Process MidiToEverything -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Process "src\App\bin\Release\net8.0-windows\MidiToEverything.exe"
```
起動後、ウィンドウの **入力モニター**を見えるようにしておく（👤）。タイトルバー横に「ルール: 受け入れテスト(基本ルール)」が出ていればテスト設定がロードされている。

### 1.5 ターゲットアプリ 〔👤〕
アクションは色々な形態のアプリに作用する。最低限:
- **メモ帳**（`notepad.exe`）… key / typeText / macro / toggle の入力先。起動して前面にしておく。
- **電卓**（`calc.exe`）… uia の対象（任意）。
- **OBS Studio**（obs-websocket 有効）… obs アクション（任意）。
- **メディアプレーヤー**（任意）… mediaKey 確認用。
- §3.A の http/osc/midiOut 用ローカル受信は各手順内で起動する。

---

## 2. MIDI 注入の使い方 〔🤖〕
`midisend <ポート名> <イベント> [...]`（イベント間は自動で 120ms 空ける）:
- `note:1:60:100:200` … ch1 Note60 を velocity100 で押して 200ms 後に離す（trigger 系はこれ）
- `noteon:1:60:100` / `noteoff:1:60`
- `cc:1:20:127` … ch1 CC20=127（absolute/relative 系）
- `monitor:<入力ポート>:<ms>` … 受信イベントを表示（midiOut 検証）

エイリアス（以降 `SEND` と書く）:
```
SEND = tools/MidiTestSender/bin/Release/net8.0-windows/midisend.exe "MIDIToEverything-In"
```

---

## 3. アクション別テスト

各アクションは **入力モニターに発火が表示される**ことを必ず確認（👤 目視 or スクリーンショット）。それに加えて以下の OS 効果を判定する。

### 3.A 完全自動で判定できるもの 〔🤖〕

| # | アクション | 注入 | 自動アサーション |
|---|---|---|---|
| 1 | **launch** (Note67) | `SEND note:1:67:100:150` | 直後に `Get-Process notepad` が存在すればOK |
| 2 | **http** (Note72) | 下記の手順 | ローカル HttpListener にリクエストが届けばOK |
| 3 | **osc** (Note73) | 下記の手順 | ローカル UDP:9000 に受信があればOK |
| 4 | **midiOut** (Note75) | 下記の手順 | `midisend monitor "MIDIToEverything-Out" 3000` が CC を受信すればOK |
| 5 | **macro** (Note76) | メモ帳に文字がある状態で `SEND note:1:76:100:150` | クリップボードにメモ帳本文が入ればOK（全選択→コピー） |
| 6 | **cursorMove** (Note63) | `SEND note:1:63:100:150` | `GetCursorPos` が送信前後で +120x 動けばOK |
| 7 | **windowsToggle** (Note70) | `SEND note:1:70:100:150` | レジストリ `AppsUseLightTheme` が反転すればOK |

**http の自動判定例**（PowerShell, バックグラウンドで受信→送信→確認）:
```powershell
$job = Start-Job { $l=[System.Net.HttpListener]::new(); $l.Prefixes.Add("http://127.0.0.1:8973/"); $l.Start(); $c=$l.GetContext(); $c.Response.Close(); "HIT "+$c.Request.Url.AbsolutePath }
Start-Sleep 1; & $SEND note:1:72:100:150; Start-Sleep 1
Receive-Job $job -Wait -ErrorAction SilentlyContinue   # "HIT /midi-test" が出ればOK
Remove-Job $job -Force
```

**osc の自動判定例**:
```powershell
$job = Start-Job { $u=[System.Net.Sockets.UdpClient]::new(9000); $ep=[System.Net.IPEndPoint]::new([System.Net.IPAddress]::Any,0); $b=$u.Receive([ref]$ep); "OSC "+$b.Length+" bytes" }
Start-Sleep 1; & $SEND note:1:73:100:150; Start-Sleep 1
Receive-Job $job -Wait; Remove-Job $job -Force   # "OSC N bytes" が出ればOK
```

**midiOut の自動判定例**:
```powershell
$mon = Start-Process $midisend -ArgumentList 'monitor','MIDIToEverything-Out','3000' -PassThru -NoNewWindow
Start-Sleep 1; & $SEND note:1:75:100:150
$mon.WaitForExit(); "exit=$($mon.ExitCode)"   # exit=0（1件以上受信）ならOK
```
（midiOut 設定の送信先は `MIDIToEverything-Out`。アプリの設定/デバイス側でこのポートが出力先として見えている必要がある。）

**macro の自動判定例**:
```powershell
# メモ帳を前面にして既知の文字列を入れておく → 全選択+コピー → クリップボード確認
& $SEND note:1:61:100:150          # まず typeText で "MidiToEverything OK " を入れる
Start-Sleep 1; & $SEND note:1:76:100:150
Start-Sleep 1; Get-Clipboard       # "MidiToEverything OK" を含めばOK
```

**cursorMove**:
```powershell
Add-Type @"
using System;using System.Runtime.InteropServices;public class C{ [DllImport("user32.dll")]public static extern bool GetCursorPos(out P p); public struct P{public int x;public int y;} }
"@
$a=New-Object C+P; [C]::GetCursorPos([ref]$a); & $SEND note:1:63:100:150; Start-Sleep 1
$b=New-Object C+P; [C]::GetCursorPos([ref]$b); "dx=$($b.x-$a.x)"   # +120 付近ならOK
```

**windowsToggle (ダークモード)**:
```powershell
$k='HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\Personalize'
$before=(Get-ItemProperty $k).AppsUseLightTheme; & $SEND note:1:70:100:150; Start-Sleep 1
$after=(Get-ItemProperty $k).AppsUseLightTheme; "before=$before after=$after"   # 値が反転すればOK
```

### 3.B 入力モニターで発火を見るもの 〔👤（モニター目視）〕
OS 効果の自動判定が難しいものは、**入力モニターに正しいアクションが出るか**を主証拠とする。各信号を送り、モニターに該当ラベルが出ることを確認（必要なら前面をメモ帳にして効果も目視）。

| アクション | 注入 | 👤 確認 |
|---|---|---|
| **key** (Note60) | `SEND note:1:60:100:150` | メモ帳前面で `a` が入力される／モニターに「key」 |
| **typeText** (Note61) | `SEND note:1:61:100:150` | メモ帳に「MidiToEverything OK」が入る |
| **mouseClick** (Note62) | `SEND note:1:62:100:150` | カーソル位置で左クリックされる／モニターに「mouseClick」 |
| **scroll** (Note64) | `SEND note:1:64:100:150` | 前面の縦スクロールが動く／モニターに「scroll」 |
| **mediaKey** (Note65) | `SEND note:1:65:100:150` | メディアプレーヤーが再生/一時停止／モニターに「mediaKey」 |
| **toggle** (Note77 ×2) | `SEND note:1:77:100:150` を2回 | メモ帳に `1` → 次に `2` と交互に入る |
| **relative ノブ** (CC30) | `SEND cc:1:30:1`（増）/ `cc:1:30:127`（減） | 増減方向にスクロール／モニターに発火 |
| **none** (Note80) | `SEND note:1:80:100:150` | **モニターに何も出ない**（発火しないのが正しい） |

### 3.C アプリ依存・視覚確認が要るもの 〔👤〕

| アクション | 注入 | 👤 確認・準備 |
|---|---|---|
| **setVolume** (CC20) | `SEND cc:1:20:0` → `cc:1:20:127` | 音量 OSD が 0%→100% に動く（マスター音量） |
| **windowControl** (Note66) | 任意ウィンドウを前面に → `SEND note:1:66:100:150` | 前面ウィンドウが最小化される |
| **uia** (Note68) | 電卓を起動・前面 → `SEND note:1:68:100:150` | 電卓の「5」が押される。**要調整**: test-config の `windowPattern`/`elementName` を対象アプリに合わせる |
| **virtualDesktop** (Note69) | `SEND note:1:69:100:150` | 仮想デスクトップが隣へ切り替わる |
| **brightness** (Note71) | `SEND note:1:71:100:150` | 画面の明るさが変化（対応ディスプレイのみ） |
| **obs** (Note74) | OBS 起動＋obs-websocket 有効、設定でホスト/ポート/パスワード設定 → `SEND note:1:74:100:150` | OBS のシーンが切り替わる（`arg` のシーン名を実在のものに調整） |
| **plugin** (Note78) | `plugins/` に対象プラグインを配置 → `SEND note:1:78:100:150` | プラグインの想定動作。未配置ならスキップ可 |

---

## 4. ルール（合算・マッチ・手動 ON/OFF）の確認 〔👤＋🤖〕

1. **合算（union）**: **メモ帳を前面**にして `SEND note:1:90:100:150`。
   - 期待: メモ帳に **`[BASE][RULE]`** の両方が入る（基本ルールとメモ帳ルールが**両方発火**）。
   - メモ帳以外を前面にして同じ送信 → **`[BASE]` のみ**（メモ帳ルールの regex が不一致）。
   - → 「全マッチが合算」「regex で有効化」が確認できる。
2. **手動トグル**: メモ帳前面で `SEND note:1:79:100:150`（switchProfile=toggle）。
   - 期待: 「ルール: …」表示からメモ帳ルールが消える（強制 OFF）。もう一度送ると戻る。

---

## 5. 「色々な形態のアプリ」への対応

アクションは作用先アプリの形態に依存する。対象に合わせて調整する:

- **キー入力系**（key/typeText/macro/toggle/mediaKey）: 受け側はテキスト編集可能なアプリ（メモ帳・エディタ）。ゲーム等で `SendInput` がブロックされる場合は対象外（README の既知の制約）。
- **uia**: アプリ固有。`windowPattern` は「プロセス名\nタイトル」に対する正規表現、`elementName` は UI 要素の名前。対象アプリごとに値を変える（アプリ内の「要素ピッカー」で実際の名前を取得できる）。`verb` は invoke/toggle/setValue。
- **obs**: OBS 起動＋obs-websocket v5 が前提。`sceneSwitch` の `arg` は実在シーン名。設定ウィンドウのデバイスタブで接続先を設定。
- **昇格アプリ**: 管理者権限で動くアプリ（や一部ゲーム）には、UIPI により本体も管理者起動でないとキー送信が届かない（README の既知の制約）。検証時はメモ帳など非昇格アプリで。
- **midiOut**: 送信先 MIDI デバイスが実在する必要がある（テストでは `MIDIToEverything-Out` ループバック）。実運用では DAW 等の入力ポート名に合わせる。

---

## 6. 受け入れチェックリスト（コピーして記入）

```
[ ] 1.1 loopMIDI 2ポート作成・list で確認
[ ] 1.2 app + sender ビルド成功
[ ] 1.3 テスト設定を投入（本番設定を退避済み）
[ ] 1.4 起動・モニター表示・「受け入れテスト(基本ルール)」ロード確認
--- 3.A 自動 ---
[ ] launch  [ ] http  [ ] osc  [ ] midiOut  [ ] macro  [ ] cursorMove  [ ] windowsToggle
--- 3.B モニター ---
[ ] key  [ ] typeText  [ ] mouseClick  [ ] scroll  [ ] mediaKey  [ ] toggle  [ ] relative  [ ] none(発火しない)
--- 3.C 視覚 ---
[ ] setVolume  [ ] windowControl  [ ] uia  [ ] virtualDesktop  [ ] brightness  [ ] obs  [ ] plugin(該当時)
--- 4 ルール ---
[ ] 合算([BASE][RULE])  [ ] regexで[BASE]のみ  [ ] 手動トグルON/OFF
--- 後片付け ---
[ ] テスト設定を本番に復元（config.json.acceptance-bak を戻す）
[ ] アプリ再起動して通常設定で動作確認
```

> 全項目 ✓ なら「MidiToEverything に期待される全アクションが安定動作する」ことを証明できたと見なす。
> 失敗項目があれば、その信号番号・アクション・期待/実際をメモして報告する。
