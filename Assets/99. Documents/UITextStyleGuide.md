# Project-PHD UI 텍스트 규격

새로 만들거나 고치는 모든 UI 글자는 이 표를 따른다.
어긋난 값이 필요하면 **먼저 이 문서를 고치고** 반영한다 — 예외를 인스펙터에만 남기지 않는다.

범용 UGUI 작업 규칙은 `UIConventions.md`에 있고, 이 문서는 PHD 고유 규격만 담는다.
화면 구조·배치는 `UIArchitecture.md`에 있다.

## 적용 범위

세 씬(`TitleScene` · `CharacterSelectScene` · `GameScene`)의 모든 `TextMeshProUGUI`.
아래 표의 값은 전부 **씬 파일에서 읽어낸 실측**이다(2026-08-28 기준).

> **이 프로젝트는 픽셀아트가 아니다.** ver3 아트로 갈아끼우면서 모든 스프라이트가
> Filter Mode `Bilinear`, Mipmap off 로 임포트돼 있고, 카메라의 `PixelPerfectCamera` 도 뗐다.
> 예전 문서에 있던 "픽셀 폰트 그리드 배수" · "반 픽셀 금지" 같은 픽셀아트 전제 규칙은
> 더 이상 적용되지 않는다. 자세한 사정은 `UIArchitecture.md` 4절에 있다.

---

## 폰트

**esamanru 한 종을 쓴다.** Bold 는 굵기 변형으로 두 자리에만 쓴다.

| 역할 | 에셋 | 씬 참조 수 |
|---|---|---|
| 본문 전부 | `05. Arts/Fonts/esamanru/esamanru Medium SDF.asset` | 21 |
| 강조 2자리 | `05. Arts/Fonts/esamanru/esamanru Bold SDF.asset` | 2 |

Bold 를 쓰는 자리는 결과창의 **`RETRY?`** 와 **`Invite a friend`** 뿐이다.
둘 다 알약 버튼/권유 문구로 "누르라"는 자리라서 굵기를 준다. 그 밖에 Bold 를 늘리지 않는다.

### 은퇴한 폰트

| 폰트 | 상태 |
|---|---|
| Nabla | **세 씬 어디에도 참조가 없다.** 장식용 영문 전용(한글 0자)이라 다국어와 함께 갈 수 없다 |
| TerrarumSans | 미사용 |

> `02. Prefabs/00. UI/General/Tmp_Nabla.prefab` 은 **아직 Nabla 를 물고 있다.**
> 이 프리팹에서 만든 텍스트는 씬에 놓는 즉시 폰트를 esamanru 로 바꾸게 되고,
> 실제로 결과창의 프리팹 인스턴스 7개가 전부 폰트 오버라이드를 달고 있다.
> **프리팹의 폰트를 esamanru Medium 으로 바꾸고 이름도 함께 정리하는 것이 맞다.**
> 아직 하지 않았다 — `UIArchitecture.md` 8절의 미해결 항목이다.

### 글리프 범위 — 한글은 esamanru 에서만 나온다

| 폰트 | ASCII | 완성형 한글 | 아틀라스 |
| --- | --- | --- | --- |
| esamanru Medium / Bold | 있음 | **있음** | **Dynamic** (원본 TTF 동봉) |
| Nabla | 95/95 | **0자** | Static |
| TerrarumSans | 있음 | 11,172자 | Static |

esamanru 의 `.ttf` 는 `includeFontData: 1` 이라 원본이 빌드에 동봉된다.
WebGL 에서도 처음 나오는 한글을 **실행 중에 구워** 쓴다.

> **⚠ 아틀라스가 거의 찼다.** `esamanru Medium SDF` 는 1024×1024 한 장,
> Sampling Point Size **132**, Padding 9 로 구워져 있다. 글리프 한 자가 약 100×100px 를
> 차지해서 **한 장에 100자가 채 안 들어간다.** 지금 50자가 들어 있고 세로로 778/1024 를 썼다 —
> **남은 자리는 10~15자쯤이다.**
>
> 그런데 `Multi Atlas Textures` 가 **꺼져 있다**(`m_IsMultiAtlasTexturesEnabled: 0`).
> 자리가 차면 그다음 글자는 **경고만 남기고 안 그려진다.** 한글 문구를 화면에 붙이기 전에
> 폰트 에셋에서 셋 중 하나를 해야 한다.
>
> 1. **Multi Atlas Textures 를 켠다** — 가장 간단하다. 아틀라스가 모자라면 장을 더 만든다
> 2. **Sampling Point Size 를 낮춘다**(132 → 64 정도) — 화면 글자가 8~30px 라 132 는 과하다.
>    면적이 1/4 이 되어 한 장에 400자쯤 들어간다. 다시 구워야 한다
> 3. Atlas Resolution 을 2048 로 올린다 — **2048 을 넘기지 않는다**(아래 참고)
>
> 에디터에서 한 번 켜 본 글자는 아틀라스에 남아 `.asset` 파일에 그대로 기록된다
> (지금 Medium 2.4MB / Bold 2.1MB). 커밋 전에 파일 크기를 본다.
> 4096 아틀라스는 YAML 로 40MB 가 되어 에디터 할당기가 저장 중에 죽는다.

### 메트릭

`esamanru Medium SDF` 의 FaceInfo 실측(Sampling Point Size 132 기준).

| 메트릭 | 값 | em 비율 |
|---|---|---|
| UnitsPerEM / PointSize | 1000 / 132 | — |
| LineHeight | 153.78 | **1.165 em** |
| CapLine | 95 | 0.720 em |
| MeanLine (x-높이) | 64 | 0.485 em |
| DescentLine | −41.58 | 0.315 em |

Bold 도 행높이 비율이 같다(140.97 / 121 = 1.165 em).

행높이가 1.17em 으로 **좁다.** 칸 높이는 글자 크기의 **1.2배**면 한 줄이 들어간다
(14px → 17px). 예전 Nabla(1.64em) 기준으로 잡아 둔 칸은 지금 넉넉하게 남는다.

크기에 배수 제약은 없다. 정수이기만 하면 된다.

---

## 크기

씬에 실제로 들어 있는 값이다. **표에 없는 값을 쓰지 않는다.**

| 크기 | 쓰는 곳 |
|---|---|
| **8** | 결과창 `round 0` · `Best 0` — 점수 아래 한 줄에 나란히 서는 자리 |
| **10** | 타이틀 언어 라벨(`KOR`/`ENG`) — 지구본 아이콘 아래 |
| **12** | 결과창 `Invite a friend` · `RANKING` |
| **14** | 기본값. 타이틀 `WITH CORTIS`, 캐릭터 선택 띠(`CHOOSE PLAYER`·`BEST`·`RANK`)와 난이도, HUD `round` 라벨·값, 결과창 `RETRY?`·`Download GIF` |
| **18** | HUD `score` 라벨·값, 결과창 `NEW BEST!` |
| **24** | 캐릭터 선택 `PLAY`·캐릭터 이름, 결과창 점수 |
| **30** | HUD 안내 문구(`ROUND 3` / `YOUR TURN` / `WRONG!` 가 지나가는 자리) |

화면이 270×480 아트 픽셀이라 14px 글자도 화면에서는 큼직하다
(1080p 표시 기준 4배 확대 = 56px).

> **어긋나 있음:** 규칙은 "한 화면에서 쓰는 단계 3개 이하" 인데
> **결과창이 8 · 12 · 14 · 18 · 24 로 다섯 단계**를 쓴다.
> 한 창 안에 서열이 다른 글자가 다섯 종류 있어서 벌어진 일이다 —
> 점수(24) > 신기록(18) > 버튼(14) > 권유·창 라벨(12) > 라운드·최고점(8).
> 줄이려면 버튼 라벨(14)을 12로 내리고 신기록(18)을 24로 올려
> **12 · 14 · 24** 로 모으는 것이 가장 가깝다. 아직 정하지 않았다.
>
> HUD 도 14 · 18 · 30 세 단계를 쓴다(경계선).

---

## 색

**파랑은 플레이스테이션 브랜드 블루 `#0070CC` 하나뿐이다.** 콜라보 화면이라 파란 글자를 전부
이 값으로 모았다(2026-08-30). 예전에 쓰던 `#0035A7`(HUD·권유 문구) · `#1668D8`(결과창 점수) ·
`#2A4A63`(캐릭터 이름·난이도)은 더 이상 쓰지 않는다 — **파랑을 새로 만들지 않는다.**
`#2A4A63` 은 결과창 `RANKING` 한 자리에만 남아 있다.

색이 갈리는 기준은 **배경**이다. ver3 아트는 배경이 밝아서, 예전 어두운 기기 화면용
팔레트(`#EEF1FF` · `#FFD84A` · `#35C1F1`)는 **전부 쓰이지 않는다.**

| 색 | 쓰는 곳 |
|---|---|
| `#3D4045` | 밝은 프레임·배경 위 기본 글자 — 타이틀 태그라인, 캐릭터 선택 위·아래 띠, HUD `round` 라벨·값 |
| `#2A4A63` | 결과창 `RANKING` — 이 한 자리만 남았다 |
| `#0070CC` | **파란 글자는 전부 이 색이다** — HUD `score` 값 · 안내 문구, 타이틀 언어 라벨,
캐릭터 선택 캐릭터 이름 · 난이도, 결과창 점수 · 권유 문구 |
| `#FFFFFF` | 결과창 `Download GIF`(파랑 알약 위) |
| `#EE3124` | 결과창 `NEW BEST!` — 이 한 자리에만 쓴다 |
| `#084D00` | 결과창 `RETRY?`(초록 알약 위) |
| `#000000` | 결과창 `round`·`Best` — 8px 이라 `#2A4A63` 로는 흰 바탕에서 흐려진다 |

역할별로 다시 보면 이렇다.

| 역할 | 크기 | 색 |
|---|---|---|
| HUD `round` 라벨·값 | 14 | `#3D4045` |
| HUD `score` 라벨 | 18 | `#3D4045` |
| HUD `score` 값 | 18 | `#0070CC` |
| HUD 안내 문구 | 30 | `#0070CC` |
| 타이틀 태그라인 (`WITH CORTIS`) | 14 | `#3D4045` |
| 타이틀 언어 라벨 (`KOR`/`ENG`) | 10 | `#0070CC` |
| 캐릭터 선택 위 띠 (`BEST`·`CHOOSE PLAYER`·`RANK`) | 14 | `#3D4045` |
| 캐릭터 선택 캐릭터 이름 | 24 | `#0070CC` |
| 캐릭터 선택 난이도 | 14 | `#0070CC` |
| 캐릭터 선택 `PLAY` | 24 | `#3D4045` |
| 결과창 점수 | 24 | `#0070CC` |
| 결과창 `round`·`Best` | 8 | `#000000` |
| 결과창 `NEW BEST!` | 18 | `#EE3124` |
| 결과창 권유 문구 | 12 | `#0070CC` |
| 결과창 `RANKING` | 12 | `#2A4A63` |
| 결과창 `RETRY?` | 14 | `#084D00` |
| 결과창 `Download GIF` | 14 | `#FFFFFF` |

색은 TMP 컴포넌트의 **Vertex Color** 에 직접 넣는다. 결과창을 이 표대로 찍어 주던
`ResultScreenBuildKit` 은 한 번 돌린 뒤 지웠다 — **지금 이 값들은 씬 안에만 있다.**
표를 고치면 인스펙터에서 손으로 맞춘다.

`GAME OVER` 는 더 이상 글자가 아니다 — `03. Sprites/ver3/GAME OVER.png` 스프라이트다.
결과창 공유 버튼(`아이콘_카톡` · `아이콘_공유`)도 아이콘만 남아 라벨이 없다.

---

## 머티리얼 프리셋

이름은 `<폰트>_<용도>` 를 쓴다(UIConventions 6절). 여기 없는 프리셋을 새로 만들지 않는다.

**다섯 프리셋 모두 `_FaceColor` 가 흰색이다.** 즉 화면에 보이는 글자색은 전부
위 표의 **Vertex Color** 에서 나온다. 프리셋이 하는 일은 **Underlay(그림자·번짐)뿐이다.**

> 이름에 `Gradiation` 이 붙어 있지만 **그라데이션이 아니다.** TMP 의 Vertex Gradient 는
> 씬의 어느 텍스트에도 켜져 있지 않고(`m_enableVertexGradient: 0`), 머티리얼에도 그라데이션
> 설정이 없다. 이름이 하는 일을 설명하지 못한다 — 고칠 때 `_Shadow` 계열로 바꾸는 것이 맞다.

| 프리셋 | Underlay 색 | Dilate / Offset / Softness | 쓰는 곳 |
|---|---|---|---|
| `esamanru Medium_Grey` | `#FFFFFF` a=1 | −0.36 / (−0.11, 0.25) / 1.0 | 밝은 띠 위 회색 글자 — 타이틀 태그라인, 캐릭터 선택 띠·`PLAY`, HUD `round`/`score` 라벨·값 |
| `esamanru Medium_Underlay` | `#FFFFFF` a=0.5 | 1.0 / (1, −1) / 0.2 | 흰 테두리 — 캐릭터 이름·난이도, `Download GIF` |
| `esamanru Medium_Gradiation_Blue` | `#FFFFFF` a=0.5 | 0.52 / (0.2, 0.2) / 0.277 | HUD 점수·안내 문구, 타이틀 언어 라벨, 결과창 점수·`round`·`Best`·`RANKING` |
| `esamanru Bold_Gradiation_Blue` | `#000000` a=0.5 | 0 / (1, −0.2) / 0 | `Invite a friend` |
| `esamanru Bold_Gradiation_Green` | `#000000` a=0.5 | 0 / (1, −0.2) / 0 | `RETRY?` |

> `esamanru Medium_Gradiation_Green` 은 참조가 0이라 지웠다(2026-08-28).

머티리얼을 늘리면 그만큼 배칭이 끊긴다. 화면 하나 때문에 프리셋을 늘리지 않는다.

---

## 행간 · 넘침

| 항목 | 값 | 이유 |
|---|---|---|
| Line Spacing | `0` | 폰트 기본 행간(1.17em)으로 충분하다 |
| Character Spacing | `0` | |
| Auto Size | **끈다** | 크기가 제멋대로 바뀌면 규격표가 의미를 잃는다 |
| Wrapping | `No Wrap` | 잘릴 만큼 길면 문구를 줄인다 |
| Overflow | `Overflow` | |
| Raycast Target | **끈다** | 글자가 클릭을 가로채면 버튼이 안 눌린다 |

세 씬의 모든 텍스트가 위 값으로 저장돼 있다(Auto Size off · No Wrap · Raycast off).

크기는 항상 **정수**로 적는다.

---

## 문구 — 결과창만 `GameText` 를 거친다

**결과창 문구는 씬이 아니라 `01. Scripts/00. Core/GameText.cs` 의 표에 있다.**
번역을 고치는 일은 그 파일 한 곳에서만 한다 — 씬·프리팹을 뒤지지 않는다.

현재 언어는 `LanguageSettings`(`GameLanguage.Korean` / `English`)가 들고 있고,
타이틀의 지구본 라벨 좌우 화살표(`LanguageButton`)가 바꾼다. 값은 PlayerPrefs `phd.language` 에 남는다.
문구가 여덟 줄뿐이라 Unity Localization 패키지는 쓰지 않는다.

> **다국어는 결과창과 타이틀 언어 라벨에서만 돈다.** HUD·캐릭터 선택의 문구는 아직 스크립트와 씬에
> 영어로 직접 적혀 있다(`"YOUR TURN"`, `"ROUND {0}"`, `PLAY`, `CHOOSE PLAYER`, `NORMAL` …).
> 결과창부터 시작한 것은 그 화면이 **공유로 남 앞에 나가는 자리**이기 때문이다.
> 다른 화면을 다국어로 돌릴 때 `TextId` 에 ID 를 추가하고 호출부를 옮긴다.

| 자리 | 채우는 방법 |
| --- | --- |
| 씬에 고정된 결과창 라벨 (`NEW BEST!` · `RETRY?` · `Invite a friend` · `Download GIF` · `RANKING`) | 그 TMP 오브젝트에 **`LocalizedText`** 를 붙이고 인스펙터 드롭다운에서 ID 를 고른다 |
| 코드가 찍는 결과창 문구 | `roundText.SetText(GameText.Get(TextId.ResultRound), round)` |
| 타이틀 언어 라벨 (`KOR`/`ENG`) | `Tmp_Language` 의 **`LocalizedText`**(ID `language_name`). 언어 이름도 표에 있어서 `LanguageSettings` 는 문자열을 하나도 들고 있지 않다 |

문구를 하나 늘리려면 `TextId` 에 상수를 넣고 `GameText` 표에 한 줄을 더한다.
언어를 하나 늘리려면 `GameLanguage` 에 항목을 넣고 표의 각 줄에 칸을 하나씩 더한다 —
`LanguageSettings` 의 순환 순서는 enum 에서 읽으므로 따로 고칠 곳이 없다.

- 표에 없는 ID 는 **ID 가 그대로 화면에 나온다.** 빈칸이 되지 않으니 빠뜨린 자리가 실행하는 순간 보인다.
- `{0}` 이 들어간 문구는 `LocalizedText` 에 물리지 않는다 — 그대로 `라운드 {0}` 이라고 찍힌다.
  `LocalizedText` 는 값이 없는 라벨 전용이다.
- 숫자가 섞인 문구는 반드시 `SetText(format, n)` 오버로드를 쓴다.
  `string.Format` 이나 `+` 연결은 매번 문자열을 만들어내고,
  WebGL 은 단일 스레드라 그 GC 가 프레임에 그대로 보인다.
  TMP 는 `{0}` 을 소수점 0자리로 찍으므로 정수는 그대로 나온다.
- **번역에서 `{0}` 을 빠뜨리면 숫자가 조용히 사라진다.** 표에서 숫자가 들어가는 줄은
  `result_round` · `result_best` 둘뿐이다.
- **한 문구의 두 언어는 길이가 크게 다르다.** 칸은 긴 쪽(대개 영어)에 맞춰 잡는다.
  `Wrapping` 이 `No Wrap` 이라 넘치면 잘리지 않고 옆으로 삐져나온다.
- `GAME OVER` 는 스프라이트라 언어를 따라가지 않는다. 다른 언어판이 필요하면
  스프라이트를 하나 더 만들어 갈아 끼우는 일이 먼저다.
- 언어 버튼은 **타이틀에만 있다.** 결과창 문구는 게임 씬이 로드된 시점의 언어로 고정된다.

### 지금 등록된 문구

| ID | 한국어 | 영어 | 채우는 곳 |
|---|---|---|---|
| `result_round` | 라운드 {0} | round {0} | `ResultScreen` 코드 |
| `result_best` | 최고 {0} | Best {0} | `ResultScreen` 코드 |
| `new_best_title` | 신기록! | NEW BEST! | `Tmp_NewBest` |
| `retry` | 다시 할래? | RETRY? | `Btn_Replay ▸ Tmp_Label` |
| `challenge_friend` | 친구 초대하기 | Invite a friend | `Tmp_Challenge` |
| `download_gif` | GIF 저장 | Download GIF | `Btn_DownloadGif ▸ Tmp_Label` |
| `ranking` | 랭킹 | RANKING | `Btn_Ranking ▸ Tmp_Label` |

위 일곱 중 **코드가 찍는 둘(`result_round` · `result_best`)만 지금 동작한다.**
나머지 다섯은 씬 라벨이라 `LocalizedText` 를 붙여야 하고, 아직 붙이지 않았다 —
붙이기 전까지는 씬에 적힌 영어가 그대로 나온다.

---

## 체크리스트

새 텍스트를 만들었다면:

- [ ] 형제 텍스트를 복제해서 만들었는가 (`Tmp_Nabla` 프리팹을 썼다면 폰트를 esamanru 로 바꿨는가)
- [ ] 폰트 에셋이 `esamanru Medium SDF` 인가 (Bold 는 결과창 두 자리만, Nabla·TerrarumSans 는 위반)
- [ ] 크기가 8 · 10 · 12 · 14 · 18 · 24 · 30 중 하나인가 (정수인가)
- [ ] 한 화면에서 쓰는 크기 단계가 3개 이하인가
- [ ] Vertex Color 가 위 표의 값인가 (배경에 맞는 쪽을 골랐는가)
- [ ] 머티리얼이 위 다섯 프리셋 중 하나인가
- [ ] 칸 높이가 글자 크기의 1.2배 이상인가 (행높이 1.17em)
- [ ] Auto Size 가 꺼져 있는가
- [ ] Raycast Target 이 꺼져 있는가
- [ ] **결과창 글자라면** `GameText` 표에 등록하고, 씬 라벨이면 `LocalizedText` 로 물렸는가
      (다른 화면은 아직 영어를 그 자리에 직접 적는다)
- [ ] 두 언어 모두 채웠고, 긴 쪽이 칸에 들어가는가
- [ ] 한글을 새로 넣었다면 폰트 아틀라스에 자리가 남아 있는가 (위 ⚠ 참고)
- [ ] 숫자 갱신에 `SetText` 오버로드를 썼는가
