# Parking Garage 층별 NavMesh 시스템 명세

## 1. 문서 목적

이 문서는 주차장 층별 천장 NavMesh, 자동차 열쇠 기믹, 인형 누적 생성, 층간 인형 이전, 공격권과 Restart 초기화 규칙을 정의한다.

구현과 테스트는 이 문서를 기준으로 진행한다.

## 2. 핵심 게임 흐름

```text
지상 4층에서 게임 시작
→ 인형 0개로 시작
→ 플레이어가 층에 있는 자동차 3~4대 조사
→ 자동차 열쇠 기믹 성공 또는 실패
→ 실패한 자동차당 인형 1개 추가 (첫 실패에서 첫 인형 생성)
→ 자동차 클랙슨 재생
→ 현재 층 외곽 Spawn Point에서 새 인형 등장
→ 플레이어가 위층 또는 아래층으로 이동
→ 인접 층 NavMeshData 활성화
→ 현재 활성 인형 전체가 대상 층으로 이전
→ 대상 층에서 자동차 조사와 추적 반복
```

## 3. 확정된 기획

| 항목 | 확정 내용 |
|---|---|
| 시작 층 | 지상 4층 |
| 층별 자동차 | 3~4대 |
| 자동차 기믹 | 자동차마다 열쇠 기믹 1개 |
| 실패 처리 | 자동차당 최초 실패 1회만 인형 추가 |
| 성공 처리 | 성공한 자동차는 재조사 불가 |
| 시작 인형 | 게임 시작 시 0개 |
| 최대 인형 | 자동차 실패로 추가되는 인형 최대 6개, 총 6개 |
| 현재 테스트 | 첫 실패부터 누적되는 인형 최대 6개 |
| 실패 누적 | 게임 전체 누적 |
| 층 이동 | 아래층 진행과 이전 층 복귀 모두 가능 |
| 인형 층 처리 | 활성 인형 전체가 플레이어의 현재 층으로 이전 |
| 인형 누적 | 자동차 실패가 발생할 때마다 인형 1개가 추가되어 계속 따라옴 |
| Spawn Point | 층당 외곽에 8개 사전 배치 |
| Spawn 선택 | 유효한 Spawn Point 중 무작위 |
| 실패 연출 | 자동차 클랙슨을 크게 재생한 뒤 인형 생성 |
| NavMesh 제작 | 에디터에서 층별 사전 Bake |
| 런타임 처리 | NavMeshData 등록과 해제만 수행 |
| 활성 층 | 현재 층과 인접 층 |
| 층 Trigger | Preload Trigger와 Commit Trigger 분리 |
| 천장 이동면 | 보이지 않는 전용 Proxy Mesh |
| 관찰 규칙 | 플레이어가 인형을 보면 인형 정지 |
| 공격 인형 | 공격 가능한 인형 중 플레이어와 가장 가까운 1개 |
| 난이도 증가 | 초기에는 인형 수만 증가 |
| 경로 실패 | 다른 Spawn Point로 이동 후 경로 재탐색 |
| 층간 연출 | 외곽으로 퇴장한 뒤 외벽을 타고 상승 또는 하강 |
| 보이는 인형 | 실제 외벽 이동 연출 사용 |
| 보이지 않는 인형 | 대상 층 Spawn Point로 재배치 가능 |
| 전환 중 역주행 | 현재 전환 완료 후 반대 전환 허용 |
| Restart | 완전한 새 게임 상태로 초기화 |

## 4. 전체 시스템 구성

```text
GameProgressController
├─ FloorNavigationManager
│  ├─ FloorController_4F
│  ├─ FloorController_3F
│  ├─ FloorController_2F
│  └─ FloorController_1F
├─ DollRosterManager
├─ DollAttackCoordinator
├─ SpawnPointManager
├─ CarKeyGimmickManager
└─ FloorTransitionCoordinator
```

### GameProgressController

게임 전체 진행 상태를 소유한다.

- 현재 층
- 자동차별 조사 결과
- 전체 실패 횟수
- 전체 인형 수
- 현재 공격 담당 인형
- 층 전환 진행 여부
- GameOver 여부
- Restart 상태

### FloorNavigationManager

층별 NavMeshData 등록과 해제를 담당한다.

- 현재 층 활성화
- 위층과 아래층 Preload
- 멀어진 층 해제
- 중복 등록 방지
- NavMeshDataInstance 수명 관리

### FloorController

한 층의 데이터와 상태를 소유한다.

- Floor ID
- 층 높이 범위
- 사전 Bake된 NavMeshData
- 천장 Proxy Mesh
- Spawn Point 목록
- Exit Point 목록
- Wall Entry Point 목록
- 자동차 기믹 목록
- Preload Trigger
- Commit Trigger

### DollRosterManager

게임 전체 인형 명단을 관리한다.

- 자동차 실패로 생성된 인형
- 실패로 추가된 인형
- 현재 활성 층
- 층 이전 중인 인형
- 수면 상태 인형
- 실패 생성 인형 총 6개 제한

### DollAttackCoordinator

동시에 한 인형만 공격하도록 관리한다.

- 공격 가능한 인형 목록
- 가장 가까운 인형 선정
- 공격권 예약
- 공격권 해제
- 층 전환과 GameOver 중 공격 금지

### SpawnPointManager

Spawn Point 검색, 예약과 점유를 관리한다.

- Available
- Reserved
- Occupied
- Blocked
- 최근 사용 시간
- 플레이어 시야 여부
- 플레이어 거리
- 인형 간 거리
- 경로 유효 여부

### CarKeyGimmickManager

자동차별 조사와 실패 이벤트를 관리한다.

- 자동차별 상태 저장
- 중복 실패 방지
- 성공한 자동차 재조사 방지
- 클랙슨 재생
- 인형 추가 요청

### FloorTransitionCoordinator

층 변경과 인형 이전 순서를 관리한다.

- Preload 요청
- Commit 처리
- 인형 공격 잠금
- 인형 외곽 퇴장
- 외벽 이동 연출
- 대상 층 재배치
- 이전 층 정리
- 역방향 전환 대기

## 5. 층별 NavMesh 제작 규칙

### 사전 Bake

각 층은 에디터에서 별도로 Bake한다.

플레이 중에는 `BuildNavMesh`를 호출하지 않는다.

```text
에디터:
층별 Proxy Mesh 제작
→ 층별 NavMesh Bake
→ NavMeshData 저장

런타임:
NavMeshData 등록
→ Agent 사용
→ 필요 없어지면 NavMeshData 해제
```

### Proxy Mesh

실제 천장 Collider를 직접 사용하지 않고 천장 아래에 인형 전용 이동면을 배치한다.

- Renderer 비활성
- 인형 전용 수집 Layer 사용
- 인형이 이동 가능한 영역만 포함
- 층별 수집 Volume 분리
- 다른 층 Geometry 수집 금지
- 실제 인형 크기를 고려한 벽 여유 공간 확보

### NavMesh 제외 영역

다음 구간에는 Proxy Mesh 또는 NavMesh를 생성하지 않는다.

1. 기둥 주변
2. 계단 중앙
3. 램프의 빈 공간
4. 천장이 없는 구간
5. 차량 출입구

추가로 실제 테스트에서 인형이 끼는 배관, 조명 구조물과 벽 모서리도 제외 후보로 관리한다.

## 6. 층 활성화 규칙

### 활성 범위

플레이어가 위층과 아래층으로 모두 이동할 수 있으므로 최대 3개 층을 활성화한다.

```text
현재 층: Active
바로 위층: Preloaded
바로 아래층: Preloaded
나머지 층: Unloaded
```

건물 최상층 또는 최하층에서는 존재하는 인접 층만 활성화한다.

### 층 상태

```text
Unloaded
→ Preloading
→ Preloaded
→ Active
→ Unloading
→ Unloaded
```

### Preload Trigger

층 경계에 도착하기 전에 배치한다.

- 대상 층 NavMeshData 등록
- 대상 층 Spawn Point 준비
- 대상 층 기믹 데이터 준비
- 현재 층은 변경하지 않음
- 중복 진입 요청 무시

### Commit Trigger

플레이어가 실제 대상 층에 진입한 위치에 배치한다.

- CurrentFloor 변경
- 인형 공격 잠금
- 인형 이전 시작
- 대상 층 자동차 기믹 활성화
- 이전 층 정리 예약

### 역방향 이동

층 전환 중 플레이어가 되돌아가도 진행 중인 이전은 취소하지 않는다.

```text
현재 전환 완료
→ 짧은 Cooldown
→ 반대 Commit Trigger가 확인되면 역방향 전환 시작
```

권장 전환 Cooldown은 2~3초다.

## 7. 자동차 열쇠 기믹

### 자동차 열쇠 상호작용 흐름

플레이어가 차키를 보유한 상태에서 자동차에 접근하면 자동차를 상호작용 대상으로 삼는다.

```text
플레이어가 차키 보유
→ 자동차 앞의 상호작용 거리 진입
→ E키 안내 표시
→ E키 입력
→ 자동차 본체의 CarKeyGimmick 실행
→ 자동차 상태 기록
→ 성공 또는 실패 처리
```

- `CarKeyGimmick`은 자동차 열쇠 모델이 아니라 실제 상호작용 대상인 자동차 본체 루트에 추가한다.
- 열쇠 모델은 플레이어가 보유한 키 또는 인벤토리 데이터가 담당한다.
- 플레이어가 차키를 하나라도 보유하면 자동차 앞에서 E키 상호작용을 허용한다.
- 자동차의 `Car ID`와 보유 키 ID가 일치하면 성공, 일치하지 않으면 해당 자동차의 최초 실패로 처리한다.
- 성공 또는 실패 후에도 플레이어의 차키는 유지한다.
- 자동차 본체의 `CarKeyGimmick`이 `CarKeyGimmickManager`에 실패/성공 결과를 전달한다.

### 자동차 상태

```text
Unexamined
→ Available
→ Failed 또는 Succeeded
→ Locked
```

### 실패

자동차별 최초 실패에서만 실행한다.

```text
실패 확정
→ 자동차 상태를 Failed로 먼저 저장
→ 자동차 클랙슨 1회 재생
→ 전체 인형 수 확인
→ 총 6개 미만이면 인형 1개 추가
→ Spawn Point 선택
→ 새 인형 생성
```

같은 자동차의 실패 이벤트가 다시 발생해도 인형을 추가하지 않는다.

### 성공

```text
성공 확정
→ 자동차 상태 Succeeded
→ 재조사 차단
→ 추가 인형 없음
```

## 8. 인형 수 규칙

### 기본값

```text
게임 시작 인형: 0개
최대 인형: 6개
추가 가능 인형: 6개
현재 구현 테스트 상한: 6개
```

게임 시작 인형은 없으며, 자동차 실패로 생성되는 인형만 최대 6개에 포함한다.

최대 수에 도달하면 기믹 실패와 클랙슨은 정상 처리하지만 추가 인형은 생성하지 않는다.

## 9. Spawn Point 규칙

### 배치

층마다 주차장 외곽에 8개를 배치한다.

각 Spawn Point는 다음 정보를 가진다.

- Floor ID
- NavMesh 위치
- 사용 가능 여부
- 예약한 인형
- 플레이어 시야 여부
- 최근 사용 시간
- 인접 Spawn Point
- 외벽 전환 Entry 또는 Exit 연결 정보

### 선택 조건

다음 조건을 모두 만족해야 한다.

1. 대상 층에 속함
2. 활성 NavMesh 위에 있음
3. 플레이어 카메라에 직접 보이지 않음
4. 플레이어와 최소 12m 이상 떨어짐
5. 다른 인형과 최소 3~4m 이상 떨어짐
6. 다른 인형이 예약하지 않음
7. 플레이어까지 완전한 경로가 존재함
8. 벽이나 구조물 내부가 아님

조건을 만족하는 지점 중 무작위로 선택한다.

### 부족한 경우

유효한 Spawn Point가 인형 수보다 적으면 대기열을 사용한다.

```text
유효 지점에 먼저 배치
→ 0.5~1초 간격으로 재검색
→ 조건을 만족하는 지점이 생기면 나머지 배치
```

같은 위치에 여러 인형을 겹쳐 생성하지 않는다.

## 10. 인형 층 이전 규칙

### 기본 원칙

활성 인형 전체는 플레이어의 현재 층으로 이전한다.

이전 층에 활성 인형을 남기지 않는다.

### 이전 순서

```text
대상 층 NavMeshData 준비
→ 인형 공격 잠금
→ 현재 층 외곽 Exit Point 지정
→ 인형들이 Exit Point로 이동
→ 플레이어 시야 여부에 따라 이전 방식 분기
→ 대상 층 Spawn Point 예약
→ 대상 NavMesh 위에 배치
→ Agent와 AI 재활성화
→ Spawn 보호시간 적용
→ 공격 잠금 해제
→ 더 이상 필요 없는 층 NavMeshData 해제
```

### 플레이어에게 보이는 인형

실제 외벽 이동 연출을 사용한다.

```text
Exit Point 도착
→ NavMeshAgent 비활성화
→ WallTransfer 상태
→ 외벽 상승 또는 하강 애니메이션
→ 대상 층 Wall Entry Point 도착
→ 대상 NavMesh에 배치
→ Agent 활성화
```

### 플레이어에게 보이지 않는 인형

외곽 퇴장 후 대상 층 Spawn Point로 재배치한다.

- 즉시 화면 안에 나타나지 않음
- 외벽 이동 예상 시간만큼 지연 가능
- 대상 Spawn Point에서 보호시간 후 추적 시작

### 상승과 하강

두 방향을 모두 지원한다.

```text
WallAscending
WallDescending
```

외벽 이동은 일반 천장 NavMesh 경로가 아니라 전용 전환 연출로 관리한다.

## 11. 인형 AI 상태

```text
Dormant
SpawnProtection
Chase
Observed
Waiting
ExitFloor
WallTransfer
AttackReserved
Jumpscare
GameOver
```

### Chase

- 플레이어 위치를 현재 층의 천장 NavMesh에 투영
- 유효한 경로 계산
- 플레이어가 보지 않을 때 이동

### Observed

- NavMeshAgent의 `isStopped` 사용
- Agent 자체를 비활성화하지 않음
- Animator 정지
- 공격 금지

### SpawnProtection

새로 생성되거나 층 이전된 직후 적용한다.

- 권장 시간 1~2초
- 공격 금지
- 필요하면 이동도 잠시 금지
- 플레이어 시야 안이면 활성화 지연

### WallTransfer

- 일반 NavMeshAgent 비활성화
- 외벽 전용 경로와 애니메이션 사용
- 완료 후 대상 층 NavMesh에 재배치

## 12. 경로 실패 처리

```text
경로 계산 실패
→ 1초 후 같은 위치에서 재탐색
→ 다시 실패하면 다른 Spawn Point 예약
→ 시야 밖일 때 재배치
→ 새 위치에서 재탐색
→ 최대 3개 Spawn Point 시도
→ 전부 실패하면 Dormant
→ 2~3초 뒤 다시 시도
```

다음 조건을 지킨다.

- 플레이어가 보는 동안 순간이동 금지
- 무한 재배치 금지
- 층이 비활성화된 상태에서 경로 탐색 금지
- 다른 층 NavMesh를 선택하지 않도록 Y 범위와 Floor ID 검증

## 13. 공격권 규칙

동시에 한 인형만 공격할 수 있다.

### 공격 후보 조건

1. 현재 층에 있음
2. WallTransfer 또는 SpawnProtection 상태가 아님
3. 플레이어에게 완전한 경로가 있음
4. 플레이어가 해당 인형을 보고 있지 않음
5. 공격 거리 조건을 만족함

### 공격자 선정

조건을 만족하는 인형 중 플레이어와 가장 가까운 인형을 선택한다.

선택된 공격자는 1~2초 동안 공격권을 예약한다.

다른 인형이 조금 더 가까워졌다는 이유로 매 프레임 공격자를 변경하지 않는다.

### 공격권 해제

- 공격 완료
- 인형 제거
- 경로 단절
- 층 전환 시작
- GameOver
- Restart

### 상태 우선순위

```text
GameOver
> Jumpscare
> FloorTransition
> AttackReserved
> Observed
> Chase
```

## 14. 인형 간 회피

- NavMeshAgent 회피 활성화
- 동일 목적지 집중 방지
- 플레이어 주변에 접근 위치 분산
- 공격자는 직접 접근
- 비공격 인형은 측면 또는 외곽 대기 위치 사용
- 인형 간 최소 간격 3~4m 권장

초기 난이도는 속도 증가 없이 인형 수만 증가시킨다.

## 15. 성능 규칙

- 최대 인형 6개
- 시작 0개와 실패 추가 최대 6개
- 모든 인형이 매 프레임 새 경로를 요청하지 않음
- 인형별 경로 갱신 프레임 분산
- 시야 Raycast 주기 분산 검토
- 현재 층과 인접 층만 NavMeshData 등록
- 비활성 층 AI 업데이트 금지
- 런타임 NavMesh Bake 금지

## 16. Restart 초기 상태

Restart는 완전한 새 게임으로 처리한다.

```text
CurrentFloor = 4F
4F = Active
인접 층 = Preloaded
나머지 층 = Unloaded
자동차 상태 = Unexamined
전체 실패 횟수 = 0
전체 인형 수 = 0
시작 템플릿 = 비활성 복제 원본
추가 인형 = 0
공격 담당 인형 = 없음
층 전환 상태 = Idle
예약된 Spawn Point = 없음
진행 중 경로 = 없음
WallTransfer 상태 = 없음
GameOver = false
```

Restart 정리 순서는 다음과 같다.

```text
모든 인형 공격과 경로 중지
→ Spawn 예약 해제
→ WallTransfer 연출 중지
→ 모든 NavMeshAgent 정리
→ 등록된 NavMeshData 해제
→ 게임 진행 데이터 초기화
→ 4층 NavMeshData 등록
→ 인접 층 Preload
→ 인형 0개로 시작 (첫 자동차 실패에서 생성)
→ 플레이어와 자동차 기믹 초기화
→ 플레이 시작
```

## 17. 예외 처리

### 층 전환 중 GameOver

- 점프스케어가 이미 시작됐으면 층 전환 중단
- 층 전환 중이면 모든 공격 금지
- GameOver가 최우선 상태

### 전환 중 역주행

- 현재 이전을 취소하지 않음
- 현재 전환 완료
- Cooldown 종료
- 반대 Commit Trigger 확인
- 역방향 이전 시작

### 최대 인형 수 도달

- 자동차 실패 상태와 클랙슨은 정상 처리
- 추가 인형 생성만 생략
- 최대 수를 초과하지 않음

### 유효 Spawn Point 없음

- 인형 중복 배치 금지
- 생성 또는 이전 대기열 사용
- 일정 간격으로 재검색
- 필요하면 Dormant 유지

### 대상 층 NavMesh 준비 실패

- Commit 금지
- 현재 층 유지
- 인형 이전 금지
- 대상 층 Preload 재시도

## 18. 구현 순서

### 1단계: 층 데이터 구조

- Floor ID 정의
- FloorController 정의
- 층별 높이 범위 정의
- 현재 층 상태 관리자 정의

### 2단계: Proxy Mesh와 사전 Bake

- 4층 Proxy Mesh 제작
- 제외 영역 적용
- 4층 NavMesh Bake
- 인접 층으로 확장
- 층간 NavMesh 분리 검증

### 3단계: NavMeshData 활성화

- 등록과 해제
- 중복 등록 방지
- 현재 층과 인접 층 활성화
- Preload와 Commit Trigger

### 4단계: 기본 인형 전환

- 기존 Raycast 이동 제거
- 천장 NavMeshAgent 이동
- 관찰 중 정지
- 경로 실패 처리
- 4층 기본 인형 검증

### 5단계: Spawn Point

- 층당 8개 배치
- 예약과 점유
- 시야와 거리 검사
- 경로 검사
- 무작위 선택

### 6단계: 자동차 기믹

- 자동차 상태
- 최초 실패 제한
- 성공 후 재조사 차단
- 클랙슨
- 실패당 인형 추가
- 최대 수 제한

### 7단계: 다중 인형

- 시작 0개와 실패 추가 최대 6개, 총 6개
- 회피와 접근 위치 분산
- 가장 가까운 인형 공격권
- 동시 공격 방지

### 8단계: 층 이전

- 외곽 Exit Point
- 보이지 않는 인형 재배치
- 보이는 인형 외벽 연출
- 상승과 하강
- 역주행 처리

### 9단계: Restart 통합

- 층 상태 초기화
- NavMeshData 초기화
- 자동차 상태 초기화
- 시작 인형 없음, 복제 템플릿만 비활성 유지
- 추가 인형 제거
- Spawn 예약과 공격권 제거

### 10단계: 최대 6개와 반복 검증

- 최대 인형 6개
- 층 왕복 반복
- 모든 자동차 실패
- Spawn Point 부족
- 경로 실패
- GameOver와 Restart 반복

## 19. 필수 테스트 시나리오

1. 새 게임 시작 시 인형이 생성되지 않고, 씬의 복제 템플릿은 비활성 상태다.
2. 플레이어가 보면 인형이 멈춘다.
3. 시선을 돌리면 기둥과 빈 공간을 피해 이동한다.
4. 자동차 최초 실패에서 클랙슨과 인형 1개가 발생한다.
5. 같은 자동차의 추가 실패에서는 인형이 증가하지 않는다.
6. 성공한 자동차를 다시 조사할 수 없다.
7. 인형은 플레이어 시야 밖 Spawn Point에서 생성된다.
8. 인형 수가 기본 포함 6개를 넘지 않는다.
9. 가장 가까운 인형 한 마리만 공격한다.
10. 플레이어가 아래층으로 가면 인형 전체가 해당 층으로 이전한다.
11. 플레이어가 위층으로 돌아가도 동일하게 이전한다.
12. 전환 중 역주행 요청은 현재 전환 완료 후 실행된다.
13. 보이는 인형은 외벽 이동 연출을 사용한다.
14. 보이지 않는 인형은 안전한 Spawn Point로 재배치된다.
15. 경로 실패 시 최대 3회 다른 Spawn Point를 사용한다.
16. 현재 층과 인접 층 이외 NavMeshData가 등록되지 않는다.
17. 층 전환 중 인형이 공격하지 않는다.
18. Restart 후 4층, 인형 0개와 실패 0으로 복원된다.
19. Restart 후 자동차 상태와 Spawn 예약이 초기화된다.
20. GameOver와 Restart를 반복해도 NavMeshData와 인형이 중복되지 않는다.

## 20. 완료 판정

- 층별 NavMesh가 서로 잘못 연결되지 않는다.
- 인형이 바닥이나 다른 층 NavMesh로 이동하지 않는다.
- 기둥과 제외 구간을 통과하지 않는다.
- 층 왕복 시 인형 수가 유지된다.
- 자동차당 인형 추가가 한 번만 발생한다.
- 자동차 실패로 생성되는 인형 총 6개 제한이 지켜진다.
- 플레이어에게 보이는 순간이동이 발생하지 않는다.
- 공격자는 동시에 한 마리다.
- Restart 후 모든 진행 상태가 초기화된다.
- 반복 실행에서 NavMeshData, Agent와 Spawn 예약이 누적되지 않는다.

## 21. 아직 미정인 기획

다음 내용은 NavMesh 핵심 구현을 시작하는 데 필수는 아니지만 최종 게임 진행 전에 결정해야 한다.

- 전체 주차장 층 수
- 최종 목적 층
- 최종 자동차 또는 진짜 열쇠의 위치
- 게임 클리어 조건
- 열쇠 성공 후 다음 층으로 가는 구체적인 유도 방식
- 최대 인형 도달 후 추가 실패의 난이도 보상
