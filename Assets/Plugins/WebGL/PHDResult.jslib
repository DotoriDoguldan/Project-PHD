// 게임오버 결과창(공유 시트) 브리지.
// 실제 UI 는 WebGL 템플릿(index.html)의 window.PHDResult 가 갖고 있고,
// 여기서는 C# <-> JS 호출만 연결한다.
//
// 페이지에 오버레이가 없거나(구버전 템플릿, 다른 호스트에 임베드) 예외가 나면
// 게임이 결과창에서 멈추지 않도록 항상 "닫힘(2)"을 돌려준다.
mergeInto(LibraryManager.library, {

  PHDResultSupported: function () {
    return (typeof window !== "undefined" && window.PHDResult) ? 1 : 0;
  },

  PHDResultShow: function (jsonPtr) {
    if (typeof window === "undefined" || !window.PHDResult) return;
    try {
      window.PHDResult.show(JSON.parse(UTF8ToString(jsonPtr)));
    } catch (e) {
      console.warn("[PHD] 결과창을 열지 못했습니다:", e);
    }
  },

  // 0 = 아직 선택 안 함, 1 = 다시하기, 2 = 닫힘
  PHDResultTakeAction: function () {
    if (typeof window === "undefined" || !window.PHDResult) return 2;
    try {
      return window.PHDResult.takeAction();
    } catch (e) {
      console.warn("[PHD] 결과창 상태를 읽지 못했습니다:", e);
      return 2;
    }
  },

  PHDResultHide: function () {
    if (typeof window === "undefined" || !window.PHDResult) return;
    try {
      window.PHDResult.hide();
    } catch (e) { /* 정리 과정이라 무시 */ }
  }
});
