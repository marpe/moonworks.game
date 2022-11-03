using MyGame.CodeGen;

var contentRoot = "Content";
var contentOutputPath = "ContentPaths.cs";
var dryRun = false;
var verbose = false;
string? ldtkFilePath = null;
var ldtkOutputPath = "LDtkGenerated.cs";

for (var i = 0; i < args.Length; i++)
{
    if (!args[i].StartsWith("--"))
        continue;

    if (args[i] == "--dry")
    {
        dryRun = true;
        continue;
    }

    if (args[i] == "--verbose")
    {
        verbose = true;
        continue;
    }

    if (i < args.Length - 1)
    {
        var (arg, value) = (args[i], args[i + 1]);
        if (arg == "--content-root")
            contentRoot = value;
        else if (arg == "--content-out")
            contentOutputPath = value;
        else if (arg == "--ldtk")
            ldtkFilePath = value;
        else if (arg == "--ldtk-out")
            ldtkOutputPath = value;
    }
}

if (ldtkFilePath != null)
{
    var ldtkFull = Path.GetFullPath(ldtkFilePath);
    var ldtkStr = LDtkGenerator.Generate(ldtkFull);

    if (dryRun || verbose)
        Console.WriteLine(ldtkStr);

    if (!dryRun)
    {
        var fullPath = Path.GetFullPath(ldtkOutputPath);
        Console.WriteLine($"File written to: {fullPath}");
        File.WriteAllText(fullPath, ldtkStr);
    }
}
else
{
    var contentRootFull = Path.GetFullPath(contentRoot);
    var contentPathsStr = ContentPathGenerator.Generate(contentRootFull);

    if (dryRun || verbose)
        Console.WriteLine(contentPathsStr);

    if (!dryRun)
    {
        var fullPath = Path.GetFullPath(contentOutputPath);
        Console.WriteLine($"File written to: {fullPath}");
        File.WriteAllText(fullPath, contentPathsStr);
    }
}
