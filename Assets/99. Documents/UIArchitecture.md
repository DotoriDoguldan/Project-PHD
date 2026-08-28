# Project-PHD UI 구조

리듬·기억력 게임(타이틀 → 캐릭터 선택 → 게임플레이, 3씬)의 UI 체계.
**게임 로직은 이 문서의 어떤 것도 알지 못한다.**

범용 UGUI 작업 규칙은 `UIConventions.md`, 폰트·크기·색·문구 규격은 `UITextStyleGuide.md` 에 있다.
이 문서는 그 위에 얹는 **PHD 고유 구조와 예외**만 담는다.

문서의 수치·구조는 세 씬 파일에서 읽어낸 **실측**이다(2026-08-28 기준).

> **이 프로젝트는 더 이상 픽셀아트가 아니다.** ver3 아트로 갈아끼우면서 스프라이트가 전부
> Filter Mode `Bilinear` · Mipmap off 로 임포트돼 있고, 임포트 PPU 도 32/96/100 으로 갈려 있다.
> 예전 문서의 픽셀아트 전제(Point 필터, 정수 좌표 강제, PPU 정수배)는 4절에서 정리했다.

---

## 1. 레이어

```
UIRoot (씬당 1개, 루트 Canvas 에 붙는다)   ← 스크린 찾기 / 팝업 스택 / 암전
      ├── UIScreen                        ← 한 덩어리로 켜고 끄는 화면
      │     ├── GameHud             (Hud)    GameScene — 루트 `UI Canvas` 에 함께 붙는다
      │     ├── TitleScreen         (Hud)    TitleScene
      │     ├── CharacterSelectScreen (Hud)  CharacterSelectScene
      │     └── ResultScreen        (Popup)  GameScene
      └── 위젯
            ├── UIButton          ← 눌림 연출 + 클릭음
            ├── UIIconStrip       ← 아이콘 줄 풀링
            │     ├── LifeIcons        (GameScene)
            │     └── ProgressDots     (GameScene)
            ├── CharacterCarousel ← 캐릭터 선택 슬롯 회전
            ├── StageBackground   ← 출제 중인 패드 색으로 무대 배경을 갈아 끼움
            ├── LanguageButton    ← 언어 전환 (타이틀)
            ├── LocalizedText     ← 씬 고정 라벨을 현재 언어 문구로 채움 (결과창용)
            ├── WebShareAnchor    ← 웹 공유 DOM 버튼이 놓일 자리
            ├── UIFloatingDeco    ← 장식이 사인 곡선으로 부드럽게 떠다님 (타이틀 4개)
            └── UIStopMotionDeco  ← 장식이 정해진 포즈 몇 장을 툭툭 갈아 끼움 (결과창 4개)
```

보조 도구: `UITween`(페이드·팝), `SafeAreaFitter`(노치), `SceneLoadButton`(씬 이동),
`ScreenFader`(씬 전환 페이드).

`ScreenFader` 와 `SoundManager` 는 **"참조는 전부 인스펙터 연결" 규칙의 예외**다.
둘 다 씬을 넘어 살아남아야 해서 처음 쓸 때 스스로 만들어지고 `DontDestroyOnLoad` 로 남는다
(`SoundManager` 는 `[RuntimeInitializeOnLoadMethod]` 로 첫 씬 전에 뜨고
`Resources/SoundLibrary` 를 읽는다). 씬에 아무것도 놓지 않는다.

`LanguageSettings` 도 같은 성격이지만 **오브젝트가 아니라 정적 클래스**다 —
`[RuntimeInitializeOnLoadMethod]` 로 저장된 언어를 읽고, 바뀌면 `Changed` 이벤트로 알린다.
정적 이벤트라 구독하는 컴포넌트는 `OnEnable` 에서 붙이고 `OnDisable` 에서 반드시 뗀다.

`UIFloatingDeco` 와 `UIStopMotionDeco` 는 **둘 다 켜질 때의 위치·각도·크기를 기준으로 잡고
꺼질 때 그 값으로 되돌린다.** 껐다 켜도 기준점이 밀리지 않는다.
차이는 보간 여부다 — 부드럽게 이으면 떠다니는 것이고, 끊어 놓으면 스톱모션이다.

---

## 2. 지켜야 할 규칙

### 게임 로직 ↔ UI 의 접점은 `GameHud` 하나뿐이다

`GameFlow` 는 화면이 어떻게 생겼는지 모른다. "라운드가 3이 됐다"고만 말한다.

```csharp
hud.SetRound(3);
hud.SetScore(40);
hud.SetMessage("ROUND {0}", 3);   hud.ClearMessage();
hud.SetupLives(3);  hud.SetLives(2);
hud.Dots.Setup(5);  hud.Dots.SetFilled(2);  hud.Dots.Clear();
```

**이 시그니처는 계약이다.** 이름이나 인자를 바꾸면 `GameFlow` 가 깨진다.
표시 방식(글꼴, 위치, 연출)은 `GameHud` 안에서 얼마든지 바꿔도 된다.

`GameFlow` 가 UI 를 직접 만지는 예외가 하나 있다 — `StageBackground` 다.
배경색이 곧 "지금 어느 패드가 출제됐는가" 라는 게임 정보라 HUD 가 아니라 무대에 붙었고,
`ShowPad(int)` / `ResetToDefault()` 두 메서드만 노출한다.

새 정보를 HUD 에 띄우고 싶다면 → `GameHud` 에 메서드를 **추가**하고, 게임 로직 쪽에서 호출한다.
UI 스크립트가 `GameFlow` 를 직접 참조해서 값을 읽어오지 않는다.

### 결과창 문구는 `GameText` 표에서 온다

**다국어는 결과창에서만 돈다.** 공유로 남 앞에 나가는 화면이라 여기부터 시작했다.
결과창 글자는 `GameText.Get(TextId.XXX)` 로 꺼내고, 씬 고정 라벨은 `LocalizedText` 를 붙인다.

HUD·타이틀·캐릭터 선택은 아직 문자열을 **스크립트와 씬에 영어로 직접 적는다**
(`hud.SetMessage("YOUR TURN")`, 씬의 `PLAY`·`CHOOSE PLAYER`·`NORMAL`).
그 화면을 다국어로 돌릴 때 `TextId` 에 ID 를 추가하고 호출부를 옮긴다.

자세한 것은 `UITextStyleGuide.md` 의 "문구" 절에 있다.

### 색은 인스펙터에서 정한다

색 팔레트를 관리하는 별도 레이어(테마 에셋)는 아직 두지 않는다.
쓰는 색이 여덟 개뿐이라, 지금은 인스펙터에서 직접 찍는 편이 빠르다.

값은 `UITextStyleGuide.md` 의 표를 따르고, 위젯이 자기 색을 들고 있는 경우
(`LifeIcons` 의 alive/lost, `ProgressDots` 의 empty/filled)는 그 필드에서 고친다.

> 화면과 색이 늘어나 손으로 관리하기 어려워지면, 그때 `UITheme`(ScriptableObject) +
> 색 슬롯 컴포넌트를 넣는다. 지금 구조는 그걸 나중에 얹어도 깨지지 않는다.

### 켜고 끌 때 `SetActive` 를 직접 부르지 않는다

`UIScreen.Show()` / `Hide()` 를 쓴다. 페이드와 입력 차단이 함께 처리되고,
사라지는 중에 버튼이 눌려 두 번 진행되는 사고를 막는다.

**화면 루트는 꺼진 채로 저장한다**(UIConventions 7절). 씬 로드 첫 프레임의 잔상을 막는다.
꺼진 루트도 `UIRoot` 의 `Screens` 배열이 인스펙터로 들고 있으므로 찾을 수 있고,
`Show()` 가 켜는 순간 `Awake` 가 돌면서 `Visible On Start` 가 방금 정한 상태를
덮어쓰지 않도록 `UIScreen` 안에서 막아 둔다.

`Hide()` 는 페이드가 끝난 뒤 **마지막에 `SetActive(false)`** 로 내린다.
그래서 꺼진 화면 안의 `Update`(스톱모션 연출 등)는 한 번도 돌지 않는다.

### 참조는 전부 인스펙터 연결

실행 중에 `Find`/`GetComponentInChildren` 으로 화면을 찾지 않는다.
`UIRoot.Screens` 는 **인스펙터에서 직접 채운다.** 화면을 새로 추가했다면 배열에 끌어다 넣는다.
비어 있으면 `UIRoot` 가 실행 시 에러를 남긴다.

씬별 등록 현황:

| 씬 | `UIRoot.Screens` | `Shade` |
|---|---|---|
| TitleScene | `Group_TitleScreen` | (없음) |
| CharacterSelectScene | `Group_CharacterSelect` | (없음) |
| GameScene | `UI Canvas`(GameHud) · `Group_ResultScreen` | `Img_Shade` |

> 예전에는 에디터 메뉴가 이 배열을 훑어서 채웠다. 그 메뉴(`UIBuildKit`)는 지금 없다 — 6절 참고.

### 화면마다 Canvas 를 따로 둔다

점수 한 글자가 바뀌었을 때 결과창까지 같이 다시 그리지 않게 하려는 것이다.
겹치는 순서는 계층이 아니라 `UILayer` 가 정한다 — 값이 그대로 `Canvas.sortingOrder` 가 된다.

| `UILayer` | 값 |
|---|---|
| `Backdrop` | −100 |
| `Hud` | 100 |
| `Popup` | 200 |
| `Overlay` | 300 |

`UIConventions` 7절은 "HUD / Popup / Tooltip 3층 컨테이너"를 말하는데,
PHD 는 컨테이너 오브젝트 대신 **화면별 중첩 Canvas 의 `sortingOrder`** 로 같은 효과를 낸다.
화면이 적어서 컨테이너를 따로 두면 계층만 깊어진다.
팝업 스택과 입력 차단에는 `UILayer.Popup` 만 참여한다 — 다른 층을 `OpenPopup` 에 넘기면 경고가 뜬다.

`UIScreen.Awake` 는 **중첩 Canvas 만** `overrideSorting` 을 켠다.
루트 Canvas 의 `sortingOrder`(GameScene 은 100, 나머지 0)는 씬이 정하는 값이라 건드리지 않는다.
`GameHud` 가 루트 `UI Canvas` 에 함께 붙어 있는 것이 그래서 문제가 되지 않는다.

### 이름은 컴포넌트 종류 접두사 + 역할

`Group_` `Panel_` `Btn_` `Img_` `Tmp_` (UIConventions 1절).
순수 배치용 빈 오브젝트는 접두사 없이 역할명(`SafeArea`, `SafeContent`, `Frame`, `Deco`).
화면 루트는 스크립트 클래스명과 맞춘다 (`Group_ResultScreen` ↔ `ResultScreen.cs`).

> **어긋나 있음:** GameScene 의 패드 4개만 `Pad Button 0 (세모)` 처럼 접두사 없는 한글 이름이다.
> 게임 로직이 이름이 아니라 `PadButton.Index` 로 찾으므로 고쳐도 안전하다.

---

## 3. 화면 배치

단위는 아트 픽셀, 원점은 화면 중심(270 × 480)이다.

ver3 아트로 갈아끼우면서 **기기 프레임을 화면 가운데 놓던 구성에서 풀스크린 구성으로 바뀌었다.**
위아래에 프레임 밴드를 붙이고 그 사이를 게임 화면으로 쓴다.

### TitleScene

```
Canvas                              Screen Space - Camera(planeDistance 5), sortingOrder 0
├ Group_Background                  화면 가득
│  └ Img_Background                 배경 (2), AspectRatioFitter
│     └ Deco                        중첩 Canvas — 떠다니는 장식 4장
│        Img_Deco_Triangle (-92.7, 208.6) 122  Img_Deco_Cross  (115, 156) 92
│        Img_Deco_Square   (-105, -39) 110x73  Img_Deco_Circle (112, -69) 88
└ Group_TitleScreen                 Canvas(Hud) + GraphicRaycaster + CanvasGroup + TitleScreen
   ├ Btn_TouchToStart               화면 가득 투명 버튼 + SceneLoadButton
   │   └ Img_TouchToStart           (0, 55.7) 165x55 — 깜빡이는 안내(CanvasGroup)
   └ SafeArea                       화면 가득 (노치 대응)
      ├ Img_Logo                    (-6, -136) 240x175 — 타이틀2, 중첩 Canvas
      ├ Tmp_Tagline                 (-35, 81.6) — "WITH CORTIS"
      ├ Img_PSLogo                  (0, 161.8) 34x34 — 로고2
      ├ Group_Language              (135.1, 108.8) 48x24 — 표시 전용, RectTransform 하나뿐
      │  ├ Img_Language             (-3.8, 9.6) 45.25x45.25 — 지구본(아이콘_언어)
      │  └ Tmp_Language             (-3.5, -16) — LocalizedText(language_name), "KOR"/"ENG"
      ├ Btn_LanguagePrev            (-40.5, 117.9) 32x30 — 투명 히트박스, LanguageButton(Previous)
      │  └ Img_ArrowPrev            16x18 — 화살표1 뒤집음(◀)
      └ Btn_LanguageNext            (33.8, 117.2) 32x30 — 투명 히트박스, LanguageButton(Next)
         └ Img_ArrowNext            16x18 — 화살표1(▶)
```

> 타이틀은 지금 배치를 손보는 중이라 위 좌표가 자주 바뀐다.

언어는 **화살표 두 개로만** 고른다(`◀ 🌐KOR ▶`). 둘 다 `LanguageButton` 이고 `direction` 만 다르다.
가운데 `Group_Language`(지구본 + 라벨)는 **표시 전용**이다 — 누를 것이 없으므로 Button 도,
레이캐스트를 막던 투명 Image 도 두지 않는다. 그래야 가운데를 눌렀을 때
"아무 일도 안 일어나는 칸"이 되지 않고 뒤의 `Btn_TouchToStart` 로 그대로 넘어간다.
`Btn_` 이 아니라 `Group_` 인 이유도 그것이다(UIConventions 1절).

`LanguageButton` 은 **누르는 일만** 한다. 라벨 갱신은 `Tmp_Language` 에 붙은
`LocalizedText`(ID `language_name`)가 맡는다 — 다른 화면의 라벨과 같은 방식이다.

화살표는 **누를 칸(32x30 투명 Image)과 아트(16x18)를 분리**했다.
아트 크기 그대로 누르게 두면 탭 타깃이 4mm 남짓인데 **바로 뒤가 화면 가득한 `Btn_TouchToStart`** 라,
몇 픽셀만 빗나가도 언어가 바뀌는 대신 게임이 시작된다 — 실수 비용이 한쪽으로만 크다.
좌향 미러(`scale -1`)도 아트 자식으로 내려서 **버튼 자신의 스케일은 1** 이다(UIConventions 4절).
누름 연출이 건드리는 것이 버튼 스케일이라, 미러와 엉키지 않는다.

> 화살표는 원래 `Img_ArrowLeft`/`Img_ArrowRight` 라는 이름으로 **시작 버튼의 자식**이었다.
> 언어 라벨 옆으로 옮기면서 이름이 실제 좌우와 어긋나 역할 이름으로 바꿨고,
> 부모도 `SafeArea` 로 옮겼다 — `Group_Language` 와 같은 안전 영역을 따라가야 노치 기기에서
> 라벨만 밀리지 않는다. 둘 다 화면 가득이라 좌표는 그대로다.

안내 문구는 글자가 아니라 **`시작.png` 스프라이트**다(`Img_TouchToStart`).
`TitleScreen` 의 `hint` 는 이 이미지의 `CanvasGroup` 을 받아 알파를 깜빡인다.

### CharacterSelectScene

```
Canvas                              sortingOrder 0
├ Group_Background
│  └ Img_Background                 배경 (2)
└ Group_CharacterSelect             Canvas(Hud) + CharacterSelectScreen
   └ SafeArea
      ├ Img_Shadow                  (0, -100) 167.7x45.4 — 캐릭터 그림자
      ├ Group_Carousel              (0, -95) 240x230 — CharacterCarousel, 슬롯 5개
      │   Slot_1 = Img_Unlocked(james_full)  Slot_2~5 = Img_Locked(실루엣_o/x/ㅁ/ㅅ) + Img_Lock(자물쇠)
      ├ Img_NameBg                  (0, -140) 168x50 — 이름UI2
      ├ Tmp_Name                    (0, -140) — 캐릭터 이름 / LOCKED
      ├ Btn_ArrowLeft / Btn_ArrowRight  (∓92, 20) 22x25 — 화살표1
      ├ Group_Difficulty            (0, -58) 200x42
      │   ├ Tmp_Difficulty          난이도 이름
      │   └ Group_Stars             (0, -26) — 별 3개(난이도별), HorizontalLayoutGroup
      ├ Img_FrameTop                (0, 0) 270x43 — 프레임_위
      │   ├ Tmp_Best  (-89.7, -13)   Tmp_Title (-3.1, -19.4)   Tmp_Rank (89.5, -13)
      └ Img_FrameBottom             (0, 80) 270x146 — 프레임_아래
          └ Btn_Play                위쪽 stretch, 높이 80 — CanvasGroup + SceneLoadButton
              └ Tmp_Play            (0, -45) "PLAY"
```

### GameScene

```
UI Canvas                    Screen Space - Camera(planeDistance 5), sortingOrder 100
                             Canvas + CanvasScaler + GraphicRaycaster + CanvasGroup
                             + UIRoot + GameHud            ← 루트에 HUD 가 함께 붙는다
├ Group_Stage                PadInput + CanvasGroup, 화면 가득
│  ├ Img_Background          배경 (2) + AspectRatioFitter + StageBackground
│  ├ Img_Phone               프레임_아래, 아래 가장자리에 폭 전체 x 157
│  │   └ Group_Pad           (0, +71) 140 x 140 다이아몬드
│  │       Img_PadBase 108 x 108(버튼 배경) + 패드 버튼 4개(버튼_0~3) 각 37 x 37
│  └ Group_QtePrompt         (0, +57) 64 x 64 — Img_James / Img_Key / Img_Ring
├ Frame                      화면 가득
│  ├ Img_FrameTop            프레임_위, 위 가장자리에 폭 전체 x 43.4
│  └ SafeContent             화면 가득 (노치 대응), HUD 는 전부 이 안
│      ├ Group_Lives         오른쪽 위 (-3, -6)  80 x 16   GridLayoutGroup + LifeIcons
│      ├ Tmp_Message         중앙 (0, +52.7)    169 x 40
│      ├ Group_ProgressDots  위 중앙 (0, -50)   120 x 23   ProgressDots
│      ├ Group_Score         위 중앙 (0, -4)    130 x 44   Tmp_Key(score) / Tmp_Value
│      └ Group_Round         왼쪽 위 (4.1, -0.1) 100 x 20   Tmp_Key(round) / Tmp_Value
├ Img_Shade [꺼진 채 저장]    화면 가득, 팝업 뒤 암전
└ Group_ResultScreen [꺼진 채 저장]   Canvas(Popup) — 9절
```

**HUD 는 `SafeContent`(safe area) 안에 둔다.** 위쪽 프레임 밴드에 얹히는 정보라
노치·상태바를 피해야 한다.

> **정리됨(2026-08-28):** `UI Canvas` 바로 밑에 자식 없는 `SafeArea` 가 하나 더 있었다.
> `SafeAreaFitter` 는 **자기 RectTransform 만** 늘리고 줄이므로 자식이 없으면 아무 효과가 없다 —
> 노치 대응은 `Frame ▸ SafeContent` 가 하고 있었다.
> 같은 이유로 비어 있던 `VFX`(GameScene) · `Deco`(CharacterSelectScene)와 함께 지웠다.

### 월드에서 캔버스로 옮긴 이유

배경·기기·패드는 원래 월드 SpriteRenderer 였다. 좌표계가 둘이라 대가가 컸다.

- 기기(월드)와 HUD(캔버스)가 계속 어긋났다. HUD 만 safe area 를 따라가던 버그가 그 예다.
- 패드 입력이 `Physics2D.OverlapPoint` 라 결과창이 떠 있어도 UI 를 통과했다.
  그래서 게임 로직이 직접 입력을 껐다. 지금은 `CanvasGroup` 한 곳에서 막는다.
- 화면 배치를 위해 UGUI 를 월드에 다시 구현한 코드가 있었다 —
  `ScreenAnchor`(주석: "UI 앵커의 월드 버전"), `CameraFitter`(주석: "CanvasScaler 와 동일한 규칙").
  둘 다 어느 씬에도 붙어 있지 않은 죽은 코드였고 그때 지웠다.

옮기면서 **게임 로직은 한 줄도 바뀌지 않았다.** 세 클래스의 이름과 public API 가 그대로라
씬의 직렬화 참조도 살아 있다.

| 클래스 | 전 | 후 | 유지된 API |
|---|---|---|---|
| `PadButton` | SpriteRenderer + CircleCollider2D | Image + `IPointerDownHandler` | `Index` `Sprite` `Pressed` `Highlight()` |
| `PadInput` | 포인터→월드 변환 44줄 | CanvasGroup 토글 | `InputEnabled` |
| `QtePrompt` | SpriteRenderer | Image | `Show()` `Hide()` |

누름은 `Button.onClick`(포인터 업)이 아니라 **닿는 순간**(`IPointerDownHandler`)에 처리한다.
박자에 맞춰 누르는 게임이라 업 판정으로는 늦다.

타이틀·캐릭터 선택의 씬 이동은 `SceneLoadButton` 이 맡는다 —
어느 씬으로 갈지는 UI 가 알 일이 아니고, 인스펙터에서 바꿀 수 있어야 한다.

---

## 4. 좌표계

세 씬의 캔버스가 모두 이렇게 저장돼 있다.

| 항목 | 값 |
|---|---|
| Render Mode | Screen Space - Camera, `planeDistance` 5 |
| UI Scale Mode | Scale With Screen Size |
| Reference Resolution | **270 × 480** |
| Screen Match Mode | **Expand** |
| Reference Pixels Per Unit | 32 |
| 루트 Canvas `sortingOrder` | GameScene 100, 나머지 0 |

> **캔버스 1 단위 = 아트 픽셀 1.**

인스펙터에 적는 위치·크기는 전부 아트 픽셀이다.
기준 기기 해상도는 **1080 × 1920** (= 270 × 480 의 정확히 4배)다.

**270 × 480 은 이제 캔버스 기준일 뿐이고, 카메라는 이 격자를 강제하지 않는다.**

> **정리됨(2026-08-28):** URP 2D 의 `PixelPerfectCamera` 를 세 씬 카메라에서 뗐다.
> 픽셀아트를 그만둔 뒤로 하는 일이 없었다 — 세 씬에 월드 렌더러(SpriteRenderer·ParticleSystem)가
> 하나도 없고 UI 는 전부 Screen Space - Camera 캔버스라, 픽셀 스냅이 걸릴 대상 자체가 없었다.
> 카메라 `orthographicSize` 는 이제 씬에 저장된 값이 그대로 쓰인다(UI 배치에 영향 없음).
>
> 다시 붙여야 하는 경우는 하나다 — **월드 스프라이트를 다시 쓰기 시작할 때.**

"아트 1픽셀 = 캔버스 1단위" 도 **배경(PPU 32) 계열에서만** 성립한다.
ver3 스프라이트의 임포트 PPU 가 32·96·100 으로 갈려 있기 때문이다.

정수 좌표 규칙도 같은 이유로 완화됐다. 여전히 **정수를 기본으로 삼되**
(칸 크기를 셈하기 쉽고 리뷰에서 눈에 띈다), 반 픽셀이 화면을 흐리게 만들지는 않는다.
지금 씬에 남아 있는 소수점: `Img_FrameTop` 43.4, `Tmp_Message` 52.7,
`Group_Round` (4.1, −0.1), `Img_Language` 22.4, `Tmp_Tagline` 81.6, `Img_Board` 386.03 등.

`UIStopMotionDeco` 의 위치 흔들림이 `Mathf.Round` 를 거치는 것은 이 규칙의 잔재다.
흔들림을 정수로 끊으면 스톱모션 느낌이 또렷해지는 효과가 따로 있어서 그대로 둔다.

> **정리됨(2026-08-28):** `GameLayout.cs` 를 지웠다. 이 좌표계의 기준값 모음으로 만들어졌지만
> 어느 스크립트도 참조하지 않았고, 값도 은퇴한 ver1/ver2 아트 실측
> (`DeviceWidth` 218, `PopupWidth` 196, `LogoWidth` 183, 이미 없는 `Device_Frame` 스프라이트 기준)
> 이라 현재 배치와 전혀 맞지 않았다.
>
> **기준값은 3절의 씬 트리가 유일한 출처다.** 다시 상수로 뽑고 싶어지면
> 그때는 씬이 실제로 그 상수를 참조하게 만들어야 한다 — 참조되지 않는 상수 파일은
> 문서보다 빨리 낡는다.

---

## 5. 새로 만들 때

### 화면 하나 추가

1. `UIScreen` 을 상속한 스크립트를 `02. Screens/` 에 만든다.
2. Canvas 아래에 `Group_XXX` 오브젝트를 만들고 `Canvas` + `GraphicRaycaster` + `CanvasGroup` + 스크립트를 붙인다.
3. `Layer` 를 고른다. 팝업이면 `Dims Background` 를 켠다.
4. **`UIRoot` 의 `Screens` 배열에 끌어다 넣는다.** 비어 있으면 실행 시 에러가 난다.
5. 띄울 때는 `UIRoot.Current.OpenPopup(screen)` 또는 `screen.Show()`.

### 아이콘 줄 위젯 추가

`UIIconStrip` 을 상속하고 `ColorFor(int index)` 만 구현한다.
개수 관리·풀링·레이아웃 연동은 베이스가 처리한다.
`Instantiate`/`Destroy` 를 직접 부르지 않는다 — WebGL 은 단일 스레드라 GC 히칭이 그대로 보인다.

아이콘은 **반드시 `Icon Prefab` 에서 복제된다.** 계층에 미리 놓아둔 오브젝트는 쓰이지 않고,
남아 있으면 실행 시 경고가 뜬다(UIConventions 7절).

### 프리팹 폴더

```
Assets/02. Prefabs/
  00. UI/General/    원자 공용        — Tmp_Nabla
  00. UI/UIContent/  반복 슬롯·행     — Img_Life(하트), Img_Dot(진행 점)
  00. UI/UIView/     화면 단위        — (비어 있음)
  00. UI/Setting/    캔버스 루트      — (비어 있음)
  99. Managers/      상시 매니저      — (비어 있음, SoundManager·ScreenFader 는 코드가 만든다)
```

**글자는 형제 텍스트를 복제해서 만든다.** 같은 화면의 형제는 크기·색·머티리얼이 이미 맞아 있다.
`Tmp_Nabla` 프리팹을 쓴다면 **폰트를 esamanru Medium 으로 반드시 바꾼다** —
프리팹이 아직 Nabla 를 물고 있어서, 결과창의 프리팹 인스턴스 7개가 전부 폰트 오버라이드를
달고 있는 상태다. 자세한 것은 `UITextStyleGuide.md` 의 "폰트" 절.

---

## 6. 아트 적용

### 에디터 툴

`Assets/Editor/` 에 넷이 있다.

| 파일 | 하는 일 |
|---|---|
| `SpriteAtlasSetup.cs` | 빌드 씬을 스캔해 SpriteAtlas 를 만들고 갱신한다 (`08. Atlases/`) |
| `WebGLBuilder.cs` | WebGL 빌드 자동화 — 로컬 확인용(`Build/WebGL`) / GitHub Pages 배포용(`docs/`) |
| `SoundIdDropdownDrawer.cs` | 사운드 ID 를 인스펙터 드롭다운으로 |
| `TextIdDropdownDrawer.cs` | 텍스트 ID 를 인스펙터 드롭다운으로 |

**UI 셋업 툴은 없다.** 예전 `PHD ▸ UI ▸ 0~9` 메뉴를 갖고 있던 `UIBuildKit.cs`,
결과창을 시안대로 찍던 `ResultScreenBuildKit.cs` 는 한 번 돌린 뒤 지웠다 —
**지금 씬 배치는 손으로 유지한다.**

> UIConventions 8절은 "씬을 손으로 고치지 말고 `MenuItem` 셋업 툴로 찍어라"라고 한다.
> UI 배치에 한해서는 그 규칙을 지키지 않는 상태다. 같은 배치를 또 만들 일이 생기면
> 툴을 되살리는 편이 낫다(멱등 · Undo · 기존 값 존중 세 가지를 지킬 것).

### 스프라이트 아틀라스

`08. Atlases/` 에 네 장이 있다 — `Atlas_Shared` · `Atlas_Title` ·
`Atlas_CharacterSelect` · `Atlas_Game`.

원본 PNG 는 가로·세로가 4의 배수가 아니라 DXT 압축이 걸리지 않고 RGBA32 로 폴백한다.
아틀라스 페이지는 POT 라 아트를 건드리지 않고 압축이 걸리고, 덤으로 같은 페이지에 묶인
UI 끼리 Canvas 배칭이 붙는다 — **WebGL 은 draw call 이 JS 브릿지를 타서 비싸다.**

2개 이상 씬이 함께 쓰는 텍스처는 `Atlas_Shared`, 한 씬 전용은 `Atlas_<씬이름>` 으로 묶인다.
한 텍스처가 두 아틀라스에 들어가면 메모리가 두 배가 되므로 **항상 정확히 한 곳에만** 들어가야 한다.
**아트를 추가·교체한 뒤에는 메뉴를 다시 실행한다.**

### 쓰는 스프라이트

현역 아트는 **`Assets/03. Sprites/ver3/`** 다.
구버전은 `Assets/03. Sprites/test/`(ver1, ver2)로 옮겼고 **GUID 가 유지돼** 씬 참조가 살아 있다.

임포트 설정은 전부 **Filter Bilinear · Mipmap off · Compression 없음**이고,
PPU 만 시트마다 32 / 96 / 100 으로 갈린다. `SetNativeSize` 를 쓰는 곳만 PPU 에 의존하고,
나머지는 RectTransform 에 크기를 직접 적는다.

| 씬 | 파일 | 쓰이는 곳 |
|---|---|---|
| 공통 | `01. UI/배경 (2).png` (PPU 32) | 세 씬의 `Img_Background` |
| 공통 | `01. UI/프레임_위.png` · `프레임_아래.png` (96) | 위·아래 프레임 밴드, GameScene 은 `Img_Phone` |
| 공통 | `01. UI/화살표1.png` (32) | 타이틀 안내 화살표, 캐릭터 선택 좌우 버튼 |
| 공통 | `01. UI/스티커1.png` (100) | 타이틀 지구본, 결과창 커서 손·화살표 |
| 타이틀 | `01. UI/타이틀2.png` (32) · `로고2.png` (100) · `시작.png` (32) | 로고 · PS 로고 · TOUCH TO START |
| 타이틀 | `01. UI/배경_세모 · _엑스 · _네모 · _원.png` (100/512) | 떠다니는 장식 4장 |
| 선택 | `00. Icons/james_full.png` (100) | 해금 캐릭터 슬롯 |
| 선택 | `00. Icons/실루엣_o · _x · _ㅁ · _ㅅ.png` (100) | 잠긴 슬롯 실루엣 |
| 선택 | `01. UI/자물쇠.png` · `이름UI2.png` · `난이도별.png` (100) | 자물쇠 · 이름판 · 난이도 별 |
| 선택 | `캐릭터 그림자.png` (100) | 무대 그림자 |
| 게임 | `01. UI/버튼.png` (96, `버튼_0`~`_3`) | 패드 버튼 4개, QTE 문양 |
| 게임 | `버튼 배경.png` (100) | `Img_PadBase` |
| 게임 | `00. Icons/james_full.png` (100) | QTE 캐릭터, 결과창 포즈 |
| 게임 | `배경_초록_세모 · 빨강_원 · 파랑_엑스 · 핑크_네모.png` (100) | `StageBackground` 의 패드 0~3 출제 배경 |
| 결과창 | `01. UI/결과창2.png` (100) | 창틀 |
| 결과창 | `GAME OVER.png` (100) | 제목 |
| 결과창 | `배경_그라데이션원2 · 3.png` (100) | 창 안 글로우 2장 |
| 결과창 | `REDRED.png` · `GREEN.png` (100) | 색 태그 장식 각 7장 |
| 결과창 | `01. UI/배경용 별.png` (32) | 창 밖 별 2장 |
| 결과창 | `버튼_초록.png` · `버튼_파랑.png` (100) | RETRY 알약 · Download GIF 알약 |
| 결과창 | `스티커_다운로드.png` · `아이콘_카톡.png` · `아이콘_공유.png` (100) | 버튼 아이콘 |
| 결과창 | `창2.png` · `랭킹.png` (100) | 랭킹 창틀 · 트로피 |
| 리더보드 | `ver5-리더보드.png` (100) · `아이콘_닫기.png` (100) | 판 그림 · 닫기 |
| **구버전** | `test/ver2/01. UI/Icon_QteRing.png` | QTE 링 — **ver3 대체가 없어 구버전을 계속 쓴다** |

---

## 7. UIConventions 대비 예외

| 규칙 | PHD 처리 | 이유 |
|---|---|---|
| 3절 — Reference Resolution 1920×1080 | **270 × 480** 로 고정 (Scale With Screen Size, Expand) | 규칙의 목적("Font Size == 기준 해상도 픽셀")은 그대로 성립한다. 1080×1920 의 정확히 1/4. 렌더 모드는 세 씬 모두 Screen Space - Camera(planeDistance 5) |
| 4절 — 임포트 PPU 를 한 값으로 고정 | **32 / 96 / 100 세 값이 섞여 있다** | 아트가 시트별로 다른 배율로 그려져 왔다. 한 시트의 PPU 를 바꾸면 그 시트를 쓰는 다른 화면이 같이 틀어진다 |
| 4절 — Image 는 예외 없이 SetNativeSize | **크기를 RectTransform 에 직접 적는다.** `SetNativeSize` 는 QTE 프롬프트 등 일부만 | 위와 같은 이유 |
| 4절 — Scale 항상 1 | 정지 상태는 항상 1. 누름·팝·스톱모션 연출만 일시적으로 스케일을 건드리고 1로 복귀 | 인스펙터에 남는 값이 아니라 추적 문제를 만들지 않는다 |
| 4절 — 픽셀아트 임포트(Point·Mipmap off) | **Bilinear.** 픽셀아트가 아니다 | 4절 참고 |
| 6절 — 크기 단계 5~7, 한 화면 최대 4 | 7단계(8·10·12·14·18·24·30), 결과창이 한 화면에 5단계 | `UITextStyleGuide.md` 의 미해결 항목 |
| 7절 — HUD/Popup/Tooltip 3층 컨테이너 | 화면별 중첩 Canvas + `UILayer` sortingOrder | 화면이 적어 컨테이너를 두면 계층만 깊어진다 |
| 7절 — 참조는 전부 인스펙터 연결 | `SoundManager` · `ScreenFader` · `LanguageSettings` 는 스스로 뜬다 | 씬을 넘어 살아남아야 한다 |
| 8절 — 셋업은 에디터 툴로 | UI 배치는 **지키지 않는 중**. 아틀라스·빌드는 툴이 있다 (6절) | — |

---

## 8. 아직 하지 않은 것

- **`Tmp_Nabla` 프리팹이 아직 Nabla 를 물고 있다.** 세 씬에서 Nabla 참조는 0인데,
  이 프리팹에서 만든 텍스트는 매번 폰트를 갈아 끼워야 한다. 프리팹 폰트를 esamanru 로 바꾸고
  이름도 함께 정리해야 한다.
- **esamanru 폰트 아틀라스가 거의 찼고 Multi Atlas Textures 가 꺼져 있다.**
  한글 문구를 화면에 붙이기 전에 조치해야 한다 — `UITextStyleGuide.md` 의 ⚠ 항목.
- **머티리얼 이름이 하는 일과 다르다** — `Gradiation` 인데 실제로는 Underlay 프리셋이다.
- **에디터 UI 셋업 툴이 없다.** UIConventions 8절 미준수 (6절).
- **뒤로가기/ESC 로 팝업을 닫는 길이 없다.** 쓰이지 않던 `UIRoot.CloseTopPopup()` 은 지웠다 —
  필요해지면 입력 처리와 함께 다시 넣는다.
- **타이틀 화살표 이름이 좌우와 어긋나 있다** (3절).
- **GIF 다운로드가 아직 없다.** `Btn_DownloadGif` 는 눌려도 아무 일도 하지 않는다 (9절).
- **리더보드에 순위 데이터가 없다.** 지금은 그림 한 장이다 (9절).

~~**함정 문양(`GameFlow.trapSprites`)**~~ 해결됨 — 함정 기능 자체가 제거됐다.

~~**수치 표기.**~~ 해결됨 — `GameHud` 가 10 미만이면 `SetText("0{0}", n)` 으로 두 자리를 채운다.

~~**카카오 아이콘이 임시다.**~~ 해결됨 — `ver3/아이콘_카톡.png` 로 바뀌었다.

~~**폰트 규격이 문서와 씬 사이에서 갈려 있다.**~~ 해결됨 — esamanru 로 정리했다.

~~**`GameLayout.cs` 가 미참조다.**~~ 해결됨 — 지웠다 (4절).

~~**`PixelPerfectCamera` 가 남아 있다.**~~ 해결됨 — 세 씬에서 뗐다 (4절).

~~**빈 오브젝트 셋이 남아 있다.**~~ 해결됨 — `SafeArea` · `Deco` · `VFX` 를 지웠다 (3절).

~~**`esamanru Medium_Gradiation_Green` 머티리얼이 참조 0이다.**~~ 해결됨 — 지웠다.

---

## 9. 결과창

**결과 카드는 통째로 Unity UI 가 그린다**(`ResultScreen` + `Group_ResultScreen`).
웹이든 에디터든 같은 화면이고, 씬에 `ResultScreen` 이 없으면 `ResultShare` 가
결과창 없이 진행하므로 아무것도 깨지지 않는다.

```
Group_ResultScreen              Canvas(Popup) + GraphicRaycaster + CanvasGroup + ResultScreen
├ Group_ResultContent           CanvasGroup — 리더보드가 뜨면 이 칸을 통째로 감춘다
│  ├ Panel_Result  208 x 376  (0, +6)   결과창2. 뒤쪽 클릭을 막는 판이기도 하다
│  │  ├ Img_Glow      170x162 (0, +54)   배경_그라데이션원2
│  │  ├ Img_Glow (1)  110x105 (4.5,-102) 배경_그라데이션원3
│  │  ├ Group_RedTags                    REDRED 42x7 짜리 7장, 창 위쪽에 흩뿌림
│  │  ├ Img_GameOver  190x22  (0, +72)   GAME OVER 스프라이트 — 평소 제목
│  │  ├ Img_Pose      68x144  (0, +54)   james_full (X 포즈)
│  │  ├ Tmp_NewBest   196x32  (0, +72)   신기록일 때 제목 자리를 대신 차지
│  │  ├ Tmp_Score     196x42  (0, +138)
│  │  ├ Tmp_Round / Tmp_Best  (∓3, +159) "round 3" · "Best 120" 한 줄
│  │  ├ Btn_DownloadGif 144x22 (0, -33)  버튼_파랑 + 라벨 + 아이콘(Shadow)
│  │  ├ Group_GreenTags                  GREEN 34x7 짜리 7장, 창 아래쪽에 흩뿌림
│  │  ├ Tmp_Challenge  200x24 (0, -59)   "Challenge with Friend!"
│  │  ├ Area_Share      88x38 (0, -98)   ← DOM 버튼이 겹칠 자리
│  │  │   ├ Btn_Kakao  38x38 (-25, 0)    아이콘_카톡
│  │  │   └ Btn_Share  38x38 (+25, 0)    아이콘_공유
│  │  └ Btn_Replay    104x31 (0, -144)   버튼_초록 + "RETRY?" (Shadow)
│  ├ Group_Stickers                      창 밖 별 2장 — 배경용 별
│  │   Img_StarBottomLeft (-102,-77) 65   Img_StarTopRight (106,193) 60
│  └ Group_Ranking                       왼쪽 위 트로피 창
│      ├ Btn_Ranking  80x105 (-92, +167) 창2 + UIStopMotionDeco
│      │   ├ Img_Trophy 52x68            랭킹
│      │   └ Tmp_Label  76x18            "RANKING"
│      └ Img_CursorHandA / HandB / Arrow  스티커1, 전부 UIStopMotionDeco
└ Group_Leaderboard [꺼진 채 저장]        CanvasGroup
   ├ Btn_Backdrop                        화면 가득 투명 — 바깥을 누르면 닫힌다
   └ Img_Board  232.5x386 (-2, +11.2)    ver5-리더보드
       ├ Img_BoardHitArea                판 안쪽 클릭이 backdrop 으로 새지 않게 막는다
       └ Btn_Close                       오른쪽 위 X (아이콘_닫기)
```

제목 자리는 하나다 — 평소에는 `Img_GameOver` 스프라이트가, 신기록이면 그 자리에
`Tmp_NewBest` 가 들어간다. `Present()` 가 둘 중 하나만 켠다.

`Group_Stickers` 는 `Panel_Result` 의 자식이 아니라 **형제**다. 창 밖으로 삐져나와 붙기 때문이고,
계층 순서가 그대로 겹침 순서다. 전부 `Raycast Target` 이 꺼져 있어 아래 버튼을 가리지 않는다.

### 리더보드

`Btn_Ranking` 을 누르면 `Group_ResultContent` 를 감추고 `Group_Leaderboard` 를 띄운다.
**둘의 창 크기가 비슷해서 겹쳐 두면 뒤가 비쳐 지저분하다** — 그래서 결과창 본체를 통째로 감춘다.
닫는 길은 둘이다: 판 바깥(`Btn_Backdrop`) · 오른쪽 위 X(`Btn_Close`).
`Img_BoardHitArea` 는 알파 0 에 `Raycast Target` 만 켜 둔 판이다 —
그림 위를 눌렀을 때 클릭이 뒤 `Btn_Backdrop` 으로 새어 창이 닫히는 것을 막는다.
열고 닫을 때는 `Img_Board` 만 커졌다 작아진다 — 뒤 판까지 같이 커지면 화면이 밀린다.

**아직 순위 데이터는 없다.** `ver5-리더보드.png` 그림 한 장이다.

### 공유 버튼만 브라우저가 클릭을 받는다

`navigator.share` 와 카카오 SDK 는 **사용자 제스처 안에서** 불려야 하는데, Unity 는 클릭을
DOM 이벤트 핸들러가 아니라 다음 프레임에 처리한다. 그래서 웹에서는

- **보이는 그림은 Unity 가 그리고**(`Btn_Kakao` / `Btn_Share`),
- **클릭은 그 위에 겹친 투명한 DOM 버튼**(`WebGLTemplates/PHDMobile/index.html` 의
  `window.PHDShare`)이 받는다.

`ResultScreen` 은 `Area_Share` 가 화면에서 차지하는 자리를 0~1 비율로 알려줄 뿐이다
(`WebShareAnchor` → `WebShare.Place`, 매 프레임 확인하되 실제로 바뀐 프레임에만 호출).
웹이 아니거나 템플릿이 구버전이면 `WebShare.IsAvailable` 이 false 라 아무 일도 하지 않는다.

> **칸 나눔이 양쪽에 중복돼 있다.** Unity 쪽 `Area_Share` 88 = 아이콘 38 + 간격 12 + 아이콘 38 이고,
> `index.html` 의 `#phd-share-layer` 가 같은 비율(`gap: 13.64%` = 12/88, 각 버튼 `43.18%` = 38/88)을
> CSS 로 다시 적는다. **한쪽만 고치면 공유를 눌렀는데 카카오가 뜬다.**
> `PHDShare.debug(true)` 로 히트 영역에 빨간 테두리를 켜서 눈으로 맞출 수 있다.
