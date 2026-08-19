using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using PHD.Core;
using PHD.Game;
using PHD.UI;

namespace PHD.EditorTools
{
    /// <summary>
    /// GameScene 의 기본 배치를 코드로 생성한다.
    ///
    /// 구성 원칙
    ///  - 게임플레이(패드 버튼, 중앙 문양)는 <b>월드</b> 스프라이트 → 2D 라이트/파티클/셰이더 연출 가능
    ///  - HUD·문구는 <b>UI</b> (Screen Space - Camera) → 텍스트 선명도와 앵커링 유지
    ///  - 픽셀아트 기준: PixelPerfectCamera 가 카메라를, PixelPerfectUIScaler 가 UI 를 같은 정수 배율로 맞춘다
    ///
    /// 수치는 전부 <see cref="GameLayout"/>(아트 픽셀 단위)에서 가져온다.
    /// 메뉴(Tools/PHD/GameScene 배치) 또는 Unity CLI -executeMethod 로 실행. 재실행하면 새로 만든다.
    /// </summary>
    public static class GameSceneUIBuilder
    {
        const string ScenePath = "Assets/00. Scenes/GameScene.unity";
        const string SpriteSheetPath = "Assets/03. Sprites/playstation-buttons.jpg";
        const string FontPath = "Assets/05. Arts/Fonts/TerrarumSans/TerrarumSansBitmap SDF.asset";

        // ---- 정렬 순서 ----
        const int OrderPad = 10;
        const int OrderStageIcon = 20;
        const int OrderCanvas = 100;

        // ---- 폰트 크기(아트 픽셀) ----
        const float FontHudKey = 8f;
        const float FontHudValue = 18f;
        const float FontMessage = 18f;

        // ---- 색상 ----
        static readonly Color ColBackground = new Color32(0x0D, 0x0F, 0x1A, 0xFF);
        static readonly Color ColPanel = new Color(1f, 1f, 1f, 0.06f);
        static readonly Color ColText = new Color32(0xEE, 0xF1, 0xFF, 0xFF);
        static readonly Color ColDim = new Color32(0x9A, 0xA3, 0xC7, 0xFF);

        /// <summary>패드 버튼 스프라이트(좌상 → 우상 → 좌하 → 우하).</summary>
        static readonly string[] ButtonSpriteNames = { "세모", "동글", "엑스", "네모" };

        [MenuItem("Tools/PHD/GameScene 배치")]
        public static void BuildMenu()
        {
            var active = SceneManager.GetActiveScene();
            if (active.path != ScenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                active = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            Build();
            EditorSceneManager.MarkSceneDirty(active);
            EditorSceneManager.SaveScene(active);
            Debug.Log("[PHD] GameScene 배치 완료");
        }

        /// <summary>Unity CLI: -executeMethod PHD.EditorTools.GameSceneUIBuilder.BuildFromCLI</summary>
        public static void BuildFromCLI()
        {
            try
            {
                var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                Build();
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                AssetDatabase.SaveAssets();
                Debug.Log("[PHD] GameScene 배치 완료 (CLI)");
                EditorApplication.Exit(0);
            }
            catch (System.Exception e)
            {
                Debug.LogError("[PHD] 배치 실패: " + e);
                EditorApplication.Exit(1);
            }
        }

        static void Build()
        {
            var sprites = LoadButtonSprites();
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            var uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

            // 재실행 대비 정리
            DestroyIfExists("Game");
            DestroyIfExists("Game World");
            DestroyIfExists("UI Canvas");

            var camera = EnsureCamera();
            EnsureEventSystem();

            var world = BuildWorld(sprites);
            var ui = BuildUI(camera, uiSprite, font);
            WireGame(world, ui);
        }

        // ---------------------------------------------------------------- 월드

        struct WorldRefs
        {
            public PadButton[] Pads;
            public PadInput Input;
            public StageIcon Stage;
        }

        struct UiRefs
        {
            public GameObject Canvas;
            public CanvasScaler Scaler;
            public TextMeshProUGUI Round;
            public TextMeshProUGUI Score;
            public TextMeshProUGUI Message;
            public ProgressDots Dots;
        }

        static WorldRefs BuildWorld(List<Sprite> sprites)
        {
            var world = new GameObject("Game World").transform;
            var padInput = world.gameObject.AddComponent<PadInput>();
            var padButtons = new PadButton[ButtonSpriteNames.Length];

            // --- 입력 패드(2x2) ---
            var pad = new GameObject("Pad").transform;
            pad.SetParent(world, false);
            SetAnchor(pad.gameObject.AddComponent<ScreenAnchor>(),
                ScreenEdge.Bottom, new Vector2(0f, GameLayout.ToUnits(GameLayout.PadAnchorY)));

            float cellUnits = GameLayout.ToUnits(GameLayout.PadCell);
            float offsetUnits = GameLayout.ToUnits(GameLayout.PadButtonOffset);

            for (int i = 0; i < ButtonSpriteNames.Length; i++)
            {
                string spriteName = ButtonSpriteNames[i];
                var sprite = sprites.FirstOrDefault(s => s != null && s.name == spriteName);

                var go = new GameObject($"Pad Button {i} ({spriteName})");
                go.transform.SetParent(pad, false);
                go.transform.localPosition = new Vector3(
                    (i % 2 == 0) ? -offsetUnits : offsetUnits,
                    (i < 2) ? offsetUnits : -offsetUnits,
                    0f);

                var renderer = go.AddComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.sortingOrder = OrderPad;

                // 임시 스프라이트라 임포트 PPU 가 제각각일 수 있어, 목표 크기에 맞춰 스케일을 맞춘다.
                // 실제 픽셀아트가 들어오면 PPU 만 맞추면 되고 이 스케일은 1 이 된다.
                Vector2 native = NativeSize(sprite);
                float scale = native.x > 0f ? cellUnits / native.x : 1f;
                go.transform.localScale = Vector3.one * scale;

                var collider = go.AddComponent<BoxCollider2D>();
                collider.size = native;   // 로컬 크기 → 스케일 적용 후 실제 버튼 크기와 일치

                var button = go.AddComponent<PadButton>();
                button.SetIndex(i);
                padButtons[i] = button;
            }

            // --- 중앙 문양(시퀀스 재생 위치) ---
            var stageIcon = new GameObject("Stage Icon");
            stageIcon.transform.SetParent(world, false);

            var stageSprite = sprites.FirstOrDefault();
            var stageRenderer = stageIcon.AddComponent<SpriteRenderer>();
            stageRenderer.sprite = stageSprite;
            stageRenderer.sortingOrder = OrderStageIcon;

            Vector2 stageNative = NativeSize(stageSprite);
            float stageScale = stageNative.x > 0f
                ? GameLayout.ToUnits(GameLayout.StageIconSize) / stageNative.x
                : 1f;
            stageIcon.transform.localScale = Vector3.one * stageScale;

            SetAnchor(stageIcon.AddComponent<ScreenAnchor>(),
                ScreenEdge.Center, new Vector2(0f, GameLayout.ToUnits(GameLayout.StageCenterY)));

            var stage = stageIcon.AddComponent<StageIcon>();

            // --- 연출용 빈 루트(파티클/2D 라이트를 여기에 붙인다) ---
            var vfx = new GameObject("VFX").transform;
            vfx.SetParent(world, false);

            return new WorldRefs { Pads = padButtons, Input = padInput, Stage = stage };
        }

        // ---------------------------------------------------------------- UI

        static UiRefs BuildUI(Camera camera, Sprite uiSprite, TMP_FontAsset font)
        {
            var canvasGo = new GameObject("UI Canvas", typeof(RectTransform));
            canvasGo.layer = LayerMask.NameToLayer("UI");

            var canvas = canvasGo.AddComponent<Canvas>();
            // Screen Space - Camera: 월드 연출(파티클 등)과 같은 카메라를 공유해 앞뒤로 끼워넣을 수 있다.
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 5f;
            canvas.sortingOrder = OrderCanvas;

            // Constant Pixel Size + scaleFactor = pixelRatio.
            // scaleFactor 는 PixelPerfectUIScaler 가 런타임에 카메라 배율과 동기화한다.
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;
            scaler.referencePixelsPerUnit = GameLayout.PixelsPerUnit;

            canvasGo.AddComponent<GraphicRaycaster>();

            var safeArea = NewRect("Safe Area", canvasGo.transform);
            Stretch(safeArea);
            safeArea.gameObject.AddComponent<SafeAreaFitter>();

            var frame = NewRect("Frame", safeArea);
            frame.anchorMin = new Vector2(0.5f, 0f);
            frame.anchorMax = new Vector2(0.5f, 1f);
            frame.pivot = new Vector2(0.5f, 0.5f);
            frame.sizeDelta = new Vector2(GameLayout.FrameWidth, -GameLayout.FrameMarginY * 2f);
            frame.anchoredPosition = Vector2.zero;

            // --- 상단 HUD ---
            var topBar = NewRect("Top Bar", frame);
            AnchorTop(topBar, 0f, GameLayout.TopBarHeight);
            var topLayout = topBar.gameObject.AddComponent<HorizontalLayoutGroup>();
            topLayout.spacing = 6f;
            topLayout.childControlWidth = true;
            topLayout.childControlHeight = true;
            topLayout.childForceExpandWidth = true;
            topLayout.childForceExpandHeight = true;

            var roundValue = CreateHudStat(topBar, "Round", "ROUND", "1", uiSprite, font);
            var scoreValue = CreateHudStat(topBar, "Score", "SCORE", "0", uiSprite, font);

            // --- 중앙 안내 문구(월드 Stage Icon 과 같은 높이) ---
            var message = NewText("Message", frame, "READY", FontMessage, ColText, font, FontStyles.Bold);
            var messageRt = message.rectTransform;
            messageRt.anchorMin = new Vector2(0f, 0.5f);
            messageRt.anchorMax = new Vector2(1f, 0.5f);
            messageRt.pivot = new Vector2(0.5f, 0.5f);
            messageRt.sizeDelta = new Vector2(0f, 36f);
            messageRt.anchoredPosition = new Vector2(0f, GameLayout.StageCenterY);

            // --- 입력 진행 표시(패드 바로 위) ---
            var dots = NewRect("Progress Dots", frame);
            AnchorBottomStretch(dots, GameLayout.DotsY, GameLayout.DotsHeight);
            var dotsLayout = dots.gameObject.AddComponent<HorizontalLayoutGroup>();
            dotsLayout.spacing = 4f;
            dotsLayout.childAlignment = TextAnchor.MiddleCenter;
            dotsLayout.childControlWidth = false;
            dotsLayout.childControlHeight = false;
            dotsLayout.childForceExpandWidth = false;
            dotsLayout.childForceExpandHeight = false;

            var progressDots = dots.gameObject.AddComponent<ProgressDots>();

            Selection.activeGameObject = canvasGo;

            return new UiRefs
            {
                Canvas = canvasGo,
                Scaler = scaler,
                Round = roundValue,
                Score = scoreValue,
                Message = message,
                Dots = progressDots
            };
        }

        static TextMeshProUGUI CreateHudStat(RectTransform parent, string name, string key, string value,
            Sprite uiSprite, TMP_FontAsset font)
        {
            var panel = NewImage(name, parent, uiSprite, ColPanel);
            panel.type = Image.Type.Sliced;
            panel.raycastTarget = false;

            var keyText = NewText("Key", panel.rectTransform, key, FontHudKey, ColDim, font);
            keyText.rectTransform.anchorMin = new Vector2(0f, 0.52f);
            keyText.rectTransform.anchorMax = new Vector2(1f, 1f);
            keyText.rectTransform.offsetMin = Vector2.zero;
            keyText.rectTransform.offsetMax = Vector2.zero;

            var valueText = NewText("Value", panel.rectTransform, value, FontHudValue, ColText, font, FontStyles.Bold);
            valueText.rectTransform.anchorMin = new Vector2(0f, 0f);
            valueText.rectTransform.anchorMax = new Vector2(1f, 0.55f);
            valueText.rectTransform.offsetMin = Vector2.zero;
            valueText.rectTransform.offsetMax = Vector2.zero;

            return valueText;
        }

        // ---------------------------------------------------------------- 게임 로직 연결

        static void WireGame(WorldRefs world, UiRefs ui)
        {
            // HUD 는 UI 캔버스에, 게임 루프는 별도 루트에 둔다.
            var hud = ui.Canvas.AddComponent<GameHud>();
            var hudSo = new SerializedObject(hud);
            hudSo.FindProperty("roundText").objectReferenceValue = ui.Round;
            hudSo.FindProperty("scoreText").objectReferenceValue = ui.Score;
            hudSo.FindProperty("messageText").objectReferenceValue = ui.Message;
            hudSo.FindProperty("dots").objectReferenceValue = ui.Dots;
            hudSo.ApplyModifiedPropertiesWithoutUndo();

            // 픽셀 배율 동기화 대상(캔버스)을 명시적으로 연결한다.
            // 런타임 자동 탐색에도 폴백이 있지만, 씬에 박아두는 편이 확실하다.
            var uiScaler = Object.FindFirstObjectByType<PixelPerfectUIScaler>();
            if (uiScaler != null && ui.Scaler != null)
            {
                var scalerSo = new SerializedObject(uiScaler);
                var targetsProp = scalerSo.FindProperty("targets");
                targetsProp.arraySize = 1;
                targetsProp.GetArrayElementAtIndex(0).objectReferenceValue = ui.Scaler;
                scalerSo.ApplyModifiedPropertiesWithoutUndo();
            }

            var gameGo = new GameObject("Game");
            var flow = gameGo.AddComponent<GameFlow>();

            var flowSo = new SerializedObject(flow);
            var padsProp = flowSo.FindProperty("pads");
            padsProp.arraySize = world.Pads.Length;
            for (int i = 0; i < world.Pads.Length; i++)
            {
                padsProp.GetArrayElementAtIndex(i).objectReferenceValue = world.Pads[i];
            }
            flowSo.FindProperty("padInput").objectReferenceValue = world.Input;
            flowSo.FindProperty("stageIcon").objectReferenceValue = world.Stage;
            flowSo.FindProperty("hud").objectReferenceValue = hud;
            flowSo.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------------------------------------------------------------- 씬 공통

        static Camera EnsureCamera()
        {
            var cam = Object.FindFirstObjectByType<Camera>();
            if (cam == null)
            {
                var go = new GameObject("Main Camera");
                cam = go.AddComponent<Camera>();
            }

            cam.gameObject.tag = "MainCamera";
            cam.transform.position = new Vector3(0f, 0f, -10f);
            cam.transform.rotation = Quaternion.identity;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = ColBackground;
            cam.orthographic = true;
            cam.orthographicSize = GameLayout.RefHeight / 2f / GameLayout.PixelsPerUnit;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 100f;

            // --- 픽셀 퍼펙트 ---
            var pixelPerfect = cam.GetComponent<PixelPerfectCamera>();
            if (pixelPerfect == null) pixelPerfect = cam.gameObject.AddComponent<PixelPerfectCamera>();
            pixelPerfect.assetsPPU = GameLayout.PixelsPerUnit;
            pixelPerfect.refResolutionX = GameLayout.RefWidth;
            pixelPerfect.refResolutionY = GameLayout.RefHeight;
            // None: 화면이 길면 그만큼 더 보여준다(세로 가변 레이아웃이라 잘라내면 안 된다).
            pixelPerfect.cropFrame = PixelPerfectCamera.CropFrame.None;
            // PixelSnapping: 스프라이트 위치만 격자에 맞춘다. 회전·스케일 연출이 살아있다.
            pixelPerfect.gridSnapping = PixelPerfectCamera.GridSnapping.PixelSnapping;

            // UI 배율을 카메라 배율(pixelRatio)과 동기화
            if (cam.GetComponent<PixelPerfectUIScaler>() == null)
            {
                cam.gameObject.AddComponent<PixelPerfectUIScaler>();
            }

            // 픽셀 퍼펙트를 끄고 쓸 때만 CameraFitter 가 필요하다(있으면 스스로 물러난다).
            var fitter = cam.GetComponent<CameraFitter>();
            if (fitter != null) Object.DestroyImmediate(fitter);

            return cam;
        }

        static void EnsureEventSystem()
        {
            var es = Object.FindFirstObjectByType<EventSystem>();
            if (es == null)
            {
                var go = new GameObject("EventSystem");
                es = go.AddComponent<EventSystem>();
            }
            // 프로젝트가 새 Input System 전용이므로 전용 모듈이 필요하다.
            if (es.GetComponent<InputSystemUIInputModule>() == null)
            {
                var legacy = es.GetComponent<StandaloneInputModule>();
                if (legacy != null) Object.DestroyImmediate(legacy);
                es.gameObject.AddComponent<InputSystemUIInputModule>();
            }
        }

        static List<Sprite> LoadButtonSprites()
        {
            var list = AssetDatabase.LoadAllAssetRepresentationsAtPath(SpriteSheetPath)
                .OfType<Sprite>()
                .ToList();

            if (list.Count == 0)
            {
                Debug.LogWarning($"[PHD] 스프라이트를 찾지 못했습니다: {SpriteSheetPath} " +
                                 "(Sprite Mode = Multiple 로 슬라이스되어 있는지 확인)");
            }
            return list;
        }

        /// <summary>스프라이트의 월드 크기(유닛). 임포트 PPU 가 반영된 값.</summary>
        static Vector2 NativeSize(Sprite sprite)
        {
            if (sprite == null) return Vector2.one;
            return sprite.bounds.size;
        }

        static void DestroyIfExists(string name)
        {
            var go = GameObject.Find(name);
            if (go != null) Object.DestroyImmediate(go);
        }

        static void SetAnchor(ScreenAnchor anchor, ScreenEdge edge, Vector2 offset)
        {
            var so = new SerializedObject(anchor);
            so.FindProperty("edge").enumValueIndex = (int)edge;
            so.FindProperty("offset").vector2Value = offset;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------------------------------------------------------------- 유틸

        static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        static Image NewImage(string name, Transform parent, Sprite sprite, Color color)
        {
            var rt = NewRect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            if (sprite != null && sprite.border != Vector4.zero) img.type = Image.Type.Sliced;
            return img;
        }

        static TextMeshProUGUI NewText(string name, Transform parent, string text, float size,
            Color color, TMP_FontAsset font, FontStyles style = FontStyles.Normal)
        {
            var rt = NewRect(name, parent);
            var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) tmp.font = font;
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.fontStyle = style;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
            return tmp;
        }

        static void Stretch(RectTransform rt, float left = 0f, float right = 0f, float top = 0f, float bottom = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
        }

        static void AnchorTop(RectTransform rt, float y, float height)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.sizeDelta = new Vector2(0f, height);
            rt.anchoredPosition = new Vector2(0f, -y);
        }

        static void AnchorBottomStretch(RectTransform rt, float y, float height)
        {
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.sizeDelta = new Vector2(0f, height);
            rt.anchoredPosition = new Vector2(0f, y);
        }
    }
}
