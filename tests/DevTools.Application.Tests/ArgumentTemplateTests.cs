using DevTools.Application.Tools;
using DevTools.Domain.Projects;

namespace DevTools.Application.Tests;

public class ArgumentTemplateTests
{
    [Fact]
    public void Every_placeholder_is_filled_in()
    {
        var repository = TestData.Repository(branch: "feature/cart");
        var solution = Path.Combine(repository.Path, "src", "Shop.slnx");
        var target = new LaunchTarget(TestData.VisualStudio, repository, solution, IsFolder: false);

        var rendered = ArgumentTemplate.Render("\"{target}\" {folder} {repo} {branch} {name}", target);

        Assert.Equal(
            $"\"{solution}\" {Path.Combine(repository.Path, "src")} {repository.Path} feature/cart Shop.slnx",
            rendered
        );
    }

    [Fact]
    public void Placeholders_are_case_insensitive()
    {
        var repository = TestData.Repository();
        var target = new LaunchTarget(TestData.Code, repository, repository.Path, IsFolder: true);

        Assert.Equal($"-d {repository.Path}", ArgumentTemplate.Render("-d {Folder}", target));
    }
}
