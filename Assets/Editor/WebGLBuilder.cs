using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// WebGL 빌드 자동화. 웹 미니게임으로 배포할 때 필요한 플레이어 설정을 함께 적용한다.
///
/// CLI:
///   Unity.exe -batchmode -nographics -projectPath . -buildTarget WebGL
///             -executeMethod WebGLBuilder.BuildFromCLI -logFile -
/// </summary>
public static class WebGLBuilder
{
    const string OutputPath = "Build/WebGL";
    const string TemplateName = "PROJECT:PHDMobile";
    const string GameScenePath = "Assets/00. Scenes/GameScene.unity";

    [MenuItem("Tools/PHD/WebGL 빌드")]
    public static void BuildMenu() => Build(false);

    public static void BuildFromCLI()
    {
        bool ok = Build(true);
        EditorApplication.Exit(ok ? 0 : 1);
    }

    static bool Build(bool cli)
    {
        ApplyWebSettings();

        var scenes = ResolveScenes();
        if (scenes.Length == 0)
        {
            Debug.LogError("[PHD] 빌드할 씬이 없습니다: " + GameScenePath);
            return false;
        }

        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = OutputPath,
            target = BuildTarget.WebGL,
            targetGroup = BuildTargetGroup.WebGL,
            options = BuildOptions.None
        };

        var report = BuildPipeline.BuildPlayer(options);
        var summary = report.summary;

        if (summary.result == BuildResult.Succeeded)
        {
            Debug.Log($"[PHD] WebGL 빌드 성공: {OutputPath} " +
                      $"({summary.totalSize / 1024 / 1024f:F1} MB, {summary.totalTime.TotalSeconds:F0}초)");
            return true;
        }

        Debug.LogError($"[PHD] WebGL 빌드 실패: {summary.result} (에러 {summary.totalErrors}개)");
        return false;
    }

    /// <summary>
    /// 빌드할 씬 목록을 정리한다.
    /// 폴더를 재구성하면서 Build Settings 에 없어진 씬 경로가 남을 수 있는데,
    /// URP/Pipeline 의 빌드 전처리기가 그 경로를 열려다 빌드 전체가 실패한다.
    /// 그래서 실제로 존재하는 씬만 남기고 목록 자체를 고쳐 쓴다.
    /// </summary>
    static string[] ResolveScenes()
    {
        var valid = EditorBuildSettings.scenes
            .Where(s => System.IO.File.Exists(s.path))
            .ToList();

        int removed = EditorBuildSettings.scenes.Length - valid.Count;
        if (removed > 0)
        {
            Debug.LogWarning($"[PHD] Build Settings 에서 존재하지 않는 씬 {removed}개를 제거했습니다.");
        }

        if (valid.All(s => s.path != GameScenePath) && System.IO.File.Exists(GameScenePath))
        {
            valid.Insert(0, new EditorBuildSettingsScene(GameScenePath, true));
            Debug.Log("[PHD] Build Settings 에 GameScene 을 추가했습니다.");
        }

        EditorBuildSettings.scenes = valid.ToArray();

        return valid.Where(s => s.enabled).Select(s => s.path).ToArray();
    }

    /// <summary>웹에서 미니게임처럼 동작하기 위한 플레이어 설정.</summary>
    static void ApplyWebSettings()
    {
        PlayerSettings.WebGL.template = TemplateName;

        // 탭이 비활성일 때도 코루틴이 죽지 않도록(브라우저가 rAF 를 멈추면 어차피 함께 멈춘다).
        PlayerSettings.runInBackground = true;

        // 압축: 로컬 확인/정적 호스팅 편의를 위해 우선 끈다.
        //  - 실제 배포 시에는 Brotli + 서버의 Content-Encoding 헤더 설정이 가장 작고 빠르다.
        //  - 헤더를 못 만지는 정적 호스팅(GitHub Pages 등)이라면 Gzip + decompressionFallback = true.
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
        PlayerSettings.WebGL.decompressionFallback = false;

        // 재방문 시 IndexedDB 캐시로 즉시 로딩
        PlayerSettings.WebGL.dataCaching = true;

        PlayerSettings.WebGL.linkerTarget = WebGLLinkerTarget.Wasm;
        PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;

        // 초기 캔버스 크기(템플릿에서 CSS 로 100% 로 늘어난다)
        PlayerSettings.defaultWebScreenWidth = 540;
        PlayerSettings.defaultWebScreenHeight = 960;

        // 모바일 브라우저 메모리 여유가 크지 않다. 스트리핑은 켜둔다.
        PlayerSettings.stripEngineCode = true;
    }
}
