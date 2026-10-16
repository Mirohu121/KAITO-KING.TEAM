# 機体移動仕様（戦場の絆準拠）

対象スクリプト: `Assets/Scripts/Player/UnitBMotor.cs`  
カメラ: `Assets/Scripts/Player/CockpitCameraShake.cs` / `FpsHeadCameraFollow.cs`  
入力: `Assets/Scripts/Player/UnitPlayerInput.cs` / `UnitInputFrame.cs`  
Input Actions: `Assets/Input/UnitControls`  
HUD: `Assets/Scripts/UI/FuelGaugeHud.cs`  
戦闘α: `Documentation/Combat_Alpha_Spec.md`

---

## 1. 単位（Inspector は wiki の機体表の値をそのまま入れる）

| 絆の単位 | 意味 | 換算 |
|----------|------|------|
| km/h | 歩行・ダッシュ・ジャンプ速度 | `km/h ÷ 3.6 × worldScale` m/s |
| cnt | カウント（タイマー） | 1 cnt ≈ 0.9 秒 |
| f | フレーム（硬直） | 60 f = 1 cnt ≈ 0.9 秒 |
| rpm | 旋回性能 | rpm × 6 = 度/秒 |

`worldScale = 0.1` は「MS 18m → 骸骨 1.8m」。体格比で見た速さが絆と同じになる。  
例: ダッシュ 210 km/h × 2.07 秒 ≈ 120 m（絆のダッシュ距離 94〜168 m の範囲）→ ゲーム内 12 m。

---

## 2. 調べた絆の仕様と実装の対応

| 絆の仕様 | 実装 |
|----------|------|
| ブーストゲージはダッシュとジャンプで**共有**。減るにつれ青→黄→赤 | `_boost`（0〜100）を1本だけ持つ。HUD は1本表示で色が変わる |
| 0 になると**オーバーヒート**。100 に戻るまでダッシュもジャンプも不可 | `overheatRecoverCnt`（例: ジム 早 90F、ゾック 約4cnt、ヘビーガンダム 約7.5cnt） |
| ペダルを**両方とも完全に離した**瞬間から回復。空中でも回復する | `regenFullCnt`、空中は `airRegenScale` 倍 |
| ダッシュ時間 = 満タンから踏みっぱなしで空になる時間（2.2〜2.5 cnt） | `dashTimeCnt` |
| ダッシュ中もレバーで方向を変えられる（90°ターンなど） | `dashSteerDegPerSec` |
| ダッシュを終えると**ダッシュ硬直**。長く走るほど大きい | `dashStunMinFrames`〜`dashStunMaxFrames` をダッシュ時間で補間 |
| 格闘型はリミッター付き: 約1/4消費でダッシュが止まり、直後はしばらく回復しない | `boostLimiterPercent` / `regenLockAfterDashCnt`（既定 0 = なし） |
| ジャンプは踏んだ瞬間に浮き、踏み続けると上昇。消費は多め | `jumpLiftOnPress` + `jumpSpeedKmh`、`jumpFullTimeCnt`（ダッシュより短い） |
| **ふわジャン**: 踏んだ瞬間に消費。離しても少し上昇してから降下 | `jumpIgnitePercent` / `riseCarrySeconds` |
| ダッシュ+ジャンプで高速上昇。空中ダッシュは水平移動 | 両方とも消費（合算）。空中ダッシュ中の重力は `airDashGravityScale` 倍 |
| 着地硬直は滞空時間に比例。表の「硬直時間」は小ジャンプの値（29〜47 f） | `stunFrames`（小）→ `bigJumpStunFrames`（大）を `smallJumpAirCnt`〜`bigJumpAirCnt` で補間 |
| 硬直中は移動できないが、射撃・格闘・旋回はできる | 歩き・ペダルの入力を無視。`allowTurnWhileStunned` |
| **グラップル**: ダッシュの終わりにジャンプを入れると、ダッシュ硬直ではなく小ジャンプの硬直になる | 空中でダッシュが終わったときはダッシュ硬直を出さず、着地硬直に任せる（専用処理なしで成立する） |
| **着地キャン**: 着地の直前にブーストを踏むと着地硬直が出ない | ダッシュ中に着地 → 着地硬直なしでダッシュ継続 |
| 旋回は rpm で上限がある | 1フレームの旋回量を `turnRpm × 6 × dt` で頭打ち |
| 手動の上下視点はない。上下の照準は赤ロックが受け持つ | `allowManualPitch = false` のとき上下視点は 0° に戻る。ロック中だけ自動で上下を向く |
| 赤ロック中は自動で正面を向き、手動の旋回は効かない | `ApplyLockLook` |
| 歩くと振動がシートに伝わる（筐体のボディソニック） | `CockpitCameraShake`: 一歩ごとの沈み込み、着地の衝撃、ダッシュ停止のつんのめり、ブースト中の振動 |

推定値（wiki に数値がないもの）: 空中の回復倍率、OH 中に着地したときの追加硬直、ダッシュ硬直の幅、ふわジャンの持続時間。

---

## 3. 操作

| Action | キー | 絆 | 動作 |
|--------|------|----|------|
| Move | WASD | レバーを同じ方向に倒す | 歩き / 空中では慣性に少し上乗せ / ダッシュの方向 |
| Look | Mouse | レバーを逆方向に倒す | 旋回（rpm で上限あり） |
| Dash | Left Shift | 右ペダル | ダッシュ（地上でも空中でも） |
| Jump | Space | 左ペダル | 踏み続けると上昇、タップでふわジャン |
| LockOn | Q / 中クリック / RB | 右ロックボタン | 青ロック中に押すと赤ロック。もう一度押すと解除 |
| LockSwitch | E / パッド Y | 左ロックボタン（ロック送り） | 射程内・FCS内の次の敵へ。赤ロックなら赤ロックのまま |

### ロックオン（`UnitLockOn`）

| 状態 | 条件 | 効果 |
|------|------|------|
| 青 | 射程内（`lockRange` 22 = 220m）で、FCS（正面 ±35°）内に見えている敵 | 自動で捕捉する。遮蔽物に隠れると外れる |
| 赤 | 青ロック中に LockOn を押す | 自動で正面に捉え、手動の旋回はできない。遮蔽物越しでも維持し、射程の 1.08 倍より離れると解除 |
| 黄 | 表示している相手が `meleeLockRange`（4.2）以内 | マーカーが黄色になり、格闘がその相手の方向へ振られる |

マーカーの下に相手までの距離を絆の単位（m）で表示する。

---

## 4. プリセット（Inspector の右クリック → 絆プリセット）

A/B/C のプレハブには**自動では適用しない**。調整するときに手で選ぶ。

| プリセット | 歩き | ダッシュ | ダッシュ時間 | ジャンプ | 旋回 | 硬直 | OH回復 | 備考 |
|------------|------|----------|--------------|----------|------|------|--------|------|
| 近距離（標準・既定値） | 108 | 210 | 2.3 | 175 | 10.6 | 36f | 3.5 | ジム系 |
| 格闘（ショートダッシュ） | 111 | 236 | 4.4 | 186 | 12.0 | 33f | 3.0 | リミッター 25%、回復停止 1cnt |
| 射撃・支援（重め） | 100 | 187 | 2.4 | 162 | 10.3 | 45f | 7.5 | ヘビーガンダム / ゾック寄り |

---

## 5. 公開 API

- ゲージ: `BoostNormalized` / `IsOverheated` / `UsesSharedBoostGauge`
- 状態: `IsDashing` / `IsJumpThrusting` / `IsStunned` / `StunRemaining` / `IsGrounded`
- 速度: `PlanarVelocity` / `PlanarSpeed` / `WalkSpeed`
- イベント: `Footstep(float)` / `Landed(float)` / `DashBraked(float)` / `Overheated`
- 旧名（共有ゲージを返す）: `JumpBoostFuelNormalized` / `DashJetFuelNormalized` / `IsJumpBoostActive` / `IsDashJetActive` / `IsHovering`
- `SetInputSource` / `RebindCameraPivot` / `ResetLook`

---

## 6. リトライ → 機体選択

`CombatAlphaBootstrap.RestartToUnitSelect()` が試合を片付けてから機体選択を再表示する。  
ポーズの Restart とリザルトの「リトライ」から呼ぶ（`SceneManager.LoadScene` は使わない）。

---

## 7. 出典

- 戦場の絆 公式（ブーストゲージ・オーバーヒート・操作）: https://gundam-kizuna.jp/
- 戦場の絆II 公式（ジャンプの浮力・ゲージの色）: https://kizuna2.gundam-kizuna.jp/
- 戦場の絆 @wiki（機体表・硬直・グラップル・ふわジャン・リミッター）: https://w.atwiki.jp/senjounokizuna/
