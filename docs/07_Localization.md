# 07. 多言語対応（言語切り替え）

実行時に UI 言語を切り替えられる仕組み。現在は **日本語 / English / 中文(简体) / Español /
Deutsch / Français / 한국어** の7言語。言語プルダウンは**メイン画面のタイトルバー**にある。

## 翻訳の対象範囲

- **日本語 / English**: 全ウィンドウを完全対応（メイン画面・プロファイル編集・アクション設定
  ダイアログ・各種ヘルプ/説明/インストラクション・ツールチップ・ステータスメッセージ）。
- **中文 / Español / Deutsch / Français / 한국어**: 主要UI（メイン画面・エディタの見出し/ボタン・
  ダイアログ）を翻訳。長い説明文や一部ツールチップは**英語にフォールバック**（後述の仕組み）。
  各 `strings.<code>.json` を埋めれば完全化できる。

## アーキテクチャ

- **言語ごとに翻訳ファイル**: `Resources/Localization/strings.<code>.json`。各ファイルは
  **翻訳キー → テキスト** の辞書（JSON）。exe の隣に配置され、起動時に読み込まれる。
  `language.name` キーにその言語の自言語表記（例 "日本語" / "English"）を入れる。
  **言語の追加 = `strings.<code>.json` を1枚置くだけ**（再ビルド不要。ユーザが追加・編集可）。
- `Loc`（`src/App/Localization/Loc.cs`）: シングルトン。起動時に `Resources/Localization` の
  全 `strings.*.json` を読み込み、`code → 辞書` を構築。`Loc.Instance[key]` が**選択中言語の辞書**
  からテキストを返す（欠落キーは en → キー名にフォールバック）。`Languages` はファイルから発見した
  言語一覧（code＋自言語名）。言語変更で `PropertyChanged("Item[]")` を発火し、`{loc:Tr}` の全
  バインディングが**ライブ更新**される。
- `TrExtension`（`{loc:Tr Some.Key}`）: `Loc` の indexer への OneWay バインディングを生成する
  マークアップ拡張。XAML で `Text="{loc:Tr main.devices}"` のように使う。コードからは `Loc.T("key")`。
- 永続化: `AppSettings.Language`（既定 "ja"）。起動時に `Loc.SetLanguage` を適用し、
  **タイトルバーの言語プルダウン**で切替＝即時反映＋設定保存。
- 配布: 翻訳ファイルは出力にコピーされ、インストーラ（`{app}\Resources\Localization`）と
  ポータブル zip に同梱される。

## 実装メモ

- メイン画面・タイトルバー・トレイは `{loc:Tr}` / `Loc.T`。VM の計算ラベル（稼働状態・検出
  モード・一時停止）は言語変更で**ライブ更新**。
- プロファイル編集ウィンドウ／アクション設定ダイアログは**モーダル**なので、開いた時点の言語で
  表示すれば十分（`EditorHelp` は `Loc.T($"help....{kind}")` でキー引き）。XAML ラベルは `{loc:Tr}`、
  ステータスメッセージは `Loc.T` / `string.Format(Loc.T(..), ..)`。

## 追加・拡張の手順

- **文字列を足す**: 各 `strings.<code>.json` に同じキーで値を追加し、XAML は `{loc:Tr キー}`、
  コードは `Loc.T("キー")` を使う。
- **言語を足す**: `strings.fr.json` のように新ファイルを `Resources/Localization` に置く
  （`language.name` を含める）。再ビルド不要で言語プルダウンに現れる。
- 変換器（`ActionKindHelp` 等）経由の文字列を言語変更でライブ更新したい場合は、対象の
  バインディングが `Loc.Language` にも依存するようにする（モーダルなエディタは開き直しで足りる）。

## 既知の制約

- カスタム ComboBox テンプレートは `DisplayMemberPath` を表示に反映しないため、言語セレクタは
  明示的な `ItemTemplate`（`{Binding Display}`）を使用している。
- 言語名（日本語 / English）は両言語で同じ綴り（自言語表記）で表示する。
