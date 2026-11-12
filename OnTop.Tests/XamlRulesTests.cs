using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Xunit;

namespace OnTop.Tests;

public class XamlRulesTests
{
    private static string AppFolder([CallerFilePath] string thisFile = "") =>
        Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(thisFile)!)!, "OnTop");

    [Fact]
    public void No_binding_takes_its_converter_from_a_DynamicResource()
    {
        // Binding.Converter is not a DependencyProperty, so WPF throws a
        // XamlParseException when the template loads (0.45.3: View Logs crashed).
        // The "every *Brush is a DynamicResource" rule does not cover converters.
        var files = Directory.GetFiles(AppFolder(), "*.xaml", SearchOption.AllDirectories)
            .Where(f => !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar)
                     && !f.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar))
            .ToArray();
        Assert.NotEmpty(files);

        var offenders = files
            .SelectMany(f => File.ReadAllLines(f)
                .Select((line, i) => (line, where: $"{Path.GetFileName(f)}:{i + 1}")))
            .Where(x => x.line.Contains("Converter={DynamicResource"))
            .Select(x => x.where)
            .ToArray();

        Assert.True(offenders.Length == 0, "Converter={DynamicResource …} in: " + string.Join(", ", offenders));
    }
}
