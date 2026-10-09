## 한국어 요약

- **목표:** MR TABLE에서 내 방(바닥·벽·테이블·소파 등)을 읽어 섬 지형을 만들 수 있는지 확인한다. 이번 라운드는 **가능성 확인용 프로브(스파이크)**만 만들었고, 지형 생성은 아직 하지 않는다.
- **바뀐 것(파일):**
  - Core `RoomScan.cs`: 언제 권한을 묻고 매니저를 켤지(`RoomScanFlow`), 첫 결과까지 시간(`RoomScanTimer`), 로그 한 줄 요약(`RoomScanSummary`). 테스트 22개(`RoomScanTests`).
  - `RoomScanProbe.cs`(새 MonoBehaviour), `PerfSample`/`PerfSpikeLogger`(`ROOM_SCAN` 기록 + 짧은 메모), 씬 빌더(XR Origin에 꺼진 `ARPlaneManager`·`ARBoundingBoxManager`, Match에 프로브), `RoomScanXRSettings.cs`(에디터 스크립트로 Android OpenXR 기능 2개 켜기).
  - `Assets/XR/Settings/OpenXRPackageSettings.asset`: `ARPlaneFeature Android`(Meta Quest: Planes)와 `ARBoundingBoxFeature Android`(Meta Quest: Bounding Boxes)의 `m_enabled` 0 → 1. 이것만 바뀌었다. 매니페스트는 손대지 않았다(Meta 패키지가 빌드 때 `USE_SCENE`·`USE_ANCHOR_API`를 넣는다).
- **결정:**
  - D1: 평면 + 바운딩 박스만. 메시(Meshing)는 끈다. 이유: 메시는 프리팹·렌더링이 더 필요하고 방 형태 그대로의 원시 데이터라 개인정보 면에서도 무겁다. 지형 1단계에는 평면·박스로 충분하다.
  - D2: 프로브는 **MR TABLE + 메인 메뉴**에서만 돈다. 매치·런이 시작되면 멈춘다(게임 중 프레임 영향 0).
  - D3: 권한은 MR TABLE을 처음 켤 때 한 번만 묻는다. 거부하면 이번 실행에서는 다시 묻지 않고 MR TABLE은 지금과 똑같다.
  - D4: 기록은 개수·분류·크기·시간만. 위치 좌표·경계 다각형·메시는 기록하지 않는다.
- **테스트·검증:** Core TDD(빨강 확인 후 초록), 전체 598/598, 컴파일, 씬 재생성(배선 확인). 헤드셋 확인은 없다.
- **기기에서 확인할 것:** Quest 설정 > 물리적 공간 > 공간 설정(Space Setup)을 먼저 한다 → 앱에서 MR TABLE → 권한 허용 → 개발 빌드에서 초록(평면)·노랑(박스) 선이 보이는지, `perf_log.txt`의 `ROOM_SCAN` 줄. 거부했을 때 MR TABLE이 평소대로인지.
- **위험:** 권한 대화상자가 메뉴에서 포커스를 잠깐 가져간다(메뉴에서는 일시정지 영향 없음). 기능을 켜면 앱 시작 때 OpenXR 확장 몇 개를 더 요청한다(런타임이 거부하면 기능만 꺼지고 앱은 정상).

# Room-based terrain: research and the S8 room-scan probe

Round 4, task S8 (decision D9). Status: probe implemented, not yet device-tested.

## 1. The question

Can MR TABLE mode get the player's room layout (planes such as floor, walls and table; bounding boxes of furniture with classifications; or the room mesh), how fast, and in what shape, so that a later round can generate island terrain from it?

## 2. What is installed (verified in `Library/PackageCache`)

- Unity OpenXR: Meta 2.6.1 (`com.unity.xr.meta-openxr@20c9b19f5e28`), AR Foundation 6.6.2 (`com.unity.xr.arfoundation@d57b0b4d336a`). No new package was added.
- Meta features (namespace `UnityEngine.XR.OpenXR.Features.Meta`):
  - `ARPlaneFeature` ("Meta Quest: Planes", id `com.unity.openxr.feature.arfoundation-meta-plane`), creates `MetaOpenXRPlaneSubsystem`. Provider type `XrFbScene` (default: Space Setup's Bounded2D components: tables, couches, wall art, simplified walls/floor/ceiling) or `XrMetaSpatialEntityRoomMesh` (room mesh faces, more detailed walls).
  - `ARBoundingBoxFeature` ("Meta Quest: Bounding Boxes", `...arfoundation-meta-bounding-boxes`), creates `MetaOpenXRBoundingBoxSubsystem`.
  - `ARMeshFeature` ("Meta Quest: Meshing", `...arfoundation-meta-mesh`): exists, left off.
- Both features return `false` from `OnInstanceCreate` when the runtime lacks the extensions or capability; the feature is then disabled and no subsystem is created. They are not required features, so the OpenXR loader still starts (VR arena and passthrough unaffected).
- Permission: both subsystems warn when `com.oculus.permission.USE_SCENE` is not granted. Meta's editor build step `ModifyAndroidManifest` adds `USE_SCENE` and `USE_ANCHOR_API` to the manifest when either feature is on. The runtime request is the app's job (`UnityEngine.Android.Permission.RequestUserPermission`).
- Important Meta behaviour (package docs): the runtime does **not** scan live. It returns what is stored in the headset's Scene Model from **Space Setup**. Without Space Setup there are no planes or boxes. `MetaOpenXRSessionSubsystem.TryRequestSceneCapture()` can launch Space Setup from the app (the app is suspended meanwhile); the probe does not call it.
- AR Foundation managers (`ARPlaneManager`, `ARBoundingBoxManager`) carry `[RequireComponent(typeof(XROrigin))]`, so they sit on the XR Origin. Trackables are spawned under `XROrigin.TrackablesParent`, which follows the Camera Offset, i.e. tracking space in real meters.

## 3. What the probe does

`RoomScanProbe` (Match object, Arena_Main only):

1. Runs only while the view is MR TABLE **and** the match is in the main menu. VR arena, Quick Match, the tutorial and RUN never touch it; a match start stops it.
2. Checks once whether a plane or bounding box subsystem is loaded. If neither: logs `UNAVAILABLE`, does nothing more.
3. Asks for `USE_SCENE` once per app session. Denied: logs `PERMISSION_DENIED`, never asks again this session, managers stay off. MR TABLE behaves as before.
4. Granted: enables the managers (`STARTED`). If AR Foundation disables a manager because its subsystem failed to start, or enabling throws, the probe turns itself off for the session and logs why.
5. Logs `FIRST_PLANES` / `FIRST_BOXES` with the count and seconds since the managers started, then a `SUMMARY` line 3 s after the last change and again when it stops.
6. Dev builds and the editor draw an outline per plane (green) and box (yellow); release builds do not (serialized `wireframe`: Off / DevBuildsOnly / Always).

Every record goes to `perf_log.txt` through `PerfSpikeLogger.Mark(PerfRecordKind.RoomScan, event, note)` and to the Unity log with the `[RoomScanProbe]` prefix. Example (shape, not real data):

```
12:01:02.345 t=40.120 ROOM_SCAN scan=STARTED frame=13.9 ... head=1 | planes_subsystem=1 boxes_subsystem=1
12:01:02.611 t=40.386 ROOM_SCAN scan=FIRST_PLANES ... | count=7 after=0.27s
12:01:05.700 t=43.475 ROOM_SCAN scan=SUMMARY ... | planes=7 [Ceiling:1,Floor:1,Table:1,WallFace:4] boxes=2 [Couch:1,Table:1] first_plane=0.27s first_box=0.31s near_table=Table(box) 1.20x0.60m area=0.72 dy=0.03 d=0.18
```

`near_table` is the largest up-facing surface (plane, or the top of a box) whose center lies within 1.0 m horizontally and 0.5 m vertically of the point where the virtual arena floor appears; `dy` is its height above that point and `d` its horizontal distance (real meters). This tells us how far the real table is from where we draw the arena today.

## 4. How scanned surfaces could become tabletop terrain (proposal, next rounds)

The arena keeps running in full-size arena units (35 x 20 x 35); MR TABLE only scales the viewer. Room data is in real meters in tracking space, so every conversion is `arena = (tracking - tablePointTracking) * WorldScale` around the chosen table.

1. **Snap the arena to the real table.** Pick the `near_table` surface (prefer a `Table` box top, else a `Table` plane, else the largest up-facing surface). Place the apparent arena floor on it: set `TabletopParams.CenterFromEye` from the table center, and `TableWidth` from the shorter table side (clamped, e.g. 0.6-1.2 m). This changes only the existing viewer scale and Camera Offset, never the XR Origin pose (ADR 4/5 still hold).
2. **Table edge = arena edge.** The table's footprint becomes the arena bounds: `ArenaBounds` from the table width/depth times `WorldScale`, so flying off the table edge is the wall. A non-square table gives a rectangular island.
3. **Furniture boxes -> cover.** Boxes whose footprint overlaps the table (lamps, plants, monitors as "Other") become pillar-like cover at their true footprint and a clamped height. Boxes beside the table (couch, bed) are out of the arena and could become "far islands" in the backdrop.
4. **Feed the existing layout rules.** `ArenaLayout` (S7) already takes piece sizes, keep-clear spheres and a line-of-sight rule. Room-derived cover goes in as fixed pieces; the seeded random pieces fill the rest under the same fairness rules (clear start and spawns, one open line of sight). If the room gives an unfair layout, fall back to the seeded one.
5. **Walls and floor.** Walls and the floor are outside the table arena; at most they set the backdrop (e.g. which side is open). Not needed for the first terrain slice.
6. **Always optional.** No permission, no Space Setup, no table found: the seeded layout from S7, exactly as today.

## 5. Privacy

- Scene data never leaves the headset. Nothing is uploaded; the app has no network code for it.
- The logs hold only counts, classification names, sizes (meters), times and one height/distance of the table surface. No positions, boundary polygons, meshes, images or trackable ids are written.
- The permission is asked only when the player chooses MR TABLE, with the system dialog. Denying it changes nothing in the game.
- A future terrain feature should keep this rule: derive terrain at runtime, store at most a table size, never the room.

## 6. Space Setup steps for Hyun (device test)

1. On the Quest 3: Settings > Physical Space > Space Setup. Scan the room, then mark at least the table you play on (Add furniture > Table), and if you like a couch or lamp.
2. Install the **dev** APK (wireframe on). Start Hand Hero, press **MR TABLE** in the main menu.
3. Allow the "spatial data" permission when asked. Green outlines = planes, yellow = boxes; the table should be among them.
4. Wait about 5 s in the menu, then back to VR ARENA or start a match (that writes the summary). Pull the log:
   `& "$env:LOCALAPPDATA\Android\Sdk\platform-tools\adb.exe" pull /sdcard/Android/data/<package>/files/perf_log.txt`
   and look for `ROOM_SCAN` lines.
5. Second check: clear the app's permission (Settings > Apps > Hand Hero > Permissions > deny) or reinstall, choose MR TABLE, **deny**: MR TABLE, Quick Match and RUN must work as before, and the log shows `PERMISSION_DENIED`.

## 7. Open questions (with recommended answers)

| # | Question | Recommended answer |
|---|---|---|
| Q1 | Should the game launch Space Setup itself when nothing is found (`TryRequestSceneCapture`)? | Not yet. It suspends the app; first see on device how many players already have Space Setup. Later: a "SCAN ROOM" button in the MR TABLE menu only. |
| Q2 | Plane provider `XrFbScene` or room mesh? | `XrFbScene` (default). The table and furniture come from it; room-mesh faces only add detail to walls, which the table arena does not use. |
| Q3 | Use Meshing for terrain? | No for the next slice. Boxes and the table plane are enough; meshing is heavier and closer to raw room geometry. Revisit for occlusion only. |
| Q4 | Which surface is "the table" if there are several? | The largest up-facing `Table` surface within reach of the seated player (about 1 m); else none, use the seeded layout. |
| Q5 | Snap the arena to the table automatically or on a button? | A button in the MR TABLE menu ("FIT TO TABLE"), so the arena never jumps unexpectedly; remember the choice per session only. |
| Q6 | Keep the probe in release builds? | Keep the logging (cheap, menu only), keep the wireframe off in release. Remove the probe when the real feature lands. |
| Q7 | Do furniture boxes stay aligned during long sessions? | Unknown; Meta warns that trackables can drift. Measure on device: compare `near_table` at session start and after a run. |
