# Restart 개선 작업 진행표

## 문서 사용 규칙

이 문서를 Restart 개선 작업의 단일 기준표로 사용한다.

- Codex가 완료한 작업은 Codex가 `해결됨`으로 변경한다.
- 사용자 작업은 실제 확인 전까지 `사용자 확인 대기`로 유지한다.
- 사용자가 작업 후 채팅에서 `끝냈어` 또는 같은 의미로 알려주면 Codex가 결과를 반영해 `해결됨`으로 변경한다.
- 실패가 발견되면 `문제 발견`으로 변경하고 증상과 다음 작업을 기록한다.
- 선행 작업이 끝나지 않은 항목은 `대기`로 유지한다.
- 코드나 씬을 변경할 때마다 Codex가 이 문서도 갱신한다.
- 모든 필수 항목이 `해결됨`이면 Restart 개선 작업을 완료한다.

## 상태 정의

| 상태 | 의미 |
|---|---|
| `해결됨` | 구현 또는 검증 완료 |
| `진행 중` | 현재 작업 중 |
| `사용자 확인 대기` | Unity에서 사용자 확인 필요 |
| `문제 발견` | 테스트 실패로 수정 필요 |
| `NavMesh 이관` | Restart 반복성은 확인했지만 정확한 위치 해결은 층별 NavMesh 작업에서 진행 |
| `대기` | 선행 작업 완료 후 진행 |

## 1단계: 코드 정리와 진단 준비

| 번호 | 담당 | 작업 | 완료 기준 | 상태 |
|---|---|---|---|---|
| 1-1 | Codex | `Assets/Scripts` 기존 주석 제거 | 기존 주석 없음 | 해결됨 |
| 1-2 | Codex | 기존 Debug 로그 제거 | Restart 진단기 외 기존 로그 없음 | 해결됨 |
| 1-3 | Codex | Restart 전용 진단 시스템 작성 | `[RestartCheck]` 로그 구조 존재 | 해결됨 |
| 1-4 | Codex | 같은 검증 로그 반복 방지 | 씬 회차당 항목별 1회 출력 | 해결됨 |
| 1-5 | Codex | 스크립트 컴파일 검사 | 오류 0개, 경고 0개 | 해결됨 |

## 2단계: 최초 실행 확인

| 번호 | 담당 | 작업 | 완료 기준 | 상태 |
|---|---|---|---|---|
| 2-1 | 사용자 | Unity에서 `Parking` 씬 열기 | 대상 씬이 열림 | 해결됨 |
| 2-2 | 사용자 | Console을 비우고 Play 시작 | 새 실행 시작 | 해결됨 |
| 2-3 | 사용자 | Console에서 `RestartCheck` 검색 | 진단 로그만 표시 | 해결됨 |
| 2-4 | 사용자 | 최초 초기화 로그 확인 | `scene-loaded`, `player-initialized`, `doll-initialized`, `smartphone-initialized`, `gameover-ui-initialized`가 각각 1회 | 해결됨 |
| 2-5 | 사용자 | 최초 인형 위치 확인 | 의도한 천장에 있고 2층 바닥에 있지 않음 | 해결됨 |
| 2-6 | Codex | 최초 실행 실패 분석 및 수정 | 실패 보고 없음 | 해결됨 |

## 3단계: 첫 번째 GameOver 확인

| 번호 | 담당 | 작업 | 완료 기준 | 상태 |
|---|---|---|---|---|
| 3-1 | 사용자 | 인형 공격 유도 | 점프스케어 시작 | 해결됨 |
| 3-2 | 사용자 | 점프스케어 로그 확인 | `jumpscare-started | PASS` 1회 | 해결됨 |
| 3-3 | 사용자 | GameOver 전달 확인 | `gameover-dispatch | PASS` 1회 | 해결됨 |
| 3-4 | 사용자 | GameOver 표시 확인 | `gameover-shown | PASS` 1회 | 해결됨 |
| 3-5 | 사용자 | UI 육안 확인 | GAME OVER 문구와 Restart 버튼 표시 | 해결됨 |
| 3-6 | Codex | 첫 GameOver 실패 수정 | 실패 보고 없음 | 해결됨 |

## 4단계: 첫 번째 Restart 확인

| 번호 | 담당 | 작업 | 완료 기준 | 상태 |
|---|---|---|---|---|
| 4-1 | 사용자 | Restart 버튼 한 번 클릭 | 씬 재로드 | 해결됨 |
| 4-2 | 사용자 | Restart 요청 로그 확인 | `restart-requested` 1회 | 해결됨 |
| 4-3 | 사용자 | 재로드 로그 확인 | `cycle` 증가 및 `restartReload=True` | 해결됨 |
| 4-4 | 사용자 | 플레이어 초기화 확인 | 시작 위치, 시점, 이동과 마우스 정상 | 해결됨 |
| 4-5 | 사용자 | 인형 초기화 확인 | 최초 실행과 Restart 후 동일 위치 | 해결됨 |
| 4-6 | 사용자 | 스마트폰 초기화 확인 | 화면, 후방 카메라와 플래시 정상 | 해결됨 |
| 4-7 | 사용자 | UI 초기 상태 확인 | GameOver UI 숨김 및 게임 입력 정상 | 해결됨 |
| 4-8 | Codex | Restart 초기화 오류 수정 | Restart 초기화 오류 제거 | 해결됨 |

## 5단계: 두 번째 GameOver 확인

| 번호 | 담당 | 작업 | 완료 기준 | 상태 |
|---|---|---|---|---|
| 5-1 | 사용자 | Restart 후 공격 다시 유도 | 두 번째 점프스케어 시작 | 해결됨 |
| 5-2 | 사용자 | 두 번째 점프스케어 확인 | 새 회차 `jumpscare-started | PASS` | 해결됨 |
| 5-3 | 사용자 | 두 번째 GameOver 전달 확인 | 새 회차 `gameover-dispatch | PASS` | 해결됨 |
| 5-4 | 사용자 | 두 번째 GameOver UI 확인 | 문구와 Restart 버튼 표시 | 해결됨 |
| 5-5 | 사용자 | 두 번째 Restart 확인 | 버튼 클릭 및 재로드 정상 | 해결됨 |
| 5-6 | Codex | 두 번째 GameOver 문제 수정 | 모든 씬 로드에서 GameOver 관리자와 UI 재생성 | 해결됨 |

## 6단계: 반복 안정성 확인

| 번호 | 담당 | 작업 | 완료 기준 | 상태 |
|---|---|---|---|---|
| 6-1 | 사용자 | GameOver와 Restart 10회 반복 | 중단 없이 완료 | 해결됨 |
| 6-2 | 사용자 | 인형 위치 반복 확인 | 사용자가 수정 결과 확인 | 해결됨 |
| 6-3 | 사용자 | 로그 반복 여부 확인 | 같은 회차에서 같은 로그 1회 | 해결됨 |
| 6-4 | 사용자 | GameOver 반복 표시 확인 | 모든 회차에서 UI와 버튼 표시 | 해결됨 |
| 6-5 | 사용자 | 런타임 오브젝트 중복 확인 | 필요한 개수보다 증가하지 않음 | 해결됨 |
| 6-6 | Codex | 반복 누적 문제 수정 | 추가 누적 문제 보고 없음 | 해결됨 |

중복 확인 대상:

- `Game Over Controller`
- `GameOverCanvas`
- `EventSystem`
- `Player Camera`
- `Smartphone Rig`
- `Smartphone Rear Camera`
- `Smartphone Flash`
- `AudioListener`

## 7단계: 최종 정리

| 번호 | 담당 | 작업 | 완료 기준 | 상태 |
|---|---|---|---|---|
| 7-1 | Codex | 진단 로그 유지 또는 제거안 제시 | NavMesh 적용과 최종 Restart 검증까지 유지 권장 | 해결됨 |
| 7-2 | 사용자 | 진단 로그 방침 선택 | 진단 로그 제거 선택 | 해결됨 |
| 7-3 | Codex | 선택에 따라 로그 정리 | Restart 진단 로그 제거, 주차장 라이트 필터 유지 | 해결됨 |
| 7-4 | Codex | 최종 컴파일 검사 | 오류 0개, 경고 0개 | 해결됨 |
| 7-5 | 사용자 | 최종 플레이 승인 | 수정 결과 확인 | 해결됨 |
| 7-6 | Codex | 작업 완료 처리 | 모든 필수 항목 `해결됨` | 해결됨 |

## 현재 진행 위치

- 현재 단계: `Restart 작업 완료`
- 다음 작업: 층별 NavMesh 시스템 설계 및 구현
- 완료 결과: 반복 Restart, 두 번째 GameOver, UI, 버튼, 오브젝트 중복과 최종 컴파일 확인 완료

## 진단 로그 판정표

| 로그 | PASS 조건 | FAIL 시 수정 대상 |
|---|---|---|
| `player-initialized` | 플레이어, 카메라와 조작 정상 | `HorrorPlayerController` |
| `doll-initialized` | 위치, Animator와 템플릿 정상 | `CeilingDollController`, 씬 인형 설정 |
| `smartphone-initialized` | Rig, 후방 카메라, Texture와 플래시 정상 | `PlayerSmartphone` |
| `gameover-ui-initialized` | 관리자, Canvas와 EventSystem 정상 | `GameOverController` |
| `jumpscare-started` | 점프스케어 인형과 조작 차단 정상 | `DollJumpscareController` |
| `gameover-dispatch` | GameOver 관리자 호출 가능 | `GameOverController.Instance` |
| `gameover-shown` | Canvas와 커서 표시 정상 | GameOver UI와 입력 설정 |

## 최종 완료 조건

- 모든 필수 작업이 `해결됨`이다.
- 첫 실행과 Restart 후 플레이 상태가 동일하다.
- 인형이 모든 회차에서 동일한 천장에 생성된다.
- 두 번째 이후에도 GameOver UI와 Restart 버튼이 작동한다.
- 10회 반복에서 중복 생성과 상태 누적이 없다.
- 최종 컴파일 오류와 경고가 없다.
