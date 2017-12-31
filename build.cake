var target = Argument("target", "Default");
var configuration = Argument("configuration", "Release");

var version = "1.0.0";

var testsResultsDir = MakeAbsolute(Directory("tests-results"));

var solutionPath = "./xUnitToJUnit.sln";

Task("Clean")
    .Does(() =>
    {
        CleanDirectory(testsResultsDir);

        var settings = new DotNetCoreCleanSettings
        {
            Configuration = configuration
        };

        DotNetCoreClean(solutionPath, settings);
    });

Task("Restore")
    .IsDependentOn("Clean")
    .Does(() =>
    {
        DotNetCoreRestore();
    });

Task("SemVer")
    .IsDependentOn("Restore")
    .Does(() =>
    {
        var gitVersion = GitVersion();
        version = gitVersion.NuGetVersion;

        Information($"Version: {version}");
    });

Task("SetAppVeyorVersion")
    .IsDependentOn("Semver")
    .WithCriteria(() => AppVeyor.IsRunningOnAppVeyor)
    .Does(() =>
    {
        AppVeyor.UpdateBuildVersion(version);
    });

Task("Build")
    .IsDependentOn("SetAppVeyorVersion")
    .Does(() =>
    {
        var settings = new DotNetCoreBuildSettings
        {
            Configuration = configuration,
            NoIncremental = true,
            ArgumentCustomization = args => args.Append("--no-restore")
        };

        DotNetCoreBuild(solutionPath, settings);
    });

Task("Test")
    .IsDependentOn("Build")
    .Does(() =>
    {
        var settings = new DotNetCoreToolSettings();

        var argumentsBuilder = new ProcessArgumentBuilder()
            .Append("-configuration")
            .Append(configuration)
            .Append("-nobuild");

        var projectFiles = GetFiles("./tests/*/*Tests.csproj");

        foreach (var projectFile in projectFiles)
        {
            var testResultsFile = testsResultsDir.Combine($"{projectFile.GetFilenameWithoutExtension()}.xml");
            var arguments = $"{argumentsBuilder.Render()} -xml \"{testResultsFile}\"";

            DotNetCoreTool(projectFile, "xunit", arguments, settings);
        }
    });

Task("PublishAppVeyorArtifacts")
    .IsDependentOn("Test")
    .WithCriteria(() => AppVeyor.IsRunningOnAppVeyor)
    .Does(() =>
    {
        CopyFiles($"src/*.xslt", MakeAbsolute(Directory("./")), false);

        GetFiles($"./*.xslt")
            .ToList()
            .ForEach(f => AppVeyor.UploadArtifact(f, new AppVeyorUploadArtifactsSettings { DeploymentName = "transform" }));
    });

Task("Default")
    .IsDependentOn("PublishAppVeyorArtifacts");

RunTarget(target);
