using UnityEditor;
using UnityEngine;

/// <summary>
/// 멀티플레이어 테스트를 위한 다중 클라이언트 빌드 및 실행 도구
/// </summary>
public class MultiplayerBuildAndRun
{
    #region Windows Build
    [MenuItem("Tools/Run Multiplayer/Win64/1 Players")]
    static void PerformWin64Build1()
    {
        PerformWin64Build(1);
    }

    [MenuItem("Tools/Run Multiplayer/Win64/2 Players")]
    static void PerformWin64Build2()
    {
        PerformWin64Build(2);
    }

    [MenuItem("Tools/Run Multiplayer/Win64/3 Players")]
    static void PerformWin64Build3()
    {
        PerformWin64Build(3);
    }

    [MenuItem("Tools/Run Multiplayer/Win64/4 Players")]
    static void PerformWin64Build4()
    {
        PerformWin64Build(4);
    }

    static void PerformWin64Build(int playerCount)
    {
        EditorUserBuildSettings.SwitchActiveBuildTarget(
            BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64);

        for (int i = 1; i <= playerCount; i++)
        {
            BuildPipeline.BuildPlayer(GetScenePaths(),
                "Builds/Win64/" + GetProjectName() + i.ToString() + "/" + GetProjectName() + i.ToString() + ".exe",
                BuildTarget.StandaloneWindows64, BuildOptions.AutoRunPlayer);
        }
    }
    #endregion

    #region Mac Build
    [MenuItem("Tools/Run Multiplayer/Mac/1 Players")]
    static void PerformMacBuild1()
    {
        PerformMacBuild(1);
    }

    [MenuItem("Tools/Run Multiplayer/Mac/2 Players")]
    static void PerformMacBuild2()
    {
        PerformMacBuild(2);
    }

    [MenuItem("Tools/Run Multiplayer/Mac/3 Players")]
    static void PerformMacBuild3()
    {
        PerformMacBuild(3);
    }

    [MenuItem("Tools/Run Multiplayer/Mac/4 Players")]
    static void PerformMacBuild4()
    {
        PerformMacBuild(4);
    }

    static void PerformMacBuild(int playerCount)
    {
        // Mac 빌드 타겟으로 설정 (StandaloneOSX 사용)
        EditorUserBuildSettings.SwitchActiveBuildTarget(
            BuildTargetGroup.Standalone,
            BuildTarget.StandaloneOSX
        );

        // 빌드 디렉토리 생성
        string buildPath = "Builds/Mac/";
        if (!System.IO.Directory.Exists(buildPath))
        {
            System.IO.Directory.CreateDirectory(buildPath);
        }

        // 실행할 클라이언트(플레이어) 갯수 만큼 반복문 실행
        for (int i = 1; i <= playerCount; i++)
        {
            string appName = GetProjectName() + i.ToString();
            string fullBuildPath = buildPath + appName + "/" + appName + ".app";
            
            Debug.Log($"Mac 빌드 시작: Player {i} - {fullBuildPath}");
            
            // Mac .app 번들로 빌드
            BuildPipeline.BuildPlayer(GetScenePaths(),
                fullBuildPath,
                BuildTarget.StandaloneOSX, 
                BuildOptions.AutoRunPlayer
            );
        }
    }
    #endregion

    #region Utility Methods
    /// <summary>
    /// 프로젝트 이름 반환
    /// </summary>
    static string GetProjectName()
    {
        string[] s = Application.dataPath.Split('/');
        return s[s.Length - 2];
    }

    /// <summary>
    /// 빌드 설정에서 씬 경로들 가져오기
    /// </summary>
    static string[] GetScenePaths()
    {
        string[] scenes = new string[EditorBuildSettings.scenes.Length];

        for (int i = 0; i < scenes.Length; i++)
        {
            scenes[i] = EditorBuildSettings.scenes[i].path;
        }

        return scenes;
    }

    /// <summary>
    /// 빌드 폴더 정리
    /// </summary>
    [MenuItem("Tools/Run Multiplayer/Clear Builds")]
    static void ClearBuilds()
    {
        if (System.IO.Directory.Exists("Builds"))
        {
            System.IO.Directory.Delete("Builds", true);
            Debug.Log("빌드 폴더가 정리되었습니다.");
        }
        else
        {
            Debug.Log("빌드 폴더가 존재하지 않습니다.");
        }
    }
    #endregion
} 