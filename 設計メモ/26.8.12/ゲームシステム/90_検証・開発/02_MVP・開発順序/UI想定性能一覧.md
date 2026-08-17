# UI想定性能一覧

作成日: 2026-08-17  
基準: `AirKnight/Assets/Prefabs/GenericAircraft.prefab` の現在値

## 1. ページ構成

| ページ | 内容 |
|---:|---|
| 1 | 機体性能 |
| 2 | 胴体 |
| 3 | 主翼 |
| 4 | 操縦翼面 |
| 5 | 補助翼 |
| 6 | エンジン |
| 7 | 燃料タンク |
| 8 | 装甲 |
| 9 | 補助装備 |
| 10 | ハードポイント |

- 機体性能を必ず1ページ目に配置する。
- 2ページ目以降はパーツ単位で分ける。
- 同種パーツが複数コンポーネント存在する場合も、最終的には各パーツを別ページとして生成する。
- ページ順は機体性能を固定の先頭とし、パーツページはUI側で定義した種別順、その中でHierarchy順とする。

## 2. 1ページの表示構造

1ページにつき、次の3つのTMPを使用する。

| TMP | 内容 | 配置 |
|---|---|---|
| ページタイトルTMP | `機体性能`、`胴体`など | 上部 |
| 項目TMP | 項目名を改行区切りで格納 | 左列 |
| 性能値TMP | 項目と対応する値を改行区切りで格納 | 右列 |

項目TMPと性能値TMPは、必ず同じ行数・同じ行順にする。

```text
┌──────────────────────────────┐
│          ページタイトルTMP          │
├──────────────┬───────────────┤
│ 項目TMP       │ 性能値TMP            │
│ 総重量        │ 1,180                │
│ 最大耐久値    │ 1,404                │
│ 総推力        │ 20,000               │
└──────────────┴───────────────┘
```

将来の出力単位は、1ページにつき次の3文字列とする。

```csharp
public readonly struct UiPerformancePage
{
    public readonly string title;
    public readonly string itemText;
    public readonly string valueText;
}

public IReadOnlyList<UiPerformancePage> BuildUiPerformancePages()
```

`AircraftPartStatusConverter`は、各行を同じ順序で`itemText`と`valueText`へ追加する。

## 3. 表示から廃止する項目

次の項目はUI性能一覧へ出力しない。

- 個数
- 1個重量
- 1個耐久値
- 材料品質
- 接合品質
- 実効安全率

重量と耐久値は、現在の`quantity`を反映した合計値だけを、それぞれ`重量`、`耐久値`として表示する。安全率はUI上では`安全率`だけを表示する。

この段階ではUI出力からの廃止であり、既存Prefabの集計値を維持するため、コード上の`quantity`、`materialQuality`、`jointQuality`はまだ削除しない。コード側からも廃止する場合は、左右主翼・尾翼・ハードポイントなどの複数パーツを別コンポーネントへ分解してから移行する。

## 4. 表示形式

- 数値は計算値を確認できる精度で出し、最終UIで丸め桁を調整する。
- 速度は`m/s / km/h`、旋回性能は`deg/s / s/180deg`を併記する。
- 寸法はm、面積はm²、幾何容積はm³として表示する。
- 重量、推力、搭載重量の正式な単位は未確定のため数値のみ表示する。
- enumは日本語UI名称へ変換する。
- boolは`有効` / `無効`で表示する。
- 対応兵装が複数ある場合は`爆弾 / ロケット / ミサイル`のように区切る。

## 5. GenericAircraft 現在値のページ別出力

### 1ページ目: 機体性能

ページタイトルTMP:

```text
機体性能
```

項目TMP:

```text
総重量
Rigidbody質量
最大耐久値
総推力
有効翼面積
総前面投影面積
水平飛行平衡速度
理想急降下平衡速度
加速性能
分解速度
ピッチ性能 C（低速時）
ピッチ性能 M（最大）
ロール性能
ロール精度
ヨー性能
全軸角速度安全上限
航続時間
ハードポイント数
最大搭載重量合計
胴体平面面積
胴体前面投影面積
翼平面面積
翼前面投影面積
胴体内部容積
最小安全率
```

性能値TMP:

```text
1,180
1.1800001
1,404
20,000
26 m²
7.5087385 m²
51.609722 m/s / 185.795 km/h
62.193028 m/s / 223.895 km/h
21.694914 m/s² / 78.102 (km/h)/s
62.193028 m/s / 223.895 km/h
8 deg/s / 22.5 s/180deg
12 deg/s / 15.0 s/180deg
10 deg/s / 18.0 s/180deg
1
8 deg/s / 22.5 s/180deg
30 deg/s / 6.0 s/180deg
30分
2
500
25 m²
4.9087386 m²
26 m²
2.6 m²
34.36117 m³
1
```

### 2ページ目: 胴体

ページタイトルTMP:

```text
胴体
```

項目TMP:

```text
パーツ名
重量
耐久値
安全率
形状
幅
高さ
長さ
機体耐久値係数
容積効率
平面面積
前面投影面積
内部容積
```

性能値TMP:

```text
Generic Fuselage
400
400
1
円筒
2.5 m
1.5 m
10 m
1
0.7
25 m²
4.9087386 m²
34.36117 m³
```

### 3ページ目: 主翼

ページタイトルTMP:

```text
主翼
```

項目TMP:

```text
パーツ名
重量
耐久値
安全率
主翼形式
幅
高さ
長さ
取付角度
容積効率
主翼内蔵ハードポイント数
主翼内蔵最大搭載重量
ピッチ性能 C（低速時）
ピッチ性能 M（最大）
ピッチ最適速度 P
ピッチ操舵限界速度 X
翼面積合計
前面投影面積合計
内部容積合計
```

性能値TMP:

```text
Generic Main Wing
300
300
1
通常翼
5 m
0.2 m
2 m
0 deg
0.35
0
0
8 deg/s / 22.5 s/180deg
12 deg/s / 15.0 s/180deg
30 m/s / 108 km/h
75 m/s / 270 km/h
20 m²
2 m²
1.4 m³
```

### 4ページ目: 操縦翼面

ページタイトルTMP:

```text
操縦翼面
```

項目TMP:

```text
パーツ名
重量
耐久値
安全率
ロール性能加算
```

性能値TMP:

```text
Generic Control Surface
40
50
1
8 deg/s
```

### 5ページ目: 補助翼

ページタイトルTMP:

```text
補助翼
```

項目TMP:

```text
パーツ名
重量
耐久値
安全率
補助翼形式
幅
高さ
長さ
取付角度
有効翼面積への加算
ロール性能倍率
翼面積合計
前面投影面積合計
```

性能値TMP:

```text
Generic Tail
50
50
1
尾翼
2 m
0.15 m
1.5 m
0 deg
有効
1
6 m²
0.6 m²
```

### 6ページ目: エンジン

ページタイトルTMP:

```text
エンジン
```

項目TMP:

```text
パーツ名
重量
耐久値
安全率
搭載位置
1基推力
総推力
プロペラ数
エンジン間隔
配置精度
```

性能値TMP:

```text
Generic Engine
200
150
1
胴体前部
20,000
20,000
1
0
1
```

### 7ページ目: 燃料タンク

ページタイトルTMP:

```text
燃料タンク
```

項目TMP:

```text
パーツ名
重量
耐久値
安全率
搭載位置
容積
```

性能値TMP:

```text
Generic Fuel Tank
100
50
1
胴体
30
```

### 8ページ目: 装甲

ページタイトルTMP:

```text
装甲
```

項目TMP:

```text
パーツ名
重量
耐久値
安全率
防御倍率
```

性能値TMP:

```text
Generic Armor
50
100
1
1.2
```

### 9ページ目: 補助装備

ページタイトルTMP:

```text
補助装備
```

項目TMP:

```text
パーツ名
重量
耐久値
安全率
機能ID
性能値
```

性能値TMP:

```text
Generic Sensor
20
30
1
Sensor
1
```

### 10ページ目: ハードポイント

ページタイトルTMP:

```text
ハードポイント
```

項目TMP:

```text
パーツ名
重量
耐久値
安全率
対応兵装
最大搭載重量合計
```

性能値TMP:

```text
Generic Hardpoint
20
40
1
ミサイル
500
```

## 6. enumのUI表示名

| コード値 | UI表示 |
|---|---|
| `FuselageShape.Cylinder` | 円筒 |
| `FuselageShape.Box` | 箱型 |
| `MainWingType.Conventional` | 通常翼 |
| `MainWingType.Delta` | デルタ翼 |
| `MainWingType.Tailless` | 無尾翼 |
| `AuxiliaryWingType.Tail` | 尾翼 |
| `AuxiliaryWingType.Canard` | カナード |
| `EngineMountPosition.FuselageFront` | 胴体前部 |
| `EngineMountPosition.FuselageRear` | 胴体後部 |
| `EngineMountPosition.MainWing` | 主翼 |
| `FuelTankLocation.Fuselage` | 胴体 |
| `FuelTankLocation.MainWing` | 主翼 |
| `SupportedWeaponTypes.Bomb` | 爆弾 |
| `SupportedWeaponTypes.Rocket` | ロケット |
| `SupportedWeaponTypes.GunPod` | ガンポッド |
| `SupportedWeaponTypes.Missile` | ミサイル |

## 7. 現段階での注意

- この文書は表示形式の確認用であり、`AircraftPartStatusConverter`のページ文字列生成APIはまだ未実装。
- パーツ名と`functionId`は現在の英語文字列をそのまま表示している。
- 有効翼面積は集計値として残っているが、翼面荷重・失速計算には使用しない。
- `全軸角速度安全上限`は`AircraftController.maxTurnRateDegrees`の値で、`AircraftStatus`には現在保持していない。
- 現在のGenericAircraftでは主翼・補助翼・ハードポイントが`quantity = 2`でまとまっているため、この資料では重量・耐久・面積・搭載重量を個数反映後の合計値で表示している。
