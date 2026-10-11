# Windows利用枠ウィジェット調査

調査日: 2026-10-11。公開READMEと指定リポジトリのソースを確認。アプリの実機動作は未検証。
「タスクに常駐」は通知領域（システムトレイ）への常駐として調査した。

| 候補 | 利用枠 | 自動起動 | 常駐 | 最前面 | 日本語・英語 |
| --- | --- | --- | --- | --- | --- |
| ognjeeen/codex-usage-widget | Codexの5時間・週 | 設定あり | トレイ・タスクバー | 対応。切替設定は追加が必要 | 英語・簡体字中国語。日本語追加が必要 |
| Anatr1/ai-usage-widget | Codex・Claudeの5時間・週 | 対応 | トレイ | 切替対応 | 日本語対応は確認できず |
| upstream-ray/codex-usage-monitor | Codex・Claudeの5時間・週 | 対応 | トレイ・タスクバー | 浮動ウィンドウでの任意切替は未確認 | 日本語対応は確認できず |
| niccolo-sabato/claude-usage-widget | Claudeの5時間・週 | READMEでは未確認 | タスクバー表示。通常のトレイ常駐は未確認 | 常時最前面。切替は未確認 | 日英対応 |
| libaie/codex-usage-widget | Codexのローカル記録による表示 | 未確認 | トレイ常駐は未確認 | 対応 | 日英対応 |

## 判断

全要件をそのまま満たすアプリは、この調査では確認できなかった。
Codexだけでよければ、指定されたMITライセンスのリポジトリに日本語と最前面切替を追加する方法が有力。
両サービスを1つのアプリで表示する場合は、Anatr1/ai-usage-widgetも候補。ただしCodexはローカルセッションの直近スナップショットを読み、Codex実行まで情報が更新されない場合がある。
指定リポジトリはローカルの公式Codex CLI app-server経由で利用枠を取得する。
libaie版はREADMEがローカル推定と明記しているため、正確なアカウント利用枠確認の第一候補にはしない。

## 作業状況

指定リポジトリをローカルに取得し、Forkを作成した。
https://github.com/EbiSenbei/codex-usage-widget
ユーザーの回答により、1つのアプリで両サービスを表示する方針を確定した。
指定リポジトリを拡張し、Claudeの独立した取得処理・同時表示・日本語・最前面切替を追加した。
Windowsでプレビュー表示、日英の即時切替、設定保存を確認した。
実データ取得ではCodexの5時間枠・週間枠を取得できた。Claudeはローカルログインがなく、ログイン案内を返すことを確認した。
ソースコードは上記のForkで管理する。配布用EXE・ZIPはローカルのreleaseフォルダにまとめてあり、GitHub Releasesでは未公開。
起動方法と制約は[日本語ガイド](../README.ja.md)を参照。

## 調査元

- https://github.com/ognjeeen/codex-usage-widget
- https://github.com/Anatr1/ai-usage-widget
- https://github.com/upstream-ray/codex-usage-monitor
- https://github.com/niccolo-sabato/claude-usage-widget
- https://github.com/libaie/codex-usage-widget
- https://github.com/ansonliam/AIUsageMonitor
