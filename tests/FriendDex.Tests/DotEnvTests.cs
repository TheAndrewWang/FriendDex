using FriendDex.Web.Services;

namespace FriendDex.Tests;

public class DotEnvTests
{
    [Fact]
    public void Load_ReadsKeysFromParentFolder_AndKeepsExistingValues()
    {
        var id = Guid.NewGuid().ToString("N");
        var root = Directory.CreateTempSubdirectory().FullName;
        var child = Directory.CreateDirectory(Path.Combine(root, "src", "app")).FullName;
        File.WriteAllLines(Path.Combine(root, ".env"),
        [
            "# comment",
            $"FD_A_{id}=plain",
            $"export FD_B_{id}=\"quoted value\"",
            $"FD_EMPTY_{id}=",
            $"FD_KEEP_{id}=from-file",
        ]);
        Environment.SetEnvironmentVariable($"FD_KEEP_{id}", "already-set");

        try
        {
            DotEnv.Load(startDir: child);

            Assert.Equal("plain", Environment.GetEnvironmentVariable($"FD_A_{id}"));
            Assert.Equal("quoted value", Environment.GetEnvironmentVariable($"FD_B_{id}"));
            Assert.Null(Environment.GetEnvironmentVariable($"FD_EMPTY_{id}"));
            Assert.Equal("already-set", Environment.GetEnvironmentVariable($"FD_KEEP_{id}"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
