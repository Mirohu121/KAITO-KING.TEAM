# 機体移動ベース仕様（Unit B → A / C 改造用）

対象スクリプト: `Assets/Scripts/Player/UnitBMotor.cs`  
入力: `Assets/Scripts/Player/UnitPlayerInput.cs` / `UnitInputFrame.cs`  
Input Actions: `Assets/Input/UnitControls`  
対象HUD: `Assets/Scripts/UI/FuelGaugeHud.cs`  
戦闘α: `Documentation/Combat_Alpha_Spec.md`

---

## 1. コンセプト（戦場の絆寄り）

αの手触り: **重い歩き + ブースト（EN）+ 着地硬直**。ホバーし放題はしない。

| 要素 | 絆の対応 | 実装 |
|------|----------|------|
| 歩き | レバー移動 | 低め加速の歩き |
| ブースト | 右ペダル | Shift 長押し、EN消費、離すと**隙** |
| ジャンプ | 左ペダル | Space、着地で**硬直** |
| オーバーヒート | EN枯渇後使用不能 | `boostOverheatSeconds` |

| 機体 | 方向 |
|------|------|
| **B** | 平均・隙ありブースト |
| **A** | 歩き/ブースト速め、隙短め |
| **C** | 重く、隙長め |

---

## 2. 操作

| Action | キー | 動作 | 燃料 |
|--------|------|------|------|
| Move | WASD | 歩き（慣性） | なし |
| Look | Mouse | 視点 | なし |
| Jump | Space | ジャンプ（着地硬直あり） | Jump Boost |
| Dash | Left Shift | ブースト（離すと隙 / 空でOH） | Dash Jet |
| LockOn | Q / 中クリック | ロック | なし |
| Pause | Esc | ポーズ | なし |

---

## 3. Inspector 要点

### Walk
- `maxWalkSpeed` / `walkAcceleration` / `coastFriction` / `brakeFriction`

### Jump
- `jumpHeight` / `jumpFuelCost`
- `landingStunSeconds` / `landingMoveScale`
- `enableHover` … 既定 false

### Boost
- `boostMaxSpeed` / `boostAcceleration` / `boostFuelPerSecond`
- `boostRecoverySeconds` … 離したあとの隙
- `boostOverheatSeconds` … EN空のロック
- `allowAirBoost` … 既定 false

---

## 4. リトライ → 機体選択

`CombatAlphaBootstrap.RestartToUnitSelect()` が試合を解体して機体選択を再表示。  
Pause の Restart / リザルトの「リトライ」から呼ばれる（シーン全リロードは使わない）。

---

## 5. 公開 API

- `JumpBoostFuelNormalized` / `DashJetFuelNormalized`
- `IsDashing`（ブースト中） / `IsHovering` / `IsGrounded`
- `PlanarSpeed` / `ResetLook` / `RebindCameraPivot`
