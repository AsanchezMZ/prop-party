using System.IO;
using UnityEditor;

public static class PropPartyBuild
{
    [InitializeOnLoadMethod]
    static void ConfigureBuildName()
    {
        BuildPlayerWindow.RegisterBuildPlayerHandler(options =>
        {
            if (options.target == BuildTarget.StandaloneWindows64 && options.subtarget == (int)StandaloneBuildSubtarget.Player &&
                !string.IsNullOrEmpty(options.locationPathName))
                options.locationPathName = Path.Combine(Path.GetDirectoryName(options.locationPathName), "PropParty.exe");
            BuildPlayerWindow.DefaultBuildMethods.BuildPlayer(options);
        });
    }
}
