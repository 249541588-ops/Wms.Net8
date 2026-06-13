#!/usr/bin/env dotnet-script
// 临时脚本：批量添加 ApiVersion 特性

var controllersDir = "Controllers";
var files = System.IO.Directory.GetFiles(controllersDir, "*Controller.cs");

foreach (var file in files)
{
    var content = System.IO.File.ReadAllText(file);
    var fileName = System.IO.Path.GetFileName(file);

    // 检查是否已经有 ApiVersion
    if (content.Contains("[ApiVersion"))
    {
        Console.WriteLine($"✓ {fileName} already has ApiVersion");
        continue;
    }

    // 在 [ApiController] 后添加 [ApiVersion("1.0")]
    var newContent = content.Replace(
        "[ApiController]\r\n[Route",
        "[ApiController]\r\n[ApiVersion(\"1.0\")]\r\n[Route"
    );

    if (newContent != content)
    {
        System.IO.File.WriteAllText(file, newContent);
        Console.WriteLine($"✓ Updated {fileName}");
    }
    else
    {
        Console.WriteLine($"✗ Skipped {fileName} (pattern not found)");
    }
}

Console.WriteLine("Done!");
