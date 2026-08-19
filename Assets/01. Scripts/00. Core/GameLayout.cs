
/// <summary>
/// 화면 배치 기준값 모음. <b>픽셀 규격이 확정되면 이 파일만 고치면 된다.</b>
///
/// 단위 규칙
///  - 모든 수치는 <b>아트 픽셀</b> 기준(= PixelPerfectCamera 의 Reference Resolution 좌표계)
///  - 월드 유닛으로 쓸 때는 <see cref="ToUnits"/> 로 변환 (유닛 = 아트픽셀 / PPU)
///  - UI(Constant Pixel Size + scaleFactor=pixelRatio)는 아트 픽셀을 그대로 쓴다
///
/// ※ 현재 값은 임시다. 실제 아트 규격이 나오면 <see cref="PixelsPerUnit"/> 와
///   레퍼런스 해상도, 패드 크기를 함께 조정한다.
/// </summary>
public static class GameLayout
{
    // ---- 픽셀 기준(임시) ----
    public const int PixelsPerUnit = 32;
    public const int RefWidth = 270;    // 1080 / 4
    public const int RefHeight = 480;   // 1920 / 4

    // ---- 입력 패드 ----
    public const float PadCell = 100f;          // 버튼 한 변
    public const float PadGap = 10f;            // 버튼 사이 간격
    public const float PadBottomMargin = 18f;   // 화면(safe area) 하단에서 패드까지

    // ---- 상단 HUD ----
    public const float TopBarHeight = 40f;
    public const float TopGap = 8f;

    // ---- 진행 표시 점 ----
    public const float DotsHeight = 10f;
    public const float DotsGap = 9f;            // 패드 위쪽 여백

    // ---- 중앙 문양 ----
    public const float StageIconSize = 120f;

    // ---- UI 프레임(중앙 컬럼) ----
    public const float FrameWidth = 228f;
    public const float FrameMarginY = 15f;

    // ---- 파생값 ----

    /// <summary>패드 전체 한 변(2x2).</summary>
    public const float PadSize = PadCell * 2f + PadGap;

    /// <summary>패드 중심에서 각 버튼 중심까지의 거리.</summary>
    public const float PadButtonOffset = (PadCell + PadGap) * 0.5f;

    /// <summary>화면 하단 기준 패드 중심 높이.</summary>
    public const float PadAnchorY = PadBottomMargin + PadSize * 0.5f;

    /// <summary>진행 표시 점의 하단 기준 높이.</summary>
    public const float DotsY = PadBottomMargin + PadSize + DotsGap;

    /// <summary>
    /// 상단바 아래 ~ 패드 위 사이 공간의 중심(화면 중앙 기준 오프셋).
    /// 위/아래 각각 자기 화면 끝에서 앵커링되므로 <b>화면 길이가 변해도 이 값은 그대로</b>다.
    /// </summary>
    public const float StageCenterY = ((PadBottomMargin + PadSize) - (TopBarHeight + TopGap)) * 0.5f;

    /// <summary>아트 픽셀 → 월드 유닛.</summary>
    public static float ToUnits(float pixels) => pixels / PixelsPerUnit;
}
