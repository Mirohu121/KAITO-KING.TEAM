# 戦闘α仕様（1v1）

## 流れ（Play）

1. **機体選択**（A / B / C）— Canvas Prefab `UnitSelectHud`
2. 選んだ `RobotPrefab` をスポーン
3. 端と端で 1v1 vs NPC（敵は機体 B）
4. コア撃破 or タイムオーバーで勝敗

関連ドキュメント:
- 移動: `Documentation/UnitB_Motor_Spec.md`
- 試合フロー指示（ポーズ / 終了二択 / 敵機体選択）: `Documentation/Match_Flow_Task.md`

シーン: `TestField` — `CombatAlphaBootstrap` が自動配線（**追加メニューなし**）。

---

## ルール

| 項目 | 内容 |
|------|------|
| 形式 | 1対1（相手は NPC） |
| 撃破 | コア HP 0 |
| タイムオーバー | 残コア HP が多い方の勝ち（同値は DRAW） |
| 開始位置 | マップの端と端 |
| 攻撃 | Attack（LMB / パッド West）近接 |

---

## UI（Canvas Prefab）

| Prefab | パス | Binder |
|--------|------|--------|
| 機体選択 | `Assets/UI/Prefabs/UnitSelectHud.prefab` | `UnitSelectBinder` |
| 燃料 | `Assets/UI/Prefabs/FuelHud.prefab` | `FuelGaugeHud` |
| 試合 | `Assets/UI/Prefabs/MatchHud.prefab` | `MatchHudBinder` |

機体プレハブ: `Assets/RobotPrefab/Player_UnitA/B/C_Cube.prefab`  
（Resources コピー: `Resources/RobotPrefab/UnitA/B/C`）

---

## 入力

NPC は `NpcChaseBrain`。  
A/B/C の移動ステータスはα配線

---

## 操作

- 選択画面: マウスで A/B/C
- 移動 / 視点 / ジャンプ / ダッシュ: 既存どおり
- **攻撃**: 左クリック（またはパッド West）
- Esc: カーソル
