# 機体移動ベース仕様（Unit B → A / C 改造用）

対象スクリプト: `Assets/Scripts/Player/UnitBMotor.cs`  
入力: `Assets/Scripts/Player/UnitPlayerInput.cs` / `UnitInputFrame.cs`  
Input Actions: `Assets/Input/UnitControls`（Resources からもロード）  
対象HUD: `Assets/Scripts/UI/FuelGaugeHud.cs`（Canvas Prefab `Assets/UI/Prefabs/FuelHud.prefab` をバインド）  
テストシーン: `Assets/Scenes/TestField.unity`  
戦闘α: `Documentation/Combat_Alpha_Spec.md`

このドキュメントは **機体 B（平均・頑強）を完成ベース** にし、他メンバーが **機体 A（俊敏）／機体 C（力強）** に数値・挙動を振り分けやすくするための短い仕様です。

---

## 1. コンセプト（地上スライド）

αの手触り方針: **し放題ホバーではなく、重さのある地上機＋スライドブースト**。

| 機体 | 役割 | 手触りの方向 |
|------|------|----------------|
| **B（現行）** | 盾剣・平均 | 加速遅め、滑って止まる、スライドは燃料食い |
| **A** | 俊敏・射撃寄り | 歩き速め、スライド加速↑／消費↓、空中制御やや↑ |
| **C** | 力強 | 歩き遅め、ブレーキ弱め（惰性）、スライド短く重い |

コードを分けず、**B の Component を複製して Inspector を変える**運用。

---

## 2. 操作（Input System）

`UnitBMotor` は `Input.*` を読まず、`IUnitInputSource`（既定: `UnitPlayerInput`）から受け取る。

| Action | Keyboard&Mouse | Gamepad | 動作 | 消費燃料 |
|--------|----------------|---------|------|----------|
| Move | WASD | 左スティック | 歩き（慣性あり） | なし |
| Look | Mouse delta | 右スティック | 視点 | なし |
| Jump | Space | South | 接地ジャンプのみ（ホバー既定OFF） | **Jump Boost** |
| Dash | Left Shift | LB | **スライドブースト**（押し込み加速、即時セット速度ではない） | **Dash Jet** |
| ToggleCursor | Esc | Start | カーソルロック切替 | なし |
| LockOn | Q / 右クリック | RB | ロックオン（別コンポーネント） | なし |

NPC / 2P は同じ `IUnitInputSource`。

---

## 3. 燃料二系統

| タンク | 用途 | HUD |
|--------|------|-----|
| **Jump Boost**（橙） | ジャンプ初速コスト（ホバーはオプション） | 左ゲージ「ブースト」 |
| **Dash Jet**（水色） | スライドブースト | 左ゲージ「ジェット」 |

- 消費後 `regenDelay` 待ってから回復  
- スライド枯渇時は追加で `slideEmptyExtraDelay` ぶん回復開始が遅れる  
- 点火に `slideIgniteCost`（空タップ連打防止）

---

## 4. Inspector（改造の本体）

### Weight / Ground Walk
- `maxWalkSpeed` … 歩き最高速  
- `walkAcceleration` … 加速（低いほど重い）  
- `coastFriction` … スティック離し時の惰性摩擦（低いほど滑る）  
- `brakeFriction` … 逆入力時のブレーキ  
- `turnResponsiveness` … 視点ヨー（スライド中はさらに鈍る）

### Air
- `airControl` … 空中操舵（αは低め）  
- `gravity` … 重力（負）

### Jump
- `jumpHeight` / `jumpFuelCost` / `requireJumpFuel`  
- `enableHover` … **α既定 false**（地上戦）

### Slide Boost (Dash Jet)
- `slideMaxSpeed` … スライド時の目標最高速  
- `slideAcceleration` … 押し込み加速（スナップしない）  
- `slideFriction` … スライド中の軽いドラッグ  
- `slideFuelPerSecond` / `slideIgniteCost`  
- `allowAirSlide` … **α既定 false**  
- `slideSteer` … スライド中のスティック操舵（0＝レール）  
- `slideEmptyExtraDelay` … 枯渇後の回復ペナルティ

---

## 5. A / C への振り分け例

### 機体 A（俊敏）
- `maxWalkSpeed` ↑、`walkAcceleration` ↑  
- `slideMaxSpeed` ↑、`slideAcceleration` ↑、`slideFuelPerSecond` やや↓  
- `slideSteer` ↑、`airControl` やや↑  

### 機体 C（力強）
- `maxWalkSpeed` ↓、`walkAcceleration` ↓、`coastFriction` ↓（惰性大）  
- `slideMaxSpeed` 普通、`slideFuelPerSecond` ↑（すぐ枯れる）  
- `jumpFuelCost` ↑、`turnResponsiveness` ↓  

---

## 6. 公開プロパティ（UI / 他）

- `JumpBoostFuelNormalized` / `DashJetFuelNormalized`  
- `IsHovering` / `IsDashing`（スライド中も `IsDashing`）  
- `IsJumpBoostActive` / `IsDashJetActive`  
- `IsGrounded` / `PlanarVelocity` / `PlanarSpeed`  
- `ResetLook` / `RebindCameraPivot` / `SetInputSource`

---

## 7. まだやらないこと

- 盾・剣・ガトリング / 部位破壊 / 物資 / 本ステージ / ネット同期  

移動の手触りが固まってから戦闘へ進む。
