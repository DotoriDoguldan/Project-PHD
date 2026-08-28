using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;

/// <summary>
/// 씬 단위 SpriteAtlas 생성·갱신.
///
/// 원본 PNG 는 가로·세로가 4의 배수가 아니라 DXT 압축이 걸리지 않고 RGBA32 로 폴백한다.
/// 아틀라스 페이지는 POT 로 생성되므로 아트를 건드리지 않고 압축이 걸리고, 덤으로
/// 같은 페이지에 묶인 UI 끼리 Canvas 배칭이 붙는다 — WebGL 은 draw call 이 JS 브릿지를 타서 비싸다.
///
/// 빌드 씬을 스캔해 2개 이상 씬이 함께 쓰는 텍스처는 Atlas_Shared 로, 한 씬 전용은
/// Atlas_&lt;씬이름&gt; 으로 묶는다. 한 텍스처가 두 아틀라스에 들어가면 그만큼 메모리가 두 배가 되므로
/// 항상 정확히 한 곳에만 들어가야 한다.
///
/// 아트를 추가·교체한 뒤 메뉴를 다시 실행하면 목록이 갱신된다.
/// </summary>
public static class SpriteAtlasSetup
{
    private const string AtlasFolder = "Assets/08. Atlases";
    private const string AtlasPrefix = "Atlas_";
    private const string AtlasExtension = ".spriteatlasv2";
    private const string SharedGroup = "Shared";
    private const string SceneSuffix = "Scene";

    [MenuItem("Tools/PHD/스프라이트 아틀라스 생성·갱신")]
    public static void Build()
    {
        string[] scenes = EditorBuildSettings.scenes
            .Where(s => s.enabled)
            .Select(s => s.path)
            .ToArray();

        if (scenes.Length == 0)
        {
            Debug.LogError("[SpriteAtlasSetup] Build Settings 에 활성화된 씬이 없습니다.");
            return;
        }

        // 텍스처 경로 -> 그 텍스처를 쓰는 씬 목록
        var usage = new Dictionary<string, List<string>>();
        foreach (string scenePath in scenes)
        {
            foreach (string dependency in AssetDatabase.GetDependencies(scenePath, true))
            {
                if (!IsSpriteTexture(dependency)) continue;

                List<string> users;
                if (!usage.TryGetValue(dependency, out users))
                {
                    users = new List<string>();
                    usage[dependency] = users;
                }
                users.Add(scenePath);
            }
        }

        // 공유 텍스처는 Shared 로, 나머지는 그 씬 전용 그룹으로
        var groups = new Dictionary<string, List<string>>();
        foreach (var entry in usage)
        {
            string group = entry.Value.Count > 1 ? SharedGroup : GroupName(entry.Value[0]);

            List<string> members;
            if (!groups.TryGetValue(group, out members))
            {
                members = new List<string>();
                groups[group] = members;
            }
            members.Add(entry.Key);
        }

        if (!AssetDatabase.IsValidFolder(AtlasFolder))
        {
            AssetDatabase.CreateFolder(Path.GetDirectoryName(AtlasFolder), Path.GetFileName(AtlasFolder));
        }

        RemoveStaleAtlases(groups.Keys);

        // StartAssetEditing 으로 묶으면 임포트가 지연돼서 바로 뒤의 AssetImporter.GetAtPath 가
        // null 을 돌려준다 — 아틀라스마다 임포트를 끝내고 설정을 얹어야 한다.
        foreach (var group in groups)
        {
            WriteAtlas(group.Key, group.Value);
        }
        AssetDatabase.Refresh();

        foreach (var group in groups.OrderBy(g => g.Key))
        {
            Debug.LogFormat("[SpriteAtlasSetup] Atlas_{0} : 텍스처 {1}개\n  {2}",
                group.Key,
                group.Value.Count,
                string.Join("\n  ", group.Value.Select(Path.GetFileName).OrderBy(n => n).ToArray()));
        }
    }

    // 이번 실행에서 만들지 않은 아틀라스는 지운다.
    // 씬을 Build Settings 에서 빼거나 이름을 바꾸면 옛 그룹의 파일이 그대로 남는데,
    // includeInBuild 가 켜져 있어 빌드에 계속 들어간다 — 그 텍스처가 새 아틀라스에도 들어가면서
    // "한 텍스처는 정확히 한 아틀라스에만" 이라는 전제가 깨지고 메모리가 두 배가 된다.
    private static void RemoveStaleAtlases(ICollection<string> liveGroups)
    {
        if (!AssetDatabase.IsValidFolder(AtlasFolder)) return;

        foreach (string file in Directory.GetFiles(AtlasFolder, AtlasPrefix + "*" + AtlasExtension))
        {
            string name = Path.GetFileNameWithoutExtension(file);
            if (liveGroups.Contains(name.Substring(AtlasPrefix.Length))) continue;

            // Directory 는 OS 구분자를 돌려주지만 AssetDatabase 는 '/' 만 받는다.
            string assetPath = file.Replace(Path.DirectorySeparatorChar, '/');
            if (AssetDatabase.DeleteAsset(assetPath))
                Debug.LogFormat("[SpriteAtlasSetup] 더 이상 쓰이지 않는 {0} 을(를) 지웠습니다.", Path.GetFileName(assetPath));
            else
                Debug.LogErrorFormat("[SpriteAtlasSetup] {0} 을(를) 지우지 못했습니다. 직접 지워 주세요.", assetPath);
        }
    }

    // 아틀라스에 넣을 대상은 Sprite 로 임포트된 텍스처뿐이다.
    // TMP 폰트 아틀라스·노멀맵 같은 건 textureType 이 달라서 여기서 걸러진다.
    private static bool IsSpriteTexture(string assetPath)
    {
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        return importer != null && importer.textureType == TextureImporterType.Sprite;
    }

    private static string GroupName(string scenePath)
    {
        string name = Path.GetFileNameWithoutExtension(scenePath);
        return name.EndsWith(SceneSuffix) ? name.Substring(0, name.Length - SceneSuffix.Length) : name;
    }

    // 목록은 매번 씬에서 다시 계산하므로 아틀라스도 통째로 다시 쓴다.
    // 갱신이 아니라 덮어쓰기라서 씬에서 빠진 텍스처가 아틀라스에 남는 일이 없다.
    // 같은 경로에 쓰면 .meta 가 유지되므로 GUID 는 그대로다.
    private static void WriteAtlas(string group, List<string> texturePaths)
    {
        string path = string.Format("{0}/{1}{2}{3}", AtlasFolder, AtlasPrefix, group, AtlasExtension);

        Object[] packables = texturePaths
            .Select(AssetDatabase.LoadAssetAtPath<Texture2D>)
            .Where(t => t != null)
            .Cast<Object>()
            .ToArray();

        SpriteAtlasAsset asset = new SpriteAtlasAsset();
        asset.Add(packables);
        SpriteAtlasAsset.Save(asset, path);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

        SpriteAtlasImporter importer = AssetImporter.GetAtPath(path) as SpriteAtlasImporter;
        if (importer == null)
        {
            Debug.LogErrorFormat("[SpriteAtlasSetup] {0} 임포터를 찾지 못했습니다. Sprite Atlas V2 모드인지 확인하세요.", path);
            return;
        }

        importer.includeInBuild = true;

        importer.packingSettings = new SpriteAtlasPackingSettings
        {
            padding = 4,
            blockOffset = 1,
            // UI 전용이라 회전·타이트 패킹은 끈다. 회전 배치는 Sliced/Tiled Image 를 깨뜨리고,
            // UI Image 는 어차피 사각 quad 로 그리므로 타이트 패킹으로 얻는 게 없다.
            enableRotation = false,
            enableTightPacking = false,
            // 압축 시 투명 픽셀 경계에 생기는 검은 테두리를 막는다.
            enableAlphaDilation = true,
        };

        importer.textureSettings = new SpriteAtlasTextureSettings
        {
            readable = false,
            // UI 는 축소 렌더가 드물고 밉맵은 메모리 +33% 다.
            generateMipMaps = false,
            sRGB = true,
            filterMode = FilterMode.Bilinear,
            anisoLevel = 1,
        };

        ApplyPlatform(importer, "DefaultTexturePlatform", false);
        ApplyPlatform(importer, "WebGL", true);

        importer.SaveAndReimport();
    }

    private static void ApplyPlatform(SpriteAtlasImporter importer, string buildTarget, bool overridden)
    {
        TextureImporterPlatformSettings settings = importer.GetPlatformSettings(buildTarget);
        settings.overridden = overridden;
        settings.maxTextureSize = 2048;
        settings.format = TextureImporterFormat.Automatic;
        settings.textureCompression = TextureImporterCompression.Compressed;
        // 크런치는 다운로드 용량을 더 줄이지만 로드 때 CPU 로 압축을 푼다.
        // 첫 로딩이 길어지는 쪽이 더 아파서 기본은 끈다.
        settings.crunchedCompression = false;
        importer.SetPlatformSettings(settings);
    }
}
