# Project-PHD UI 텍스트 규격

새로 만들거나 고치는 모든 UI 글자는 이 표를 따른다.
어긋난 값이 필요하면 **먼저 이 문서를 고치고** 반영한다 — 예외를 인스펙터에만 남기지 않는다.

범용 UGUI 작업 규칙은 `UIConventions.md`에 있고, 이 문서는 PHD 고유 규격만 담는다.

## 적용 범위


---

## 폰트

**한 종만 쓴다. 두 번째 폰트를 추가하지 않는다.**

| 항목 | 값 |
|---|---|
| 폰트 에셋 | `Assets/05. Arts/Fonts/Nabla/Nabla-Regular-VariableFont_EDPT,EHLT SDF.asset` |
| 원본 | Nabla (Google Fonts, 장식용 디스플레이 폰트) |
| 머티리얼 | 폰트 에셋 내장 머티리얼 |

새 텍스트는 **`Assets/02. Prefabs/00. UI/General/Tmp_Nabla.prefab` 을 복제해서** 만든다.
빈 오브젝트에 TMP 를 붙이면 TMP Settings 의 기본 폰트(영문 LiberationSans)가 들어간다.
같은 역할의 형제 텍스트가 이미 있으면 그쪽을 복제하는 편이 낫다 — 크기·색·정렬이 이미 맞아 있다.

### 글리프 범위 — 영문 전용

폰트 파일의 cmap 을 직접 확인한 값이다.

| 폰트 | 전체 | ASCII | 완성형 한글 |
| --- | --- | --- | --- |
| Nabla | **376자** | 95/95 | **0자** |

**모든 UI 문구는 영어로 쓴다.** 한글을 넣으면 네모(`□`)로 나오고, 그 자리는 실행해 봐야 드러난다.
한글이 꼭 필요해지면 한글이 되는 폰트를 하나 더 들이는 결정이 먼저다.

> `05. Arts/Fonts/TerrarumSans` 는 더 이상 UI 에 쓰지 않는다(한글 11,172자를 갖고 있어
> 나중에 한글이 필요해지면 후보가 된다). 지금 UI 에서 그 폰트가 보이면 규격 위반이다.

### 메트릭

| 메트릭 | 값 | em 비율 |
|---|---|---|
| UnitsPerEM / PointSize | 1000 / 90 | — |
| LineHeight | 147.87 | **1.643 em** |
| CapLine | 84 | 0.933 em |
| MeanLine (x-높이) | 63 | 0.700 em |
| DescentLine | −34.2 | 0.380 em |

행높이가 1.64em 으로 크다. **칸 높이를 잡을 때 글자 크기의 1.7배는 확보한다.**
Nabla 24 는 한 줄에 39px 이 필요하다 — `GameLayout.HeadingHeight` = 40 이 그 값이다.

Nabla 는 픽셀 폰트가 아니라 **크기에 배수 제약이 없다.** 정수이기만 하면 된다.

---

## 크기 · 색

**세 단계까지만 쓴다.** 표에 없는 값을 쓰지 않는다.

> `GameLayout.cs` 에 `TextBase` · `TextDisplay` 상수가 있지만 **지금 어느 스크립트도 그 파일을
> 참조하지 않고**, 18(`TextHeading`)은 아예 없다. 값은 인스펙터에 직접 들어가 있다.
> `GameLayout` 을 되살릴지 지울지는 `UIArchitecture.md` 4절의 미해결 항목이다.

| 토큰 | 크기 | 캡 | 행 | 역할 |
|---|---|---|---|---|
| `TextBase` | **14** | 13px | 23px | 수치 · 라벨 · 버튼 · 안내 — 사실상 전부 |
| `TextHeading` | **18** | 17px | 30px | 결과창 제목(`GAME OVER`) — 창 폭 208px 에 들어가야 하는 자리 |
| `TextDisplay` | **24** | 22px | 39px | 팝업 제목, 결과창 점수 |

- 화면이 270×480 아트 픽셀이라 14px 글자도 화면에서는 큼직하다 (1080p 표시 기준 4배 확대 = 56px).
- **한 화면에서 쓰는 단계는 3개 이하.**
  결과창만 세 단계를 쓴다 — 점수(24)가 제목(18)보다 커야 하는 화면이라 24 하나로는 서열이 안 나온다.
  `GAME OVER` 를 24로 두면 208px 창을 넘어 오른쪽 스티커에 물린다.

| 역할 | 크기 | 색 | 정렬 |
|---|---|---|---|
| HUD 수치 (score / round 값) | 14 | `#EEF1FF` | Center |
| HUD 라벨 (score / round 글자) | 14 | `#EEF1FF` 55% | Center |
| HUD 안내 문구 (Message) | 14 | `#FFD84A` | Center |
| 팝업 제목 | 24 | `#35C1F1` | Center |
| 값 (팝업 안 수치) | 14 | `#EEF1FF` | Midline Right |
| 신기록 배지 | 14 | `#FFD84A` | Center |
| 항목 이름 (팝업 안 키) | 14 | `#EEF1FF` 55% | Midline Left |
| 버튼 라벨 | 14 | `#2A4A63` | Center |
| 타이틀 문구 (`With CORTIS`, `Touch to Start`) | 14 | `#FFFFFF` | Center |
| 결과창 제목 (`GAME OVER` / `NEW BEST!`) | 18 | `#EE3124` | Center |
| 결과창 점수 | 24 | `#1668D8` | Center |
| 결과창 라운드·최고점 (`round 3` / `Best 120`) | 14 | `#2A4A63` | Midline Right / Midline Left |
| 결과창 버튼 라벨 (`RETRY?`, 공유) | 14 | `#2A4A63` | Center |

색이 갈리는 기준은 **배경**이다.

| 색 | 쓰는 곳 |
|---|---|
| `#EEF1FF` | 어두운 기기 화면 위 기본 글자 |
| `#EEF1FF` 55% (알파 0x8C) | 같은 배경의 라벨·단위 |
| `#2A4A63` | 밝은 버튼 베이스 위 글자, 결과창 흰 바탕 위 본문 |
| `#FFFFFF` | 하늘색 배경(타이틀) 위 글자 |
| `#FFD84A` | 점수 상승·신기록처럼 눈에 띄어야 하는 문구 |
| `#35C1F1` | 제목, 진행 점 (로고 블루) |
| `#EE3124` | 결과창 흰 바탕 위 제목 — 이 한 자리에만 쓴다 |
| `#1668D8` | 결과창 흰 바탕 위 점수 |

결과창(`결과창2_0`)은 안이 흰 창이라 어두운 배경용 글자색이 통째로 뒤집힌다.
그 창 안에서만 위 세 색(`#2A4A63` · `#EE3124` · `#1668D8`)을 쓴다.

색은 TMP 컴포넌트의 `Vertex Color` 에 직접 넣는다.
결과창을 이 표대로 찍어 주던 `ResultScreenBuildKit` 은 한 번 돌린 뒤 지웠다 —
**지금 이 값들은 씬 안에만 있다.** 표를 고치면 인스펙터에서 손으로 맞춘다.

> **미해결:** 타이틀은 하늘색 배경 위 흰 글자라 그림자가 필요하다. 배치 예상도에도 파란 그림자가 있다.
> Nabla 용 Underlay 머티리얼 프리셋이 아직 없다(예전에 쓰던 `TerrarumSansBitmap_Shadow` 는
> 다른 폰트의 아틀라스를 참조해서 Nabla 에 붙이면 글자가 깨진다).
> 인스펙터에서 Nabla 머티리얼을 복제해 Underlay 를 켜고 `Nabla_Shadow` 로 저장한 뒤 이 표에 등록한다.

---

## 행간 · 넘침

| 항목 | 값 | 이유 |
|---|---|---|
| Line Spacing | `0` | 폰트 기본 행간(1.64em)이 이미 넉넉하다 |
| Character Spacing | `0` | |
| Auto Size | **끈다** | 크기가 제멋대로 바뀌면 규격표가 의미를 잃는다 |
| Wrapping | `No Wrap` | 잘릴 만큼 길면 문구를 줄인다 |
| Overflow | `Overflow` | |
| Raycast Target | **끈다** | 글자가 클릭을 가로채면 버튼이 안 눌린다 |

크기는 항상 **정수**로 적는다.

---

## 문구

- **영문만 쓴다.** 폰트에 한글이 아예 없다.
- 지금 쓰는 문구: `TAP TO START`, `Touch to Start`, `With CORTIS`, `score`, `round`,
  `YOUR TURN`, `PERFECT +10`, `GAME OVER`, `NEW BEST!`, `WRONG!`, `RETRY?`, `Best`, `PLAY`
  그리고 결과창 공유 버튼의 한글 라벨 `친구에게 알리기` (위 폰트 절의 미해결 항목)
- 숫자가 섞인 문구는 `SetText("ROUND {0}", n)` 을 쓴다.
  `string.Format` 이나 `+` 연결은 매번 문자열을 만들어내고,
  WebGL 은 단일 스레드라 그 GC 가 프레임에 그대로 보인다.
- 구운 문자셋 밖의 글자는 네모(`□`)로 나오고, 그 자리는 실행해 봐야 드러난다.

---

## 아틀라스는 Static

폰트 에셋은 **Static + 사전 문자셋**으로 굽는다. Dynamic 으로 두면 글자가 처음 나올 때마다
아틀라스가 갱신되고 **그 결과가 .asset 파일에 기록된다.** 이 프로젝트는 에셋을 텍스트(YAML)로
직렬화하므로 아틀라스 픽셀이 통째로 파일에 들어간다 — 4096×4096 아틀라스는 그것만으로 **40MB** 다.

**굽는 방법**

   (문구 상당수가 씬이 아니라 코드에 있어서 ASCII 는 통째로 넣는다)
2. `Window ▸ TextMeshPro ▸ Font Asset Creator` 를 열고 원본 폰트를 지정한다.
3. `Character Set` 을 **Custom Characters** 로 두고 1번에서 나온 문자열을 붙여넣는다.
4. `Atlas Resolution` 은 **2048 을 넘기지 않는다.** 4096 은 YAML 로 40MB 가 되어
   에디터 할당기가 저장 중에 죽는다.
5. `Save` 로 기존 에셋에 덮어쓴다 — GUID 와 서브에셋 fileID 가 보존되어 씬 참조가 끊기지 않는다.

현재 Nabla 에셋의 생성 설정: Sampling Point Size 90, Padding 9, Render Mode SDFAA, Atlas 1024.

---

## 체크리스트

새 텍스트를 만들었다면:

- [ ] `Tmp_Nabla` 프리팹이나 형제 텍스트를 복제해서 만들었는가
- [ ] 폰트 에셋이 Nabla 인가 (TerrarumSans 가 보이면 위반)
- [ ] 크기가 14 · 18 · 24 중 하나인가 (정수인가)
- [ ] 한 화면에서 쓰는 크기 단계가 3개 이하인가
- [ ] 색이 위 표의 값인가 (배경에 맞는 쪽을 골랐는가)
- [ ] 칸 높이가 글자 크기의 1.7배 이상인가 (행높이 1.64em)
- [ ] Auto Size 가 꺼져 있는가
- [ ] Raycast Target 이 꺼져 있는가
- [ ] 문구가 영어인가
- [ ] 숫자 갱신에 `SetText` 오버로드를 썼는가
