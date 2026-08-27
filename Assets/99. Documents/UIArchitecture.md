# Project-PHD UI 구조

리듬·기억력 게임(타이틀 → 캐릭터 선택 → 게임플레이, 3씬)의 UI 체계.
**게임 로직은 이 문서의 어떤 것도 알지 못한다.**

범용 UGUI 작업 규칙은 `UIConventions.md`, 폰트·크기·색 규격은 `UITextStyleGuide.md` 에 있다.
이 문서는 그 위에 얹는 **PHD 고유 구조와 예외**만 담는다.

---

## 1. 레이어

```
UIRoot (씬당 1개, Canvas 에 붙는다)     ← 스크린 찾기 / 팝업 스택 / 암전
      ├── UIScreen                      ← 한 덩어리로 켜고 끄는 화면
      │     ├── GameHud             (Hud)    GameScene
      │     ├── TitleScreen         (Hud)    TitleScene
      │     ├── CharacterSelectScreen (Hud)  CharacterSelectScene
      │     └── ResultScreen        (Popup)  GameScene
      └── 위젯
            ├── UIButton          ← 눌림 연출 + 클릭음
            ├── UIIconStrip       ← 아이콘 줄 풀링
            │     ├── LifeIcons
            │     └── ProgressDots
            ├── CharacterCarousel ← 캐릭터 선택 슬롯 회전
            ├── UIFloatingDeco    ← 장식이 사인 곡선으로 부드럽게 떠다님
            └── UIStopMotionDeco  ← 장식이 정해진 포즈 몇 장을 툭툭 갈아 끼움(스톱모션)
```

보조 도구: `UITween`(페이드·팝), `SafeAreaFitter`(노치), `SceneLoadButton`(씬 이동),
`ScreenFader`(씬 전환 페이드 — 씬을 넘어 살아남아야 해서 "인스펙터 연결" 규칙의 예외로,
`SoundManager` 처럼 처음 쓸 때 스스로 만들어진다).

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

새 정보를 HUD 에 띄우고 싶다면 → `GameHud` 에 메서드를 **추가**하고, 게임 로직 쪽에서 호출한다.
UI 스크립트가 `GameFlow` 를 직접 참조해서 값을 읽어오지 않는다.

### 색은 인스펙터에서 정한다

색 팔레트를 관리하는 별도 레이어(테마 에셋)는 아직 두지 않는다.
쓰는 색이 적어서, 지금은 인스펙터에서 직접 찍는 편이 빠르다.

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
그래서 꺼진 화면 안의 `Update`(스티커 연출 등)는 한 번도 돌지 않는다.

### 참조는 전부 인스펙터 연결

실행 중에 `Find`/`GetComponentInChildren` 으로 화면을 찾지 않는다.
`UIRoot.Screens` 는 **인스펙터에서 직접 채운다.** 화면을 새로 추가했다면 배열에 끌어다 넣는다.
비어 있으면 `UIRoot` 가 실행 시 에러를 남긴다.

> 예전에는 에디터 메뉴가 이 배열을 훑어서 채웠다. 그 메뉴(`UIBuildKit`)는 지금 없다 — 6절 참고.

### 화면마다 Canvas 를 따로 둔다

점수 한 글자가 바뀌었을 때 결과창까지 같이 다시 그리지 않게 하려는 것이다.
겹치는 순서는 계층이 아니라 `UILayer`(Backdrop / Hud / Popup / Overlay)가 정한다.

`UIConventions` 7절은 "HUD / Popup / Tooltip 3층 컨테이너"를 말하는데,
PHD 는 컨테이너 오브젝트 대신 **화면별 중첩 Canvas 의 `sortingOrder`** 로 같은 효과를 낸다.
화면이 적어서 컨테이너를 따로 두면 계층만 깊어진다.
팝업 스택과 입력 차단에는 `UILayer.Popup` 만 참여한다 — 다른 층을 `OpenPopup` 에 넘기면 경고가 뜬다.

`UIScreen.Awake` 는 **중첩 Canvas 만** `overrideSorting` 을 켠다.
루트 `UI Canvas` 의 `sortingOrder`(GameScene 은 100)는 씬이 정하는 값이라 건드리지 않는다.

### 이름은 컴포넌트 종류 접두사 + 역할

`Group_` `Panel_` `Btn_` `Img_` `Tmp_` (UIConventions 1절).
순수 배치용 빈 오브젝트는 접두사 없이 역할명(`SafeArea`, `SafeContent`, `Frame`).
화면 루트는 스크립트 클래스명과 맞춘다 (`Group_ResultScreen` ↔ `ResultScreen.cs`).

---

## 3. 화면 배치 (GameScene)

단위는 아트 픽셀, 원점은 화면 중심(270 × 480)이다. 모든 요소가 한 캔버스 안에 있다.

ver3 아트로 갈아끼우면서 **기기 프레임을 화면 가운데 놓던 구성에서 풀스크린 구성으로 바뀌었다.**
위아래에 프레임 밴드를 붙이고 그 사이를 게임 화면으로 쓴다.

```
UI Canvas                    Screen Space - Camera, sortingOrder 100
├ Group_Stage                PadInput + CanvasGroup, 화면 가득
│  ├ Img_Background          배경 (2)_0, 화면 가득
│  ├ Img_Phone               프레임_아래, 아래 가장자리에 폭 전체 x 157
│  │   └ Group_Pad           (0, +71) 기준 140 x 140 다이아몬드
│  │       Img_PadBase 108 x 108 + 패드 버튼 4개(버튼_0~3)
│  ├ VFX
│  └ Group_QtePrompt         (0, +57) 64 x 64 — Img_James / Img_Key / Img_Ring
├ SafeArea                   화면 가득 (노치 대응)
├ Group_ResultScreen         Popup, 꺼진 채로 저장 — 9절
├ Frame                      화면 가득
│  ├ Img_FrameTop            프레임_위, 위 가장자리에 폭 전체 x 43.4
│  └ SafeContent             화면 가득 (노치 대응), HUD 는 전부 이 안
│      ├ Group_Score         위 중앙 (0, -4)    130 x 44
│      ├ Group_Round         왼쪽 위 (4, 0)     100 x 20
│      ├ Group_Lives         오른쪽 위 (-3, -6)  80 x 16
│      ├ Group_ProgressDots  위 중앙 (0, -50)   120 x 23
│      └ Tmp_Message         중앙 (0, +52.7)    169 x 40
└ Img_Shade                  화면 가득, 팝업 뒤 암전
```

**HUD 는 `SafeContent`(safe area) 안에 둔다.** 위쪽 프레임 밴드에 얹히는 정보라
노치·상태바를 피해야 한다.

> **미해결:** `Img_FrameTop` 높이 43.4, `Tmp_Message` y 52.7 처럼 소수점이 남아 있다.
> 4절의 정수 픽셀 규칙에 어긋난다 — 아트 실측에 맞춰 정수로 정리해야 한다.

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

타이틀은 화면 전체가 투명 버튼이고, 씬 이동은 그 위의 `SceneLoadButton` 이 맡는다.

---

## 4. 좌표계

카메라에 URP 2D 의 `PixelPerfectCamera` 가 붙어 **270 × 480 아트 픽셀, PPU 32** 로 맞춰져 있다.
세 씬의 캔버스는 모두 이렇게 저장돼 있다.

| 항목 | 값 |
|---|---|
| Render Mode | Screen Space - Camera, `planeDistance` 5 |
| UI Scale Mode | Scale With Screen Size |
| Reference Resolution | **270 × 480** |
| Screen Match Mode | Expand (`Match Width Or Height` = 0 → 폭 기준) |
| Reference Pixels Per Unit | 32 |

> **캔버스 1 단위 = 아트 1 픽셀.**

그래서 인스펙터에 적는 위치·크기는 전부 아트 픽셀이다. 소수점을 쓰지 않는다 —
반 픽셀에 놓인 스프라이트는 화면에서 흐려진다.
기준 기기 해상도는 **1080 × 1920** (= 270 × 480 의 정확히 4배)다.

같은 이유로 **연출이 만들어 내는 오프셋도 정수로 끊는다.**
`UIStopMotionDeco` 의 위치 흔들림이 `Mathf.Round` 를 거치는 것이 그 때문이다.
(각도·배율 흔들림은 어차피 리샘플되므로 예외다.)

> **미해결:** `GameLayout.cs` 는 이 좌표계의 기준값 모음으로 만들어졌지만
> **지금 어느 스크립트도 참조하지 않는다.** 값도 은퇴한 ver1/ver2 아트 실측
> (`DeviceWidth` 218, `PopupWidth` 196, `LogoWidth` 183 …)이라 현재 배치와 맞지 않는다.
> 되살려 쓸지(3절 수치를 여기로 옮기고 씬이 참조) 지울지 정해야 한다.
> 그 전까지 `UITextStyleGuide.md` 가 말하는 "상수는 `GameLayout` 에 있다"는 사실과 다르다.

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
Assets/02. Prefabs/00. UI/
  General/    원자 공용        — Tmp_Nabla
  UIContent/  반복 슬롯·행     — Img_Life(하트), Img_Dot(진행 점)
  UIView/     화면 단위        — (비어 있음)
  Setting/    캔버스 루트      — (비어 있음)
```

**글자는 `Tmp_Nabla` 를 복제해서 만든다.** GameObject 메뉴로 새로 만들면
TMP Settings 의 기본 폰트(영문 LiberationSans)가 붙고, 폰트 지정을 매번 잊게 된다.
다만 1순위는 **같은 화면의 형제 텍스트 복제**다 — General 프리팹은 기본값만 들고 있어
역할 정보(크기·색·정렬)가 없다.

> **폰트는 문서와 씬이 어긋나 있다.** 프리팹 이름은 `Tmp_Nabla` 지만 세 씬의 TMP 는
> 전부 `esamanru Medium SDF` 로 덮여 있고, 한글 문구도 이미 화면에 있다(결과창 공유 버튼).
> 어느 쪽이 규격인지는 `UITextStyleGuide.md` 의 "미해결" 항목에서 다룬다.
> 정해지기 전까지 새 텍스트는 **같은 화면의 형제를 따라** 만든다.

---

## 6. 아트 적용

**에디터 셋업 툴은 지금 하나도 없다.** `Assets/Editor/UI/` 는 비어 있고,
예전 `PHD ▸ UI ▸ 0~9` 메뉴를 갖고 있던 `UIBuildKit.cs` 는 삭제됐다.
결과창을 시안대로 찍던 `ResultScreenBuildKit.cs` 도 한 번 돌린 뒤 지웠다 —
**지금 씬 배치는 손으로 유지한다.**

> UIConventions 8절은 "씬을 손으로 고치지 말고 `MenuItem` 셋업 툴로 찍어라"라고 한다.
> 지금은 그 규칙을 지키지 않는 상태다. 같은 배치를 또 만들 일이 생기면
> 툴을 되살리는 편이 낫다(멱등 · Undo · 기존 값 존중 세 가지를 지킬 것).

`Assets/Editor/` 에 남아 있는 것은 UI 와 무관한 `WebGLBuilder.cs` 와
`SoundIdDropdownDrawer.cs` 둘뿐이다.

### 쓰는 스프라이트

현역 아트는 **`Assets/03. Sprites/ver3/`** 다(`00. Icons`, `01. UI`).
구버전은 `Assets/03. Sprites/test/`(ver1, ver2)로 옮겼고 **GUID 가 유지돼** 씬 참조가 살아 있다.

시트마다 임포트 PPU 가 다르다. `SetNativeSize` 를 쓰는 곳만 PPU 에 의존하고,
나머지는 RectTransform 에 크기를 직접 적는다.

| 파일 | PPU | 쓰이는 곳 |
|---|---|---|
| `01. UI/배경 (2).png` | 32 | `Img_Background` — 화면 가득 |
| `01. UI/프레임_위.png` · `프레임_아래.png` | 96 | 위·아래 프레임 밴드 |
| `01. UI/버튼.png` (`버튼_0`~`_3`) | 96 | 패드 버튼 4개, QTE 문양 |
| `01. UI/결과창2.png` (`결과창2_0`) | 100 | 결과창 창틀 |
| `01. UI/스티커1.png` (`_0` CD / `_6` 손 / `_7` 휴지통) | 100 | 결과창 스티커 |
| `01. UI/배경용 별.png` (`배경용 별_0`) | 32 | 결과창 크롬 별 2장 |
| `01. UI/제임스.png` (`제임스_0`) | 32 | 결과창 픽셀 캐릭터 |
| `00. Icons/james_full.png` (`_0` O포즈 / `_1` X포즈) | 100 | 결과창 사진 |
| `00. Icons/james.png` (`james_0`) | 32 | QTE 프롬프트 |
| `ver3/버튼 배경.png` | — | `Img_PadBase` |
| `test/…/Icon_QteRing.png` | — | QTE 링 — ver3 대체가 없어 구버전을 계속 쓴다 |

캐릭터 선택 화면은 `이름UI` · `자물쇠` · `난이도별` · `실루엣_*` 을 함께 쓴다.

---

## 7. UIConventions 대비 예외

| 규칙 | PHD 처리 | 이유 |
|---|---|---|
| 3절 — Reference Resolution 1920×1080 | **270 × 480** 로 고정 (Scale With Screen Size, 폭 기준) | 규칙의 목적("Font Size == 기준 해상도 픽셀")은 그대로 성립한다. 1080×1920 의 정확히 1/4 이라 아트 1픽셀 = 캔버스 1단위가 된다. 렌더 모드는 세 씬 모두 Screen Space - Camera(planeDistance 5) |
| 4절 — Image 는 예외 없이 SetNativeSize | 시트 PPU 가 32/96/100 으로 갈려 있어 **크기를 RectTransform 에 직접 적는다.** `SetNativeSize` 는 QTE 프롬프트 등 일부만 | 한 시트의 PPU 를 바꾸면 그 시트를 쓰는 다른 화면이 같이 틀어진다 |
| 4절 — Scale 항상 1 | 정지 상태는 항상 1. 누름·팝·스톱모션 연출만 일시적으로 스케일을 건드리고 1로 복귀 | 인스펙터에 남는 값이 아니라 추적 문제를 만들지 않는다 |
| 7절 — HUD/Popup/Tooltip 3층 컨테이너 | 화면별 중첩 Canvas + `UILayer` sortingOrder | 화면이 적어 컨테이너를 두면 계층만 깊어진다 |
| 8절 — 셋업은 에디터 툴로 | **지키지 않는 중** (6절) | — |

---

## 8. 아직 하지 않은 것

- **`GameLayout.cs` 가 미참조 · 값이 은퇴한 아트 기준이다.** 되살릴지 지울지 결정 (4절).
- **소수점 좌표가 남아 있다.** `Img_FrameTop` 43.4, `Tmp_Message` 52.7 (3절).
- **폰트 규격이 문서와 씬 사이에서 갈려 있다.** `UITextStyleGuide.md` 의 미해결 항목 (5절).
- **에디터 셋업 툴이 없다.** UIConventions 8절 미준수 (6절).
- **카카오 아이콘이 임시다.** 결과창 `Btn_Kakao` 가 공식 이미지가 없어 노란 네모로 서 있다 (9절).

~~**함정 문양(`GameFlow.trapSprites`)**~~ 해결됨 — 함정 기능 자체가 제거됐다.

~~**수치 표기.**~~ 해결됨 — `GameHud` 가 10 미만이면 `SetText("0{0}", n)` 으로 두 자리를 채운다.

---

## 9. 결과창

**결과 카드는 통째로 Unity UI 가 그린다**(`ResultScreen` + `Group_ResultScreen`).
웹이든 에디터든 같은 화면이고, 씬에 `ResultScreen` 이 없으면 `ResultShare` 가
결과창 없이 진행하므로 아무것도 깨지지 않는다.

```
Group_ResultScreen           Canvas(Popup) + GraphicRaycaster + CanvasGroup + ResultScreen
├ Panel_Result   208 x 376   (0, +6)   결과창2_0. 뒤쪽 클릭을 막는 판이기도 하다
│  ├ Tmp_Heading             (0, +154) GAME OVER / NEW BEST!
│  ├ Img_Pose     68 x 144   (0, +58)  james_full_1 (X 포즈)
│  ├ Tmp_Score              (0, -40)
│  ├ Tmp_Round / Tmp_Best   (∓3, -63)  "round 3" · "Best 120" 한 줄
│  ├ Area_Share  152 x 37    (0, -95)  ← DOM 버튼이 겹칠 자리
│  │   ├ Btn_Kakao  32 x 32  (-60, 0)  아이콘만
│  │   └ Btn_Share 116 x 37  (+18, 0)  초록 바 + "친구에게 알리기"
│  └ Btn_Replay  120 x 30    (0, -126) "RETRY?" (판은 투명, 글자만 보인다)
└ Group_Stickers             창 위에 겹치는 장식 7장, 전부 UIStopMotionDeco
   Img_StarBottomLeft · Img_James · Img_StarTopRight · Img_PoseTopRight
   Img_StickerCd · Img_StickerTrash · Img_StickerHand
```

스티커는 `Panel_Result` 의 자식이 아니라 **형제**다. 창 밖으로 삐져나와 붙기 때문이고,
배열 순서가 그대로 겹침 순서다(별 위에 사람, 창 위에 스티커).
전부 `Raycast Target` 이 꺼져 있어 아래 버튼을 가리지 않는다.

### 공유 버튼만 브라우저가 클릭을 받는다

`navigator.share` 와 카카오 SDK 는 **사용자 제스처 안에서** 불려야 하는데, Unity 는 클릭을
DOM 이벤트 핸들러가 아니라 다음 프레임에 처리한다. 그래서 웹에서는

- **보이는 그림은 Unity 가 그리고**(`Btn_Kakao` / `Btn_Share`),
- **클릭은 그 위에 겹친 투명한 DOM 버튼**(`WebGLTemplates/PHDMobile/index.html` 의
  `window.PHDShare`)이 받는다.

`ResultScreen` 은 `Area_Share` 가 화면에서 차지하는 자리를 0~1 비율로 알려줄 뿐이다
(`WebShare.Place`, 매 프레임 확인하되 실제로 바뀐 프레임에만 호출).
웹이 아니거나 템플릿이 구버전이면 `WebShare.IsAvailable` 이 false 라 아무 일도 하지 않는다.

> **칸 나눔이 양쪽에 중복돼 있다.** Unity 쪽 `Area_Share` 152 = 아이콘 32 + 간격 4 + 바 116 이고,
> `index.html` 의 `#phd-kakao` / `#phd-share` 가 같은 비율(21.05% / 2.63% / 나머지)을 CSS 로
> 다시 적는다. **한쪽만 고치면 초록 바를 눌렀는데 카카오가 뜬다.**
> `PHDShare.debug(true)` 로 히트 영역에 빨간 테두리를 켜서 눈으로 맞출 수 있다.
