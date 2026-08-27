// 결과창 공유 버튼(카카오톡 공유 / 공유하기) 브리지.
//
// 결과 카드는 Unity UI(ResultScreen)가 그리고, 이 두 버튼만 브라우저 DOM 버튼으로 캔버스 위에
// 겹쳐 놓는다. navigator.share 와 카카오 SDK 가 사용자 제스처 안에서 불려야 하기 때문이다 —
// Unity 는 클릭을 DOM 이벤트 핸들러가 아니라 다음 프레임에서 처리해서, Unity 버튼을 거치면
// iOS 사파리에서 공유가 막힌다.
//
// 실제 버튼과 공유 동작은 WebGL 템플릿(index.html)의 window.PHDShare 가 갖고,
// 여기서는 C# <-> JS 호출만 연결한다. 오버레이가 없거나(구버전 템플릿, 다른 호스트에 임베드)
// 예외가 나도 게임은 그대로 진행돼야 하므로 전부 조용히 넘어간다.
mergeInto(LibraryManager.library, {

  PHDShareSupported: function () {
    return (typeof window !== "undefined" && window.PHDShare) ? 1 : 0;
  },

  PHDShareShow: function (jsonPtr) {
    if (typeof window === "undefined" || !window.PHDShare) return;
    try {
      window.PHDShare.show(JSON.parse(UTF8ToString(jsonPtr)));
    } catch (e) {
      console.warn("[PHD] 공유 버튼을 띄우지 못했습니다:", e);
    }
  },

  // 버튼이 놓일 자리. 화면 대비 0~1 비율이고 좌상단이 (0,0) 이다.
  // 픽셀 환산은 캔버스의 실제 CSS 크기를 아는 JS 쪽에서 한다.
  PHDSharePlace: function (x, y, w, h) {
    if (typeof window === "undefined" || !window.PHDShare) return;
    try {
      window.PHDShare.place(x, y, w, h);
    } catch (e) { /* 위치만 못 맞춘 것이라 무시 */ }
  },

  PHDShareHide: function () {
    if (typeof window === "undefined" || !window.PHDShare) return;
    try {
      window.PHDShare.hide();
    } catch (e) { /* 정리 과정이라 무시 */ }
  }
});
