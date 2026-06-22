# 07. 多言語対応（言語切り替え）

実行時に UI 言語を切り替えられる仕組み。現在は **日本語 / English**。

## 仕組み

- `Loc`（`src/App/Localization/Loc.cs`）: シングルトン。`Loc.Instance[key]` が現在言語の文字列を返す。
  言語変更で `PropertyChanged("Item[]")` を発火し、`{loc:Tr}` の全バインディングが**ライブ更新**される。
  欠落キーは en → キー名にフォールバック。
- `TrExtension`（`{loc:Tr Some.Key}`）: `Loc` の indexer への OneWay バインディングを生成する
  マークアップ拡張。XAML で `Text="{loc:Tr main.devices}"` のように使う。
- `Strings`（`Strings.cs`）: `ja` / `en` の文字列テーブル（コード辞書。サテライトアセンブリ不要）。
- 永続化: `AppSettings.Language`（"ja"/"en"、既定 "ja"）。起動時に `Loc.SetLanguage` を適用し、
  メイン画面の言語コンボで切替＝即時反映＋設定保存。

## 現在の対象範囲（このパス）

- ✅ メインウィンドウ全体、ウィンドウのタイトルバー、トレイメニュー、言語セレクタ。
  VM の計算ラベル（稼働状態・検出モード・一時停止）も言語変更でライブ更新。
- ⏳ **プロファイル編集ウィンドウ／アクション設定ダイアログは未対応**（日本語のまま＝
  ウィンドウ内で一貫）。エディタは英語時も日本語で表示される。次の増分で対応予定。

## 追加・拡張の手順

1. `Strings.Ja` / `Strings.En` に同じキーで文字列を追加。
2. XAML は `{loc:Tr キー}`、コードは `Loc.T("キー")` を使う。
3. 変換器（`ActionKindHelp` 等）経由の文字列を言語変更でライブ更新したい場合は、対象の
   バインディングが `Loc.Language` にも依存するようにする（モーダルなエディタは開き直しで足りる）。

## 既知の制約

- カスタム ComboBox テンプレートは `DisplayMemberPath` を表示に反映しないため、言語セレクタは
  明示的な `ItemTemplate`（`{Binding Display}`）を使用している。
- 言語名（日本語 / English）は両言語で同じ綴り（自言語表記）で表示する。
