# 08. 設定項目ロードマップ

設定ウィンドウ（`SettingsWindow`、タイトルバーの ⚙ から開く）に追加していく設定の
バックログ。実装したら `[x]` にする。新規設定は基本 `AppSettings`（+ DTO + ProfileMapper）
に永続化し、設定ウィンドウにUIを置く。

既存（実装済み）: 言語 / 外部起動を許可 / スタートアップ起動 / アップデートを自動確認・今すぐ確認。

## A. 起動・常駐・緊急
- [x] トレイに最小化して起動（起動時にウィンドウを表示せずトレイ常駐）
- [x] 閉じるボタンの挙動を選択（トレイへ最小化 / 終了）。`App.OnWindowClosing` で `CloseToTray` 判定
- [x] 起動時の発火状態（有効 / 無効）。`GatedInputSink.Enabled` を `StartEmissionEnabled` から設定
- [x] 緊急停止ホットキーの編集UI（`HotkeyParser` で config 値を解析・登録、変更時に再登録、無効値は赤字）
- [x] 多重起動の禁止（Mutex。2回目起動は既存ウィンドウを前面化して終了）

## B. デバイス／連携の集約
- [x] デバイス検出モード（自動ポーリング / 手動）を設定へ集約＋永続化（`AutoDetectDevices`）。rescan はデバイスパネルに残置
- [ ] ポーリング間隔（後回し: MIDIソースへの配線が必要）
- [ ] 監視デバイスのフィルタ（`WatchedDevices`）（後回し: 実配線とリスト編集UIが必要）
- [x] OBS接続（host/port/password）を設定ウィンドウへ移動
- [ ] OBS起動時自動接続（後回し: `IObsClient` に Connect API が無く遅延接続のため）

## C. 入力モニター・ログ・診断
- [x] モニター最大ログ行数 / UI更新間隔（`monitor.maxLogLines`/`uiThrottleMs` のUI、最大行数・間隔とも即時反映）
- [ ] ウィンドウ非表示時はモニターを自動一時停止（後回し: ウィンドウ可視性の配線が必要、CPU節約は軽微）
- [x] ログレベル（`LoggingLevelSwitch` で即時反映）＋「ログフォルダを開く」ボタン
- [x] クラッシュ時の自動再起動 オン/オフ（`CrashReporter.AutoRestart`、`CrashAutoRestart` 設定）
- [x] ログ保持日数（`LogRetentionDays`、起動時に反映）

## D. アップデート拡張
- [x] 更新チャンネル（`stable` = /releases/latest / `prerelease` = /releases から最新を選択）。`UpdateChannel`
- [x] 確認間隔を設定可能（`UpdateCheckHours`、変更時にタイマー再設定）

## E. 設定管理・通知・安全
- [ ] 設定フォルダを開く / 設定のリセット / インポート・エクスポート（設定ウィンドウに集約）
- [ ] トレイ通知のオン/オフ（プロファイル切替・緊急停止・更新）
- [ ] 外部起動の確認ダイアログ（`AllowExternalLaunch` の補強）
- [ ] 特定アプリ前面時は発火を抑止（任意・大きめ）

## F. 外観（任意・大きめ）
- [ ] ライト/ダークテーマ、アクセントカラー、フォントサイズ（拡大）
