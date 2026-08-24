# Project-PHD UI 구조

기억력 게임(타이틀 + 게임플레이 2씬)의 UI 체계. **게임 로직은 이 문서의 어떤 것도 알지 못한다.**

범용 UGUI 작업 규칙은 `UIConventions.md`, 폰트·크기·색 규격은 `UITextStyleGuide.md` 에 있다.
이 문서는 그 위에 얹는 **PHD 고유 구조와 예외**만 담는다.

---

## 1. 레이어

```
UIRoot (씬당 1개, Canvas 에 붙는다)     ← 스크린 찾기 / 팝업 스택 / 암전
      ├── UIScreen                      ← 한 덩어리로 켜고 끄는 화면
      │     ├── GameHud      (Hud)
      │     ├── TitleScreen  (Hud)
      │     └── ResultScreen (Popup)
      └── 위젯
            ├── UIButton      ← 눌림 연출 + 클릭음
            └── UIIconStrip   ← 아이콘 줄 풀링
                  ├── LifeIcons
                  └── ProgressDots
```

보조 도구: `UITween`(페이드·팝), `SafeAreaFitter`(노치), `SceneLoadButton`(씬 이동).

---

## 2. 지켜야 할 규칙

### 게임 로직 ↔ UI 의 접점은 `GameHud` 하나뿐이다

`GameFlow` 는 화면이 어떻게 생겼는지 모른다. "라운드가 3이 됐다"고만 말한다.

```csharp
hud.SetRound(3);
hud.SetScore(40);
hud.SetMessage("ROUND {0}", 3);
hud.SetupLives(3);  hud.SetLives(2);
hud.Dots.Setup(5);  hud.Dots.SetFilled(2);
```

**이 시그니처는 계약이다.** 이름이나 인자를 바꾸면 `GameFlow` 가 깨진다.
표시 방식(글꼴, 위치, 연출)은 `GameHud` 안에서 얼마든지 바꿔도 된다.

새 정보를 HUD 에 띄우고 싶다면 → `GameHud` 에 메서드를 **추가**하고, 게임 로직 쪽에서 호출한다.
UI 스크립트가 `GameFlow` 를 직접 참조해서 값을 읽어오지 않는다.

### 색은 인스펙터에서 정한다

색 팔레트를 관리하는 별도 레이어(테마 에셋)는 아직 두지 않는다.
쓰는 색이 적고 화면이 둘뿐이라, 지금은 인스펙터에서 직접 찍는 편이 빠르다.

값은 `UITextStyleGuide.md` 의 표를 따르고, 위젯이 자기 색을 들고 있는 경우
(`LifeIcons` 의 alive/lost, `ProgressDots` 의 empty/filled)는 그 필드에서 고친다.
에디터 메뉴로 조립할 때 찍히는 기본값은 `UIBuildKit` 의 색 상수에 모여 있다.

> 화면과 색이 늘어나 손으로 관리하기 어려워지면, 그때 `UITheme`(ScriptableObject) +
> 색 슬롯 컴포넌트를 넣는다. 지금 구조는 그걸 나중에 얹어도 깨지지 않는다.

### 켜고 끌 때 `SetActive` 를 직접 부르지 않는다

`UIScreen.Show()` / `Hide()` 를 쓴다. 페이드와 입력 차단이 함께 처리되고,
사라지는 중에 버튼이 눌려 두 번 진행되는 사고를 막는다.

**화면 루트는 꺼진 채로 저장한다**(UIConventions 7절). 씬 로드 첫 프레임의 잔상을 막는다.
꺼진 루트도 `UIRoot` 의 `Screens` 배열이 인스펙터로 들고 있으므로 찾을 수 있고,
`Show()` 가 켜는 순간 `Awake` 가 돌면서 `Visible On Start` 가 방금 정한 상태를
덮어쓰지 않도록 `UIScreen` 안에서 막아 둔다.

### 참조는 전부 인스펙터 연결

실행 중에 `Find`/`GetComponentInChildren` 으로 화면을 찾지 않는다.
`UIRoot.Screens` 는 **에디터 메뉴가 훑어서 채운다**. 화면을 손으로 추가했다면
메뉴를 다시 실행하거나 배열에 직접 끌어다 넣는다. 비어 있으면 `UIRoot` 가 실행 시 에러를 남긴다.

### 화면마다 Canvas 를 따로 둔다

점수 한 글자가 바뀌었을 때 결과창까지 같이 다시 그리지 않게 하려는 것이다.
겹치는 순서는 계층이 아니라 `UILayer`(Backdrop / Hud / Popup / Overlay)가 정한다.

`UIConventions` 7절은 "HUD / Popup / Tooltip 3층 컨테이너"를 말하는데,
PHD 는 컨테이너 오브젝트 대신 **화면별 중첩 Canvas 의 `sortingOrder`** 로 같은 효과를 낸다.
화면이 셋뿐이라 컨테이너를 따로 두면 계층만 깊어진다.
팝업 스택과 입력 차단에는 `UILayer.Popup` 만 참여한다 — 다른 층을 `OpenPopup` 에 넘기면 경고가 뜬다.

### 이름은 컴포넌트 종류 접두사 + 역할

`Group_` `Panel_` `Btn_` `Img_` `Tmp_` (UIConventions 1절).
순수 배치용 빈 오브젝트는 접두사 없이 역할명(`SafeArea`).
화면 루트는 스크립트 클래스명과 맞춘다 (`Group_TitleScreen` ↔ `TitleScreen.cs`).

---

## 3. 화면 배치 (배치 예상도 기준)

수치는 전부 `Device_Frame` 스프라이트 실측이고 `GameLayout` 에 있다.

모든 요소가 **한 캔버스 안에** 있다. 단위는 아트 픽셀, 원점은 화면 중심(270 x 480)이다.

```
UI Canvas
├ Group_Stage                PadInput + CanvasGroup, 화면 가득
│  ├ Img_Background          BG_Gradient_Blue, 화면 가득 (비율 달라도 여백 없음)
│  │   └ Deco/*              별 3 + 흰 원호 4
│  ├ Img_Phone  218 x 386    (0, 0)
│  │   └ Group_Pad           (0, -117) = 하단 휠 중심
│  │       세모(0,+31) 동글(+31,0) 엑스(0,-31) 네모(-31,0)   각 29 x 29
│  └ Stage Icon              (0, +55.5), 배율 2.21 → 64 x 64
│                             (타이틀 씬은 Img_Background 만 들어간다)
├ Frame  169 x 203           (0, +55.5) = 기기 화면판. HUD 는 전부 이 안
│  ├ score 라벨+값            위에서 8,    높이 48
│  ├ 안내 문구                중앙,        높이 40   ┐ 정답 문양과 시간대가
│  ├ 진행 점                  아래에서 58, 높이 10   │ 갈려 자리를 나눠 쓴다
│  ├ round 라벨+값            아래에서 30, 높이 24   ┘
│  └ 하트                     아래에서 12, 높이 14
├ Group_ResultScreen         꺼진 채로 저장
└ Img_Shade
```

**HUD 는 Safe Area 안에 두지 않는다.** 기기 화면판 위에 정확히 얹혀야 하므로 캔버스 중앙에
고정한다. 가장자리에 붙는 UI(타이틀의 로고·문구)만 safe area 를 쓴다.

### 월드에서 캔버스로 옮긴 이유

배경·기기·패드는 원래 월드 SpriteRenderer 였다. 좌표계가 둘이라 대가가 컸다.

- 기기(월드)와 HUD(캔버스)가 계속 어긋났다. HUD 만 safe area 를 따라가던 버그가 그 예다.
- 패드 입력이 `Physics2D.OverlapPoint` 라 결과창이 떠 있어도 UI 를 통과했다.
  그래서 게임 로직이 직접 입력을 껐다. 지금은 `CanvasGroup` 한 곳에서 막는다.
- 화면 배치를 위해 UGUI 를 월드에 다시 구현한 코드가 있었다 —
  `ScreenAnchor`(주석: "UI 앵커의 월드 버전"), `CameraFitter`(주석: "CanvasScaler 와 동일한 규칙").
  둘 다 어느 씬에도 붙어 있지 않은 죽은 코드였고 이번에 지웠다.

옮기면서 **게임 로직은 한 줄도 바뀌지 않았다.** 세 클래스의 이름과 public API 가 그대로라
씬의 직렬화 참조도 살아 있다.

| 클래스 | 전 | 후 | 유지된 API |
|---|---|---|---|
| `PadButton` | SpriteRenderer + CircleCollider2D | Image + `IPointerDownHandler` | `Index` `Sprite` `Pressed` `Highlight()` |
| `PadInput` | 포인터→월드 변환 44줄 | CanvasGroup 토글 | `InputEnabled` |
| `StageIcon` | SpriteRenderer | Image + `SetNativeSize` | `Show()` `Hide()` |

누름은 `Button.onClick`(포인터 업)이 아니라 **닿는 순간**(`IPointerDownHandler`)에 처리한다.
박자에 맞춰 누르는 게임이라 업 판정으로는 늦다.

타이틀은 화면 전체가 투명 버튼(`Btn_TouchToStart`)이고, 씬 이동은 그 위의
`SceneLoadButton` 이 그대로 맡는다. 로고는 화면 중앙, `With CORTIS` 는 로고 위,
`Touch to Start` 와 양옆 화살표는 아래에서 50 이다.

---

## 4. 좌표계

`PixelPerfectCamera` 가 **270 × 480 아트 픽셀**, PPU 32 로 맞춰져 있고,
`PixelPerfectUIScaler` 가 캔버스 배율을 카메라 배율(정수)과 똑같이 맞춘다.
기준 기기 해상도는 **1080 × 1920** (= 270×480 의 정확히 4배)이고, 두 씬의 CanvasScaler 는
그 기준값(scaleFactor 4, Reference Resolution 1080×1920)으로 저장해 둔다 —
실행 중에는 `PixelPerfectUIScaler` 가 기기에 맞는 정수 배율로 매 프레임 덮어쓴다.

> **캔버스 1 단위 = 아트 1 픽셀.**

그래서 인스펙터에 적는 위치·크기는 전부 아트 픽셀이다. 소수점을 쓰지 않는다 —
반 픽셀에 놓인 스프라이트는 화면에서 흐려진다.

기준값은 `GameLayout` 에 모여 있다. 화면 규격이 바뀌면 그 파일만 고친다.

---

## 5. 새로 만들 때

### 화면 하나 추가

1. `UIScreen` 을 상속한 스크립트를 `02. Screens/` 에 만든다.
2. Canvas 아래에 `Screen_XXX` 오브젝트를 만들고 `Canvas` + `GraphicRaycaster` + `CanvasGroup` + 스크립트를 붙인다.
3. `Layer` 를 고른다. 팝업이면 `Dims Background` 를 켠다.
4. 띄울 때는 `UIRoot.Current.OpenPopup(screen)` 또는 `screen.Show()`.

`UIRoot` 의 `Screens` 는 비워둔다 — 실행할 때 자식에서 알아서 찾는다.

### 아이콘 줄 위젯 추가

`UIIconStrip` 을 상속하고 `ColorFor(int index)` 만 구현한다.
개수 관리·풀링·레이아웃 연동은 베이스가 처리한다.
`Instantiate`/`Destroy` 를 직접 부르지 않는다 — WebGL 은 단일 스레드라 GC 히칭이 그대로 보인다.

아이콘은 **반드시 `Icon Prefab` 에서 복제된다.** 계층에 미리 놓아둔 오브젝트는 쓰이지 않고,
남아 있으면 실행 시 경고가 뜬다(UIConventions 7절).

### 프리팹 폴더

```
Assets/02. Prefabs/00. UI/
  General/    원자 공용        — Tmp_Nabla
  UIContent/  반복 슬롯·행     — Img_Life(하트), Img_Dot(진행 점)
  UIView/     화면 단위
  Setting/    캔버스 루트
```

**글자는 반드시 `Tmp_Nabla` 를 복제해서 만든다.** GameObject 메뉴로 새로 만들면
TMP Settings 의 기본 폰트(영문)가 붙고, 폰트 지정을 매번 잊게 된다.
다만 1순위는 **같은 화면의 형제 텍스트 복제**다 — General 프리팹은 기본값만 들고 있어
역할 정보(크기·색·정렬)가 없다.

**UI 폰트는 Nabla 하나다.** 영문 전용이라 모든 문구를 영어로 쓴다. 자세한 건 `UITextStyleGuide.md`.

---

## 6. 아트 적용 (에디터 메뉴)

**PHD ▸ UI** 메뉴에 모여 있다. 전부 Undo 가 되고, 이미 있는 오브젝트의 값은 덮어쓰지 않는다.

| 메뉴 | 씬 | 하는 일 |
|---|---|---|
| `0. 공용 UI 프리팹 만들기 · 갱신` | — | `General/Tmp_Nabla` 생성·규격 재적용 |
| `1. 타이틀 화면 UI 만들기` | Title | 로고·소개문구·Touch to Start·화살표 구성 |
| `2. 게임 화면 UI 보강` | Game | 결과 팝업 생성, 목숨에 하트 아트 적용, 진행 점 프리팹(Img_Dot) 연결 |
| `3. 기존 HUD 규격 맞추기` | Game | 글자 크기·폰트 통일·Life 프리팹 연결·이름 접두사 **(1회성)** |
| `5. HUD 배치 (배치 예상도 기준)` | Game | Frame 을 화면판 위치로, score 위 / round·하트 아래 |
| `6. 중복 · 잔재 정리` | 아무 씬 | 중첩 복제된 화면·자리 벗어난 암전 판·잘못 붙은 UIRoot 제거 |
| `8. 폰트 문자셋 뽑기` | — | Font Asset Creator 에 붙여넣을 문자셋을 콘솔에 출력. 에셋은 안 건드림 |
| `9. 규격 점검` | 아무 씬 | 폰트·크기·Auto Size·Raycast·Scale·정수 픽셀·접두사를 훑어 보고 |

실행 후 인스펙터에서 위치를 다듬고 **씬을 저장**한다.
`1` 은 화면 밖에 남은 옛 오브젝트를 콘솔에 알려준다(지우지는 않는다).

처음 여는 프로젝트라면 이 순서로 한 번씩 돌린다.

```
0  →  Game: 6 → 2 → 3 → 5 → 9  →  Title: 6 → 1 → 9
```

> `4. 월드 → 캔버스 이전`(WorldToCanvas.cs)은 두 씬의 이전이 끝나 지웠다.
> 이전 경위는 3절에, 캔버스 설정 기준(Screen Space - Camera, planeDistance 5)은 7절에 남아 있다.

`6` 을 먼저 돌리는 이유는, 예전 `FindCanvas` 버그로 화면이 중첩 복제된 씬이 있을 수 있어서다.
깨끗한 씬에서는 아무것도 하지 않는다.

### 쓰는 스프라이트

`Assets/03. Sprites/01. UI/Base_sheet2.png` — **지금은 blue 계열만 쓴다.**
주황/복숭아 변형(`Logo_Orange`, `Btn_Base_Peach`)은 같은 시트에 있으니
필요해지면 인스펙터에서 스프라이트만 바꿔 끼우면 된다.

| 슬라이스 | 크기 | 쓰임 |
|---|---|---|
| `Logo_Blue` | 183×192 | 타이틀 로고 |
| `Btn_Base_Blue` | 76×27 | 버튼 베이스 (전부) |
| `Icon_Play_Red` | 11×17 | 시작 버튼 아이콘 |
| `Icon_Heart` | 14×13 | 목숨 |
| `Icon_Arrow_Solid` / `Icon_Arrow_Light` | 24×32 / 22×30 | 다음·넘김 (미사용) |
| `Icon_Arrow_Solid` | 24×32 | Touch to Start 양옆 (오른쪽은 Z 180° 회전) |
| `Pad_Triangle` / `Circle` / `Cross` / `Square` | 29×29 | 패드 문양 |
| `Device_Frame` | 218×386 | 기기 프레임 |

`Assets/03. Sprites/01. UI/Background.png`

| 슬라이스 | 크기 | 쓰임 |
|---|---|---|
| `BG_Gradient_Blue` | 270×480 | 배경 바닥. 화면을 가득 채우게 늘려 쓴다 |
| `BG_Star_A` / `B` / `Big` | ~97×153 등 | 장식 별. 반투명 하늘색 |
| `Background_0` ~ `_3` | ~108×136 등 | 장식 흰 원호 |
| `BG_Deco_A` | 78×43 | 미사용 |

---

## 7. UIConventions 대비 예외

| 규칙 | PHD 처리 | 이유 |
|---|---|---|
| 3절 — Scale With Screen Size + 1920×1080 고정 | `ConstantPixelSize` + `scaleFactor = pixelRatio` | 픽셀 퍼펙트. 규칙의 목적("Font Size == 기준 해상도 픽셀")은 캔버스 1단위 = 아트 1픽셀로 그대로 성립한다. **렌더 모드는 두 씬 모두 Screen Space - Camera(planeDistance 5)로 통일** |
| 7절 — HUD/Popup/Tooltip 3층 컨테이너 | 화면별 중첩 Canvas + `UILayer` sortingOrder | 화면이 셋뿐이라 컨테이너를 두면 계층만 깊어진다 |
| 4절 — Scale 항상 1 | 정지 상태는 항상 1. 누름·팝 연출만 일시적으로 스케일을 건드리고 정확히 1로 복귀 | 인스펙터에 남는 값이 아니라 추적 문제를 만들지 않는다 |

---

## 8. 아직 하지 않은 것

**함정 문양(`GameFlow.trapSprites`)이 아직 `playstation-buttons.jpg` 를 가리킨다.**
참고용 이미지를 임시로 꽂아둔 상태다. `james.png`(6조각)가 함정 아트로 보이는데
임포트 PPU 가 100 이라 다른 아트(32)와 어긋난다. 쓰기로 정하면 PPU 를 32 로 맞춘다.

~~**수치 표기.**~~ 해결됨 — `GameHud` 가 10 미만이면 `SetText("0{0}", n)` 으로 두 자리를
채운다(배치 예상도의 `score 00` / `round 00` 표기. 게임 로직과의 계약은 그대로다).

## 9. 결과창

웹 빌드에서는 브라우저 오버레이(`WebGLTemplates` 의 `window.PHDResult`)를 그대로 쓴다 —
공유·클립보드가 웹 API 라 그쪽이 맞다.

에디터·스탠드얼론에는 그 오버레이가 없어 예전에는 결과 화면이 아예 보이지 않았다.
이제 씬에 `ResultScreen` 이 있으면 `ResultShare` 가 그쪽으로 돌린다.
스크린이 없으면 예전과 똑같이 동작하므로, 넣지 않아도 아무것도 깨지지 않는다.
