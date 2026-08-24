
/// <summary>
/// 화면 배치 기준값 모음. 모든 수치는 아트 픽셀(270×480) 기준이고,
/// 기기 프레임 관련 값은 Device_Frame 스프라이트 실측이다 — 규격이 바뀌면 이 파일만 고친다.
/// </summary>
public static class GameLayout
{
    // ---- 픽셀 기준(임시) ----
    public const int PixelsPerUnit = 32;
    public const int RefWidth = 270;    // 1080 / 4
    public const int RefHeight = 480;   // 1920 / 4

    // ---- 기기 프레임 (Device_Frame 스프라이트 실측) ----
    // 프레임은 화면 중앙에 원본 크기로 놓는다. 좌우 26, 상하 47 여백이 남는다.

    public const float DeviceWidth = 218f;
    public const float DeviceHeight = 386f;

    public const float DeviceScreenWidth = 169f;
    public const float DeviceScreenHeight = 203f;

    public const float DeviceScreenCenterY = 55.5f;   // 화면판 중심이 화면 중심에서 위로 이만큼

    public const float PadWheelCenterY = -117f;       // 하단 원형 휠(패드 다이아몬드) 중심, 프레임 중심에서 아래로

    // ---- 입력 패드 ----

    public const float PadSize = 29f;

    public const float PadOffset = 31f;

    // ---- 화면판 안 HUD ----
    // 배치 예상도: score(라벨+값)는 위쪽 중앙, round 는 아래쪽 중앙, 하트는 그 밑.
    public const float TopBarHeight = 48f;
    public const float HudTopMargin = 8f;
    public const float HudRowGap = 4f;

    // ---- 진행 표시 점 ----
    public const float DotsHeight = 10f;

    // ---- 목숨 ----
    // 배치 예상도 기준. 하트는 화면판 <b>아래쪽</b>에 붙는다.
    public const float LifeIconSize = 14f;      // Icon_Heart 원본 (14x13)
    public const float LifeBottomMargin = 12f;  // 화면판 아래 가장자리에서 하트까지
    public const float LifeGap = 5f;

    // ---- 중앙 문양 ----
    // 위로는 score 블록, 아래로는 진행 점을 피해 들어가는 크기.
    public const float StageIconSize = 64f;

    // ---- 타이틀 화면 ----
    public const float LogoWidth = 183f;        // Logo_* 원본 크기
    public const float LogoHeight = 192f;
    public const float LogoCenterY = 0f;        // 배치 예상도: 화면 중앙
    public const float TaglineGap = 8f;         // 로고 위 한 줄 소개까지
    public const float HintBottom = 50f;        // 화면 아래에서 "Touch to Start" 까지
    public const float HintArrowGap = 10f;      // 문구와 양옆 화살표 사이
    public const float TitleGap = 12f;          // 로고 아래 기록 표시까지

    // ---- 버튼 ----
    public const float ButtonWidth = 76f;       // Btn_Base_* 스프라이트 원본 크기
    public const float ButtonHeight = 27f;
    public const float ButtonGap = 8f;

    // ---- 팝업 ----
    // 제목 40(Nabla 24 한 줄) + 항목 3줄(24) + 배지 24 + 버튼 27 + 사이 여백.
    public const float PopupWidth = 196f;
    public const float PopupHeight = 216f;
    public const float PopupPadding = 12f;
    public const float PopupRowHeight = 24f;
    public const float PopupRowGap = 2f;

    // ---- 텍스트 ----
    // 폰트는 Nabla 한 종이다. 캡 0.93em / 행 1.64em 으로 크고 넓다. 영문 전용.
    // 픽셀 폰트가 아니라 크기에 배수 제약이 없다 — 정수이기만 하면 된다.
    // 자세한 근거는 UITextStyleGuide.md.

    public const float TextBase = 14f;

    public const float TextDisplay = 24f;

    public const float HeadingHeight = 40f;

    // ---- 파생값 ----

    public const float DeviceScreenTop = DeviceScreenCenterY + DeviceScreenHeight * 0.5f;

    public const float DeviceScreenBottom = DeviceScreenCenterY - DeviceScreenHeight * 0.5f;

    public const float PadClusterSize = PadOffset * 2f + PadSize;

    public static float ToUnits(float pixels) => pixels / PixelsPerUnit;
}
