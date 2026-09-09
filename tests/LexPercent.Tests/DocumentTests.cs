using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using LexPercent.Core;
using LexPercent.Documents;

namespace LexPercent.Tests;

public class DocumentTests
{
    [Theory]
    [InlineData("docx", false)]
    [InlineData("xlsx", false)]
    [InlineData("docx", true)]
    [InlineData("xlsx", true)]
    public void ExportIsValidOpenXml(string extension, bool protocol)
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + "." + extension);
        try
        {
            var project = ReferenceExample.Create() with { Signatories = [LexPercent.Domain.Signatory.Moroz] };
            Exporter.Export(path, Report.Create(project, new Calculator().Calculate(project), protocol));
            using OpenXmlPackage document = extension == "docx" ? WordprocessingDocument.Open(path, false) : SpreadsheetDocument.Open(path, false);
            var errors = new OpenXmlValidator().Validate(document).Select(e => e.Description + " " + e.Path?.XPath).ToArray();
            Assert.True(errors.Length == 0, string.Join("\n", errors.DistinctBy(e => e.Split(" /")[0]).Take(15)));
        }
        finally { File.Delete(path); }
    }
}
