# Unity 안내 및 작업 기록 규칙

이 문서는 이 프로젝트에서 Codex가 Unity 작업을 안내할 때 반드시 지켜야 하는 설명 순서와 작업 기록 규칙이다.

## 1. Unity 조작 설명의 고정 순서

모든 Unity 조작 안내는 아래 순서를 **항상 같은 순서로** 작성한다.

1. **현재 상태**: 사용자가 올린 화면 또는 이미 완료한 설정 중, 이미 맞는 값과 건드리지 않을 값을 먼저 밝힌다.
2. **Hierarchy**: 선택해야 하는 오브젝트의 전체 경로를 적는다.
3. **Inspector**: 선택한 오브젝트의 어떤 컴포넌트를 볼지 적는다.
4. **Inspector 값 순서**: 해당 컴포넌트에 실제로 표시되는 위에서 아래 순서 그대로 설정값을 나열한다.
5. **Action**: 클릭, 체크, 선택, 입력, Bake 등 사용자가 할 행동을 적는다.
6. **확인 기준**: 정상 완료 시 화면, Console, Inspector에서 보여야 하는 결과를 적는다.
7. **건드리지 않을 것**: 혼동하기 쉬운 다른 오브젝트·컴포넌트·값을 명시한다.

## 2. Hierarchy 규칙

- `Navigation`, `ProxyMesh_4F`, `PolyShape (1)`처럼 이름이 비슷하거나 부모·자식 관계가 중요한 경우 반드시 전체 Hierarchy 경로를 쓴다.
- 부모 오브젝트를 선택해야 하는지, 실제 Mesh가 들어 있는 자식 오브젝트를 선택해야 하는지 분명하게 구분한다.
- `NavMeshSurface가 붙은 오브젝트`와 `실제 MeshRenderer/MeshFilter가 붙은 오브젝트`를 절대로 같은 대상으로 뭉뚱그려 설명하지 않는다.
- 사용자가 선택할 오브젝트는 매 단계마다 다시 명시한다. 이전 단계의 선택 상태를 당연하다고 가정하지 않는다.

## 3. Inspector 규칙

- Inspector 맨 위의 GameObject 설정(`Tag`, `Layer`, `Static`)과 컴포넌트 내부 설정을 반드시 구분한다.
- `Layer`와 `NavMesh Surface > Include Layers`처럼 이름이 비슷한 설정은 서로 다른 기능임을 먼저 설명한다.
- 컴포넌트 값을 설명할 때는 Inspector에 표시되는 **위에서 아래 순서**를 그대로 사용한다. 중간 값을 건너뛰거나 다른 순서로 나열하지 않는다.
- 각 값에는 아래 셋 중 하나를 붙인다.
  - `유지`: 현재 값이 맞으므로 변경하지 않음
  - `변경`: 바꿔야 하는 값과 목표값
  - `확인`: 값은 바꾸지 않고 맞는지만 확인
- 화면에 이미 목표값이 보이면 `이미 완료 — 건드리지 않음`이라고 먼저 말한다. 사용자가 이미 맞춘 값을 다시 찾거나 바꾸게 하지 않는다.
- Inspector에서 값을 찾을 위치는 `컴포넌트 이름 > 접힌 영역 이름 > 항목 이름` 형식으로 쓴다.

## 4. NavMeshSurface 안내 규칙

`Navigation` 오브젝트에 NavMeshSurface가 붙어 있는 현재 구조에서는 다음을 구분한다.

```text
FloorController_4F
└─ Navigation                         : NavMeshSurface를 보유하는 오브젝트
   └─ ProxyMesh_4F                    : Proxy Mesh Root
      └─ PolyShape (1)                : 실제 ProBuilder Mesh
```

- `Navigation`의 Inspector 최상단 `Layer`는 Proxy Mesh Bake 대상 설정과 별개다. 명시적 이유가 없으면 변경하지 않는다.
- 실제 Mesh의 Layer는 `PolyShape (1)` Inspector 최상단 `Layer`에서 설정한다.
- `Include Layers`는 `Navigation > NavMesh Surface > Object Collection > Include Layers`에 있는 **NavMeshSurface 컴포넌트 내부 필터**다.
- `Include Layers`에 포함된 Layer와 실제 Mesh(`PolyShape (1)`)의 Layer가 일치해야 한다.
- `Use Geometry = Render Meshes`인 경우 Bake 확인 단계에서는 실제 Mesh의 `Mesh Renderer` 상태도 확인한다.
- Bake 전에는 NavMeshSurface를 활성화하고, Bake 후 런타임 설계에 따라 Proxy Renderer와 NavMeshSurface를 다시 비활성화할지 명시한다.

## 5. 초보자 안내 규칙

- 메뉴·버튼·드롭다운의 위치를 생략하지 않는다.
- `선택한다`, `확인한다`만 쓰지 않고, 어느 창에서 무엇을 눌러야 하는지 적는다.
- 하나의 단계에는 하나의 목적만 둔다.
- 설정값의 의미를 한 문장으로 설명하되, 사용자가 지금 해야 할 클릭보다 앞서지 않게 한다.
- 확실하지 않은 Unity UI 이름이나 상태는 추측으로 단정하지 않고, 화면 또는 프로젝트 상태를 확인한 뒤 안내한다.

## 6. 오류 진단 규칙

- Console 오류를 원인별로 분리한다. 하나의 오류가 다른 오류의 원인이라고 단정하지 않는다.
- 우선순위는 `NavMesh가 실제로 Bake/등록되었는지` → `Agent가 NavMesh 근처에 있는지` → `코드의 2차 오류` 순서로 설명한다.
- 이미 해결된 설정과 현재 실패 원인을 구분한다.
- 문제 해결을 위해 바꾸지 않아도 되는 설정은 명시적으로 `건드리지 않음`으로 표시한다.

## 7. 작업 진행 문서 규칙

- 시스템 구현의 진행 상태는 `NAVMESH_IMPLEMENTATION_PROGRESS.md`를 기준으로 관리한다.
- 작업 목록은 문서 상단에 유지한다.
- Codex가 구현하고 검증까지 마친 작업만 `해결됨`으로 표시한다.
- 사용자가 직접 해야 하는 Unity 작업은 사용자가 완료했다고 명시적으로 말한 뒤에만 `해결됨`으로 표시한다.
- 사용자가 완료를 말하기 전에는 `진행 중` 또는 `대기`로 유지한다.
- 오류 해결·설명 단계는 실제 검증이 끝나기 전까지 완료 처리하지 않는다.

## 8. 안내 작성 전 최종 체크

응답을 보내기 전 아래 항목을 확인한다.

- [ ] 현재 화면에서 이미 맞는 값을 다시 바꾸라고 하지 않았는가?
- [ ] Hierarchy의 선택 대상이 정확한가?
- [ ] Inspector 최상단 Layer와 컴포넌트 내부 설정을 구분했는가?
- [ ] Inspector 값이 화면에 표시되는 순서대로 나열되었는가?
- [ ] 사용자가 눌러야 할 위치와 행동을 생략하지 않았는가?
- [ ] 완료 확인 기준과 건드리지 않을 값을 적었는가?
