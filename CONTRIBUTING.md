# コントリビューションガイド / コミット規約

本リポジトリ (MidiToEverything) のブランチ運用・コミットメッセージ・PR の慣習を定める。
設計ドキュメントは [docs/](docs/) を参照。

---

## 1. ブランチ戦略

- `main` … 常にビルド可能・テストグリーンを維持する保護ブランチ。直接 push せず PR 経由を原則とする。
- 作業ブランチは `main` から切り、種別プレフィックスを付ける:

| プレフィックス | 用途 | 例 |
|----------------|------|----|
| `feat/` | 機能追加 | `feat/mapping-resolver` |
| `fix/` | バグ修正 | `fix/cc-deadzone-off-by-one` |
| `docs/` | ドキュメントのみ | `docs/profile-schema-examples` |
| `refactor/` | 挙動を変えない整理 | `refactor/extract-trigger-eval` |
| `test/` | テスト追加・改善 | `test/resolver-conflict-table` |
| `chore/` | ビルド・依存・雑務 | `chore/bump-drywetmidi` |

- 1 ブランチ = 1 マイルストーン or 1 関心事。肥大化したら分割する。

---

## 2. コミットメッセージ規約 (Conventional Commits)

[Conventional Commits 1.0.0](https://www.conventionalcommits.org/) に準拠する。

```
<type>(<scope>): <subject>

<body>

<footer>
```

### type（必須）
`feat` / `fix` / `docs` / `refactor` / `test` / `chore` / `perf` / `build` / `ci` / `style`

### scope（任意・推奨）
変更が属するモジュール。本プロジェクトの主な scope:

`core` / `domain` / `mapping` / `midi` / `window` / `input` / `persistence` / `ui` / `tray` / `build` / `docs`

### subject（必須）
- 命令形・現在形（"add" であって "added"/"adds" ではない）。
- 末尾にピリオドを付けない。72 文字以内を目安。
- 日本語・英語どちらでも可。ただし 1 コミット内では統一する。

### body（任意）
- 「何を」より「なぜ」を書く。関連要件 ID（PRD の `FR-x`）やマイルストーン（`M1` 等）を参照する。

### footer（任意）
- 破壊的変更は `BREAKING CHANGE:` を明記。
- 関連 Issue は `Closes #123` / `Refs #123`。

### 例

```
feat(mapping): add MappingResolver with base/override layering

基本プロファイルと個別プロファイルをレイヤ合成し、上書き・フォールバック・
none ブロックを解決する純粋ロジックを実装 (FR-6.2/6.3/6.4, M1)。
解決結果は (profileVersion, contextHash) でキャッシュする。

Refs: docs/02_Architecture.md §3.2
```

```
test(mapping): cover conflict-resolution table from schema §6
```

```
chore(build): pin target framework to net8.0 across projects
```

---

## 3. コミットの粒度

- **論理的に 1 つの変更 = 1 コミット**。ビルドが通らない中間状態を `main` に残さない。
- 「フォーマット直し」と「ロジック変更」は混ぜない（レビュー容易性のため）。
- WIP コミットは作業ブランチ内に留め、`main` へは squash/整理してからマージする。

---

## 4. マージ前チェックリスト

PR を出す / マージする前に、ローカルで以下を確認する:

```sh
dotnet build MidiToEverything.sln -c Release
dotnet test  tests/Core.Tests/Core.Tests.csproj -c Release
```

- [ ] ビルドが 0 エラー
- [ ] テストが全て緑（新規ロジックにはテストを追加）
- [ ] 公開 API / スキーマ変更時は対応ドキュメント（`docs/`）を更新
- [ ] CI (`.github/workflows/ci.yml`) がグリーン

---

## 5. バージョニング

- [Semantic Versioning](https://semver.org/) に従う。`Directory.Build.props` の `<Version>` はローカル/CI 既定値（タグなしビルド用）。
- 公開リリースのバージョンは **Git タグ**が決定する（タグ `v1.2.3` → 製品バージョン `1.2.3`）。
- マイルストーン完了時にマイナー（`0.x.0`）を上げることを目安とする（`1.0.0` 到達まではすべてプレリリース扱い）。

## 6. リリース（GitHub Releases）

タグを push すると `.github/workflows/release.yml` が自動で **self-contained 単一 exe をビルド→テスト→
zip 化→GitHub Release を作成**する（リリースノートは自動生成、`MidiToEverything-<tag>-win-x64.zip` を添付）。

手順:
```sh
# main がグリーンであることを確認してから
git switch main && git pull
git tag v1.0.0            # SemVer。プレリリースは v1.0.0-rc1 のように "-" 付き → Release も prerelease 扱い
git push origin v1.0.0    # ← これだけでリリースが走る
```
- タグ名は `v` プレフィックス必須（ワークフローの起動条件 `tags: ['v*']`）。
- リリース成果物 zip には `MidiToEverything.exe`・`README.md`・`samples/config.sample.json` を同梱。
- 初回のみ、リポジトリ設定で Actions に「Read and write permissions」が必要（`gh release create` 用）。
  ワークフロー側でも `permissions: contents: write` を宣言済み。
