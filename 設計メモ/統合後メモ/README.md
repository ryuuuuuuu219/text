# 統合後メモ

`統合前メモ` に散在していた企画・世界設定・物語案・会話規則を、実装や執筆から参照しやすい単位へ整理した正本候補である。

本プロジェクトの主目的は、**Codexによる動的シナリオ生成**を、空母運営・自動空戦・人物関係シミュレーションへ接続することである。

## 記述上の区分

- **確定**：参照元で確定事項として明記されている、または複数資料で一貫している内容。
- **初期仕様**：MVPや初期実装で採用する内容。将来拡張とは分けて扱う。
- **候補**：比較案・分岐案・未決定事項。確定設定として外部へ出さない。
- **未確定**：決定が必要だが、現時点で根拠がない内容。

参照元に残る `B国` は初期企画時の仮称であり、現在の戦争設定ではゼア過激派を開戦主体とする。地図上の `B` は別途ベティアを指すため、混同しない。

## 入口

### ゲーム設計

- [企画概要と中核ループ](gameplay/overview-and-core-loop.md)
- [空母・艦載機・任務](gameplay/carrier-and-air-wing.md)
- [パイロット・名声・関係性](gameplay/pilots-and-reputation.md)
- [空戦・情報制限・救難](gameplay/combat-information-and-rescue.md)
- [会話と生成AI](gameplay/dialogue-and-generation.md)
- [テキスト表示・データ仕様](gameplay/text-presentation-and-data.md)
- [ミッション候補集](gameplay/mission-library.md)
- [デュランダル飛行隊・検証シナリオA](gameplay/simulation-fixture-durandal.md)
- [MVPと開発順序](gameplay/mvp-and-roadmap.md)

### 物語

- [主人公陣営と開始勢力](narrative/protagonist-faction.md)
- [焦土作戦イベント](narrative/scorched-academy-city.md)

### 世界設定

- [世界地図](world/geography/world-map.md)
- [Z包囲戦争](world/current-war/belligerents.md)
- [ゼア政府の戦時変質](world/current-war/zea-government-transition.md)
- [国際連合](world/institutions/united-nations.md)
- [航空騎士道](world/military/ace-culture.md)
- [禁制兵器とUAV区分](world/military/prohibited-weapons.md)
- [センサー運用思想](world/military/sensor-doctrine.md)
- [技術史](world/technology/technology-history.md)

## 正本化で残る作業

- 各国・都市・空母・人物の正式名を決める。
- 主人公の開始勢力を本当に乱数決定するか、選択式にするかを決める。
- UAV登場を段階導入にするか、物語開始時から有人・無人混成にするかを決める。
- 焦土作戦を固定イベントにするか、条件分岐イベントにするかを決める。
