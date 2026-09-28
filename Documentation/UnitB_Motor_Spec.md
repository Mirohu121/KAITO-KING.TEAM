# 機体移動ベース仕様（Unit B → A / C 改造用）

対象スクリプト: `Assets/Scripts/Player/UnitBMotor.cs`  
対象HUD: `Assets/Scripts/UI/FuelGaugeHud.cs`  
テストシーン: `Assets/Scenes/TestField.unity`

このドキュメントは **機体 B（平均・頑強）を完成ベース** にし、他メンバーが **機体 A（俊敏）／機体 C（力強）** に数値・挙動を振り分けやすくするための短い仕様です。

---

## 1. コンセプト

| 機体 | 役割 | 手触りの方向 |
|------|------|----------------|
| **B（現行）** | 盾剣・平均性能 | やや重い加速、安定した空中、標準ダッシュ |
| **A** | 俊敏・射撃寄り | 速い移動・高い空中制御・ダッシュ強め／火力は後工程 |
| **C** | 力強・部位破壊寄り | 遅い移動・重いジャンプ／ホバー弱め・一撃は後工程 |

コードを最初から分けず、**B の Prefab / Component を複製して Inspector を変える**運用を推奨します。

---

## 2. 操作

| 入力 | 動作 | 消費燃料 |
|------|------|----------|
| WASD | 移動 | なし |
| マウス | 視点 | なし |
| Space（接地） | ジャンプ | **Jump Boost**（`jumpFuelCost`） |
| Space（空中・長押し） | 浮上／ホバー | **Jump Boost**（`hoverFuelPerSecond`） |
| Left Shift（長押し） | ダッシュ（ジェット） | **Dash Jet**（`dashFuelPerSecond`） |
| Esc | カーソルロック切替 | なし |

見た目のジェットパック演出は後回し（仕様どおり見た目は変えなくてよい）。

---

## 3. 燃料二系統

| タンク | 用途 | HUD |
|--------|------|-----|
| **Jump Boost**（橙） | ジャンプ初速コスト ＋ 空中ホバー | 左ゲージ「ブースト」 |
| **Dash Jet**（水色） | 水平ダッシュ | 左ゲージ「ジェット」 |

共通パラメータ（各 `FuelTank`）:

- `max` … 最大量  
- `current` … 現在量  
- `regenPerSecond` … 回復速度  
- `regenDelay` … 消費後、回復開始までの待ち  

ホバー中は Jump Boost は回復しない。ダッシュ中は Dash Jet は回復しない。

---

## 4. Inspector で触る項目（改造の本体）

### Weight / Ground Feel
- `maxSpeed` … 最高速度  
- `acceleration` … 加速（低いほど重い）  
- `deceleration` … 減速  
- `turnResponsiveness` … 視点ヨーの追従  

### Air Feel
- `airControl` … 空中の左右制御  
- `gravity` … 重力（負の値）  
- `hoverMoveSpeedScale` … ホバー中の水平速度倍率  

### Jump
- `jumpHeight`  
- `jumpFuelCost`  
- `requireJumpFuel`  

### Hover / Jetpack
- `enableHover`  
- `hoverFuelPerSecond`  
- `hoverAscendSpeed` … 長押し中の上昇速度目標  
- `hoverVerticalAcceleration` … そこに追いつく速さ  
- `hoverGravityScale` … ホバー中に残す重力感  
- `hoverArmDelay` … ジャンプ直後にホバー可能になるまでの猶予  

### Dash / Jet
- `dashKey`  
- `dashSpeed`  
- `dashFuelPerSecond`  
- `allowAirDash`  
- `boostUpWhileDash` / `dashUpSpeed`  

---

## 5. A / C への振り分け例（初期提案）

数値はたたき台。プレイしてから詰める。

### 機体 A（俊敏）目安
- `maxSpeed` ↑（例: 8〜9）  
- `acceleration` / `deceleration` ↑  
- `airControl` ↑（例: 0.55〜0.7）  
- `turnResponsiveness` ↑  
- `dashSpeed` ↑、`dashFuelPerSecond` やや↓（長く飛べる）  
- `jumpHeight` やや↑  
- `hoverAscendSpeed` ↑、`hoverFuelPerSecond` やや↓  

### 機体 C（力強）目安
- `maxSpeed` ↓（例: 4.5〜5.5）  
- `acceleration` / `deceleration` ↓（重い）  
- `airControl` ↓  
- `turnResponsiveness` ↓  
- `dashSpeed` ↓ か短時間高消費  
- `jumpHeight` 普通〜やや低  
- `hoverAscendSpeed` ↓、`hoverFuelPerSecond` ↑（ホバーは苦手）  

戦闘（盾・剣・ガトリング／部位破壊）は移動とは別コンポーネントで追加予定。

---

## 6. 改造手順（推奨）

1. `Player_UnitB_Cube` を Prefab 化（または複製）  
2. 名前を `Player_UnitA` / `Player_UnitC` に  
3. 同じ `UnitBMotor` のまま Inspector だけ変更  
   - 後で差分が大きくなったら `UnitAMotor` へリネーム／継承でも可  
4. `FuelGaugeHud` はそのまま流用（燃料参照は `UnitBMotor` 前提）  
5. 変更したプリセット値をこの仕様書の表に追記して共有  

---

## 7. 公開プロパティ（UI / 他システム用）

- `JumpBoostFuelNormalized` / `DashJetFuelNormalized`  
- `IsHovering` / `IsDashing`  
- `IsJumpBoostActive` / `IsDashJetActive`  
- `IsGrounded`  

---

## 8. まだやらないこと（スコープ外）

- 盾・剣・ガトリング  
- 部位破壊  
- 物資争奪  
- 本ステージ（火星マップ）  
- ネット同期  

移動の手触りが固まってから戦闘へ進む。
