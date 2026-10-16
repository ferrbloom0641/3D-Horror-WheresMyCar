# Parking Garage NavMesh 구현 진행표

## 1. 문서 목적

이 문서는 `NAVMESH_SYSTEM_SPEC.md`를 실제 구현 순서로 변환한 작업 기준서다.

앞으로 NavMesh 시스템 작업은 이 문서의 순서와 상태를 기준으로 진행한다.
ㅁㅁㅁㅁㅁㅁㅁㅁㅁㅁㅁㅁㅁㅁㅁㅁㅁ
## 작업리스트 (상단 바로가기)

현재 작업 위치와 단계별 상태를 먼저 확인하는 요약표다. 상세 절차와 완료 조건은 아래 [구현 순서 상세표](#5-구현-순서)에서 확인한다.

| 단계 | 작업 ID 범위 | 작업자 | 현재 상태 | 다음 확인 |
|---|---|---|---|---|
| 단계 0. 구현 기준 | `P-01 ~ P-05` | Codex/사용자 | 해결됨 | 완료 |
| 단계 1. 층 데이터/관리자 | `F-01 ~ F-06` | Codex/사용자 | 해결됨 | 완료 |
| 단계 2. 4층 Proxy/Bake | `N-01 ~ N-07` | Codex/사용자 | 해결됨 | 완료 |
| 단계 3. 기본 인형 NavMesh | `D-01 ~ D-04` | Codex | 해결됨 | 완료 |
| 단계 3. 기본 인형 연결/검증 | `D-05 ~ D-06` | Codex/사용자 | 해결됨 | 완료 |
| 단계 3. 기본 인형 외형 보정 | `D-07` | 사용자 | 해결됨 | 완료 |
| 단계 4. Preload/Commit | `T-01 ~ T-03` | Codex | 해결됨 | 완료 |
| 단계 4. Preload/Commit | `T-04` | 사용자 | 해결됨 | 4F↔1F 전체 하강·상승 Commit 검증 완료 |
| 단계 4. Preload/Commit | `T-05` | 사용자 | 해결됨 | 4F↔1F 전체 왕복과 중복 Commit 없음 확인 |
| 단계 5. Spawn Point | `S-01 ~ S-03` | Codex | 해결됨 | S-04 외곽 지점 배치 |
| 단계 5. Spawn Point 배치 | `S-04` | 사용자 | 해결됨 | S-05 참조 연결 |
| 단계 5. Spawn Point 참조 연결 | `S-05` | 사용자 | 해결됨 | S-06 시야 밖 검증 |
| 단계 5. Spawn Point 검증 | `S-06` | 사용자 | 해결됨 | C-03 연결 후 실제 생성 재검증 |
| 단계 6. 자동차/인형 누적 | `C-01 ~ C-03` | Codex | 해결됨 | C-04 시작 인형 0개 + 실패 생성 최대 6개 제한 |
| 단계 6. 자동차/인형 누적 | `C-04` | Codex | 진행 중 | 시작 0개 + 실패 생성 6개 규칙 반영 |
| 단계 6. 자동차 상호작용 | `C-05A ~ C-05C` | Codex | 해결됨 | C-05D 자동차/플레이어 연결 |
| 단계 6. 자동차 연결 | `C-05D` | 사용자 | 해결됨 | 현재 검증용 자동차 1대와 플레이어 Inspector 연결 완료 |
| 단계 6. 자동차 10대 연결 | `C-05E` | Codex/사용자 | 해결됨 | 10대 기믹·층 배열 연결, 탈출 차량 정답 처리 Play Mode 확인 |
| 단계 6. 탈출 연출 확장 | `C-05F` | Codex/사용자 | 대기 | 차량 탑승·주차장 탈출 연출 기획 확정 후 성공 이벤트에 연결 |
| 단계 6. 자동차 검증 | `C-06` | 사용자 | 해결됨 | 현재 검증용 자동차 1대 Play Mode 테스트 완료 |
| 단계 7. 단일 공격권 | `A-01` | Codex | 해결됨 | 가장 가까운 공격 후보 1개만 예약 |
| 단계 7. 공격권 유지 | `A-02` | Codex | 해결됨 | 공격 완료/제거/경로 단절 시 공격권 해제 |
| 단계 7. 목적지 분산/회피 | `A-03` | Codex | 해결됨 | 비공격 인형의 접근 지점 분산과 Agent 회피 적용 |
| 단계 7. 층 전환/GameOver 잠금 | `A-04` | Codex | 해결됨 | 전환·GameOver 중 공격권 획득/유지 차단 |
| 단계 7. 다중 인형 검증 | `A-05` | 사용자 | 해결됨 | 완료 |
| 단계 8. 인접층 확장 | `X-01 ~ X-07` | Codex/사용자 | 해결됨 | 완료 |
| 단계 8. 인접층 확장 오류 보정 | `X-06-1` | Codex/사용자 | 해결됨 | 3F Proxy Mesh 복구, NavMeshData 재Bake, 안전 재배치 확인 |
| 단계 9. Restart 통합 | `R-01 ~ R-04` | Codex/사용자 | 진행 중 | 새 시작 인형 0개 기준 재검증 |
| 단계 10. 최종 검증 | `V-01 ~ V-05` | Codex/사용자 | 보류 | 남은 개별 검증을 디테일 작업 후 최종 통합 플레이로 이관 |
| 단계 11. 플레이어 계단 이동 보정 | `M-00 ~ M-03` | Codex/사용자 | 진행 중 | CharacterController 단차 시험 또는 계단 Ramp 배치 후 이동 검증 |

현재 다음 작업: 디테일 보정 — 반복해서 출력되는 층 전환 Preload 경고와 남은 연출 디테일을 정리한다. 이후 V-04 미실행 항목을 포함해 최종 통합 플레이를 한 번 수행한다. C-05F 탈출 연출은 기획 확정 후 진행한다.

### 최근 점검: 플레이어 눈높이와 중앙 계단

- `Parking/Player/Main Camera`는 `HorrorPlayerController`가 런타임에 플레이어 자식으로 배치한다. `CharacterController` 높이 2m 기준 발바닥에서 카메라까지 `1.72m`가 되도록 `HorrorPlayerController`에서 자동 보정했다. 기존 `0.65m` 값은 런타임에 `0.72m` 로컬 Y로 보정된다.
- 중앙 나선계단으로 추정되는 `Parkgarage_prefab`의 `stairwell`에는 활성 비트리거 `MeshCollider`가 있어 플레이어가 물리적으로 계단을 밟고 이동할 수 있다.
- 다만 저장된 씬의 플레이어용 `FloorTransitionTrigger`는 현재 `Target Floor ID = 3`과 `4`만 존재한다. 따라서 4F→3F는 가능하지만 3F→2F와 2F→1F는 하위 Trigger 보완 후에야 층 전환까지 완성된다.

## 2. 상태 작성 규칙

### 사용자 안내 형식 규칙

- 사용자가 수정해야 하는 값만 안내한다. 이미 정상인 값, 유지할 값, 변경하지 않을 값은 목록에 넣지 않는다.
- Inspector 값은 실제 표시 순서대로 나열하고, 각 항목의 정확한 Hierarchy 위치와 컴포넌트 이름을 함께 적는다.
- 확인 결과나 오류 원인을 먼저 제시한 뒤 수정 항목을 안내한다.

| 상태 | 의미 |
|---|---|
| `대기` | 선행 작업이 끝나지 않아 아직 시작하지 않음 |
| `진행 중` | 현재 작업 중 |
| `확인 필요` | 작업은 끝났으나 담당자의 검증 또는 응답이 필요함 |
| `해결됨` | 완료 조건과 검증을 모두 충족함 |
| `막힘` | 필요한 정보나 에셋이 없어 진행할 수 없음 |

상태 변경 원칙:

- Codex 담당 작업은 Codex가 구현과 가능한 검증을 마친 뒤 `해결됨`으로 변경한다.
- 사용자 담당 작업은 사용자가 완료했다고 말하기 전까지 `해결됨`으로 변경하지 않는다.
- 사용자가 완료했다고 말하면 Codex가 관련 연결 상태를 확인하고 이 문서에 `해결됨`으로 기록한다.
- 선행 작업이 해결되지 않은 항목은 임의로 건너뛰지 않는다.
- 계획이나 기획이 변경되면 먼저 이 문서를 수정한 뒤 구현한다.
- 각 작업의 완료 조건을 만족하지 않으면 파일을 만들었더라도 `해결됨`으로 처리하지 않는다.

### 현재 기획 변경 기록 (2026-08-27)

사용자 결정에 따라 인형 누적 규칙을 다음과 같이 변경한다.

- 새 게임 시작 시 활성 인형: `0개`
- 자동차 실패 1회마다 런타임 인형: `1개`
- 자동차 실패로 생성되는 인형 총 상한: `6개`
- 시작 인형 오브젝트: 삭제하지 않고 비활성 복제 템플릿으로 유지
- 기존의 `기본 인형 1개 + 추가 5개` 기록은 참고용이며, 최종 검증은 새 규칙으로 수행한다.

## 3. 확인된 현재 프로젝트 상태

| ID | 확인 내용 | 담당 | 상태 |
|---|---|---|---|
| B-01 | `NAVMESH_SYSTEM_SPEC.md` 전체 검토 | Codex | 해결됨 |
| B-02 | Unity 버전 확인: `6000.3.18f1` | Codex | 해결됨 |
| B-03 | AI Navigation 패키지 확인: `com.unity.ai.navigation 2.0.13` | Codex | 해결됨 |
| B-04 | 현재 메인 씬 확인: `Assets/Scenes/Parking.unity` | Codex | 해결됨 |
| B-05 | 현재 씬에 저장된 NavMeshData가 없음을 확인 | Codex | 해결됨 |
| B-06 | 기존 `CeilingDollController`가 NavMeshAgent 없이 Raycast와 직접 위치 이동을 사용하는 상태 확인 | Codex | 해결됨 |
| B-07 | 현재 Restart가 씬 전체 재로드 방식임을 확인 | Codex | 해결됨 |
| B-08 | 구현 진행표 작성 | Codex | 해결됨 |

## 4. 역할 분담 원칙

### Codex가 담당하는 일

- 런타임 구조와 C# 코드 구현
- 데이터 상태, 중복 방지, 수명 관리와 예외 처리
- 인형 AI를 NavMeshAgent 기반으로 전환
- Spawn, 자동차 기믹, 공격권, 층 이전, Restart 시스템 통합
- 코드 수준 검증과 가능한 자동 테스트
- Inspector 연결 목록과 사용자 검증 절차 작성
- 완료한 Codex 작업을 이 문서에 `해결됨`으로 갱신

### 사용자가 담당하는 일

- Unity Editor에서 실제 공간을 보고 결정해야 하는 층 범위와 높이 확정
- Proxy Mesh 제작과 제외 영역 다듬기
- Spawn, Exit, Wall Entry, Trigger 위치 배치
- 층별 NavMesh Bake와 육안 검증
- 자동차, 클랙슨, 인형 프리팹, 애니메이션 등 실제 에셋 연결
- Play Mode에서 체감, 시야, 연출과 동선 검증
- 완료한 작업을 Codex에게 알려 상태 갱신 요청

## 5. 구현 순서

### 단계 0. 구현 기준 확정

| 순서 | ID | 작업 | 담당 | 완료 조건 | 상태 |
|---:|---|---|---|---|---|
| 0.1 | P-01 | 시스템 명세와 현재 코드의 차이 분석 | Codex | 현재 상태가 3절에 기록됨 | 해결됨 |
| 0.2 | P-02 | 구현 역할과 선후 관계 문서화 | Codex | 이 진행표에 전체 단계가 기록됨 | 해결됨 |
| 0.3 | P-03 | 실제 사용할 층 수와 Floor ID 확정 | 사용자 | `4F`, `3F`, `2F`, `1F`의 4개 층으로 확정 | 해결됨 |
| 0.4 | P-04 | 각 층의 기준 높이 범위 확정 | 사용자 | 층별 `FloorReference`의 천장 기준 Y 값 전달 | 해결됨 |
| 0.5 | P-05 | 첫 검증 범위 확정 | 사용자 | `4층 + 시작 인형 0개 + 자동차 실패 생성 최대 6개`로 확정 | 해결됨 |

단계 0 사용자 전달 형식:

```text
층 목록: 예) 4F, 3F, 2F, 1F
층 높이: 예) 4F Y 9~12, 3F Y 6~9 ...
첫 검증 범위: 기본값 사용 / 변경 내용
```

확정된 층별 천장 기준 높이:

| Floor ID | 기준 오브젝트 | Y |
|---|---|---:|
| `4F` | `FloorReference_4F` | `13.006` |
| `3F` | `FloorReference_3F` | `9.561` |
| `2F` | `FloorReference_2F` | `6.081` |
| `1F` | `FloorReference_1F` | `2.776` |

이 값은 층별 천장 NavMesh와 인형 위치를 검증하는 기준이다. 플레이어의 현재 층은 높이만으로 변경하지 않고 Commit Trigger로 확정한다. 층별 NavMesh 검증 범위의 내부 경계는 인접한 두 `FloorReference` Y 값의 중간 높이를 기본값으로 사용한다.

### 단계 1. 층 데이터와 런타임 관리자

| 순서 | ID | 작업 | 담당 | 완료 조건 | 상태 |
|---:|---|---|---|---|---|
| 1.1 | F-01 | `FloorId`, 층 상태, 자동차 상태, 인형 상태 정의 | Codex | 명세의 상태가 코드에서 표현되고 컴파일됨 | 해결됨 |
| 1.2 | F-02 | `FloorController` 구현 | Codex | 층 ID, 높이, NavMeshData, 지점과 Trigger 참조를 보유 | 해결됨 |
| 1.3 | F-03 | `GameProgressController` 구현 | Codex | 현재 층, 실패 수, 전환, GameOver, Restart 상태를 단일 소유 | 해결됨 |
| 1.4 | F-04 | `FloorNavigationManager` 구현 | Codex | NavMeshData 등록/해제, 중복 방지, Instance 수명 관리 완료 | 해결됨 |
| 1.5 | F-05 | 현재층과 인접층 활성화 규칙 구현 | Codex | 최상/최하층을 포함해 최대 3개 층만 등록 | 해결됨 |
| 1.6 | F-06 | 시스템 루트 오브젝트를 씬에 배치하고 참조 연결 | 사용자 | NavMeshSystem, 4개 FloorController/Reference, GameProgressController, FloorNavigationManager 연결 완료 확인 | 해결됨 |

#### F-06 사용자 실행 절차

F-06은 `Assets/Scenes/Parking.unity`에서 수행한다. 이 단계에서는 씬 계층과 관리자 참조만 연결한다. Proxy Mesh 제작, NavMesh Bake, Spawn/Exit Point 배치와 Trigger 배치는 각각 N-03~N-07, S-04~S-06, T-04에서 진행하므로 아직 억지로 채우지 않는다.

1. `Parking.unity`를 열고 Hierarchy 빈 공간에서 `Create Empty`로 `NavMeshSystem`을 만든다.
2. `NavMeshSystem`의 Transform을 Position `(0, 0, 0)`, Rotation `(0, 0, 0)`, Scale `(1, 1, 1)`로 둔다.
3. `NavMeshSystem`에 `GameProgressController`와 `FloorNavigationManager` 컴포넌트를 추가한다.
4. `NavMeshSystem`의 자식으로 빈 오브젝트 `Floors`를 만들고 Transform을 모두 기본값으로 둔다.
5. `Floors`의 자식으로 다음 네 개의 빈 오브젝트를 만든다.

   ```text
   Floor_4F
   Floor_3F
   Floor_2F
   Floor_1F
   ```

6. 각 `Floor_XF`에 `FloorController` 컴포넌트를 하나씩 추가한다. Floor 루트는 실제 주차장 Geometry의 자식으로 넣지 않고, 시스템 데이터용 루트로 유지한다.
7. 각 Floor 루트의 `FloorController`에서 `Floor ID`를 다음처럼 지정한다.

   | 오브젝트 | Floor ID |
   |---|---|
   | `Floor_4F` | `Floor4F` |
   | `Floor_3F` | `Floor3F` |
   | `Floor_2F` | `Floor2F` |
   | `Floor_1F` | `Floor1F` |

8. 각 Floor 루트의 자식으로 빈 오브젝트를 만들고 다음 이름으로 지정한다.

   ```text
   FloorReference_4F
   FloorReference_3F
   FloorReference_2F
   FloorReference_1F
   ```

   이 오브젝트는 라이트의 자식으로 넣지 않는다. 대표 천장 라이트와 같은 위치를 참고하되, 시스템 데이터 계층인 `Floor_XF`의 자식으로 둔다.

9. `FloorReference`의 월드 Y를 다음 값으로 설정한다. X/Z는 대표 천장 라이트를 식별하기 쉬운 위치에 둔다.

   | Reference | World Y |
   |---|---:|
   | `FloorReference_4F` | `13.006` |
   | `FloorReference_3F` | `9.561` |
   | `FloorReference_2F` | `6.081` |
   | `FloorReference_1F` | `2.776` |

10. 각 `FloorController`의 `Floor Reference` 슬롯에 해당 Reference 오브젝트를 드래그한다.
11. `Navigation Min Y`와 `Navigation Max Y`는 Proxy Mesh의 실제 높이를 확인한 뒤 설정한다. Reference Y를 그대로 최소/최대값 양쪽에 복사하지 않는다. 인접 Reference 사이의 중간값은 초기 경계 후보로 사용할 수 있지만, 최종값은 Bake 후 NavMesh가 포함하는 Y 범위를 기준으로 조정한다.
12. `GameProgressController`를 다음처럼 설정한다.

   | 필드 | 값 |
   |---|---|
   | `Starting Floor Id` | `Floor4F` |
   | `Starting Floor Controller` | `Floor_4F` 드래그 |
   | `Doll Limit` | `6` |
   | `Initialize On Awake` | 체크 |

13. `FloorNavigationManager`를 다음처럼 설정한다.

   | 필드 | 값 |
   |---|---|
   | `Initial Floor Id` | `Floor4F` |
   | `Initialize On Enable` | 체크 |
   | `Auto Sync With Progress` | 체크 |
   | `Floor Controllers` | `Floor_4F`, `Floor_3F`, `Floor_2F`, `Floor_1F`를 각각 한 번씩 등록 |

14. `Floor Controllers` 배열에는 같은 Floor ID를 중복 등록하지 않는다. 순서는 4F→3F→2F→1F를 권장한다.
15. 이 단계에서는 아직 `Baked NavMesh Data`, `Ceiling Proxy Mesh Root`, Spawn/Exit/Wall Entry Point, 자동차 기믹과 Preload/Commit Trigger 슬롯을 채우지 않아도 된다. 해당 참조가 연결되기 전에는 Play Mode에서 NavMeshData 누락 로그가 나오는 것이 정상이다.
16. Hierarchy를 저장하고 사용자 확인용으로 다음 항목을 점검한다.

   - `NavMeshSystem`이 씬에 하나만 있다.
   - `FloorController`가 4개이고 Floor ID가 서로 다르다.
   - 네 개의 Floor Reference가 올바른 층 Controller의 자식이다.
   - Reference Y 값이 확정값과 일치한다.
   - `GameProgressController.Starting Floor Controller`가 `Floor_4F`를 가리킨다.
   - `FloorNavigationManager.Floor Controllers`에 네 층이 모두 한 번씩 들어 있다.

완료했다고 알려줄 때 다음 형식을 사용한다.

```text
F-06 완료
NavMeshSystem 생성: 완료
FloorController 4개 연결: 완료
FloorReference 4개와 Y값 연결: 완료
GameProgressController 연결: 완료
FloorNavigationManager 4개 층 연결: 완료
```

### 단계 2. 4층 Proxy Mesh와 사전 Bake

| 순서 | ID | 작업 | 담당 | 완료 조건 | 상태 |
|---:|---|---|---|---|---|
| 2.1 | N-01 | 인형 전용 NavMesh Layer와 Agent Type 설정값 제안 | Codex | 이름, 반지름, 높이, 경사, Step 값이 문서화됨 | 해결됨 |
| 2.2 | N-02 | 인형 전용 Layer와 Agent Type 생성 | 사용자 | `DollNavMesh` Layer와 `DollCeiling` Agent Type 및 초기값 설정 완료 확인 | 해결됨 |
| 2.3 | N-03 | 4층 천장 아래 Proxy Mesh 제작 | 사용자 | 4층 이동 영역, 제외 구간, `DollNavMesh` Layer와 FloorController 연결 완료 확인 | 해결됨 |
| 2.4 | N-04 | 기둥, 계단, 램프 빈 공간, 천장 없는 곳, 출입구 제외 | 사용자 | Scene 뷰에서 제외 영역 적용 및 확인 완료 | 해결됨 |
| 2.5 | N-05 | 4층 전용 NavMeshSurface 설정과 Bake | 사용자 | `DollCeiling` NavMeshData 저장·FloorController 연결·런타임 Surface 비활성화 완료 확인 | 해결됨 |
| 2.6 | N-06 | 4층 Bake 결과 연결 검사 도구 또는 점검 코드 작성 | Codex | 다른 층/바닥 연결과 샘플 경로를 검사 가능 | 해결됨 |
| 2.7 | N-07 | 4층 NavMesh 육안 검증 | 사용자 | 기둥, 빈 공간, 바닥으로 잘못 이어진 영역이 없다고 확인 | 해결됨 |

#### N-06 구현 내용과 사용 절차

`Assets/Scripts/FloorNavMeshValidator.cs`에 런타임 점검 컴포넌트를 추가했다. 이 컴포넌트는 Bake를 다시 수행하거나 NavMeshData를 수정하지 않고 다음 항목만 검사한다.

- `FloorController`에 사전 Bake된 NavMeshData가 연결되어 있는지 확인
- `FloorNavigationManager`가 해당 Floor ID를 등록했는지 확인
- `DollCeiling` Agent Type이 존재하는지 확인
- `FloorReference`와 Proxy Mesh Bounds 주변을 샘플링해 NavMesh를 찾는지 확인
- 샘플 결과가 해당 층의 `Navigation Min Y ~ Navigation Max Y` 안에 있는지 확인
- 서로 다른 유효 샘플 사이의 경로가 `PathComplete`인지 확인
- 경로 코너가 다른 층이나 바닥의 Y 범위로 벗어나지 않는지 확인

N-06 도구 연결과 실행은 사용자가 N-07 육안 검증을 시작할 때 수행한다.

1. Hierarchy에서 `NavMeshSystem`을 선택하고 `FloorNavMeshValidator` 컴포넌트를 추가한다.
2. `Floor Controller`에 `Floor_4F`를 드래그한다.
3. `Navigation Manager`에 같은 오브젝트의 `FloorNavigationManager`를 드래그한다. 비워 두면 컴포넌트가 같은 오브젝트와 씬에서 자동으로 찾는다.
4. `Agent Type Name`은 `DollCeiling`으로 둔다.
5. `Sample Radius`는 `1.5`, `Floor Height Tolerance`는 `0.35`로 시작한다. 실제 Proxy Mesh 폭이나 층 경계에 맞춰야 할 때만 조정한다.
6. `Parking.unity`를 Play Mode로 실행한다. `FloorNavigationManager`가 4층 NavMeshData를 등록한 뒤 `NavMeshSystem`의 `FloorNavMeshValidator` 컴포넌트 메뉴에서 `Validate Floor NavMesh`를 실행한다.
7. Console에 `4F NavMesh 검증 PASS`가 출력되면 N-06 코드 점검을 통과한 것이다. `FAIL`이면 메시지의 누락 참조, 등록 여부, 높이 범위, 경로 실패 원인을 수정한 뒤 다시 검사한다.

`Validate On Start`는 기본적으로 끈다. Play Mode 시작 순서에 따라 Manager 등록보다 먼저 검사하는 것을 방지하기 위해 수동 Context Menu 실행을 기본으로 사용한다.

완료 조건:

- `FloorNavMeshValidator.cs`가 추가되고 컴파일 확인됨.
- 런타임 Bake 호출 없이 등록 상태와 샘플 경로를 검사함.
- 4층 검사 실패 시 다른 층/바닥으로 이어진 높이 이탈을 오류로 보고함.
- 진행표의 N-06 상태를 `해결됨`으로 갱신함.

#### N-07 4층 NavMesh 육안 검증 절차

N-07은 4층 Bake 결과를 실제 Scene 뷰에서 확인하는 사용자 작업이다. 이 단계에서는 Proxy Mesh나 NavMeshData를 수정하지 말고, 먼저 현재 Bake 결과가 의도한 천장 이동 영역과 일치하는지 확인한다.

1. `Assets/Scenes/Parking.unity`를 열고 저장되지 않은 변경 사항이 있으면 먼저 저장한다.
2. Hierarchy에서 `NavMeshSystem`이 활성화되어 있는지 확인한다. `Floor_4F`, `Floor_3F`, `Floor_2F`, `Floor_1F`의 `FloorController`는 비활성화하지 않는다.
3. `NavMeshSystem`의 `FloorNavigationManager`와 `GameProgressController`가 4층을 시작 층으로 가리키는지 확인한다.
4. Scene 뷰 우측 상단의 `Gizmos`를 켠다. Scene 뷰를 먼저 `Top`으로 맞춘 뒤, 이후 `Perspective`와 `Side` 뷰로 다시 확인한다.
5. Play Mode를 실행한다. `FloorNavigationManager`가 4층 NavMeshData를 등록한 뒤 `NavMeshSystem/FloorNavMeshValidator`의 컴포넌트 메뉴에서 `Validate Floor NavMesh`를 실행한다.
6. Scene 뷰에 파란 NavMesh 영역이 보이지 않으면 다음을 확인한다.

   - `Floor_4F.Baked NavMesh Data`가 비어 있지 않다.
   - `FloorNavigationManager.Registered Floor Count`가 4층 시작 시 4층을 포함한다.
   - `Navigation`의 `NavMeshSurface` 컴포넌트는 꺼져 있고 `Navigation` GameObject 자체는 켜져 있다.
   - Scene 뷰의 `Gizmos`와 Navigation 시각화가 켜져 있다.

7. Top 뷰에서 파란 영역이 다음 조건을 모두 만족하는지 확인한다.

   - 4층 천장 아래의 실제 인형 이동 가능 구역만 덮는다.
   - 기둥 내부, 벽 안쪽, 계단 중앙, 램프의 빈 공간에는 파란 영역이 없다.
   - 차량 지붕이나 주차장 바닥에 파란 영역이 없다.
   - 천장이 끊긴 출입구와 외부 공간으로 파란 영역이 이어지지 않는다.
   - 이동해야 하는 통로에 의도하지 않은 틈이나 고립된 작은 조각이 없다.
   - 외곽 끝은 실제 천장이 끝나는 지점에서 멈추며, `Exit Point`를 둘 수 있는 안쪽 여유가 남아 있다.

8. Perspective 또는 Side 뷰에서 높이를 확인한다.

   - 파란 영역이 `FloorReference_4F` 기준의 천장 아래 높이에 있다.
   - 바닥을 향해 내려가는 경사나 계단 연결이 없다.
   - 3층·2층·1층의 Geometry 또는 NavMesh로 세로 방향 연결이 생기지 않는다.
   - 기둥과 천장 사이에 인형이 끼일 만큼 좁은 가장자리 조각이 없다.

9. `FloorNavMeshValidator` Console 결과가 `4F NavMesh 검증 PASS`인지 확인한다. `FAIL`이면 파란 영역이 잘못된 위치, 누락된 통로, 높이 범위 이탈 중 어느 항목인지 기록하고 N-07을 완료 처리하지 않는다.
10. 육안과 Validator 모두 통과하면 다음 형식으로 완료 내용을 알려준다.

```text
N-07 완료
4층 파란 NavMesh 영역 확인: 완료
기둥/계단/빈 공간 제외 확인: 완료
바닥/다른 층 연결 없음: 확인
FloorNavMeshValidator PASS: 확인
```

#### N-05 사용자 실행 절차

N-05는 에디터에서 한 번 Bake해 `NavMeshData` 에셋을 저장하는 단계다. 플레이 중 Bake하지 않으며, Bake 후 `FloorNavigationManager`가 등록할 수 있도록 `NavMeshSurface` 컴포넌트는 비활성화한다.

1. Project 창에서 `Assets/Navigation/NavMesh/4F` 폴더를 만든다. 폴더가 이미 있으면 새로 만들지 않고 사용한다.
2. Hierarchy에서 `NavMeshSystem/Floors/Floor_4F/Navigation`을 선택한다. 이 오브젝트의 Transform은 Position `(0,0,0)`, Rotation `(0,0,0)`, Scale `(1,1,1)`이어야 한다.
3. `Navigation`에 `NavMeshSurface` 컴포넌트를 추가한다.
4. `NavMeshSurface`의 기본 설정을 다음처럼 바꾼다.

   | 필드 | 값 |
   |---|---|
   | `Agent Type` | `DollCeiling` |
   | `Default Area` | `Walkable` |
   | `Generate Links` | 끔 |
   | `Use Geometry` | `Render Meshes` |
   | `Collect Objects` | `Children` 또는 `Current Object Hierarchy` |
   | `Include Layers` | `DollNavMesh`만 체크 |

5. `Collect Objects`가 `Children`인 경우 `Navigation` 아래에 Proxy Mesh 외의 Mesh가 들어 있지 않은지 확인한다. 다른 Mesh가 있으면 Bake 전에 옮기거나 `DollNavMesh`가 아닌 Layer로 분리한다.
6. Proxy Mesh Renderer가 꺼져 있어 Bake 결과가 비어 보이면, `ProxyMesh_4F`와 자식 MeshRenderer만 잠시 켠다. `Navigation` GameObject 자체는 비활성화하지 않는다.
7. `NavMeshSurface`의 `Bake` 버튼을 누른다. 이때 런타임 코드나 `BuildNavMesh`를 사용하지 않는다.
8. Bake가 끝나면 Scene 뷰에서 파란 NavMesh가 이동 가능한 천장 아래 영역에만 나타나는지 확인한다. N-07에서 정식 육안 검증을 다시 수행한다.
9. Bake가 끝나면 AI Navigation이 NavMeshData를 자동으로 저장한다. 현재 씬이 `Assets/Scenes/Parking.unity`이므로 기본 생성 위치는 보통 `Assets/Scenes/Parking/`이며, 에셋 이름은 Surface 오브젝트 이름을 기준으로 `NavMesh-이름.asset` 형태가 된다. Project 창 검색창에 `t:NavMeshData` 또는 `NavMesh-`를 입력해 찾는다.
10. Project 창에서 `Assets/Navigation/NavMesh/4F` 폴더를 열고, 방금 생성된 `.asset`을 그 폴더로 드래그한다. Unity가 `.asset`과 `.meta`를 함께 이동하며 Surface의 참조도 갱신한다. 원하는 경우 에셋 이름을 `NavMesh_4F_DollCeiling.asset`으로 바꾼다.
11. `Floor_4F`를 선택하고 `FloorController`의 `Baked NavMesh Data` 슬롯에 이동한 4층 NavMeshData 에셋을 드래그한다. 에셋을 Project 창에서 잃어버렸다면 검색창에 `t:NavMeshData`를 다시 사용한다.
12. 에셋을 이동한 뒤 `Navigation`의 `NavMeshSurface`와 `FloorController_4F`가 같은 에셋을 참조하는지 확인한다. `Clear` 버튼은 에셋을 삭제할 수 있으므로 누르지 않는다.
13. Bake를 다시 하면 패키지가 씬 폴더에 새 NavMeshData를 만들 수 있다. Proxy Mesh나 Agent 설정을 수정해 재Bake한 경우에는 새 에셋을 다시 `Assets/Navigation/NavMesh/4F`로 이동하고 FloorController 참조를 최신 에셋으로 교체한다.
14. `FloorController`의 `Ceiling Proxy Mesh Root`가 `ProxyMesh_4F`를 가리키는지 다시 확인한다.
15. Bake 검수가 끝나면 잠시 켜 둔 Proxy Mesh Renderer를 다시 끈다.
16. `Navigation`의 `NavMeshSurface` 컴포넌트 체크를 끈다. 컴포넌트만 끄고 `Navigation` GameObject는 활성 상태로 둔다. 이 설정이 있어야 런타임에는 `FloorNavigationManager`만 NavMeshData를 등록한다.
17. `Floor_4F`와 `Navigation`을 저장한다.

N-05 완료 조건:

- `DollCeiling`으로 Bake된 4층 NavMeshData 에셋이 Project 창에 존재한다.
- `FloorController_4F.Baked NavMesh Data`에 그 에셋이 연결되어 있다.
- Proxy Mesh Renderer가 꺼져 있다.
- `NavMeshSurface` 컴포넌트가 꺼져 있다.
- NavMeshData가 바닥, 다른 층, 기둥 내부를 포함하지 않는다.

완료했다고 알려줄 때 다음 형식을 사용한다.

```text
N-05 완료
4층 NavMeshSurface 설정: 완료
DollCeiling으로 Bake: 완료
4층 NavMeshData 에셋 저장: 완료
FloorController_4F 연결: 완료
Proxy Renderer 비활성화: 완료
NavMeshSurface 컴포넌트 비활성화: 완료
```

#### N-03 사용자 실행 절차

N-03에서는 4층의 실제 천장 Geometry를 Bake에 직접 사용하지 않고, 인형이 이동할 수 있는 영역만 복제한 보이지 않는 Proxy Mesh를 만든다. 이 단계에서는 아직 Bake하지 않는다.

1. Hierarchy에서 `NavMeshSystem/Floors/Floor_4F`를 찾는다.
2. `Floor_4F` 아래에 빈 오브젝트 `Navigation`을 만들고 Transform을 기본값으로 둔다.
3. `Navigation` 아래에 빈 오브젝트 `ProxyMesh_4F`를 만든다. 이 오브젝트의 Layer를 `DollNavMesh`로 지정하고 자식에게도 적용한다.
4. `ProxyMesh_4F` 아래에 이동 영역을 나눌 자식 Mesh를 만든다. 평평한 구역은 Plane 또는 ProBuilder Plane을 사용하고, 불규칙한 구역은 ProBuilder로 영역을 잘라 만든다.
5. Proxy Mesh의 표면은 실제 4층 천장 바로 아래에 배치한다. `FloorReference_4F`의 Y `13.006`은 기준점일 뿐 실제 천장 표면 높이라는 뜻은 아니므로, 대표 천장 라이트의 위치와 천장 Collider를 Scene 뷰에서 함께 확인한다.
6. Proxy Mesh는 실제 천장보다 약간 아래에 둔다. 라이트 중심이 아니라 천장 하부면을 기준으로 약 `0.05~0.10m` 아래에서 시작하고, 인형 모델이 천장에 끼지 않는지 나중에 조정한다.
7. Proxy Mesh는 월드 X/Z 방향으로 실제 인형 이동 영역만 덮는다. 바닥, 차량 지붕, 조명, 천장 위쪽 면을 Proxy Mesh에 포함하지 않는다.
   주차장 외곽으로 나가는 연출이 필요하더라도 Plane을 실제 천장보다 넓게 확장하지 않는다. 인형은 천장 NavMesh 위의 `Exit Point`까지 이동한 뒤 Agent를 끄고 WallTransfer로 전환한다. `Exit Point`는 실제 이동 가능 경계 안쪽에 두며, Proxy Mesh 경계는 최소 `DollCeiling Radius + 0.10~0.20m`만큼 안쪽으로 넣는다.
8. 다음 영역에는 Mesh를 만들지 않거나 영역을 끊어 둔다.

   - 기둥과 벽 모서리 주변
   - 계단 중앙
   - 램프의 빈 공간과 층이 끊기는 구간
   - 천장이 실제로 없는 구간
   - 차량 출입구
   - 배관·조명 구조물 때문에 인형이 끼는 구간

9. 제외 영역의 여유 폭은 최소한 `DollCeiling Radius 0.35m` 이상을 확보하고, 실제 인형 폭과 벽 간격을 고려해 필요하면 더 넓힌다. Proxy Mesh를 기둥 위로 덮은 뒤 Bake에서 피하려고 하지 않는다.
10. 여러 Mesh 조각 사이의 틈은 인형이 지나갈 필요가 없는 곳에만 둔다. 이동해야 하는 구역의 조각 사이에는 NavMesh가 끊길 만큼의 틈을 만들지 않는다.
11. 각 Proxy Mesh에 `MeshFilter`와 `MeshRenderer`가 있는지 확인한다. `MeshCollider`는 Physics Colliders 방식으로 Bake할 때만 필요하므로 지금 추가하지 않는다.
12. Proxy Mesh의 Renderer는 최종적으로 꺼 둔다. Bake에서 Mesh가 수집되지 않는지 확인이 필요한 경우에는 N-05의 `NavMeshSurface`를 먼저 설정한 뒤 잠시 Renderer를 켜서 Bake 결과를 확인하고, 결과 확인 후 다시 끈다. GameObject 자체는 비활성화하지 않는다.
13. `FloorController_4F`의 `Ceiling Proxy Mesh Root` 슬롯에 `ProxyMesh_4F`를 드래그한다.
14. Top, Side, Perspective Scene 뷰에서 다음을 확인한다.

   - 이동 가능한 천장 아래 영역이 모두 Proxy Mesh로 덮여 있다.
   - Proxy Mesh가 바닥이나 다른 층 높이에 내려가 있지 않다.
   - 기둥과 계단 중앙에 Mesh가 남아 있지 않다.
   - Proxy Mesh가 실제 화면에서 보이지 않는다.
   - `ProxyMesh_4F`와 모든 자식이 `DollNavMesh` Layer다.

15. N-03 완료 시점에는 NavMeshData가 아직 없어도 된다. N-05에서 `NavMeshSurface`의 `Agent Type = DollCeiling`, `Include Layers = DollNavMesh`, `Collect Objects = Children`으로 Bake한다.

ProBuilder Plane에 기둥·계단 구멍을 만드는 방법:

1. `ProxyMesh_4F`의 Plane을 선택하고 ProBuilder 편집 모드에서 `Face` 선택 모드로 전환한다.
2. Scene 뷰를 Top 또는 수직으로 내려다보는 시점으로 맞춘다.
3. Tools Overlay에서 `Cut Tool`을 선택한다.
4. 기둥 또는 계단 외곽의 안쪽 여유선을 따라 점을 순서대로 클릭한다. 사각형이면 네 모서리, 불규칙한 기둥이면 6~8개의 점으로 영역을 닫는다.
5. 첫 점을 다시 클릭하거나 Cut Settings의 `Complete`를 눌러 면을 분할한다.
6. 새로 분할된 구멍 후보 Face를 선택한다.
7. 메뉴 `Tools > ProBuilder > Geometry > Delete Faces`를 실행한다. Windows에서 일반 `Delete` 키를 누르면 Mesh 전체가 삭제될 수 있으므로 ProBuilder 메뉴를 사용한다.
8. 삭제된 영역이 실제로 비어 있는지 Top, Perspective 뷰에서 확인한다. 재질을 검게 칠하거나 Renderer만 끄는 것은 구멍이 아니다.
9. 기둥·계단 가장자리에서 최소 `0.35m` 이상 안쪽을 비운다. 인형이 통과할 수 없는 구간이면 더 넓게 비운다.
10. 구멍을 만든 뒤 주변 Face를 다시 합치거나 Edge를 임의로 Merge하지 않는다. Cut이 만든 경계를 유지해야 다음 Bake에서 구멍이 보존된다.

구멍이 많은 경우에는 하나의 거대한 Plane에 구멍을 반복해서 만드는 것보다, 기둥과 계단 사이의 실제 이동 가능 영역을 여러 개의 ProBuilder Plane 조각으로 나누는 편이 안전하다. 각 조각은 같은 `DollNavMesh` Layer를 사용하고 서로 이어져야 하는 경계만 맞닿게 둔다.

PolyShape에서 `Delete Faces`가 비활성화될 때:

1. Hierarchy에서 부모 `ProxyMesh_4F`가 아니라 실제 PolyShape Mesh 오브젝트를 선택한다.
2. 현재 `New PolyShape` 또는 `Edit ProBuilder Shape` 모드라면 `Esc`를 눌러 점 편집 모드를 종료한다.
3. Tools Overlay에서 Active Context를 `ProBuilder`로 바꾼다.
4. Tool Settings Overlay에서 `Face` 편집 모드로 바꾼다. 단축키 `F`도 사용할 수 있다.
5. PolyShape의 상단 면을 클릭해 선택한 뒤 `Cut Tool`로 기둥/계단 외곽을 분할한다.
6. 새로 생긴 내부 Face를 선택하고 `Tools > ProBuilder > Geometry > Delete Faces`를 실행한다.
7. Proxy Mesh는 이동면이므로 PolyShape Inspector의 `Extrusion`을 `0`으로 둔다. 두께가 있는 PolyShape는 상·하단 면과 측면이 함께 Bake될 수 있다.
8. Face 모드 자체가 보이지 않으면 선택한 오브젝트에 `ProBuilder Mesh`가 있는지 확인한다. 일반 Mesh라면 백업 후 우클릭 `MeshFilter > ProBuilderize`로 변환해야 한다.
9. PolyShape의 외곽 점만 수정하는 경우에는 `Edit ProBuilder Shape` 모드에서 점을 선택하고 `Backspace`로 삭제한다. 이 모드에서는 내부 Face 삭제를 하지 않는다.

외곽 이동 설계 원칙:

```text
천장 Proxy Mesh
→ 외곽 안쪽 Exit Point
→ NavMeshAgent 비활성화
→ WallTransfer 외벽 연출
→ 다음 층 Wall Entry Point
```

실제 천장 밖에 Plane을 추가하지 않는다. 외곽 통로가 필요하면 실제 천장이 존재하는 범위 안에서만 별도의 좁은 Proxy Mesh 조각을 만들고, 그 끝에 Exit Point를 둔다.

완료했다고 알려줄 때 다음 형식을 사용한다.

```text
N-03 완료
4층 ProxyMesh_4F 제작: 완료
천장 아래 높이 배치: 완료
제외 영역 반영: 완료
DollNavMesh Layer 적용: 완료
Renderer 비활성화: 완료
FloorController_4F Proxy Mesh Root 연결: 완료
```

#### N-02 사용자 실행 절차

N-02에서는 `DollNavMesh` 수집 Layer와 인형 전용 Agent Type만 만든다. Layer와 Agent Type은 서로 다른 설정이다. Layer는 Proxy Mesh를 Bake에 포함할지 결정하고, Agent Type은 인형의 반지름·높이·경사·계단 설정과 NavMeshData의 종류를 결정한다.

1. Unity 상단 메뉴에서 `Edit > Project Settings`를 연다.
2. 왼쪽에서 `Tags and Layers`를 선택한다.
3. 비어 있는 `User Layer` 한 칸을 선택한다. 기존 오브젝트가 사용 중인 Layer를 이름만 바꾸지 않는다.
4. Layer 이름을 `DollNavMesh`로 입력하고 저장한다.
5. `DollNavMesh`는 GameObject Layer다. Tag, Sorting Layer 또는 Navigation Area에 같은 이름을 만들 필요는 없다.
6. Unity 상단 메뉴에서 `Window > AI > Navigation`을 연다.
7. `Agents` 탭에서 `+`를 눌러 새 Agent Type을 추가한다. 기존 `Humanoid` 또는 기본 Agent Type의 값을 수정하지 않는다.
8. 새 Agent Type 이름을 `DollCeiling`으로 지정한다.
9. 초기값은 다음처럼 입력한다. 실제 인형 프리팹의 크기를 확인한 뒤 N-05 Bake 전에 보정한다.

   | 설정 | 초기값 | 기준 |
   |---|---:|---|
   | `Radius` | `0.35` | 인형 어깨 폭의 절반보다 크거나 같게 설정 |
   | `Height` | `1.80` | 인형의 실제 높이에 맞춤 |
   | `Step Height` | `0.20` | Proxy Mesh의 작은 단차만 허용 |
   | `Max Slope` | `45` | 주차장 램프를 포함하되 제외 영역은 별도 처리 |
   | `Drop Height` | `0` | 천장 NavMesh 자동 낙하 링크 금지 |
   | `Jump Distance` | `0` | 천장 NavMesh 자동 점프 링크 금지 |

10. `DollCeiling`의 Radius는 벽 여유 공간을 포함해야 한다. 인형이 기둥이나 벽에 끼면 Radius를 줄이기보다 Proxy Mesh에서 해당 구간을 넓게 제외하거나 벽 여유를 확보한다.
11. `DollCeiling`을 저장한 뒤 Navigation 창을 닫는다.
12. `DollNavMesh` Layer가 실제로 생성됐는지 Project Settings에서 다시 확인한다.
13. 아직 Proxy Mesh나 NavMeshSurface를 만들지 않았다면 이 단계에서 씬 오브젝트의 Layer를 일괄 변경하지 않는다. N-03에서 Proxy Mesh 루트와 자식만 `DollNavMesh`로 설정한다.
14. N-02 완료 확인 항목:

   - 비어 있던 User Layer 하나가 `DollNavMesh`가 됐다.
   - 기존 Layer 이름을 덮어쓰지 않았다.
   - Agent Type 목록에 `DollCeiling`이 추가됐다.
   - 기본 `Humanoid` Agent Type을 변경하지 않았다.
   - `DollCeiling`의 초기값이 기록된 값과 일치한다.

완료했다고 알려줄 때 다음 형식을 사용한다.

```text
N-02 완료
DollNavMesh Layer 생성: 완료
DollCeiling Agent Type 생성: 완료
Radius / Height / Step / Slope 설정: 완료
기본 Agent Type 변경 없음: 확인
```

### 단계 3. 기본 인형 NavMesh 전환

| 순서 | ID | 작업 | 담당 | 완료 조건 | 상태 |
|---:|---|---|---|---|---|
| 3.1 | D-01 | 기존 `CeilingDollController`의 직접 이동을 NavMeshAgent 이동으로 교체 | Codex | 런타임 직접 좌표 추적이 제거되고 Agent 경로를 사용 | 해결됨 |
| 3.2 | D-02 | 플레이어 위치를 현재 층 천장 NavMesh에 투영 | Codex | Floor ID와 Y 범위를 벗어난 NavMesh를 선택하지 않음 | 해결됨 |
| 3.3 | D-03 | 관찰 중 `isStopped`와 Animator 정지 구현 | Codex | 플레이어가 볼 때 Agent 비활성화 없이 정지 | 해결됨 |
| 3.4 | D-04 | 경로 갱신 주기 분산과 경로 실패 3회 처리 구현 | Codex | 무한 재배치 없이 Dormant 재시도까지 동작 | 해결됨 |
| 3.5 | D-05 | 4층 기본 인형 프리팹에 Agent와 참조 연결 | 사용자 | Agent Type과 Controller 참조 연결 완료 | 해결됨 |
| 3.6 | D-06 | 4층 기본 인형 Play Mode 검증 | 사용자 | 관찰 정지, 시선 해제 이동, 장애물 회피를 확인 | 해결됨 |
| 3.7 | D-07 | 천장 이동용 인형 외형 방향과 부착 위치 보정 | 사용자 | VisualRoot 분리, 모델 반전, 천장면 거리 조정 | 해결됨 |

#### D-01 구현 내용

`Assets/Scripts/CeilingDollController.cs`의 이동 방식을 NavMeshAgent 기반으로 전환했다.

- `NavMeshAgent` 필수 컴포넌트 요구 및 참조 처리
- 기존 천장 높이 Raycast와 `transform.position` 직접 대입 제거
- 이동 속도, 회전 속도, 정지 거리를 `NavMeshAgent` 설정에 반영
- 플레이어가 보이지 않고 공격 거리 밖이면 `SetDestination`으로 경로 요청
- 플레이어가 보거나 공격 중이거나 경로를 사용할 수 없으면 `isStopped = true`와 `ResetPath`로 정지
- 기존 Animator 정지/재생과 Jumpscare 흐름 유지
- Agent가 아직 없는 경우 기본 Agent를 런타임에 임의 생성하지 않고 D-05 연결을 요구

D-01에서는 플레이어 목적지의 X/Z만 사용한다. 플레이어 위치를 현재 층 천장 NavMesh에 투영하고 Floor ID/Y 범위를 검사하는 작업은 D-02에서 구현한다.

검증 결과:

- `Assembly-CSharp.csproj` 컴파일 성공
- 경고 0개, 오류 0개
- 직접 위치 이동 메서드와 천장 높이 보정 메서드 제거 확인

#### D-02 구현 내용

`CeilingDollController.TryProjectPlayerPosition`을 추가해 플레이어의 X/Z를 현재 층 천장 NavMesh 위치로 투영한다.

- `GameProgressController.CurrentFloorId`를 현재 층의 1차 기준으로 사용
- `FloorNavigationManager`에서 같은 Floor ID의 `FloorController`를 조회
- 해당 Floor ID의 NavMeshData가 현재 Manager에 등록되어 있는지 확인
- 인형 `NavMeshAgent.agentTypeID`와 `NavMesh.AllAreas`로 `NavMeshQueryFilter` 구성
- `FloorReference Y`, 층 Navigation Y 중간값, 현재 Agent Y를 샘플 기준 높이로 사용
- `NavMesh.SamplePosition` 결과가 대상 FloorController의 Y 범위와 허용 오차 안에 있을 때만 승인
- 승인된 위치와 Floor ID를 `ProjectedPlayerPosition`, `ProjectedFloorId`에 기록
- GameOver/Restarting, 현재 층 미등록, 다른 층 Y 범위 샘플은 목적지로 사용하지 않음

검증 결과:

- `Assembly-CSharp.csproj` 컴파일 성공
- 경고 0개, 오류 0개
- NavMesh 등록 상태와 Floor ID/Y 범위 검사를 통과하지 못하면 이동 요청이 거부됨

#### D-03 구현 내용

관찰 중 정지를 `CeilingDollController.FreezeForObservation`으로 분리했다.

- `NavMeshAgent.enabled`를 끄지 않고 `isStopped = true`로 정지
- 관찰 시작 순간에만 기존 경로를 `ResetPath`로 제거
- Animator 컴포넌트는 유지하고 `Animator.speed = 0`으로 애니메이션만 정지
- 시야를 벗어나면 정지 플래그를 해제하고 새 NavMesh 목적지와 애니메이션을 재개
- `IsAgentEnabled`, `IsObservationFrozen` 상태를 외부 검증용으로 제공
- 공격 중, 플레이어 참조 누락, NavMesh 경로 실패 정지는 기존 일반 정지 흐름으로 유지

검증 결과:

- `Assembly-CSharp.csproj` 컴파일 성공
- 경고 0개, 오류 0개
- 관찰 정지 경로에 `NavMeshAgent.enabled = false` 또는 `Animator.enabled = false`가 없음

#### D-04 구현 내용

`CeilingDollController`에 경로 갱신 분산과 제한된 실패 재시도를 추가했다.

- 기본 경로 갱신 주기 `0.2초`를 사용하고 인형 Instance ID 기반 오프셋으로 갱신 시점을 분산
- 경로가 유효한 동안 매 프레임 `SetDestination`을 호출하지 않음
- `PathPending`을 기다린 뒤 `PathComplete`가 아니면 실패로 기록
- 목적지 투영 실패, Agent 미등록, `SetDestination` 실패를 동일한 경로 실패로 집계
- 기본 3회 연속 실패 시 Agent를 끄지 않고 `DollRuntimeState.Dormant`로 전환
- 실패 사이에는 기본 `1초`, Dormant 전환 뒤에는 기본 `2.5초` 후 재탐색
- 성공한 완전 경로에서 실패 카운터를 초기화
- Spawn Point 재예약은 S-01~S-03에서 연결할 수 있도록 현재 위치 무한 재배치를 수행하지 않음

검증 결과:

- `Assembly-CSharp.csproj` 컴파일 성공
- 경고 0개, 오류 0개
- 경로 갱신 주기, 3회 실패 임계값, Dormant 재시도, `PathComplete` 검사가 코드에 존재함

#### D-05 사용자 실행 절차

D-05에서는 기본 인형의 루트 오브젝트에 `NavMeshAgent`를 추가하고 `DollCeiling` Agent Type 및 D-01~D-04에서 사용하는 Controller 참조를 연결한다. Agent를 자식 뼈 오브젝트에 추가하지 않는다.

1. `Assets/Scenes/Parking.unity`를 열고 Hierarchy에서 `Ceiling Killer Doll 01`을 찾는다. 인형의 머리·팔·뼈 자식이 아니라 `Ceiling Killer Doll 01` 루트 오브젝트를 선택한다.
2. Inspector에서 `Add Component`를 누르고 `NavMeshAgent`를 추가한다. 이미 있다면 새로 추가하지 않는다.
3. `NavMeshAgent`를 다음처럼 설정한다.

   | 필드 | 값 |
   |---|---|
   | `Agent Type` | `DollCeiling` |
   | `Radius` | `0.35` |
   | `Height` | `1.80` |
   | `Base Offset` | `0`에서 시작하고 실제 천장면에 맞춰 조정 |
   | `Speed` | `1.45` |
   | `Angular Speed` | `240` |
   | `Acceleration` | `8` 이상 권장 |
   | `Stopping Distance` | `1.1` |
   | `Auto Braking` | 켬 |
   | `Auto Repath` | 켬 |
   | `Update Position` | 켬 |
   | `Update Rotation` | 켬 |
   | `Update Up Axis` | 켬 |
   | `Area` | `Walkable` |
   | `Agent Enabled` | 켬 |

4. 같은 `Ceiling Killer Doll 01` 루트의 `CeilingDollController`에서 다음 참조를 연결한다.

   | Controller 필드 | 연결 대상 |
   |---|---|
   | `Nav Mesh Agent` | 같은 루트에 있는 `NavMeshAgent` 컴포넌트 |
   | `Floor Navigation Manager` | `NavMeshSystem`의 `FloorNavigationManager` |
   | `Progress Controller` | `NavMeshSystem`의 `GameProgressController` |

5. `Animator Controller`, `Jumpscare Doll Template`, 기존 시야 관련 설정은 기존 연결을 유지한다. `DollNavMesh` Layer는 Proxy Mesh 전용이므로 인형 루트 Layer를 임의로 바꾸지 않는다.
6. Project 창의 기본 프리팹 `Assets/RamsterZ_FreeDoll/Prefabs/KillerDollWood.prefab`도 실제 생성 원본으로 사용한다면 프리팹을 열어 같은 NavMeshAgent와 설정을 적용하고 저장한다. Scene의 `Ceiling Killer Doll 01`이 프리팹 인스턴스라면 `Overrides > Apply All`로 원본에 반영한다.
7. Scene의 `Ceiling Killer Doll 01`이 일반 오브젝트로 표시되어 프리팹 연결이 없다면 Scene 루트에 직접 NavMeshAgent를 추가한 상태를 저장한다. 이 경우 자식 뼈에는 별도 Agent를 추가하지 않는다.
8. 이 단계에서는 `NavMeshSurface`를 켜거나 다시 Bake하지 않는다. Agent가 4층 파란 NavMesh 위에 있는지와 실제 이동은 D-06에서 확인한다. Agent가 NavMesh 위에 없다는 이유로 `Base Offset`을 크게 바꾸거나 인형을 순간이동시키지 않는다.
9. 저장 후 다음 항목을 확인한다.

   - `NavMeshAgent.Agent Type`이 `DollCeiling`이다.
   - 기본 Agent Type이나 `Humanoid` 설정을 변경하지 않았다.
   - `CeilingDollController.Nav Mesh Agent`가 비어 있지 않다.
   - `Floor Navigation Manager`와 `Progress Controller`가 `NavMeshSystem`을 가리킨다.
   - NavMeshAgent와 CeilingDollController가 모두 켜져 있다.

완료했다고 알려줄 때 다음 형식을 사용한다.

```text
D-05 완료
Ceiling Killer Doll 01 루트에 NavMeshAgent 추가: 완료
DollCeiling Agent Type 연결: 완료
NavMeshAgent 기본값 설정: 완료
CeilingDollController Nav Mesh Agent 연결: 완료
Floor Navigation Manager 연결: 완료
Progress Controller 연결: 완료
```

#### D-06 사용자 Play Mode 검증 절차

D-06은 4층 기본 인형이 실제로 천장 NavMesh 위에서 관찰 정지, 시야 해제 이동, 장애물 회피를 수행하는지 확인하는 단계다. 이 단계에서는 코드나 Bake 설정을 바꾸지 않고 한 번에 한 항목씩 확인한다.

1. `Assets/Scenes/Parking.unity`를 저장한다.
2. `Ceiling Killer Doll 01`의 루트에 `NavMeshAgent`와 `CeilingDollController`가 모두 켜져 있는지 확인한다.
3. `NavMeshAgent.Agent Type = DollCeiling`, `CeilingDollController.Nav Mesh Agent` 연결, `Floor Navigation Manager`와 `Progress Controller` 연결을 다시 확인한다.
4. Play Mode를 실행하고 Console에 다음 오류가 없는지 확인한다.

   - `CeilingDollController에는 NavMeshAgent가 필요합니다.`
   - `FloorNavigationManager를 찾을 수 없습니다.`
   - `NavMeshData가 FloorNavigationManager에 등록되지 않았습니다.`
   - `4층 NavMesh 샘플 검증 실패`

5. Scene 뷰에서 `Ceiling Killer Doll 01`이 4층 파란 NavMesh 위에 있는지 확인한다. 가능하면 Play Mode Inspector의 Debug 표시에서 `Is On NavMesh`를 확인한다. false이거나 파란 영역과 높이가 다르면 이동 테스트를 중단하고 위치를 먼저 알려준다. 인형을 순간이동시키거나 Base Offset을 크게 바꾸지 않는다.
6. 관찰 정지 테스트를 수행한다.

   - Game 뷰에서 플레이어 카메라가 인형의 몸 또는 머리를 직접 볼 수 있게 한다.
   - 2~3초 동안 인형을 계속 바라본다.
   - 인형의 월드 위치가 변하지 않고 Animator 동작이 멈추는지 확인한다.
   - Inspector에서 `NavMeshAgent` 컴포넌트가 계속 켜져 있는지 확인한다. Agent 컴포넌트가 꺼지면 실패다.

7. 시야 해제 이동 테스트를 수행한다.

   - 벽이나 기둥 뒤로 이동하거나 카메라를 돌려 인형을 화면과 시야에서 완전히 벗어난다.
   - 약 1~2초 기다린다. 경로 갱신 주기 때문에 즉시 움직이지 않을 수 있다.
   - 인형이 천장 파란 영역 위에서 플레이어 방향으로 자연스럽게 이동하는지 확인한다.
   - 한 프레임 순간이동, 바닥으로 내려가기, 다른 층으로 이동, 벽 관통이 없어야 한다.

8. 장애물 회피 테스트를 수행한다.

   - 인형과 플레이어 사이에 주차장 기둥 또는 벽이 오도록 위치를 잡는다.
   - 인형이 장애물 내부로 직진하지 않고 Bake된 이동 가능 영역을 따라 우회하는지 확인한다.
   - 계단 중앙, 램프 빈 공간, 천장 제외 영역으로 빠지지 않아야 한다.
   - 경로가 막히면 인형이 무한히 재배치되지 않고 잠시 멈춘 뒤 제한된 재시도를 수행해야 한다.

9. 세 테스트를 통과하면 다음 형식으로 완료 내용을 알려준다.

```text
D-06 완료
관찰 중 인형 정지: 확인
관찰 중 Agent 활성 유지: 확인
시야 해제 후 NavMesh 이동: 확인
장애물 우회: 확인
바닥/다른 층 이탈 없음: 확인
```

이동하지 않는 경우에는 다음 정보와 함께 완료 처리하지 말고 알려준다.

- 인형이 시작한 위치의 층과 대략적인 Y
- `NavMeshAgent`의 `Is On NavMesh` 표시 여부
- Console 오류 또는 경고 문구
- 관찰 정지는 되지만 이동만 안 되는지 여부

#### D-06 오류 대응: `no valid NavMesh`와 `not close enough`

스크린샷에 다음 오류가 함께 나타날 때는 아래 순서로 해결한다.

```text
Failed to create agent because there is no valid NavMesh
Failed to create agent because it is not close enough to the NavMesh
Stop can only be called on an active agent that has been placed on a NavMesh
```

이 세 메시지는 서로 다른 세 가지 문제가 아니라, 대부분 `NavMeshAgent가 유효한 4층 NavMesh 위에 배치되기 전에 실행됨`에서 연쇄적으로 발생한다.

1. Play Mode를 종료한다. Play Mode에서 인형을 움직여 해결하지 않는다.
2. Hierarchy에서 `Ceiling Killer Doll 01` 루트를 선택한다. 자식 뼈를 선택하지 않는다.
3. `NavMeshAgent`의 `Agent Type`이 `DollCeiling`인지 확인한다. `Humanoid`이면 4층 DollCeiling NavMesh와 다른 종류이므로 생성되지 않을 수 있다.
4. `NavMeshAgent`와 `CeilingDollController`의 체크박스를 잠시 끈다. 이것은 Agent를 삭제하는 것이 아니라 편집 중 오류 로그를 막는 임시 조치다.
5. Scene 뷰에서 4층 파란 NavMesh가 보이는지 확인한다. Top 뷰에서 기둥과 벽을 피한 넓은 파란 영역을 하나 선택한다.
6. `Ceiling Killer Doll 01` 루트의 Move Tool로 인형 전체를 그 파란 영역 안쪽으로 옮긴다. 머리나 몸 자식만 옮기지 않는다.
7. 인형 루트의 Y는 숫자 `13.006`을 무조건 입력하지 않는다. 실제 파란 NavMesh 표면과 같은 높이가 되도록 Scene 뷰의 Perspective/Side 뷰에서 맞춘다. `FloorReference_4F`의 Y는 기준값이며, 최종 위치는 Bake된 파란 면이 기준이다.
8. `NavMeshAgent.Base Offset`은 먼저 `0`으로 둔다. 인형 모델이 천장면에 반쯤 묻힐 때만 아주 조금씩 조정한다. 큰 값을 넣어 Agent를 NavMesh까지 억지로 끌어올리지 않는다.
9. `NavMeshAgent`와 `CeilingDollController`를 다시 켜고 저장한다.
10. `Floor_4F`를 선택해 `FloorController.Baked NavMesh Data`가 비어 있지 않은지 확인한다. 비어 있으면 N-05의 4층 에셋 연결부터 복구해야 한다.
11. 다시 Play Mode를 실행한다. Agent가 파란 4층 면 위에서 생성되면 `no valid NavMesh`와 `not close enough` 오류가 사라져야 한다.

#### D-06 오류 대응: `Floor3F에 사전 Baked NavMeshData가 없습니다`

현재 검증 범위는 4층만 Bake한 상태다. 따라서 3층 NavMeshData가 없는 것은 사용자 실수가 아니다. `FloorNavigationManager`가 현재 4층의 인접층인 3층까지 Preload하려고 하기 때문에 표시된다.

- `Floor_3F.Baked NavMesh Data`에 4층 에셋을 연결하지 않는다.
- 3층을 지금 당장 Bake할 필요가 없다. 3층 Bake는 단계 X-01의 작업이다.
- 이 메시지는 4층 단독 검증을 허용하도록 Manager에서 `인접층 NavMeshData 없음`을 보류/경고 처리하는 코드 수정이 필요하다.
- 4층 NavMesh가 정상 등록되기 전에 3층 에러만 먼저 해결하려고 Proxy Mesh를 수정하지 않는다.

즉, 4층 단독 검증에서의 올바른 해결 순서는 `4층 Baked NavMesh 연결 확인 → 인형 루트를 4층 파란 면에 배치 → 인접층 누락을 Manager가 보류하도록 코드 보정`이다.

#### D-06 오류 대응: `Stop can only be called ...`

이 메시지는 원인 오류가 아니라 결과 오류다. Agent가 아직 NavMesh에 배치되지 않았는데 `CeilingDollController`가 `isStopped` 또는 `ResetPath`를 호출해서 발생한다.

- 인형 루트가 NavMesh 위에 배치되면 대부분 함께 사라진다.
- 그래도 계속 나오면 코드에서 `navMeshAgent.isOnNavMesh == true`일 때만 `isStopped`와 `ResetPath`를 호출하도록 보호 처리가 필요하다.
- 이 오류를 없애기 위해 Agent를 끄거나 `DollCeiling`을 기본 Agent로 바꾸지 않는다.

#### D-07 사용자 천장 외형 방향/부착 위치 보정

D-07은 NavMesh의 이동 위치와 인형 모델의 시각 방향을 분리하는 작업이다. `NavMeshAgent`가 붙은 `Ceiling Killer Doll 01` 루트는 이동용으로 유지하고, 모델 자식만 `DollVisualRoot` 아래에서 180도 회전한다. 이 단계에서는 NavMeshSurface를 다시 Bake하지 않는다.

##### D-07-1. Visual Root 만들기

```text
[Hierarchy]
Ceiling Killer Doll 01 선택

[Action]
마우스 오른쪽 클릭 > Create Empty Child
새 오브젝트 이름을 DollVisualRoot로 변경
```

```text
[Hierarchy]
Ceiling Killer Doll 01
├─ DollVisualRoot
└─ KillerDollUnity_BaseBody
```

```text
[Inspector: DollVisualRoot > Transform]
Position X: 0       유지
Position Y: 0       유지
Position Z: 0       유지
Rotation X: 0       유지
Rotation Y: 0       유지
Rotation Z: 0       유지
Scale X: 1          유지
Scale Y: 1          유지
Scale Z: 1          유지
```

`KillerDollUnity_BaseBody`는 기존 Animator와 뼈대를 포함한 모델 루트다. 이 오브젝트를 `DollVisualRoot` 아래로 드래그한다. 드래그 후 모델의 월드 위치가 달라지면 `KillerDollUnity_BaseBody`의 Transform에서 Position과 Rotation을 기존 값으로 되돌리고, Scale은 1로 유지한다.

##### D-07-2. 모델만 천장 방향으로 뒤집기

```text
[Hierarchy]
Ceiling Killer Doll 01 > DollVisualRoot 선택

[Inspector: DollVisualRoot > Transform]
Position X: 0       유지
Position Y: 0       유지
Position Z: 0       유지
Rotation X: 180     변경
Rotation Y: 0       유지
Rotation Z: 0       유지
Scale X: 1          유지
Scale Y: 1          유지
Scale Z: 1          유지
```

`Ceiling Killer Doll 01` 루트 자체를 회전하지 않는다. 루트에는 NavMeshAgent와 CeilingDollController가 있으므로 루트를 뒤집으면 이동 방향과 Agent 회전이 함께 바뀐다.

##### D-07-3. 천장면과 모델 거리 조정

```text
[Hierarchy]
Ceiling Killer Doll 01 > DollVisualRoot 선택

[Inspector: DollVisualRoot > Transform]
Position X: 0       유지
Position Y: 0       변경 대상
Position Z: 0       유지
Rotation X: 180     유지
Rotation Y: 0       유지
Rotation Z: 0       유지
Scale X: 1          유지
Scale Y: 1          유지
Scale Z: 1          유지
```

Scene의 Side/Perspective 뷰에서 손·발이 천장면에 너무 멀거나 메쉬 안에 들어가면 `Position Y`만 `0.05`씩 조정한다. 인형마다 Pivot 위치가 다르므로 처음부터 고정값을 입력하지 않는다. 모델을 움직일 때 `Ceiling Killer Doll 01` 루트나 NavMeshAgent의 Base Offset은 변경하지 않는다.

##### D-07 완료 기준

- 루트의 NavMeshAgent가 계속 켜져 있다.
- 모델이 천장 아래쪽을 향해 뒤집혀 있다.
- 손·발이 천장면에서 과도하게 떨어지거나 천장 메쉬를 뚫지 않는다.
- Play Mode에서 모델의 월드 이동은 루트/NavMeshAgent가 담당한다.
- NavMeshSurface를 다시 Bake하지 않았다.

완료 보고 형식:

```text
D-07 완료
VisualRoot 생성: 완료
KillerDollUnity_BaseBody 이동: 완료
모델 Rotation X 180 적용: 완료
천장면과 모델 거리 조정: 완료
루트 NavMeshAgent 유지: 확인
```

### 단계 4. Preload와 Commit Trigger

| 순서 | ID | 작업 | 담당 | 완료 조건 | 상태 |
|---:|---|---|---|---|---|
| 4.1 | T-01 | Preload/Commit Trigger 컴포넌트 구현 | Codex | 중복 진입 무시와 대상 Floor ID 전달이 동작 | 해결됨 |
| 4.2 | T-02 | Commit 전 대상 NavMesh 준비 검증 구현 | Codex | 준비 실패 시 현재 층과 인형 상태를 유지 | 해결됨 |
| 4.3 | T-03 | 전환 중 역주행 대기와 2~3초 Cooldown 구현 | Codex | 현재 전환 완료 뒤 반대 전환만 시작 | 해결됨 |
| 4.4 | T-04 | 4층↔인접층 Preload/Commit Trigger 위치 배치 | 사용자 | 플레이어 이동 순서가 Preload 후 Commit이 되도록 배치 | 해결됨 |
| 4.5 | T-05 | 층 왕복 Trigger 검증 | 사용자 | 4F→3F→2F→1F→2F→3F→4F 왕복과 중복 Commit 없음 확인 | 해결됨 |

T-04 Trigger 배치 규칙:

- 서로 분리된 계단·램프·하강 진입 경로마다 `Preload`와 `Commit` Trigger를 한 쌍으로 만든다.
- 같은 연속 공간에 있고 모든 진입 방향이 동일한 대상 층으로 연결되면 하나의 넓은 Trigger로 합칠 수 있다.
- `Preload`는 이동 경로상 먼저, `Commit`은 실제 대상 층 진입 위치에 둔다.
- 여러 경로가 같은 대상 층으로 내려가도 각 Trigger의 `Target Floor ID`는 동일하게 설정한다.
- Trigger를 합쳐 플레이어가 다른 경로에 있는데도 오작동할 가능성이 있으면 합치지 않고 경로별로 분리한다.
- 현재 씬에서는 `FloorNavigationManager`와 `GameProgressController`가 모두 `NavMeshSystem` 오브젝트에 붙어 있으므로, 두 참조 슬롯에 같은 `NavMeshSystem` 오브젝트를 드래그한다. Unity가 각 슬롯에 맞는 컴포넌트를 자동으로 선택한다.

#### T-04 현재 보정: 하위 층 Trigger 자동 보완

현재 씬에는 4F→3F 하강용 `FloorTransitionTrigger`만 있고, 하위 층 구간의 일부 Trigger가 빠져 있었다. 수동 배치 실수를 막기 위해 `Assets/Editor/ParkingFloorTransitionConfigurator.cs`에 기존 4F→3F 경로를 기준으로 누락된 하강/상승 Trigger를 생성하는 메뉴를 추가했다.

사용자는 `Parking.unity`를 연 상태에서 다음 메뉴를 한 번 실행한다.

```text
Tools → Parking Garage → Create All Missing Floor Transition Triggers
```

이 메뉴는 `Parking/NavMeshSystem/Floors/FloorTransitionTriggers` 아래에 누락된 Trigger만 만든다. 기존 Trigger의 Transform, Collider, Mode, 참조는 수정하지 않는다. 생성 후 씬을 저장하고 다음 검증 메뉴를 실행한다.

```text
Tools → Parking Garage → Validate Floor Transition Triggers
```

Console에 다음 PASS 로그가 나오면 T-04의 씬 보정이 끝난 것이다.

```text
[TransitionSetup] PASS: 하강/상승 3개 층 구간의 경로 Trigger 48개가 모두 있습니다.
```

메뉴 실행 전 조건:

- Play Mode를 끈다.
- 현재 씬이 `Assets/Scenes/Parking.unity`인지 확인한다.
- `Floor_4F`, `Floor_3F`, `Floor_2F`, `Floor_1F`의 `Floor Reference`가 모두 연결되어 있어야 한다.

### 단계 5. Spawn Point 시스템

| 순서 | ID | 작업 | 담당 | 완료 조건 | 상태 |
|---:|---|---|---|---|---|
| 5.1 | S-01 | Spawn Point 상태와 예약/점유 구현 | Codex | Available, Reserved, Occupied, Blocked 전이가 동작 | 해결됨 |
| 5.2 | S-02 | 시야, 12m 거리, 인형 3~4m 간격, 경로 검사 구현 | Codex | 모든 선택 조건을 통과한 지점만 반환 | 해결됨 |
| 5.3 | S-03 | 무작위 선택, 최근 사용 제한과 부족 시 대기열 구현 | Codex | 같은 위치 중복 생성 없이 0.5~1초 간격 재검색 | 해결됨 |
| 5.4 | S-04 | 4층 외곽 Spawn Point 10~12개 배치 | 사용자 | 모두 4층 NavMesh 위이며 외벽/구조물 내부가 아님 | 해결됨 |
| 5.5 | S-05 | Spawn Point 참조 연결 | 사용자 | FloorController 목록에 모든 지점 연결 | 해결됨 |
| 5.6 | S-06 | 시야 밖 Spawn 검증 | 사용자 | 카메라 안 순간 생성과 인형 겹침이 없음을 확인 | 해결됨 |

#### S-01 구현 내용

S-01에서는 실제 Spawn Point 위치를 씬에 배치하지 않고 상태 전이와 소유권만 구현했다.

- `Assets/Scripts/SpawnPoint.cs` 추가
  - `Available → Reserved → Occupied` 전이
  - 예약 취소와 점유 해제 시 `Available` 복귀
  - 점유되지 않은 지점의 `Blocked ↔ Available` 전이
  - 예약 인형과 점유 인형을 각각 기록해 다른 인형의 해제를 거부
  - Restart 시 예약/점유/Blocked를 초기화하는 런타임 초기화
- `Assets/Scripts/SpawnPointManager.cs` 추가
  - `FloorController.SpawnPoints` 목록을 층별로 등록
  - 중복 연결된 Spawn Point를 한 번만 등록
  - 지점 예약, 예약 확정, 예약 취소, 점유 해제, Blocked 전환 API 제공
  - 아직 Spawn Point가 배치되지 않은 상태도 오류 없이 0개로 처리
  - `FloorController` 목록을 비워 두면 씬에서 자동 검색

현재 씬에는 아직 S-04의 4층 Spawn Point를 배치하지 않는다. S-02~S-03의 후보 검사와 선택 코드가 끝난 뒤 사용자가 실제 지점을 만든다.

#### S-02 구현 내용

`Assets/Scripts/SpawnPointManager.cs`에 후보 검사 API를 추가했다. `CollectValidSpawnPoints`는 대상 층의 등록된 Spawn Point를 순회하고, `TryEvaluateCandidate`는 첫 번째 탈락 조건을 `SpawnPointValidationFailure`로 반환한다.

검사 순서는 다음과 같다.

1. Spawn Point 등록 여부와 대상 Floor ID 확인
2. Spawn Point 상태가 `Available`인지 확인
3. 대상 층 FloorController와 등록된 `DollCeiling` NavMeshData 확인
4. Spawn Point 위치를 `DollCeiling` NavMesh에 샘플링
5. 샘플 Y가 해당 층의 `Navigation Min Y ~ Navigation Max Y` 범위인지 확인
6. 플레이어 카메라 화면 안에 있고 장애물이 없는지 확인하여, 보이는 지점은 제외
7. 플레이어와의 수평 거리가 기본 `12m` 이상인지 확인
8. 같은 층 인형과의 수평 거리가 기본 `3m` 이상인지 확인
9. 4방향 수평 장애물 Raycast로 벽/구조물 내부 여부 확인
10. 플레이어 위치를 같은 층 NavMesh에 투영하고 `PathComplete` 경로인지 확인
11. 경로 코너가 다른 층 높이 범위로 벗어나지 않는지 확인

S-02 기본값은 `minimumPlayerDistance = 12`, `minimumDollDistance = 3`, `navMeshSampleRadius = 0.5`, `floorHeightTolerance = 0.35`다. 3~4m 간격은 최소 3m를 보장하고, S-03의 무작위 선택에서 지점 분산으로 처리한다.

현재 씬에는 아직 S-04의 4층 Spawn Point를 배치하지 않으므로, 지금은 Unity Inspector에서 후보 검사 값을 설정하거나 지점을 만들지 않는다.

#### S-03 구현 내용

`Assets/Scripts/SpawnPointManager.cs`에 S-02 후보 결과를 실제 생성 요청으로 연결하는 선택·대기열 API를 추가했다.

- `TryReserveRandomValidPoint`
  - S-02의 모든 후보 조건을 먼저 검사한다.
  - 최근 점유 확정 후 기본 `3초` 이내인 지점은 이번 선택에서 제외한다.
  - 남은 후보를 매번 무작위로 섞고, `Available` 상태를 다시 확인한 뒤 하나만 `Reserved`로 전환한다.
  - 후보가 없거나 모두 최근 사용 제한 중이면 즉시 실패하고 같은 위치를 중복 예약하지 않는다.
- `QueueSpawnRequest`
  - 즉시 예약할 수 없는 인형을 대기열에 추가한다.
  - 기본 `0.75초` 간격으로 재검사하며, 실제 간격은 `0.5~1초` 범위로 제한한다.
  - 같은 층·같은 인형의 중복 요청을 거부하고, 기본 최대 대기 요청 수는 실패 생성 상한인 `6개`다.
- `TryDequeueReadySpawnRequest`
  - 지점 예약에 성공한 요청을 꺼낸다. 이 시점의 Spawn Point는 아직 `Reserved` 상태다.
- `TryCommitSpawnRequest` / `CancelSpawnRequest`
  - 생성이 끝나면 `Commit`으로 `Occupied` 확정한다.
  - 생성 실패나 Restart면 `Cancel`으로 `Available` 반환한다.
- `ClearSpawnQueue`
  - Restart 시 대기 요청과 Ready 예약을 모두 정리한다.
- `SpawnPoint.IsRecentlyUsed`
  - 마지막 점유 확정 시각을 기준으로 최근 사용 제한을 검사한다.
  - 런타임 초기화 시 마지막 사용 시각도 함께 초기화한다.

S-03은 코드와 컴파일 검증까지 완료했다. 아직 실제 4층 Spawn Point를 씬에 배치하지 않았으므로, 다음 단계는 사용자가 진행하는 S-04다.

#### S-04 사용자 완료 내용

사용자가 4층 외곽 Spawn Point 배치 작업의 완료를 확인했다.

- `Floor_4F` 아래 Spawn Point 전용 계층 구성
- 4층 외곽의 Spawn Point 10~12개 배치
- 각 지점의 `Floor ID = Floor4F` 설정
- 4층 `DollCeiling` NavMesh 위 배치 확인
- 외벽, 기둥, 계단, 구조물 내부를 제외한 위치 배치 확인

S-04에서 만든 지점은 다음 S-05에서 `FloorController_4F`의 `Spawn Points` 목록에 연결한다.

#### S-05 사용자 완료 내용

사용자가 `Floor_4F`의 `FloorController`에 4층 Spawn Point 목록을 연결하고 확인했다.

- `Spawn Points` 배열 크기를 실제 배치 개수에 맞게 설정
- `SpawnPoint_4F` 전체를 배열에 연결
- `None` 슬롯 없음
- 모든 Spawn Point의 `Floor ID = Floor4F`
- 중복 연결 없음

#### S-06 사용자 완료 내용

사용자가 4층 Spawn Point 등록과 시야 밖 배치 검증을 완료했다.

- `NavMeshSystem`에 `SpawnPointManager` 추가
- `Floor_4F`의 Spawn Point 등록 로그 확인
- 등록 로그: `Spawn Point 10개를 등록했습니다.`
- 카메라에 직접 보이는 Spawn Point 없음 확인
- 기본 인형과 Spawn Point 간 최소 거리 확인
- Spawn Point 중복 배치 없음 확인

자동차 실패 이벤트에서 실제 추가 인형을 생성하는 연동은 C-03에서 구현한 뒤 같은 조건으로 다시 검증한다.

#### C-01 구현 내용

C-01의 요구사항은 기존 `GameProgressController`와 `NavMeshSystemTypes`에 구현되어 있음을 확인하고 컴파일 검증했다. C-01에서 자동차 오브젝트를 씬에 연결하지 않으며, 실제 자동차 기믹 연결은 C-05에서 사용자가 진행한다.

- `NavMeshSystemTypes.CarInvestigationState`
  - `Unexamined`, `Available`, `Failed`, `Succeeded`, `Locked` 상태를 정의한다.
- `GameProgressController.CarProgressRecord`
  - 자동차의 고유 `CarId`, 소속 `FloorId`, 현재 조사 상태를 함께 저장한다.
  - `GameProgressController`의 직렬화된 `Car Results` 목록으로 보존된다.
- `RegisterCar`
  - 빈 ID, `FloorId.None`, 중복 ID를 거부한다.
  - 초기 상태는 `Unexamined` 또는 `Available`만 허용한다.
- 상태 조회/변경 API
  - `TryGetCarState`
  - `TrySetCarAvailable`
  - `TryRecordCarFailure`
  - `TryRecordCarSuccess`
  - `TryLockCar`
- `ResetForNewGame`
  - 등록된 자동차 ID와 층 정보는 유지하고, 조사 상태를 `Unexamined`로 초기화한다.
  - 전체 실패 횟수도 0으로 초기화한다.

`Assembly-CSharp.csproj` 컴파일 결과는 경고 0개, 오류 0개다.

#### C-02 구현 내용

C-02의 차단 조건은 `GameProgressController`의 자동차 상태 API에 이미 구현되어 있음을 확인하고 컴파일 검증했다. 자동차 오브젝트의 실제 조사 이벤트 연결은 C-03 이후 C-05에서 진행한다.

- 최초 실패 차단
  - `TryRecordCarFailure`는 `Unexamined` 또는 `Available`에서만 성공한다.
  - 첫 호출에서 상태를 `Failed`로 먼저 저장하고 `TotalFailureCount`를 1회 증가시킨다.
  - 같은 자동차의 두 번째 실패 호출은 `Failed` 상태 때문에 `false`를 반환하고 실패 수를 증가시키지 않는다.
- 성공 후 재조사 차단
  - `TryRecordCarSuccess`는 `Unexamined` 또는 `Available`에서만 성공한다.
  - 성공 즉시 상태를 `Succeeded`로 저장한다.
  - `Succeeded` 상태에서 실패 또는 성공을 다시 호출하면 모두 `false`를 반환한다.
- 결과 잠금
  - `TryLockCar`는 `Failed` 또는 `Succeeded`에서만 `Locked`로 전환한다.
  - `Locked` 상태에서는 실패, 성공, 재잠금이 모두 거부된다.
- 새 게임 초기화
  - `ResetForNewGame`은 자동차 ID와 Floor ID는 보존하고 상태만 `Unexamined`로 되돌린다.
  - 전체 실패 수는 0으로 초기화한다.

실제 자동차 상호작용에서 이 API를 호출하는 순서와 클랙슨/인형 생성 연결은 C-03에서 구현한다.

#### C-03 구현 내용

C-03에 자동차 실패 후속 순서를 담당하는 `CarKeyGimmickManager`와 개별 자동차 연결 컴포넌트 `CarKeyGimmick`을 추가했다.

처리 순서는 다음과 같다.

```text
CarKeyGimmick.TryReportFailure
→ CarKeyGimmickManager가 자동차 ID/Floor ID 등록 확인
→ GameProgressController.TryRecordCarFailure
→ 상태를 Failed로 먼저 저장
→ 실패 경적 1회 재생
→ GameProgressController.CanAddDoll 확인
→ 가능할 때 CarFailureSpawnRequest 발행
→ C-04 인형 명단/생성기가 인형 생성 후 SpawnPointManager 대기열에 요청
```

- `Assets/Scripts/CarKeyGimmickManager.cs` 추가
  - 실패 상태 저장 후 경적을 재생하는 순서 보장
  - 인형 최대 수에 도달하면 경적만 재생하고 Spawn 요청은 발행하지 않음
  - `DollSpawnRequested` 이벤트로 C-04에 생성 요청 전달
  - 생성된 인형을 `SpawnPointManager.QueueSpawnRequest`에 연결하는 API 제공
  - `Assets/Music/car horn.wav`를 `Default Failure Horn` 슬롯에 연결할 수 있도록 AudioClip 필드 제공
- `Assets/Scripts/CarKeyGimmick.cs` 추가
  - 자동차별 `Car ID`, `Floor ID`, 선택적 경적 클립을 보유
  - C-05에서 자동차 오브젝트에 추가해 `TryReportFailure`를 호출하는 연결 지점으로 사용
- `GameProgressController.TryGetCarRecord` 추가
  - 같은 자동차 ID가 다른 층에 중복 등록되지 않는지 Manager가 확인할 수 있음

현재 C-03에서는 실제 자동차 오브젝트 연결과 경적 클립 드래그를 하지 않는다. C-05에서 각 자동차에 `CarKeyGimmick`을 추가하고 `car horn.wav`를 Manager의 `Default Failure Horn` 또는 자동차의 `Failure Horn Override`에 연결한다.

#### C-04 구현 내용

C-04의 실제 인형 명단과 생성 흐름을 담당하는 `DollRosterManager`를 추가했다.

처리 순서는 다음과 같다.

```text
씬의 CeilingDollController는 복제 템플릿으로만 유지하고 활성 명단에는 등록하지 않음
→ CarKeyGimmickManager.DollSpawnRequested 수신
→ GameProgressController의 현재 수와 대기 생성 수를 함께 확인
→ 제한 6개 이하면 템플릿 또는 Doll Prefab을 복제
→ 복제 인형을 비활성화한 상태로 SpawnPointManager 대기열에 등록
→ 유효 Spawn Point 예약 완료 대기
→ 예약 지점에 배치하고 DollCeiling NavMeshAgent 활성화
→ Spawn Point 점유 확정
→ GameProgressController.TryAddDoll로 인형 수 1 증가
→ 인형 명단에 새 인형 등록
```

- `Assets/Scripts/DollRosterManager.cs` 추가
  - 시작 시 템플릿을 `ActiveDolls` 명단에 등록하지 않음
  - `CarKeyGimmickManager.DollSpawnRequested` 이벤트 구독
  - `DollPrefab`이 비어 있으면 비활성 복제 템플릿을 복제
  - Spawn Point가 준비될 때까지 복제 인형과 생성 요청을 대기
  - 예약 지점 배치 → Agent 활성화 → 점유 확정 → 인형 수 증가 순서를 보장
  - 현재 인형 수와 대기 중인 생성 수를 합쳐 `GameProgressController.DollLimit`(현재 6개)을 넘지 않게 차단
  - 생성 실패 시 예약 반환과 복제 오브젝트 제거
  - 자동차 실패로 생성된 인형만 최대치에 포함하고, 제한 도달 시 C-03의 경적만 재생되도록 유지
  - `DollRosterManager`는 `NavMeshSystem`에 추가한 뒤 C-05에서 `CarKeyGimmickManager`, `SpawnPointManager`, 복제 템플릿을 Inspector에 연결한다.

`Assembly-CSharp.csproj` 컴파일 결과는 경고 0개, 오류 0개다.

#### C-05A ~ C-05C 구현 내용

플레이어가 시작부터 차키 1개를 보유하고 자동차 앞에서 E키를 눌러 성공/실패를 판정하는 실제 상호작용 흐름을 추가했다.

```text
PlayerCarKeyInventory가 시작 차키 ID 보유
→ CarInteractionController가 카메라 중앙으로 자동차 Collider 감지
→ 자식 Collider에서 부모 CarKeyGimmick 검색
→ 조사 가능한 자동차면 [E] 차키 사용 안내 표시
→ E키 입력
→ 보유 키 ID와 자동차 Car ID 비교
→ 일치하면 성공, 다르면 최초 실패
→ 실패 시 기존 C-03 경적/인형 생성 요청 실행
```

- `Assets/Scripts/PlayerCarKeyInventory.cs` 추가
  - 시작 차키 ID 기본값 `CAR_4F_01`
  - 현재 보유 차키 ID와 자동차 ID의 일치 여부 제공
  - 성공/실패 후 차키를 소비하지 않음
  - 새 게임 초기값 복원 API 제공
- `Assets/Scripts/CarInteractionController.cs` 추가
  - 플레이어 카메라 화면 중앙에서 기본 3m Raycast
  - 자동차 자식 Collider를 맞혀도 `GetComponentInParent<CarKeyGimmick>`으로 자동차 루트 검색
  - Input System의 E키 입력 처리
  - UI가 연결되지 않았으면 `[E] 차키 사용` 안내를 런타임 자동 생성
  - 차키가 없으면 `차키가 필요합니다` 안내 표시
- `Assets/Scripts/CarKeyGimmick.cs` 확장
  - `CanInvestigate`로 완료 자동차 재조사 차단
  - 보유 키와 자동차 ID 비교
  - 일치 시 `TryReportSuccess`, 불일치 시 `TryReportFailure` 호출
  - 자동차 ID 앞뒤 공백 정리

코드 구현과 컴파일 검증은 경고 0개, 오류 0개로 완료했다. 사용자 작업 `C-05D`에서는 플레이어와 현재 검증용 자동차 1대의 Inspector 연결을 완료했다. 추가 자동차 2~3대는 추후 실제 자동차 에셋을 추가할 때 같은 규칙으로 연결한다.

### 단계 6. 자동차 열쇠 기믹과 인형 누적

| 순서 | ID | 작업 | 담당 | 완료 조건 | 상태 |
|---:|---|---|---|---|---|
| 6.1 | C-01 | 자동차별 고유 ID와 상태 저장 구현 | Codex | Unexamined부터 Failed/Succeeded/Locked까지 저장 | 해결됨 |
| 6.2 | C-02 | 최초 실패 1회와 성공 후 재조사 차단 구현 | Codex | 동일 자동차 이벤트 중복에도 추가 실패가 기록되지 않음 | 해결됨 |
| 6.3 | C-03 | 클랙슨 후 인형 생성 요청 순서 구현 | Codex | 실패 상태 저장 후 소리와 생성 요청이 순서대로 발생 | 해결됨 |
| 6.4 | C-04 | `DollRosterManager`와 시작 0개 + 실패 추가 최대 6개 제한 구현 | Codex | 총 6개 상한을 넘지 않음 | 진행 중 |
| 6.5a | C-05A | 플레이어 보유 차키 ID 구현 | Codex | 게임 시작 시 차키 1개를 보유하고 ID 비교 가능 | 해결됨 |
| 6.5b | C-05B | 자동차 감지, E 안내와 입력 구현 | Codex | 자식 Collider를 통해 자동차 루트를 찾고 E 입력 가능 | 해결됨 |
| 6.5c | C-05C | 키 ID 비교와 성공/실패 연결 | Codex | 일치 시 성공, 불일치 시 최초 실패와 기존 후속 처리 실행 | 해결됨 |
| 6.5d | C-05D | 플레이어와 현재 검증용 자동차 1대에 컴포넌트/ID/경적 연결 | 사용자 | 플레이어 참조와 검증용 자동차 1대의 ID/경적 연결 완료 | 해결됨 |
| 6.6 | C-06 | 실패/성공/중복 조사 Play Mode 검증 | 사용자 | 현재 검증용 자동차 1대의 E키 상호작용 테스트 완료 | 해결됨 |

> C-05 연결 대상 보정: 현재 기획의 상호작용은 플레이어가 차키를 보유한 상태로 자동차 앞에서 E키를 눌러 시작한다. 따라서 `CarKeyGimmick`은 `SImple_03`, `Simple_02` 열쇠 프리팹이 아니라 실제 상호작용 대상인 자동차 본체 루트에 추가한다. 열쇠 오브젝트는 플레이어의 보유 키/인벤토리 데이터가 담당하고, 자동차의 `Car ID`가 조사 결과와 실패 인형 생성을 식별한다.

### 단계 7. 다중 인형과 단일 공격권

| 순서 | ID | 작업 | 담당 | 완료 조건 | 상태 |
|---:|---|---|---|---|---|
| 7.1 | A-01 | `DollAttackCoordinator` 구현 | Codex | 공격 가능 인형 중 최단 거리 1개만 예약 | 해결됨 |
| 7.2 | A-02 | 공격권 1~2초 유지와 해제 조건 구현 | Codex | 매 프레임 공격자가 교체되지 않고 모든 해제 조건 처리 | 해결됨 |
| 7.3 | A-03 | 회피와 플레이어 주변 접근 위치 분산 구현 | Codex | 비공격 인형이 같은 목적지에 겹치지 않음 | 해결됨 |
| 7.4 | A-04 | 층 전환과 GameOver 중 공격 잠금 구현 | Codex | 우선순위가 명세와 일치 | 해결됨 |
| 7.5 | A-05 | 인형 2~4개 추적과 공격 Play Mode 검증 | 사용자 | 동시에 한 인형만 공격하고 인형 겹침이 허용 범위 | 해결됨 |

A-05 오브젝트 역할 주의:

- A-05에서 복제하는 대상은 `Ceiling Killer Doll 01`의 천장 인형 루트다. 이 루트에는 `NavMeshAgent`와 `CeilingDollController`가 있어야 한다.
- 점프스퀘어 시각 오브젝트는 `Main Camera > JumpscareDollAnchor > JunpscareDoll`에 있는 별도 템플릿이며, `JumpscareDollSettings`를 사용한다.
- A-05에서 점프스퀘어 템플릿을 복제하거나 이동하지 않는다.
- `DollJumpscareController`는 현재 코드에서 천장 인형 루트의 공격 진행을 제어하는 내부 런타임 컴포넌트다. `CeilingDollController.Awake`가 루트에 없으면 자동으로 추가한다. 따라서 A-05에서는 이 컴포넌트를 점프스퀘어 템플릿에 추가하거나 연결하지 않는다.

#### A-05 사용자 실행 절차

현재 4층에는 검증용 자동차 `4F_Retro Car` 1대만 연결되어 있으므로, 새 게임은 인형 0개로 시작하고 해당 자동차의 첫 실패에서 런타임 인형 1개가 생성되는지 검증한다. 추가 자동차 에셋을 연결한 뒤에는 실패 횟수에 따라 총 6개까지 반복한다.

1. `Parking/NavMeshSystem`의 `GameProgressController`에서 다음 값을 위에서 아래 순서로 확인한다.
   - `Starting Floor ID = Floor4F` — 확인
   - `Starting Floor Controller = FloorController_4F` — 확인
   - `Doll Limit = 6` — 확인
   - `Initialize On Awake = On` — 확인
2. 같은 오브젝트의 `DollRosterManager`에서 다음 값을 위에서 아래 순서로 확인한다.
   - `Progress Controller = NavMeshSystem (Game Progress Controller)` — 확인
   - `Car Key Gimmick Manager = NavMeshSystem (Car Key Gimmick Manager)` — 확인
   - `Spawn Point Manager = NavMeshSystem (Spawn Point Manager)` — 확인
   - `Initial Doll = Ceiling Killer Doll 01` — 확인
   - `Doll Prefab = None` — 유지. 비어 있으면 기본 천장 인형을 복제하며, `JunpscareDoll`을 연결하지 않는다.
   - `Log Roster Events = On` — 유지
3. 같은 오브젝트의 `DollAttackCoordinator`에서 다음 값을 위에서 아래 순서로 확인한다.
   - `Progress Controller = None` — 유지. 런타임에 자동 검색한다.
   - `Player Controller = None` — 유지. 런타임에 자동 검색한다.
   - `Reservation Duration = 1.5` — 확인
   - `Log Reservations = On` — 유지
   - `Approach Radius = 2.4` — 확인
   - `Minimum Approach Separation = 1.5` — 확인
4. `Parking/4F_Retro Car` 루트의 `CarKeyGimmick`에서 다음 값을 위에서 아래 순서로 확인한다.
   - `Car ID = Car_4F_01` — 확인
   - `Floor ID = Floor4F` — 확인
   - `Failure Horn Override = None` — 유지. `CarKeyGimmickManager`의 기본 경적을 사용한다.
5. Play Mode에서 자동차 앞에서 `E`를 눌러 실패 후속 처리를 실행한다. 현재 플레이어 키 ID `CAR_4F_01`과 자동차 ID `Car_4F_01`은 대소문자가 달라 실패 경로로 처리되며, 이번 단계에서는 추가 인형 생성 검증에 사용한다.
6. 약 1~2초 뒤 Hierarchy에 `Ceiling Killer Doll 01_Runtime_01`이 생성되고, Console에 인형 수가 `1/6`으로 증가하는지 확인한다.
7. 두 인형이 같은 목적지에 완전히 겹치지 않고 서로 다른 접근 위치를 사용하는지 확인한다.
8. 두 인형이 공격 범위에 들어온 뒤 플레이어가 시선을 돌렸을 때 가장 가까운 인형 하나만 공격을 시작하는지 확인한다. Console의 공격권 예약 로그가 한 번만 나오고, 두 인형이 동시에 점프스케어를 시작하지 않아야 한다.
9. 점프스케어가 시작되면 GameOver가 표시되므로, 다음 시나리오는 Restart 후 새 Play Mode 회차에서 확인한다.

A-05에서 복제 대상은 `Parking/Ceiling Killer Doll 01` 천장 인형 루트이며, `Main Camera/JumpscareDollAnchor/JunpscareDoll`은 복제하거나 이동하지 않는다. `Doll Head Tracker`가 남아 있어도 현재 비활성 상태이므로 A-05 검증에는 영향을 주지 않는다.

완료 보고 형식:

```text
A-05 완료
총 인형 수: 2개 / 3개 / 4개
추가 인형 생성: 확인 / 실패
인형 겹침: 없음 / 문제 있음
동시 공격: 없음 / 문제 있음
공격권 로그: 1개만 기록 / 문제 있음
Console 오류: 없음 / 있음 (오류 문구 첨부)
```

#### A-05 1차 검증 진단 기록

- 자동차 `Car_4F_01` 실패 처리와 경적 재생은 정상이다.
- `Ceiling Killer Doll 01_Runtime_01` 생성 요청 등록까지는 정상이다.
- 실제 Spawn Point 예약과 생성 완료 로그가 없어 요청이 대기열에 남아 있다.
- 씬 저장값 확인 결과 `Parking/NavMeshSystem/Floors/Floor4F`의 `FloorController`에서 `Navigation Min Y = 0`, `Navigation Max Y = 0`이다.
- 4층 Spawn Point와 Proxy NavMesh 샘플 높이는 `Y = 12.931`이므로 모든 후보가 `WrongNavigationHeight` 조건에서 탈락하는 상태로 판단된다.
- 다음 사용자 조치: `Floor_4F`의 `Navigation Min Y = 12.5`, `Navigation Max Y = 13.5`로 설정한 뒤 A-05를 다시 실행한다.
- 재확인 결과 현재 저장값이 `Navigation Min Y = 13.5`, `Navigation Max Y = 12.5`로 역전되어 있다. `Min`은 반드시 더 작은 값 `12.5`, `Max`는 더 큰 값 `13.5`여야 한다.
- 수정 후 재검증 결과 `Ceiling Killer Doll 01_Runtime_01` 생성 완료 로그와 `현재 인형 수 1/6개` 로그를 새 규칙 기준으로 확인한다. 4층 Spawn Point 예약과 추가 인형 배치 단계는 통과했으며, A-05의 겹침·단일 공격권 검증은 계속 진행한다.
- A-05 후속 문제 보고: 런타임에서 생성된 인형의 루트 또는 시각 모델이 4층 천장 아래가 아니라 천장 위로 보이는 현상이 보고되었다. 생성 좌표(`SpawnPoint`), 4층 `ProxyMesh/NavMesh` 높이, `NavMeshAgent Base Offset`, `DollVisualRoot`의 로컬 위치를 순서대로 대조한 뒤 수정한다. 원인 확인 전에는 값을 임의로 변경하지 않는다.
- 원인 확인(코덱스): 생성 루트 Y는 `12.96833`으로 SpawnPoint Y `12.931` 및 4층 기준 Y `13.006`과 일치 범위다. 위치 문제가 아니다. Prefab의 `DollVisualRoot`가 이미 X축 180° 회전되어 있는데, `CeilingDollController.FlipModelOntoCeiling()`가 Animator 모델에 Z축 180°를 추가 적용한다. 두 회전이 결합되어 천장 반전이 상쇄되고 모델 전방도 반대로 바뀐다. 결과적으로 인형이 천장 위로 보이며 엉덩이 방향으로 이동한다.
- 1차 수정 완료(코덱스): `CeilingDollController.cs`에서 추가 Z축 180° 회전 처리와 관련 상태를 제거했다. Prefab의 `DollVisualRoot` 회전만으로 천장 방향을 유지한다. 이 수정으로 런타임 인형이 천장 아래에 생성되는 것을 확인했다.
- 2차 수정 완료(코덱스): 천장 아래 배치는 유지되지만 시각 모델의 전방이 NavMeshAgent 진행 방향과 반대인 것을 확인했다. Animator 모델에 로컬 Y축 180° 회전만 적용하도록 변경했다. Z축 회전을 사용하지 않으므로 천장 아래 방향은 상쇄되지 않는다. 사용자 확인 결과 정상 보행으로 해결됨.
- `Floor3F에 사전 Bake된 NavMeshData가 없습니다` 오류는 별도의 T-05 보류 항목이며, 이번 4층 Spawn 후보 탈락 원인과 분리한다.

A-01 구현 기록:

- `Assets/Scripts/DollAttackCoordinator.cs` 추가
- `NavMeshSystem`에 `DollAttackCoordinator` 컴포넌트 연결
- 현재 `GameProgressController`의 단일 `CurrentAttacker` 예약/해제 API 재사용
- 공격 후보는 현재 층, `Chase` 상태, 시야 밖, 유효 NavMesh 경로, 공격 거리 조건을 모두 확인
- 조건을 만족하는 후보 중 플레이어와 수평 거리가 가장 가까운 인형만 예약
- `CeilingDollController`는 예약을 얻은 인형만 `BeginJumpscare`를 호출
- 예약 기본 시간은 `1.5초`이며 A-02에서 유지/해제 조건을 확장한다.

A-02 구현 기록:

- 예약 유효 시간은 `1.5초`로 유지하며, 매 프레임 가장 가까운 인형으로 교체하지 않는다.
- `DollJumpscareController.IsAttackComplete`를 추가해 게임오버 지연이 끝난 공격의 완료를 명시한다.
- 공격 완료 시 `DollAttackCoordinator`가 `GameProgressController.ReleaseAttackReservation`을 호출한다.
- 예약 인형이 제거되거나 참조가 사라진 경우 예약을 해제한다.
- 예약 중인 인형의 상태가 `Chase`가 아니거나, 플레이어에게 보이거나, NavMesh 경로가 끊기거나, 공격 거리 밖으로 벗어나면 예약을 해제한다.
- 층 전환 시작, GameOver, Restart의 전역 공격권 해제는 기존 `GameProgressController`의 상태 전환/초기화 API를 사용한다. 세부 잠금 우선순위 확장은 `A-04`에서 담당한다.

A-03 구현 기록:

- 가장 가까운 비공격 후보 1개는 플레이어에게 직접 접근해 공격 후보가 될 수 있도록 유지한다.
- 나머지 인형은 플레이어 주변 `2.4m` 반경의 서로 다른 NavMesh 접근 지점을 사용한다.
- 접근 지점은 인형 간 최소 `1.5m` 간격을 검사하며, 같은 층 높이의 NavMesh 위치만 승인한다.
- 접근 지점이 장애물이나 NavMesh 경계 때문에 유효하지 않으면 기존 직접 목적지로 안전하게 되돌아간다.
- `NavMeshAgent`의 `HighQualityObstacleAvoidance`와 인형별 결정적 `Avoidance Priority`를 적용했다.
- 추가 Inspector 설정은 `NavMeshSystem > Doll Attack Coordinator`의 `Approach Radius = 2.4`, `Minimum Approach Separation = 1.5`다.

A-04 구현 기록:

- `DollAttackCoordinator.IsAttackLocked`를 추가해 `Playing`이 아닌 상태에서는 공격권을 획득하지 않는다.
- `FloorTransition` 진입 시 기존 공격권을 즉시 해제한다.
- `GameOverController.ShowGameOver`가 `GameProgressController.TrySetGameOver`를 호출해 UI 상태와 게임 진행 상태를 동기화한다.
- GameOver UI가 표시된 동안 코디네이터는 공격권을 해제하고 새 공격을 차단한다.
- `CeilingDollController`는 공격 직전에 잠금 상태를 다시 확인해 층 전환/GameOver 중 점프스케어를 시작하지 않는다.
- Restart는 기존 씬 재로드를 통해 코디네이터와 공격권을 함께 초기화한다.

### 단계 8. 인접층 확장과 전체 인형 이전

| 순서 | ID | 작업 | 담당 | 완료 조건 | 상태 |
|---:|---|---|---|---|---|
| 8.1 | X-01 | 나머지 층 Proxy Mesh와 층별 Bake | 사용자 | 층별 NavMeshData가 분리 저장되고 서로 연결되지 않음 | 해결됨 |
| 8.2 | X-02 | 층별 Spawn 8개, Exit, Wall Entry 배치 | 사용자 | 상승/하강 양방향 연결 정보가 설정됨 | 해결됨 |
| 8.3 | X-03 | `FloorTransitionCoordinator` 구현 및 씬 연결 | Codex/사용자 | 공격 잠금부터 이전 층 정리까지 순서 보장 | 해결됨 |
| 8.4 | X-04 | 보이지 않는 인형의 안전 재배치 구현 | Codex | 시야 밖 예약 지점과 예상 이동 지연을 사용 | 해결됨 |
| 8.5 | X-05 | 보이는 인형의 WallTransfer 상승/하강 구현 | Codex | Agent 비활성화, 외벽 연출, 대상 NavMesh 복귀 순서 준수 | 해결됨 |
| 8.6 | X-06 | 외벽 이동 애니메이션/경로 연결과 연출 확인 | 사용자 | 상승과 하강이 벽을 따라 자연스럽게 보임 | 해결됨 |
| 8.7 | X-07 | 모든 인형 층 왕복 검증 | 사용자 | 이전 층에 활성 인형이 남지 않고 총수가 유지됨 | 해결됨 |

#### X-01 사용자 실행 절차

X-01은 3층부터 시작한다. 2층과 1층은 3층 Bake가 완료된 뒤 같은 방식으로 진행한다. X-01에서는 Proxy Mesh와 층별 NavMeshData만 만들며, Spawn Point·Exit Point·Wall Entry Point 배치는 X-02에서 진행한다.

3층 작업 완료 보고를 반영했다. `ProxyMesh_3F` 제작, `NavMeshData_3F` 저장, `Floor3F` 연결, `NavMeshSurface`와 Proxy Renderer 비활성화가 완료됐다. 2층과 1층 작업도 같은 조건으로 완료되어 X-01 전체가 해결됐다. 다음 작업은 X-02다.

현재 4층의 실제 계층과 기준값은 다음과 같다.

```text
Parking/NavMeshSystem/Floors/Floor4F/Navigation/ProxyMesh_4F
```

4층 `Navigation`의 현재 Local Position Y는 `1.48`이다. 같은 구조를 복제해 층 기준 Y 차이만큼 이동할 때의 초기 Local Position Y는 다음과 같다. 배치 후 Scene 뷰에서 해당 층 천장 하부면과 일치하는지 반드시 확인한다.

| 대상 층 | FloorReference Y | Navigation Local Y 초기값 | Proxy 이름 | NavMeshData 이름 |
|---|---:|---:|---|---|
| `Floor3F` | `9.561` | `-1.965` | `ProxyMesh_3F` | `NavMeshData_3F` |
| `Floor2F` | `6.081` | `-5.445` | `ProxyMesh_2F` | `NavMeshData_2F` |
| `Floor1F` | `2.776` | `-8.750` | `ProxyMesh_1F` | `NavMeshData_1F` |

복제한 `Navigation`의 `NavMeshSurface > NavMesh Data`에는 4층 에셋이 남아 있을 수 있으므로, 새 층 Bake 전에 반드시 `Clear`로 비운다. Bake가 끝나면 각 층의 `FloorController`에 해당 NavMeshData를 연결하고 `Navigation Min Y`는 더 작은 값, `Navigation Max Y`는 더 큰 값으로 실제 Bake 높이를 포함하도록 설정한다.

### 단계 9. Restart 통합

| 순서 | ID | 작업 | 담당 | 완료 조건 | 상태 |
|---:|---|---|---|---|---|
| 9.1 | R-01 | 명시적 Restart 정리 절차 구현 | Codex | 공격, 예약, Agent, NavMeshData, 진행 상태를 순서대로 정리 | 해결됨 |
| 9.2 | R-02 | 4층과 인접층 재등록, 시작 인형 0개 복원 | Codex | Restart 직후 명세의 초기값과 일치 | 재검증 필요 |
| 9.3 | R-03 | 자동차, WallTransfer, 대기열과 공격권 초기화 | Codex | 이전 실행 상태가 남지 않음 | 해결됨 |
| 9.4 | R-04 | GameOver/Restart 반복 Play Mode 검증 | 사용자 | 여러 번 반복해도 인형 0개 시작, NavMeshData, 예약이 중복되지 않음 | 확인 필요 |

#### R-01 구현 완료 기록

`GameOverController`의 Restart 버튼이 씬을 다시 로드하기 전에 `RestartCoordinator.CleanupBeforeSceneReload()`를 호출하도록 연결했다. 정리 순서는 다음과 같다.

1. `GameProgressController`를 Restarting 상태로 전환하고 공격권을 해제한다.
2. `DollAttackCoordinator`의 공격 예약과 `FloorTransitionCoordinator`의 Exit/WallTransfer 추적을 중단한다.
3. 모든 `CeilingDollController`의 추적 경로, NavMeshAgent, 점프스케어 상태를 정리한다.
4. `SpawnPointManager`의 Spawn 예약·점유를 초기화하고 `FloorNavigationManager`의 등록된 NavMeshData를 해제한다.
5. 진행 데이터와 플레이어 차키를 새 게임 기준으로 초기화한 뒤 씬을 재로드한다.

Restart 호출 시 `[Restart] cleanup-start`, `[Restart] cleanup-complete` 로그를 각각 한 번 출력하도록 했다. 코드 컴파일 검사는 Unity 6000.3.18f1 참조 어셈블리 기준으로 오류 없이 통과했다.

#### R-02 구현 완료 기록

씬이 열린 뒤 한 프레임 후 `RestartCoordinator.InitializeCurrentSceneForNewGame()`이 실행되도록 연결했다. 현재 기획 변경에 따라 이 초기화는 `GameProgressController`를 4층·인형 0개·실패 0회·자동차 미조사 상태로 되돌리고, `FloorNavigationManager`의 등록 NavMeshData를 비운 뒤 현재 4층과 인접한 3층만 다시 등록한다. `SpawnPointManager`의 모든 예약·점유·Blocked 상태도 초기화한다.

`DollRosterManager`는 런타임 인형과 대기 인형을 제거하고, 씬의 시작 인형 오브젝트는 비활성 복제 템플릿으로 유지한다. 따라서 Restart 직후 활성 인형은 없으며, 첫 자동차 실패에서 템플릿 복제본이 Spawn Point에 배치된다.

추가된 초기화 로그는 `[Restart] new-game-initialized floor=Floor4F, registeredFloors=2, initialDoll=0` 형식으로 재검증한다. 기존의 `initialDoll=1` 기록은 기획 변경 전 결과다. R-02 코드는 유지되지만 새 초기값 검증이 필요하다.

#### R-03 구현 완료 기록

Restart 정리 단계에 자동차와 플레이어 상호작용의 런타임 상태 초기화를 추가했다. `CarKeyGimmickManager`는 재생 중인 경적을 중지하고, `CarInteractionController`는 현재 조준 자동차와 차키 안내 UI를 비운다. 자동차별 조사 결과·전체 실패 횟수·차키 보유 상태는 `GameProgressController`와 `PlayerCarKeyInventory`의 새 게임 초기값으로 되돌아간다.

`DollRosterManager`는 추가 인형과 생성 대기 인형을 Restart 전에 제거한다. `SpawnPointManager`는 예약·점유·Blocked 상태와 Spawn 대기열을 비우고, `FloorTransitionCoordinator`는 Exit 이동·WallTransfer 추적과 전환 대기 목록을 지운다. `DollAttackCoordinator`는 공격 인형과 예약 만료 시간을 해제한다. 따라서 씬 재로드 전에 자동차, WallTransfer, Spawn 대기열, 공격권의 이전 실행 참조가 남지 않는다.

정리 직후에는 `[Restart] runtime-cleared cars=true, failureCount=0, dollCount=0, wallTransferActive=False, pendingDolls=0, spawnQueue=0, attackReservation=False` 형식의 요약 로그를 출력해 초기화 결과를 확인할 수 있다.

코드 컴파일 검사는 Unity 6000.3.18f1 참조 어셈블리 기준으로 오류 없이 통과했다. R-03은 해결됨이며, 다음은 사용자 담당 R-04 GameOver/Restart 반복 Play Mode 검증이다.

#### R-04 사전 오류 보정 기록

R-04 첫 Play Mode 결과에서 기본 인형은 `외벽 이동을 시작합니다` 뒤 3층 완료 로그 없이 4층에 남았고, 3층 플레이어에게 공격 판정이 발생했다. 원인은 외벽 도착 지점에서 NavMeshAgent를 먼저 활성화해 이전 NavMesh 위치로 되돌아갈 수 있던 처리와, NavMesh 위에 아직 올라가지 않은 Agent에 `isStopped`를 호출하던 처리였다.

외벽 이동 완료 시에는 먼저 대상 층의 DollCeiling NavMesh 위치를 최대 3m 반경으로 찾고, 인형 Transform을 그 위치로 옮긴 뒤 Agent를 활성화·Warp하도록 수정했다. 또한 NavMesh에 실제로 올라가 있지 않은 인형은 현재 층 추적·공격 후보에서 제외한다. 이로써 전환 실패가 생겨도 4층 인형이 3층 플레이어를 공격할 수 없으며, `Stop can only be called on an active agent that has been placed on a NavMesh` 오류도 발생하지 않도록 모든 정지 호출을 NavMesh 연결 여부로 보호했다. 코드 컴파일 검사는 통과했으며, R-04 상태는 사용자 재검증 전까지 `대기`를 유지한다.

전환 보정 사용자 확인: `Floor4F → Floor3F` Commit 뒤 기본 인형 `Ceiling Killer Doll 01`과 추가 인형이 각각 `Floor3F` Spawn Point로 안전 재배치됐고, Console에 `모든 인형 이전 완료. 층 전환을 종료합니다.`가 출력됐다. 4층에 남은 인형이 3층 플레이어를 공격하던 문제는 해결됐다. 이 확인은 전환 보정 항목에 대한 것이며, GameOver → Restart 반복 검증인 R-04는 아직 수행하지 않았다.

R-04 테스트에서 반복 확인된 노란 `Failed to create agent because there is no valid NavMesh` 경고는 기본 인형의 NavMeshAgent가 NavMeshData 등록 전 활성화된 상태에서 생성되던 것이 원인이었다. `Assets/Prefabs/Ceiling Killer Doll 01.prefab`의 NavMeshAgent를 기본 비활성으로 저장하고, 씬 초기화가 4층 시작 Spawn Point에 배치한 뒤 Agent를 활성화하도록 수정했다. 기본 인형 GameObject와 `CeilingDollController`는 계속 활성 상태이며, 추가 인형은 유효한 Spawn Point에서만 활성화된다. 다음 R-04 재실행에서 이 경고가 사라지는지 확인한다.

#### R-04 사용자 실행 절차

R-04는 GameOver 화면의 `RESTART`를 세 번 반복해 새 게임 초기값이 중복 없이 적용되는지 확인한다. 이 단계에서 사용자가 변경할 Hierarchy 또는 Inspector 값은 없다.

1. Play Mode를 종료하고 Console의 `Clear`를 누른다.
2. Play Mode를 시작한다. 첫 프레임 초기화가 끝난 뒤 Console에 `new-game-initialized floor=Floor4F, registeredFloors=2, initialDoll=0`이 출력되는지 확인한다.
3. 자동차를 한 번 실패시켜 `Ceiling Killer Doll 01_Runtime_01`을 생성한다. Console에서 `현재 인형 수 1/6개`를 확인한다.
4. 인형이 플레이어를 공격하도록 카메라를 돌려 GameOver 화면을 띄운다.
5. GameOver 화면에서 `RESTART` 버튼을 한 번만 클릭한다.
6. Restart 직후 Console에 다음 네 로그가 각각 한 번씩 출력되는지 확인한다.

```text
[Restart] cleanup-start
[Restart] runtime-cleared cars=true, failureCount=0, dollCount=0, wallTransferActive=False, pendingDolls=0, spawnQueue=0, attackReservation=False
[Restart] cleanup-complete
[Restart] new-game-initialized floor=Floor4F, registeredFloors=2, initialDoll=0
```

7. `[Hierarchy] Parking > Ceiling Killer Doll 01`은 비활성 템플릿으로 남아 있고, 런타임 인형이 Restart 후 남아 있지 않은지 확인한다. `Ceiling Killer Doll 01_Runtime_01`이 씬에 남아 있으면 실패다.
8. `[Inspector: GameProgressController]`에서 Restart 후 다음 값만 확인한다.
   - `Current Floor ID = Floor4F`
   - `Current Doll Count = 0`
   - `Total Failure Count = 0`
   - `Progress State = Playing`
   - `Floor Transition State = Idle`
   - `Current Attacker = None`
9. 같은 과정을 두 번 더 반복한다. 매번 활성 인형은 0개, 초기 층은 4층, 등록 층 수는 2개여야 한다.

R-04 통과 조건은 세 번의 Restart에서 추가 인형·자동차 실패 기록·Spawn 예약·WallTransfer·공격권이 누적되지 않고, 빨간 Console 오류와 노란 `Failed to create agent because there is no valid NavMesh` 경고가 없으며, `new-game-initialized` 로그가 중복 호출되지 않는 것이다. 경고가 계속 나오면 발생 횟수와 시점을 별도로 기록해 전달한다.

R-04 완료 확인: 사용자가 GameOver 후 `RESTART`를 세 번 반복했다. Console 상단 카운터가
로그 25, 경고 0, 오류 0으로 표시됐고, `cleanup-start`,
`runtime-cleared cars=true, failureCount=0, dollCount=0, wallTransferActive=False,
pendingDolls=0, spawnQueue=0, attackReservation=False`, `cleanup-complete`가 각각 3회
집계됐다. Restart 후 기본 인형 등록도 각 회차에서 다시 수행됐으며 NavMeshAgent 경고가
발생하지 않았다. R-04는 해결됨이다.

#### R-04와 최종 검증의 진행 순서 판단

R-04는 최종 검증과 병행하거나 건너뛰지 않는다. R-04가 확인하는 것은 단순히 Restart
버튼이 눌리는지가 아니라, 이전 회차의 인형·자동차 실패 횟수·Spawn 예약·WallTransfer·
공격권·NavMeshData가 모두 제거된 뒤 새 회차가 `Floor4F + 활성 인형 0개`로 시작하는지다.
이 초기화가 보장되지 않으면 V-04의 20개 시나리오가 이전 테스트 상태의 영향을 받아 결과를
신뢰할 수 없다. 따라서 V-01~V-03의 코드·문서 검증은 먼저 진행할 수 있지만, 사용자
Play Mode 최종 검증인 V-04는 R-04를 세 번 통과한 뒤에 시작한다.

### 단계 10. 최대 6개와 최종 검증

| 순서 | ID | 작업 | 담당 | 완료 조건 | 상태 |
|---:|---|---|---|---|---|
| 10.1 | V-01 | 시작 0개 + 실패 추가 최대 6개 제한 검증 | Codex | 자동차 실패 생성 인형이 최대 6개로 제한 | 해결됨 |
| 10.2 | V-02 | 변경 명세의 필수 시나리오를 테스트 체크리스트로 변환 | Codex | 20개 시나리오의 수행 방법과 예상 결과 기록 | 해결됨 |
| 10.3 | V-03 | 변경 명세의 코드와 자동화 가능한 테스트 실행 | Codex | 컴파일과 상태/중복/제한 테스트 통과 | 해결됨 |
| 10.4 | V-04 | 변경 명세의 필수 시나리오 20개 Play Mode 검증 | 사용자 | 통과 기록은 유지하고 미실행 항목은 최종 통합 플레이에서 확인 | 보류 |
| 10.5 | V-05 | 실패 항목 수정과 진행표 최종 갱신 | Codex | 완료 판정 10개가 모두 충족되고 모든 항목 상태 반영 | 대기 |

#### V-01 완료 기록

`NavMeshSystemTypes`의 고정값을 대조한 결과 `InitialDollCount = 0`,
`MaximumAdditionalDollCount = 6`, `MaximumDollCount = 6`으로 확정되어 있다.
`GameProgressController`의 `CanAddDoll`과 `TryAddDoll`은 현재 수가 제한값에 도달하면
추가를 거부하고, `DollRosterManager`는 생성 요청을 만들기 전 활성·대기 인형 수를
함께 계산해 여섯 번째 추가 요청을 차단한다. Spawn 대기열도 같은 제한 범위로
클램프된다. 저장된 `Parking.unity`의 `GameProgressController > Doll Limit` 역시 `6`이다.
전체 C# 스크립트 컴파일과 위 제한값 정적 대조를 통과했으므로 V-01은 해결됨이다.

#### V-02 필수 테스트 시나리오 체크리스트

V-02는 명세 19절의 20개 시나리오를 사용자가 Play Mode에서 그대로 실행할 수 있는
형식으로 정리하는 작업이다. 이 단계에서는 Hierarchy나 Inspector 값을 변경하지 않는다.
각 항목은 독립 테스트를 기본으로 하며, 한 항목에서 GameOver가 발생하면 `RESTART`를
한 번 눌러 다음 항목을 새 게임 상태에서 시작한다.

##### 공통 준비

1. `[Scene]` `Assets/Scenes/Parking.unity`를 연다.
2. `[Hierarchy]` `Parking > NavMeshSystem`을 선택해 관리자들이 씬에 존재하는지만 확인한다.
3. `[Inspector]` 값을 수정하지 않는다. 현재 씬은 `GameProgressController > Doll Limit = 6`,
   층당 Spawn Point 8개, 기믹 자동차 10대 상태다.
4. `[Action]` Play Mode를 시작하기 전 Console의 `Clear`를 누른다. 빨간 오류가 하나라도
   있으면 해당 시나리오는 실패로 기록하고 오류 원인을 먼저 해결한다.
5. `[Input]` 이동은 `W/A/S/D`, 시선은 마우스, 자동차 조사는 화면 중앙 조준 후 `E`다.
   GameOver 후에는 화면의 `RESTART` 버튼을 한 번만 누른다.
6. 서로 다른 자동차 실패가 필요한 항목은 저장된 씬의 고유한 `Car ID` 자동차 10대를 사용한다.

##### 시나리오별 실행과 예상 결과

| 번호 | `[Action]` 실행 순서 | `[확인 기준]` 성공 조건 |
|---:|---|---|
| 1 | 새 Play Mode를 시작하고 첫 프레임 초기화가 끝날 때까지 기다린다. | `[Hierarchy]` `Ceiling Killer Doll 01` 템플릿은 비활성 상태이고 활성 런타임 인형은 0개다. Console에 `new-game-initialized floor=Floor4F`가 출력된다. |
| 2 | 실패 자동차가 없는 상태에서 주차장 천장을 둘러본다. | 시작하자마자 인형이 보이지 않는다. 자동차 실패 전에는 추적·공격 인형이 없다. |
| 3 | 첫 자동차 실패 후 생성된 인형을 시야 밖에 둔 상태로 기둥·빈 공간 주변을 이동시킨다. | 인형이 기둥·제외 구간을 통과하지 않고 천장 NavMesh 위에서만 이동한다. 바닥으로 떨어지거나 다른 층으로 가지 않는다. |
| 4 | 보유 차키와 다른 `Car ID`의 미조사 자동차를 중앙 조준하고 `E`를 한 번 누른다. | 경적이 1회 재생되고 자동차가 실패 상태가 된다. Console에 실패 처리와 Spawn 요청이 출력되며 인형 수가 `1/6`이 된다. 추가 인형은 플레이어 시야 밖에서 생성된다. |
| 5 | 4번에서 실패시킨 동일 자동차를 다시 조준한다. | 자동차가 다시 조사되지 않고 새 인형도 생성되지 않는다. 인형 수와 실패 횟수가 증가하지 않는다. |
| 6 | 보유 차키와 일치하는 자동차를 조준하고 `E`를 누른다. 이어 같은 자동차를 다시 조준한다. | 첫 조사는 성공 상태가 되고, 두 번째 조사 프롬프트가 나타나지 않는다. 성공 자동차의 실패 경적·추가 인형 생성은 없어야 한다. |
| 7 | 실패 가능한 자동차를 조준한 뒤 `E`를 누르는 순간 카메라를 반대 방향으로 둔다. | 생성 로그는 나오지만 생성 순간 인형이 화면에 보이지 않는다. 인형은 유효한 Spawn Point에서 시작한다. |
| 8 | 서로 다른 실패 자동차 6대를 각각 한 번씩 실패시킨다. | 첫 실패부터 `1/6`으로 시작해 `6/6`에서 멈춘다. 일곱 번째 인형은 절대 발생하지 않는다. |
| 9 | 인형이 2개 이상인 상태에서 둘을 시야 밖에 둔 뒤 공격 거리까지 접근시킨다. | 가장 가까운 인형 하나만 공격권을 예약하고 공격한다. 동시에 두 인형이 점프스케어를 시작하지 않는다. |
| 10 | 4층에서 `4F_to_3F` 경로의 Preload 후 Commit Trigger를 차례로 통과한다. | Commit 후 모든 활성 인형이 3층으로 이동하고, 4층에 남은 인형이 3층 플레이어를 공격하지 않는다. |
| 11 | 10번 완료 후 전환 쿨다운 2.5초가 지난 뒤 `3F_to_4F` 역방향 Trigger를 통과한다. | 모든 인형이 4층으로 복귀하고 3층에 남지 않는다. 전체 인형 수가 유지된다. |
| 12 | 한 층 전환이 진행 중일 때 반대 방향 Trigger를 즉시 통과한다. | 역방향 요청은 현재 전환이 끝날 때까지 대기하고, 첫 전환 완료 후 한 번만 실행된다. 전환 중 중복 Commit이나 인형 중복 생성이 없다. |
| 13 | 전환 직전 인형을 화면 중앙에 보이게 유지한 채 Commit Trigger를 통과한다. | 인형이 `현재 층 천장 → Exit Point → 외벽 → 대상 층 Wall Entry Point → 대상 층 천장` 순서로 이동한다. 외벽 구간에서 순간이동하지 않는다. |
| 14 | 전환 직전 인형을 카메라 뒤에 둔 채 Commit Trigger를 통과한다. | 외벽 연출 대신 대상 층의 시야 밖 Spawn Point로 안전 재배치된다. 대상 층 NavMesh에 올라온 뒤 추적을 재개한다. |
| 15 | 테스트용 장애물로 대상 층 Spawn 후보 또는 인형 경로 일부를 막고 전환을 실행한다. | 실패한 경로를 무한 반복하지 않고 다른 Spawn Point/경로를 재시도한다. 최대 3회 실패 후 Dormant 대기 또는 안전한 대체 경로로 전환되며 게임이 멈추지 않는다. 테스트 후 임시 장애물은 삭제한다. |
| 16 | 4층, 3층, 2층에서 각각 전환을 실행하고 Console의 초기화·전환 로그를 확인한다. | 현재 층과 바로 위·아래층만 등록된다. 예를 들어 4층은 `4F+3F`, 3층은 `4F+3F+2F`, 2층은 `3F+2F+1F` 범위여야 하며 먼 층 NavMesh는 등록되지 않는다. |
| 17 | 인형이 2개 이상인 상태에서 전환 Commit 직전 공격 거리로 유도하고, 전환 중에도 시선을 돌렸다가 다시 본다. | 전환 상태 동안 공격권이 해제되고 새 점프스케어가 시작되지 않는다. 모든 인형 이전 완료 후에만 일반 추적·공격이 재개된다. |
| 18 | 인형을 추가 생성하고 GameOver를 발생시킨 뒤 `RESTART`를 누른다. | 새 회차가 4층, 인형 0개, 실패 0회로 시작한다. Console에 `new-game-initialized floor=Floor4F`가 출력된다. |
| 19 | 자동차 실패와 Spawn 예약이 발생한 직후 GameOver를 만들고 `RESTART`를 누른다. | 자동차 조사 상태가 새 게임 기준으로 돌아가고, Spawn 예약·점유·대기열이 남지 않는다. Restart 요약에서 `failureCount=0`, `dollCount=0`, `spawnQueue=0`이어야 한다. |
| 20 | 18~19번 과정을 최소 3회 반복한다. | NavMeshData 등록 수, 인형 수, Agent, Spawn 예약, 공격권이 회차마다 누적되지 않는다. 빨간 오류와 `Failed to create agent because there is no valid NavMesh` 경고가 없어야 한다. |

##### V-04에서 사용할 기록 양식

사용자는 각 번호를 테스트한 뒤 아래 형식으로 결과를 전달한다. 자동차가 부족해 실행할 수
없는 항목은 임의로 성공 처리하지 말고 `자동차 추가 후 실행`으로 기록한다.

```text
V-04 결과
1: 성공 / 실패 / 자동차 추가 후 실행
2: 성공 / 실패 / 자동차 추가 후 실행
...
20: 성공 / 실패 / 자동차 추가 후 실행
Console 빨간 오류: 0개 / 발생
NavMesh 경고: 0개 / 발생
```

V-02는 위 20개 항목의 실행 순서·예상 결과·현재 자동차 1대 제한을 문서화했으므로 해결됨이다.

#### V-03 자동 검증 완료 기록

코드와 저장된 씬을 대상으로 다음 검사를 실행했다.

| 검사 | 결과 |
|---|---|
| `InitialDollCount = 0`, `MaximumAdditionalDollCount = 6`, `MaximumDollCount = 6` | PASS |
| `GameProgressController.CanAddDoll`의 제한값·하드 상한 검사 | PASS |
| `GameProgressController.TryAddDoll`의 제한 도달 시 거부 | PASS |
| `DollRosterManager`의 활성 인형 + 대기 인형 합산 초과 요청 차단 | PASS |
| Spawn 대기열 최대값과 하드 상한 클램프 | PASS |
| Spawn 예약·점유·대기열 Restart 정리 | PASS |
| `FloorNavigationManager`의 유효 Instance 중복 등록 방지 | PASS |
| 현재 층과 인접 층 외 NavMeshData 해제 로직 | PASS |
| Restart 시 NavMeshData·인형·Spawn·진행 상태 초기화 호출 | PASS |
| 씬의 FloorController 4개 고유 ID와 층당 Spawn Point 8개 | PASS |
| Unity 참조 어셈블리 기준 전체 C# 컴파일 | PASS |

자동 검증 중 프로젝트 코드의 제한·상태·중복 관련 실패는 발견되지 않았다. 따라서 V-03은
해결됨이며, 다음은 사용자가 수행하는 V-04 Play Mode 20개 시나리오 검증이다.

#### V-04 사용자 Play Mode 실행 절차

V-04는 명세 19절의 20개 시나리오를 실제 씬에서 실행하는 사용자 담당 단계다. 이 단계에서
생산용 Inspector 값을 임의로 바꾸지 않는다. 자동차가 1대뿐인 현재 씬에서는 서로 다른
자동차 6대가 필요한 8번을 완료할 수 없으므로 `자동차 추가 후 실행`으로 기록한다.

##### 공통 준비

1. `[Scene]` `Assets/Scenes/Parking.unity`를 연다.
2. Play Mode를 종료한 상태에서 씬을 저장한다.
3. `[Hierarchy]` `Parking > NavMeshSystem`이 존재하는지만 확인한다.
4. `[Inspector]` 생산용 값은 변경하지 않는다. 현재 확인된 기준은 `Doll Limit = 6`,
   층당 Spawn Point 8개, R-04 통과 상태다.
5. Console의 `Clear`를 누른 뒤 Play Mode를 시작한다.
6. 빨간 오류가 발생하면 해당 번호를 실패로 기록하고, 오류를 해결하기 전 다음 번호로
   넘어가지 않는다. 노란 `Failed to create agent because there is no valid NavMesh`도 실패다.
7. 한 시나리오에서 GameOver가 발생하면 `RESTART`를 한 번 눌러 다음 시나리오를 새 회차에서
   시작한다. Restart 버튼을 연속으로 누르지 않는다.
8. 자동차가 필요한 시나리오는 자동차의 조사 상태가 `Unexamined`인 새 회차에서 시작한다.
   같은 자동차를 한 회차에서 두 번 실패시키지 않는다.

저장된 씬에는 기믹 자동차 10대가 연결돼 있다. 플레이어 `PlayerCarKeyInventory > Starting Key ID`는
`CAR_4F_01`이고, 이에 일치하는 정답 차량은 2층의 `2F_BBW30_UP_ESCAPE`다. 4층의
`4F_Retro Car`는 `CAR_4F_02`이므로 실패 테스트에 사용한다. 성공 시나리오 6번은
`2F_BBW30_UP_ESCAPE`, 최대 인형 수 시나리오 8번은 정답 차량을 제외한 서로 다른 실패 차량을 사용한다.

##### V-04 부분 검증 기록

- `1~5번`, `7번`: 해결됨. 사용자 Play Mode 확인에서 새 게임은 `Floor4F`, 시작 인형 `0개`로
  초기화됐고 `4F_Retro Car(CAR_4F_02)` 실패 시 경적 1회, 시야 밖 Spawn 요청, 런타임 인형
  `1/6` 등록과 생성 완료가 확인됐다.
- 생성된 인형은 관찰 중 정지, 시야 밖 이동, 천장 NavMesh 이동이 정상이며 같은 자동차의
  재조사와 중복 생성이 차단됐다.
- 해당 실행의 Console은 빨간 오류 `0개`, 노란 경고 `0개`였다.
- `6번`: 해결됨. 2층 `2F_BBW30_UP_ESCAPE`에서 보유 키와 자동차 ID가 모두
  `CAR_4F_01`로 일치해 `처리=True`가 확인됐다. 재조사 안내, 실패 경적, 인형 생성 없이
  인형 `0개`가 유지됐다.
- 이 실행의 `StairA_Preload_2F_to_3F` 처리되지 않음 경고는 현재 층을 다시 요청한 반대 방향
  Preload 중복 감지이며, 직후 `Floor3F → Floor2F` Commit과 정답 차량 처리는 정상 완료됐다.
- `8번` 1차 실행 실패: 2층 진입 시 기존 인형 5개와 신규 인형 1개의 안전 재배치 후보가
  모두 `NotOnNavMesh 8개`로 탈락했다. 활성 명단 `5개 + 대기 1개 / 제한 6개` 상태가 되어
  2층부터 인형이 보이지 않았으므로 최대 6개 검증 완료로 처리하지 않는다.
- 저장 씬 진단 결과 2층 Spawn Point 월드 Y는 약 `6.006`, Proxy Mesh 월드 Y는 약 `5.985`로
  서로 정상 범위다. 직접 원인은 `Floor_2F > FloorController > Ceiling Proxy Mesh Root`가
  `ProxyMesh_2F`가 아니라 `FloorReference2F`를 참조하는 것이다. `BakedNavMeshOrigin`이 잘못된
  부모 Transform을 사용해 NavMeshData 등록 위치가 약 `5.445m` 위로 이동한다.
- 다음 조치: `Ceiling Proxy Mesh Root`를 `Navigation_F2 > ProxyMesh_2F`로 다시 연결한 뒤
  3F→2F 인형 이전과 2층 Spawn을 재검증한다.

- 2층 재검증 완료: `Floor_2F > FloorController > Ceiling Proxy Mesh Root`를 `ProxyMesh_2F`로
  연결한 뒤 2층에서 여섯 번째 런타임 인형이 정상 생성되어 `6/6`에 도달했다.
- `8번`: 해결됨. `6/6` 도달 후 `CAR_2F_02` 추가 실패에서 `Spawn 요청 가능 여부=False`가
  출력됐고 추가 인형은 생성되지 않았다.
- 1층 전환 검증 실패: 활성 인형 6개 모두 `Floor1F 안전 재배치 후보 없음`, 탈락 원인
  `WrongNavigationHeight 8개`로 이전되지 않았다.
- 저장 씬 진단 결과 `SpawnPoints_1F`의 Local Y가 2층과 같은 `-6.925`로 저장돼 자식 Spawn
  Point의 월드 Y가 모두 약 `6.006`이다. 1층 Proxy Mesh는 약 `Y 2.680`, 허용 범위는
  `2.2~3.2`이므로, `SpawnPoints_1F` Local Y를 `-10.230`으로 내려 자식 월드 Y를 약
  `2.701`로 맞춰야 한다. Proxy와 NavMeshData는 다시 Bake할 필요가 없다.
- 1층 Spawn Point 보정 적용: 저장된 `Parking.unity`의 `SpawnPoints_1F` Local Y를
  `-6.925`에서 `-10.230`으로 수정했다. 자식 Spawn Point 월드 Y는 약 `2.701`이며,
  Play Mode에서 2F→1F 인형 이전 확인만 남았다.
- 1층 인형 이전 재검증 완료: `Floor2F → Floor1F 전환 Commit 완료` 뒤 런타임 인형이
  `Floor1F의 SpawnPoint_1F_03`으로 안전 재배치됐고 `WrongNavigationHeight` 및
  `Floor1F 안전 재배치 후보 없음` 로그가 사라졌다. 1층 Spawn Point 높이 문제는 해결됨이다.
- `V-04 9번`: 해결됨. 활성 인형 2개 상태에서 `Ceiling Killer Doll 01_Runtime_02` 한 개만
  공격권을 `1.50초` 예약했고 동시 공격 예약은 발생하지 않았다.
- `V-04 18번`: 해결됨. GameOver 후 Restart에서 `runtime-cleared cars=True`,
  `failureCount=0`, `dollCount=0`이 확인됐고 `cleanup-complete` 뒤 Spawn Point 32개가
  새 회차 기준으로 다시 등록됐다.
- `V-04 10·11·13·14번`: 해결됨. 사용자가 전날 반복 검증에서 관찰 중 인형의
  `현재 층 천장 → Exit Point → 외벽 → Wall Entry Point → 대상 층 천장` 이동과,
  비관찰 인형의 대상 층 Spawn Point 안전 재배치, 4F↔3F 왕복 및 인형 수 유지를 확인했다.
  이후 1층 Spawn 높이 보정 검증에서도 동일한 안전 재배치 흐름이 정상 동작했다.
- 사용자 결정에 따라 V-04의 남은 개별 검사 `12·15·16·17·19·20번`은 지금 반복하지
  않고 디테일 작업 완료 후 최종 통합 플레이로 이관한다. 기존 통과 항목은 유지하며,
  미실행 항목을 임의로 해결됨 처리하지 않는다.

#### 디테일 보정: 정상 Preload 접촉 경고 정리

- 양방향 경계의 `Commit`과 반대 방향 `Preload`가 같은 위치를 공유하고, 계단 이동 중 다음
  구간 Preload를 현재 Commit보다 먼저 스칠 수 있음을 저장 씬 좌표로 확인했다.
- `FloorTransitionTrigger`는 현재 NavMesh 층과 대상 층이 인접하지 않은 Preload 접촉을
  예상된 무시로 처리한다. 현재 층 재요청과 두 층 이상 떨어진 사전 접촉은 경고를 출력하지
  않으며, 실제 인접층 Preload 실패 경고와 Commit 검증은 그대로 유지한다.
- Unity가 꺼진 상태라 생성된 `Temp/obj`가 없어 독립 빌드는 수행되지 않았고, Unity를 열 때
  에디터 컴파일 확인이 남아 있다.

#### 디테일 보정: 실패 차량 경보 전조등

- 자동 탐지가 차량 전체 Renderer를 발광시키는 문제가 확인되어 사용자 결정으로 기능을
  전부 제거했다. `CarAlarmFlash`, 자동차별 점멸 필드, 씬 저장값과 Configurator 연결을
  삭제했으며 자동차 실패 연출은 기존 경적 1회만 유지한다.

#### 디테일 보정: 인형의 붉은 발광 눈

- 기존 `DollEyeGlowController`를 `Ceiling Killer Doll 01` 프리팹 루트에 연결해 비활성
  템플릿과 자동차 실패로 생성되는 모든 런타임 복제 인형에 자동 적용했다.
- 1차 구현의 추가 눈 구체와 Point Light가 얼굴과 몸 전체를 붉게 비추는 문제가 확인되어
  해당 방식과 눈 유리 재질 변경을 모두 제거했다.
- 최종 구현은 프리팹에 이미 존재하는 `LeftPupil`, `RightPupil` Renderer 두 개만 찾아
  붉은 URP Unlit 재질을 적용한다. 몸·얼굴·눈 유리·천장 재질은 변경하지 않고 실제 Light도
  생성하지 않는다. 초기 `Glow Intensity`는 `2.2`다.
- 카메라 Post Processing은 활성 상태이므로 Bloom 설정이 존재하면 두 Pupil 픽셀에만 작은
  번짐이 나타난다. Unity 에디터 컴파일과 인형 1개의 육안 확인이 남아 있다.

##### 1~3번: 시작 인형 없음과 생성 후 이동

1. `[Action]` Play Mode 시작 후 첫 프레임 초기화가 끝날 때까지 기다린다.
   `[확인 기준]` `Parking > Ceiling Killer Doll 01` 템플릿은 비활성이고 활성 런타임 인형은
   0개다. Console에 `new-game-initialized floor=Floor4F`가 출력된다.
2. `[Action]` 첫 자동차를 실패시켜 런타임 인형 1개를 생성한 뒤 화면 중앙에 보이게 둔다.
   이어 마우스로 시선을 돌려 인형을 화면 밖으로 보낸다.
   `[확인 기준]` 보이는 동안 인형은 정지하고, 시야 밖에서 다시 이동한다. 순간이동이 없어야 한다.
3. `[Action]` 생성된 인형을 시야 밖에 둔 채 기둥·계단·천장 구멍 주변을 지나가도록 유도한다.
   `[확인 기준]` 인형이 제외 구간과 바닥을 통과하지 않고 천장 NavMesh 위에서만 이동한다.

##### 4~8번: 자동차와 인형 생성

4. `[Action]` 보유 차키와 다른 `Car ID`의 미조사 자동차를 조준하고 `E`를 한 번 누른다.
   `[확인 기준]` 경적 1회, 실패 처리 로그, Spawn 요청, `현재 인형 수 1/6개`, 시야 밖 생성이
   모두 확인된다.
5. `[Action]` 4번에서 실패한 같은 자동차를 다시 조준한다.
   `[확인 기준]` 조사 프롬프트와 새 Spawn 요청이 없고 인형 수가 그대로다.
6. `[Action]` 새 Play 회차에서 보유 차키와 일치하는 자동차를 조준하고 `E`를 누른 뒤 같은
   자동차를 다시 조준한다.
   `[확인 기준]` 첫 조사만 성공하고 두 번째 조사나 실패 경적·추가 인형이 없다.
7. `[Action]` 새 Play 회차에서 실패 가능한 자동차를 조준한 뒤 `E`를 누르는 순간 카메라를
   반대 방향으로 돌린다.
   `[확인 기준]` 생성 순간 인형이 화면에 보이지 않고 유효한 Spawn Point에서 시작한다.
8. `[Action]` 서로 다른 자동차 6대를 각각 한 번씩 실패시킨다.
   `[확인 기준]` `6/6`에서 멈추고 여섯 번째 추가 요청은 거부된다. 현재 자동차가 1대뿐이면
   `자동차 추가 후 실행`으로 기록하며 성공 처리하지 않는다.

##### 9번: 단일 공격권

9. `[Action]` 4번 또는 8번으로 인형을 2개 이상 만든 뒤 둘을 시야 밖에 둔다. 두 인형이
   플레이어 근처에 접근하도록 기다린다.
   `[확인 기준]` 가장 가까운 인형 하나만 공격권을 예약하고 점프스케어를 시작한다. GameOver가
   뜨면 두 인형이 동시에 공격했는지 먼저 기록한 뒤 `RESTART`한다.

##### 10~17번: 층 전환·외벽 이동·NavMesh 범위

10. `[Action]` 4층에서 `4F_to_3F` 경로의 낮은 위치 `Preload`, 높은 위치 `Commit` 순서로
    `StairA` Trigger를 통과한다.
    `[확인 기준]` 모든 인형이 3층으로 이동하고 4층 인형이 3층 플레이어를 공격하지 않는다.
11. `[Action]` 10번 완료 후 2.5초 이상 기다린 뒤 `StairA`의 역방향 `3F_to_4F` Trigger를
    낮은 위치부터 통과한다.
    `[확인 기준]` 모든 인형이 4층으로 복귀하고 3층에 남지 않으며 인형 수가 유지된다.
12. `[Action]` 한 층 전환 중 반대 방향 Trigger를 즉시 통과한다.
    `[확인 기준]` 역방향 요청이 현재 전환 완료 후 실행되고 중복 Commit·중복 생성이 없다.
13. `[Action]` Commit 직전 인형을 화면 중앙에 계속 보이게 둔다.
    `[확인 기준]` `천장 → Exit Point → 외벽 → Wall Entry Point → 대상 층 천장` 순서로
    이동하며 외벽 구간에서 순간이동하지 않는다.
14. `[Action]` 별도 새 회차에서 Commit 직전 인형을 카메라 뒤에 둔다.
    `[확인 기준]` 외벽 연출 대신 대상 층 시야 밖 Spawn Point로 재배치되고 대상 NavMesh에서
    추적을 재개한다.
15. `[Action]` 테스트가 끝나면 삭제할 임시 Collider로 대상 층의 한 경로 또는 Spawn 후보를
    막은 뒤 전환한다. 테스트가 끝나면 임시 Collider를 삭제한다.
    `[확인 기준]` 다른 경로·Spawn Point를 재시도하고, 최대 3회 실패 후 Dormant 또는 대체
    경로로 처리되며 무한 대기하지 않는다.
16. `[Action]` 4층·3층·2층을 각각 현재 층으로 만든 뒤 `[Hierarchy]`에서
    `Parking > NavMeshSystem > Floors > Floor4F/3F/2F/1F`를 선택한다.
    `[Inspector]` 각 `FloorController`의 `Runtime State`만 읽는다.
    `[확인 기준]` 현재 층은 `Active`, 바로 위·아래층은 `Preloaded`, 먼 층은 `Unloaded`다.
17. `[Action]` 인형이 2개 이상인 상태에서 전환 Commit 직전 공격 거리로 유도하고 전환 중
    시선을 돌렸다가 다시 본다.
    `[확인 기준]` 전환 중 공격권이 해제되고 새 점프스케어가 시작되지 않는다. 이전 완료 후
    일반 추적과 공격이 재개된다.

##### 18~20번: GameOver·Restart 반복

18. `[Action]` 인형을 추가한 뒤 GameOver를 발생시키고 `RESTART`를 누른다.
    `[확인 기준]` 새 회차가 4층·인형 0개·실패 0회로 시작한다.
19. `[Action]` 자동차 실패와 Spawn 예약이 발생한 직후 GameOver를 만들고 `RESTART`를 누른다.
    `[확인 기준]` 자동차 조사 상태, Spawn 예약·점유·대기열이 초기화되고
    `failureCount=0`, `dollCount=0`, `spawnQueue=0`이다.
20. `[Action]` 18~19번을 최소 3회 반복한다.
    `[확인 기준]` NavMeshData, 인형, Agent, Spawn 예약, 공격권이 누적되지 않고 빨간 오류와
    `Failed to create agent because there is no valid NavMesh` 경고가 없다.

##### V-04 결과 보고 형식

20개 항목을 모두 실행한 뒤 다음 형식으로 전달한다. `자동차 추가 후 실행`은 실패가 아니라
현재 에셋 부족으로 보류한 상태이며, 해당 항목을 해결하려면 먼저 자동차를 추가해야 한다.

```text
V-04 결과
1: 성공 / 실패 / 자동차 추가 후 실행
2: 성공 / 실패 / 자동차 추가 후 실행
...
20: 성공 / 실패 / 자동차 추가 후 실행
Console 빨간 오류: 0개 / 발생
NavMesh 경고: 0개 / 발생
```

## 6. 구현 중 고정해서 지킬 기술 규칙

1. 플레이 중 `BuildNavMesh`를 호출하지 않는다.
2. 층별로 사전 Bake된 NavMeshData만 등록하고 해제한다.
3. 현재 층과 바로 위/아래층 외의 NavMeshData는 등록하지 않는다.
4. NavMeshData 등록은 중복되지 않아야 하며 Instance 해제를 보장한다.
5. Floor ID와 층 Y 범위를 함께 검사해 다른 층 NavMesh 선택을 막는다.
6. 관찰 중에는 Agent를 끄지 않고 `isStopped`로 정지한다.
7. 플레이어가 보고 있을 때 인형을 순간이동시키지 않는다.
8. 자동차 실패 상태를 먼저 저장한 다음 클랙슨과 생성 요청을 처리한다.
9. 시작 템플릿은 전체 인형 수에 포함하지 않고, 자동차 실패로 생성된 인형만 최대치에 포함한다.
10. 층 전환, 점프스케어, GameOver, Restart는 공격권보다 우선한다.
11. 모든 예약과 등록은 소유자를 기록하고 해제 경로를 가진다.
12. Restart는 이전 플레이의 런타임 상태를 남기지 않는다.

## 7. 현재 다음 작업

현재 구현 범위는 사용자가 확정한 `4층 + 시작 인형 0개 + 자동차 실패 생성 최대 6개(총 6개)`다.

3층·2층·1층 NavMeshData가 모두 저장됐지만, 현재 씬의 `FloorTransitionTrigger`는 4F→3F 중심으로만 배치되어 있어 `T-04` 하위 층 Trigger 보완이 먼저다. 현재 범위에서 `S-01~S-06`, `C-01~C-06`, `A-01~A-05`, `X-01`은 해결됐다. 인형 수는 시작 0개이며 자동차 실패로 최대 6개까지 생성된다. `C-05D`와 `C-06`은 현재 검증용 자동차 1대로 완료했으며, 추가 자동차 2~3대 연결과 실패 자동차 비교 검증은 추후 실제 자동차 에셋 추가 시 진행한다. `C-06` 테스트를 위해 적용했던 `TEMP-C06` 인접층 오류 우회는 제거하여 원래 동작으로 복구했다.

T-04 설정 완료 보고 반영: 사용자가 `FloorTransitionTriggers`를 각 층·구역별로 설정했다. 이후 작업은 Trigger를 추가하는 것이 아니라 플레이어가 실제 계단을 통과한 뒤 해당 Preload/Commit이 한 번씩 동작하는지 검증하는 것이다. Commit이 Preload보다 먼저 호출되면 대상 층이 활성 범위에 없다는 경고가 출력되므로, 순서 검증 전까지 T-04는 `확인 필요`로 유지한다.

경로 테스트 원칙: 테스트에서 한 경로를 먼저 사용하는 것은 원인 분리를 위한 절차일 뿐, 플레이어의 이동 경로를 하나로 제한하는 규칙이 아니다. 현재 `StairA`, `StairB`, `Aside`, `Bside` 각 경로에 층별 `Preload`와 `Commit` 쌍이 있으면 어느 경로든 사용할 수 있다. 단, 선택한 경로의 Preload를 먼저 통과한 뒤 같은 경로의 Commit을 통과해야 한다.

T-04 Play Mode 부분 검증 기록: `Floor4F → Floor3F`, `Floor3F → Floor2F`, `Floor2F → Floor3F` Commit은 완료 로그가 확인됐다. `StairA_Commit_2F_to_1F`에서는 `Floor1F가 현재 활성 범위에 없습니다. Preload 후 Commit해야 합니다.` 경고가 발생했으므로, `Preload_2F_to_1F`를 먼저 통과하는 2F→1F 순서 검증이 남아 있다. 해당 테스트는 시작 인형 0개 상태여서 층 전환만 검증한 결과다.

Commit 거부 후 동작: 경고가 난 Commit은 현재 층을 바꾸지 않는다. 예를 들어 2F→1F Commit이 거부되면 플레이어 Transform이 물리적으로 1층 방향에 있어도 시스템의 현재 층은 계속 `Floor2F`다. 그 상태에서 1F→2F Trigger를 통과하면 대상 `Floor2F`가 이미 현재 층이므로 중복 전환 방지 로직이 조용히 무시하며 Commit 로그가 나오지 않는다. 이 경우에는 Play Mode를 다시 시작해 현재 층 상태를 초기화한 후 Preload→Commit 순서로 다시 테스트한다.

T-04 Commit 진단 보완: `FloorTransitionTrigger`는 Commit 직전에 `FloorNavigationManager`를 현재 `GameProgressController` 층 상태와 동기화한다. NavMeshData가 등록되지 않았거나 활성 범위를 벗어난 Commit은 기존처럼 거부한다. 이제 Preload 실패·활성 범위 실패 경고에는 현재 논리 층과 NavMesh 관리 층을 함께 출력해 Trigger 위치 문제와 상태 문제를 구분할 수 있다. `Assembly-CSharp` 컴파일은 오류 0개로 확인했다.

T-04 저장 씬 전수 대조 결과: 48개 Trigger 중 8개는 GameObject 이름의 `to_층`과 Inspector의 `Target Floor ID`가 달랐다. 예를 들어 `Aside_Commit_3F_to_2F`는 이름과 달리 `Target Floor ID = Floor3F`로 저장돼 있었다. 이 상태에서는 해당 경로를 밟아도 잘못된 층 요청이 전달되어 Commit과 인형 이전이 시작되지 않는다. `Tools > Parking Garage > Repair Floor Transition Trigger Settings` 메뉴를 추가했다. 이 메뉴는 이름을 기준으로 `Mode`, `Target Floor ID`, 두 관리자 참조, `Is Trigger`만 복구하며, 사용자가 배치한 Transform 위치와 BoxCollider 크기는 변경하지 않는다. 실행 후 `Validate Floor Transition Triggers`에서 PASS를 확인한다. 에디터 컴파일은 오류 0개로 확인했다.

T-04/T-05 최종 왕복 검증 완료: 사용자 Play Mode 기록에서 `Floor4F → Floor3F → Floor2F → Floor1F → Floor2F → Floor3F → Floor4F` Commit이 순서대로 각각 한 번씩 완료됐다. `Bside_Preload_2F_to_3F`, `StairA_Preload_3F_to_2F`의 처리되지 않음 경고는 현재 층을 대상으로 하는 반대 방향 Preload Trigger가 같은 이동 구간에서 함께 감지된 결과이며, 뒤따른 올바른 Commit과 현재 층 변경은 정상 완료됐다. 중복 Commit이나 활성 범위 실패는 확인되지 않았다.

#### X-02 사용자 실행 절차

X-02의 현재 확정 목표는 층당 Spawn Point 8개다. 4층 `SpawnPoints_4F`를 복제해 층별로 재사용하고, 각 층의 Spawn Point `Floor ID`와 `FloorController` 배열을 함께 맞춘다. Exit/Wall Entry는 Spawn 배치가 끝난 뒤 인접층별로 연결한다.

3층 Spawn Point 부모의 초기 Local Y는 `-3.445`다. 4층 Spawn Point의 기존 자식 Local Y `12.931`을 유지하면 월드 Y가 약 `9.486`이 되어 3층 NavMesh 높이와 맞는다.

3층 Spawn Point 작업 완료를 반영했다. `SpawnPoints_3F`와 자식 10개가 생성됐고, 모든 자식의 `Floor ID = Floor3F`, `Floor3F > FloorController > Spawn Points` 배열 연결, None 슬롯 없음까지 확인됐다. 이후 밸런스 결정을 반영해 4층과 3층의 Spawn Point를 각각 8개로 정리한다. 외곽 분포를 유지하기 위해 `02`, `05`를 제거하고 `01`, `03`, `04`, `06`, `07`, `08`, `09`, `10`을 유지한다. 2층과 1층은 이 8개 구성으로 복제한다.

2층은 축소 완료된 `SpawnPoints_3F`를 복제해 `SpawnPoints_2F`로 만들고, 부모 Local Position Y를 `-6.925`로 설정한다. 1층은 같은 8개 구성을 `SpawnPoints_1F`로 복제하고, 부모 Local Position Y를 `-10.230`으로 설정한다. 두 층 모두 자식 Spawn Point의 Local Position Y `12.931`은 유지하며, 자식 `Floor ID`와 `FloorController > Spawn Points` 배열만 해당 층으로 바꾼다.

사용자 완료 보고를 반영했다. 4층·3층 Spawn Point를 8개로 정리했고, 2층·1층도 같은 8개 구성으로 복제하여 Floor ID와 FloorController 배열 연결까지 완료했다. X-02의 다음 작업은 Exit Point와 Wall Entry Point 배치다.

X-02-A 완료를 반영했다. `TransitionPoints_4F`와 `TransitionPoints_3F` 아래에 4층↔3층용 `ExitPoint`와 `WallEntryPoint`를 생성했다. 다음은 3층↔2층용 전환 표식 배치다.

X-02-B 완료를 반영했다. `TransitionPoints_3F`에 3층→2층용 표식을 추가했고, `TransitionPoints_2F`에 2층→3층용 표식을 생성했다. 다음은 2층↔1층용 전환 표식 배치다.

X-02-C 완료를 반영했다. `TransitionPoints_2F`에 2층→1층용 표식을 추가했고, `TransitionPoints_1F`에 1층→2층용 표식을 생성했다. 다음은 네 `FloorController`의 Exit Points와 Wall Entry Points 배열 연결이다.

X-02-D 완료를 반영했다. `Floor4F`, `Floor3F`, `Floor2F`, `Floor1F`의 `Exit Points`와 `Wall Entry Points` 배열에 모든 전환 표식이 연결됐고 None 슬롯이 없다. X-02 전체는 해결됐으며, 다음은 Codex 담당 X-03이다.

X-03 구현 기록: `FloorTransitionCoordinator`를 추가했다. 이 컴포넌트는 Commit Trigger에서 전환을 시작할 때 공격권 해제 → 대상 층 Commit → 활성 인형 명단 고정 → 인형별 Exit/Wall Entry 경로 결정 → 모든 인형 이전 완료 후 전환 종료 순서를 단일 소유한다. 실제로 보이지 않는 인형을 재배치하는 X-04와 보이는 인형을 외벽으로 이동시키는 X-05는 다음 단계에서 완료 신호를 이 코디네이터에 전달한다. 기존 `FloorTransitionTrigger`는 같은 `NavMeshSystem`에 코디네이터가 있을 때만 새 흐름을 사용하며, 아직 씬에 추가하지 않은 동안에는 기존 흐름을 유지한다.

X-03-A 사용자 연결 절차: `Parking > NavMeshSystem`을 선택하고 Inspector 맨 아래 `Add Component`에서 `Floor Transition Coordinator`를 추가한다. 새 컴포넌트의 Inspector 표시 순서는 `References` 안의 `Floor Navigation Manager`, `Progress Controller`, `Doll Roster Manager`, 다음 `Diagnostics` 안의 `Log Transition Events`다. 세 참조는 모두 비워 둔다. 같은 `NavMeshSystem`의 기존 관리자와 씬의 Doll Roster Manager를 실행 시 자동으로 찾아 연결한다. `Log Transition Events`는 켠 상태로 둔다. 컴포넌트가 추가됐다고 보고하면 X-03을 해결됨으로 바꾸고 X-04로 진행한다.

X-03-A 완료를 반영했다. `NavMeshSystem`에 `FloorTransitionCoordinator`가 추가됐으며 자동 참조 방식으로 동작한다. X-03은 해결됐고, 다음은 Codex 담당 X-04다.

X-04 구현 기록: 전환이 Commit되면 모든 활성 인형의 일반 추적을 우선 정지한다. 플레이어가 현재 보지 않는 인형은 `SpawnPointManager`의 기존 후보 검증(대상 층 NavMesh, 시야 밖, 플레이어 거리, 인형 간 거리, 장애물, 경로)을 통과한 Spawn Point만 예약한다. 이전 층의 예약/점유 상태를 반환한 뒤 Agent를 새 지점으로 이동시키고 새 점유를 확정한다. 안전 지점이 없으면 0.5초 간격으로 다시 시도하며, 재배치가 끝난 인형만 전환 완료로 처리한다. 플레이어가 보고 있는 인형은 다음 X-05에서 외벽 이동으로 처리하므로 이 단계에서는 정지 상태로 유지한다.

X-05 구현 기록: 플레이어가 보고 있는 인형은 일반 추적을 멈춘 채 `Exit Point`까지 NavMeshAgent로 이동한다. Exit 도착 후 Agent를 비활성화하고, `Exit Point`와 `Wall Entry Point` 사이를 1.25초 동안 외벽 이동으로 연출한다. Wall Entry 도착 후 대상 층 NavMesh 근처를 다시 탐색하고 Agent를 활성화해 그 NavMesh 위에 올린다. 이전 Spawn Point 점유는 이때 반환된다. Exit 경로가 막혀도 4초 뒤 외벽 이동으로 전환해 층 전환 상태가 영구적으로 멈추지 않게 했다. 기본값은 모두 코드에 포함돼 있어 Inspector에서 수치를 변경할 필요가 없다.

X-06-0 오류 원인과 수정 기준: 저장된 씬의 `NavMeshSystem > FloorNavigationManager > Floor Controllers` 배열에는 `Floor4F`, `Floor3F` 두 개만 연결되어 있었다. 3층 Commit 시 활성 범위에 필요한 `Floor2F`를 찾지 못해 `Floor2F FloorController를 찾을 수 없습니다` 오류가 발생했다. 또한 `SpawnPointManager > Floor Controllers` 배열도 `Floor4F` 하나만 연결되어 있어, X-04의 3층 안전 재배치가 Spawn Point를 찾지 못하는 상태였다. 두 배열 모두 `Floor4F`, `Floor3F`, `Floor2F`, `Floor1F` 순서로 4개를 각각 한 번씩 연결한 뒤 X-06을 다시 검증한다. 이 연결이 끝나기 전 X-04는 코드 구현만 완료된 확인 필요 상태다.

X-06-0 완료를 반영했다. Console에서 `Floor2F FloorController를 찾을 수 없습니다` 오류가 사라졌고, `Floor4F → Floor3F 전환 Commit 완료, 이전 대상 인형 1개를 대기합니다` 로그까지 확인됐다. `Floor Navigation Manager`와 `Spawn Point Manager`의 4개 층 배열 연결은 정상이다. 다음은 인형의 실제 외벽 이동 또는 시야 밖 재배치 결과를 확인하는 X-06이다.

추가 Console 대조 완료: 저장된 씬의 `Floor Navigation Manager`와 `Spawn Point Manager`에는 `Floor4F`, `Floor3F`, `Floor2F`, `Floor1F`가 모두 연결되어 있고, `GameProgressController > Doll Limit`은 6, `Spawn Point Manager > Max Queued Doll Count`는 5로 저장되어 있다. 4개 층 Spawn Point도 각 8개 구성이므로 이 항목은 해결됨이다.

Spawn 후보 진단 보강: `SpawnPointManager`가 첫 예약 실패 시 후보별 탈락 원인을 한 번 요약하도록 추가했다. 이제 새 요청이 계속 비활성 대기하면 `VisibleToPlayer`, `NotOnNavMesh`, `IncompletePath` 등 실제 원인이 Console에 표시된다. 이 로그는 설정 변경의 근거로 사용하며, 원인 확인 전 수치를 임의로 낮추지 않는다.

4층 Spawn 후보 `NotOnNavMesh` 원인 수정: 각 층의 Bake 기준은 `FloorController` 루트가 아니라 자식 `Navigation` 오브젝트다. 기존 `FloorNavigationManager`가 NavMeshData를 FloorController 위치에 등록해 4층 Navigation 기준과 1.48m 차이가 발생했다. `FloorController.BakedNavMeshOrigin`을 추가하고, NavMeshData를 각 층 `Navigation` Transform의 위치·회전으로 등록하도록 수정했다. 이제 Play Mode를 재시작하면 4층 Spawn Point 위치와 등록된 DollCeiling NavMesh 높이가 일치한다.

X-06-1 3층 NotOnNavMesh 원인 진단 완료: 4F→3F Commit은 성공했지만 Console에 `Floor3F 안전 재배치 후보가 없습니다. 탈락 원인: NotOnNavMesh 8개`가 두 인형 모두에 표시됐다. 저장된 씬을 직접 대조한 결과 3층 `Navigation`과 Spawn Point의 월드 Y는 정상 범위에 있으나, `Floor3F > Navigation > ProxyMesh_3F > PolyShape (1)`의 내부 Mesh 데이터가 `VertexCount: 0`, `DataSize: 0`, Bounds가 0인 빈 Mesh였다. 반면 4층·2층·1층 동일 Proxy Mesh는 `VertexCount: 163`, `DataSize: 7824`다. 따라서 3층 NavMeshData가 스폰 위치를 덮지 못한 직접 원인은 좌표나 Agent Type이 아니라 3층 Proxy Mesh의 빈 Mesh이며, 3층 유효 Mesh를 복구한 뒤 `NavMeshData_F3`를 다시 Bake해야 한다.

X-06-1 완료 확인: 사용자가 4층 정상 PolyShape를 `Floor3F > Navigation > ProxyMesh_3F` 아래에 복구하고 3층 NavMeshData를 다시 Bake했다. Play Mode Console에서 `Floor4F → Floor3F 전환 Commit 완료` 뒤 두 인형이 각각 `SpawnPoint_3F_04`, `SpawnPoint_3F_02`로 안전 재배치됐으며, `NotOnNavMesh 8개` 없이 `모든 인형 이전 완료. 층 전환을 종료합니다.`가 출력됐다. 3층 안전 재배치는 해결됨이다.

X-06 콘솔 검증 기록: 사용자가 인형을 바라보는 상태에서 4F→3F Commit을 실행했다. `Ceiling Killer Doll 01 외벽 이동을 시작합니다. 방향: Descending`이 출력되어 보이는 인형은 외벽 이동 분기로 진입했고, 보이지 않는 `Ceiling Killer Doll 01_Runtime_01`은 `SpawnPoint_3F_06`으로 안전 재배치됐다. 이후 `모든 인형 이전 완료. 층 전환을 종료합니다.`가 출력됐으며 빨간 오류는 없었다. `이전 대기 원인 확인`은 전환 중 상태를 남기는 일반 진단 로그이며 오류가 아니다. X-06의 최종 상태는 사용자가 Scene에서 인형이 Exit Point→외벽→Wall Entry Point를 자연스럽게 이동했는지 확인한 뒤 해결됨으로 갱신한다.

X-06 완료 확인: 사용자가 Scene에서 보이는 인형의 `4F 천장 → Exit Point → 외벽 → 3F Wall Entry Point → 3F 천장` 이동을 확인했다. X-06의 4F→3F 하강 외벽 연출은 해결됨이다. 다음은 X-07 모든 인형 층 왕복 검증이다.

자동차 실패 후 인형 생성 기존 검증 기록: `Floor4F` Spawn 요청이 대기열에 등록된 뒤 `Ceiling Killer Doll 01_Runtime_01 생성 완료`, `현재 인형 수 2/6개` 로그를 확인했다. 이 기록은 기획 변경 전(시작 인형 1개) 결과이며, 새 규칙에서는 `현재 인형 수 1/6개`로 재검증한다.

X-06/T-05 사용자 검증 절차: 현재 씬에는 `StairA`, `Bside`, `StairB`, `Aside`의 `4F_to_3F` Preload/Commit Trigger만 있으며, `3F_to_4F` Trigger는 아직 없다. Unity Console에 빨간 컴파일 오류가 없는 상태에서 Play Mode를 시작한다. 인형을 카메라에 보이게 한 채 위 네 경로 중 하나의 `Preload_4F_to_3F` Trigger를 먼저 지나고 해당 `Commit_4F_to_3F` Trigger를 지난다. 인형이 `Exit Point`까지 기어간 뒤 외벽을 따라 3층 `Wall Entry Point`로 이동하고, 3층 천장 NavMesh에서 다시 추적하면 하강 검증 통과다. 인형을 보지 않은 상태로 Commit하면 외벽 연출 대신 3층의 시야 밖 Spawn Point로 재배치되는 것이 정상이다. 상승/왕복 검증은 `3F_to_4F` Trigger를 추가한 뒤 별도로 진행한다. 테스트 뒤 보이는 인형 하강 결과, 보이지 않는 인형 재배치 결과, Console 오류 유무를 알려주면 X-06과 T-05 상태를 갱신한다.

#### X-07 왕복 테스트 준비 및 실행 절차

현재 씬에는 `4F_to_3F` Trigger만 저장되어 있으므로, 먼저 `StairA` 한 경로에 역방향 Trigger를 추가한다. 기존 Trigger와 같은 위치를 사용해도 된다. 단, 기존 StairA의 낮은 위치는 `Commit`, 높은 위치는 `Preload`이므로, 상승 방향에서는 두 Mode를 서로 바꿔야 한다. 플레이어가 현재 3층일 때 기존 `4F_to_3F` Trigger는 현재 층과 같은 대상이므로 무시되고, 새 `3F_to_4F` Trigger만 처리된다.

```text
[Hierarchy]
Parking
└─ NavMeshSystem
   └─ FloorTransitionTriggers
      ├─ StairA_Preload_4F_to_3F
      └─ StairA_Commit_4F_to_3F
```

1. Play Mode를 종료한다.
2. 기존 `StairA_Commit_4F_to_3F`(낮은 위치)를 선택하고 `Ctrl + D`로 복제한다. 복제본 이름을 `StairA_Preload_3F_to_4F`로 바꾼다.
3. 복제본의 `Floor Transition Trigger`에서 위에서부터 `Mode = Preload`, `Target Floor ID = Floor4F`로 바꾼다.
4. 기존 `StairA_Preload_4F_to_3F`(높은 위치)를 선택하고 `Ctrl + D`로 복제한다. 복제본 이름을 `StairA_Commit_3F_to_4F`로 바꾼다.
5. 복제본의 `Floor Transition Trigger`에서 위에서부터 `Mode = Commit`, `Target Floor ID = Floor4F`로 바꾼다.
6. 두 복제본의 Transform Position은 변경하지 않는다. 기존 StairA Trigger와 같은 위치에 둔다.
7. 씬을 저장하고 Play Mode를 다시 시작한다. 시작 층은 4층이어야 한다.
8. 4층에서 `StairA` 경로를 통해 3층으로 내려간다. 이번 하강은 X-06에서 이미 검증했으므로, 두 인형의 이전이 끝날 때까지 기다린다.
9. 3층에서 2.5초 이상의 전환 쿨다운이 지난 뒤 같은 `StairA` 경로를 반대 방향으로 올라간다. 낮은 위치의 역방향 Trigger가 Preload, 높은 위치의 역방향 Trigger가 Commit으로 처리된다.

```text
[확인 기준: 상승]
Floor3F → Floor4F 전환 Commit 완료
보이는 인형: 외벽 이동을 시작합니다. 방향: Ascending
보이지 않는 인형: Floor4F Spawn Point 안전 재배치
모든 인형 이전 완료. 층 전환을 종료합니다.
```

10. 상승 후 인형이 `3F 천장 → 3F Exit Point → 외벽 → 4F Wall Entry Point → 4F 천장` 순서로 이동했는지 확인한다.
11. 두 인형이 4층으로 복귀하고, 이전 층인 3층에 남지 않으며, Console의 총 인형 수가 `2/6`으로 유지되면 X-07 왕복 검증을 통과한다.

`Bside`, `StairB`, `Aside`에서도 상승이 가능해야 한다면 각 경로의 기존 낮은 `Commit`을 역방향 `Preload`로, 기존 높은 `Preload`를 역방향 `Commit`으로 복제한다. 복제할 때 변경하는 값은 각 Trigger의 이름, `Mode`, `Target Floor ID = Floor4F`이다.

X-07 완료 확인: 사용자가 4층에서 3층으로 하강한 뒤 3층에서 4층으로 다시 상승하는 왕복을 실행했다. 상승 후 인형이 플레이어를 따라 4층으로 복귀하는 것을 확인했으므로, X-07의 층 왕복 검증은 해결됨이다. T-05의 4F↔3F 왕복 Trigger 검증도 함께 해결됨으로 갱신했다. 다음 작업은 R-01 Restart 통합 검증이다.

## 8. 플레이어 계단 이동 보정 계획

`NavMeshData`는 인형용이며 플레이어의 이동에는 사용하지 않는다. 플레이어는 `Parking > Player`의 `CharacterController`와 `HorrorPlayerController`로 이동한다. 원본 계단 `Stairs`와 `stairwell`에는 비트리거 `MeshCollider`가 있지만, 실제 계단 단차가 `CharacterController > Step Offset 0.3`보다 높으면 계단 앞에서 멈출 수 있다.

### M-00 CharacterController 단차 시험 — Codex 도구 추가 / 사용자 실행

계단 Ramp를 만들기 전에 `CharacterController` 값만 조정해 단차 원인을 시험할 수 있도록 에디터 메뉴를 추가했다. Unity가 새 스크립트를 임포트한 뒤 다음 메뉴를 실행한다.

`Tools > Parking Garage > Configure Player Stair Traversal Test`

이 메뉴는 현재 `Parking` 씬의 `Player > CharacterController`에 `Slope Limit = 60`, `Step Offset = 0.6`을 적용하고 씬을 저장한다. Play Mode 중에는 실행하지 않는다. 기본값으로 되돌릴 때는 다음 메뉴를 사용한다.

`Tools > Parking Garage > Restore Player Controller Defaults`

M-00은 사용자가 메뉴 실행 후 계단 이동 결과를 확인했다고 보고하기 전까지 `확인 필요`로 유지한다. 값 조정만으로 통과하지 않으면 M-01~M-03의 보이지 않는 Ramp 방식을 사용한다.

`ParkingFloorTransitionConfigurator.cs`의 Unity `CS1628` 컴파일 오류를 수정했다. `TryBuildContext`의 `out` 매개변수를 LINQ 람다에서 직접 캡처하지 않고 로컬 변수로 복사해 사용하도록 변경했으며, 씬 데이터와 Trigger 배치는 변경하지 않았다.

M-00 결과 확인: 사용자가 `Player > Character Controller`의 높이를 줄였을 때 계단 문을 통과했다. 따라서 이 구간의 직접 원인은 열린 문짝이 아니라 `Height = 2`인 플레이어 캡슐의 상단이 문 상부 프레임과 충돌한 수직 여유 부족이다. 플레이어 실제 신장에 맞춘 `Height = 1.8`을 사용하고, M-00 시험을 위해 임시 변경한 `Slope Limit`과 `Step Offset`은 기본값으로 되돌린다. `Capsule Collider`는 비활성 상태이므로 조정 대상이 아니다.

추가 확인: 다른 계단 구간은 별도의 Ramp 없이 플레이어가 통과한다. 따라서 플레이어 계단 이동 문제는 전체 계단 Collider가 아니라 특정 계단 문의 수직 여유 문제로 한정한다. 다른 계단에서 막힘이나 추락이 재현되지 않는 한 M-01~M-03의 Ramp 제작은 보류하고, 해당 문 통과 후 층 전환 Trigger 배치로 진행한다.

### M-01 계단 이동면 계층 만들기 — 사용자

`Parking` 루트 바로 아래에 빈 오브젝트 `PlayerWalkableStairs`를 만든다. `NavMeshSystem` 아래에 만들지 않는다. 이 오브젝트 아래에 계단 이동면을 배치한다.

```text
Parking
└─ PlayerWalkableStairs
   ├─ Stairs_PlayerRamp_4F_to_3F
   ├─ Stairs_PlayerRamp_3F_to_2F
   ├─ Stairs_PlayerRamp_2F_to_1F
   ├─ Stairwell_PlayerRamp_4F_to_3F
   ├─ Stairwell_PlayerRamp_3F_to_2F
   └─ Stairwell_PlayerRamp_2F_to_1F
```

실제 곡선 때문에 한 구간을 하나의 직선으로 덮을 수 없으면 해당 Ramp를 여러 개의 짧은 Cube로 나눈다. 이 경우에도 각 Cube는 `PlayerWalkableStairs` 아래에 둔다.

### M-02 보이지 않는 경사 콜라이더 설정 — 사용자

각 Ramp는 `Create > 3D Object > Cube`로 만든 뒤 계단 디딤판을 따라 배치한다. 새 Cube에서 바꾸는 항목만 다음 순서로 설정한다.

`[Hierarchy]` `Parking > PlayerWalkableStairs > 해당 Ramp`

`[Inspector: GameObject 상단]`

1. `Name`: 위 목록의 Ramp 이름
2. `Layer`: `Default`

`[Inspector: Transform]`

1. `Position`: 해당 층 계단의 시작 지점과 끝 지점 사이
2. `Rotation`: 계단이 내려가는 방향으로 기울임
3. `Scale`: 계단 폭을 모두 덮고, 경사면 길이가 다음 landing까지 닿도록 조정

`[Inspector: Mesh Renderer]`

1. 컴포넌트 체크 해제

`[Inspector: Box Collider]`

1. `Is Trigger`: 체크하지 않음
2. `Center`: 기본값 `(0, 0, 0)` 유지. 실제 계단보다 `0.03m` 위로 띄우는 작업은 `Transform > Position`에서 조정

기존 `Parkgarage_prefab > Stairs`와 `stairwell`의 `MeshCollider`는 끄거나 Trigger로 바꾸지 않는다. Ramp는 플레이어 물리용 `Default` 레이어에 두고 `DollNavMesh` 레이어에는 넣지 않는다.

### M-03 계단 이동 검증 — 사용자

1. 평지에서 W/A/S/D가 움직이는 상태에서 테스트한다.
2. 4층 계단 입구에서 Ramp 위로 이동한다.
3. 3층, 2층, 1층 landing까지 끊김 없이 내려가는지 확인한다.
4. 계단을 내려가는 동안 플레이어가 튕기거나 바닥 아래로 빠지지 않는지 확인한다.
5. 물리 이동이 확인된 뒤에만 각 landing에 `FloorTransitionTrigger`의 Preload/Commit Box를 배치한다.

`M-01~M-03`은 Ramp 배치와 Play Mode 이동 확인을 사용자가 완료했다고 보고하기 전까지 `대기`로 유지한다.

