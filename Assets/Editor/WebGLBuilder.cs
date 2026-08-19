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
    // GitHub Pages 는 저장소 안의 폴더를 그대로 서빙한다(Settings > Pages > main /docs).
    const string PagesOutputPath = "docs";
    const string TemplateName = "PROJECT:PHDMobile";
    const string GameScenePath = "Assets/00. Scenes/GameScene.unity";

    [MenuItem("Tools/PHD/WebGL 빌드 (로컬 확인용)")]
    public static void BuildMenu() => Build(OutputPath, false);

    [MenuItem("Tools/PHD/WebGL 빌드 (GitHub Pages 배포용)")]
    public static void BuildPagesMenu() => Build(PagesOutputPath, true);

    public static void BuildFromCLI()
    {
        bool ok = Build(OutputPath, false);
        EditorApplication.Exit(ok ? 0 : 1);
    }

    public static void BuildPagesFromCLI()
    {
        bool ok = Build(PagesOutputPath, true);
        EditorApplication.Exit(ok ? 0 : 1);
    }

    static bool Build(string outputPath, bool forPages)
    {
        ApplyWebSettings(forPages);

        var scenes = ResolveScenes();
        if (scenes.Length == 0)
        {
            Debug.LogError("[PHD] 빌드할 씬이 없습니다: " + GameScenePath);
            return false;
        }

        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = outputPath,
            target = BuildTarget.WebGL,
            targetGroup = BuildTargetGroup.WebGL,
            options = BuildOptions.None
        };

        var report = BuildPipeline.BuildPlayer(options);
        var summary = report.summary;

        if (summary.result == BuildResult.Succeeded)
        {
            if (forPages) WritePagesFiles(outputPath);

            Debug.Log($"[PHD] WebGL 빌드 성공: {outputPath} " +
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

    /// <summary>
    /// GitHub Pages 는 Jekyll 로 사이트를 처리하는데, 밑줄로 시작하는 파일을 무시하고
    /// 빌드 시간도 늘어난다. .nojekyll 을 두면 폴더를 그대로 정적 서빙한다.
    /// </summary>
    static void WritePagesFiles(string outputPath)
    {
        System.IO.File.WriteAllText(System.IO.Path.Combine(outputPath, ".nojekyll"), string.Empty);
        StampCacheBuster(outputPath);
    }

    /// <summary>
    /// index.html 안의 Build/ 파일 주소에 배포 시각을 쿼리로 붙인다.
    ///
    /// 왜 필요한가
    ///  - 빌드마다 파일 이름이 같다(docs.data.unityweb).
    ///  - Unity 로더는 .data 만 IndexedDB 에 "must-revalidate" 로 캐시하고,
    ///    나머지는 "no-store"(= 브라우저 HTTP 캐시, Pages 는 max-age=600)로 받는다.
    ///  - 그래서 재배포 직후 이전 빌드를 본 브라우저가 <b>옛 .data + 새 .wasm</b> 을
    ///    섞어 쥘 수 있고, 부팅 중 "memory access out of bounds" 로 죽는다.
    ///
    /// 주소가 배포마다 달라지면 한 index.html 이 가리키는 네 파일이 항상 같은 빌드가 된다.
    /// (index.html 자체가 캐시돼 옛 버전이 떠도, 그 안의 주소는 옛 빌드로 일관된다)
    /// </summary>
    static void StampCacheBuster(string outputPath)
    {
        string indexPath = System.IO.Path.Combine(outputPath, "index.html");
        if (!System.IO.File.Exists(indexPath))
        {
            Debug.LogWarning("[PHD] index.html 을 찾지 못해 캐시 버스터를 건너뜁니다: " + indexPath);
            return;
        }

        string buildId = System.DateTime.UtcNow.ToString("yyMMddHHmmss");
        string html = System.IO.File.ReadAllText(indexPath);

        // 따옴표로 감싼 Build/... 경로만 바꾼다. 이미 쿼리가 붙은 주소는 건드리지 않는다.
        string stamped = System.Text.RegularExpressions.Regex.Replace(
            html, @"(?<=[""'])Build/[^""'?\s]+(?=[""'])", m => m.Value + "?v=" + buildId);

        System.IO.File.WriteAllText(indexPath, stamped);
        Debug.Log($"[PHD] 캐시 버스터 적용: ?v={buildId}");
    }

    /// <summary>웹에서 미니게임처럼 동작하기 위한 플레이어 설정.</summary>
    static void ApplyWebSettings(bool forPages)
    {
        PlayerSettings.WebGL.template = TemplateName;

        // 탭이 비활성일 때도 코루틴이 죽지 않도록(브라우저가 rAF 를 멈추면 어차피 함께 멈춘다).
        PlayerSettings.runInBackground = true;

        // 압축
        //  - 로컬 확인: 껐을 때 빌드가 빠르고 서버 설정도 필요 없다.
        //  - GitHub Pages: Content-Encoding 헤더를 설정할 수 없으므로, 압축 파일을
        //    브라우저가 아니라 Unity 로더(JS)가 풀도록 decompressionFallback 을 켠다.
        //    Brotli 가 더 작지만 JS 디코딩이 느리고 순간 메모리를 더 쓴다 → 모바일 고려해 Gzip.
        if (forPages)
        {
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
        }
        else
        {
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
            PlayerSettings.WebGL.decompressionFallback = false;
        }

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
