using THWTicketApp.Web.Pages;

namespace THWTicketApp.Tests.Pages;

public class DocumentsTests
{
    [Fact]
    public void ParseDocuments_validJson_returnsList()
    {
        var json = """{"documents":[{"_id":"d1","name":"Anleitung","category":"Handbuch","size":1024,"createdAt":"2026-04-26T10:00:00Z","uploadedBy":{"fullname":"Max"}}]}""";
        var result = Documents.ParseDocuments(json);

        Assert.Single(result);
        Assert.Equal("Anleitung", result[0].Name);
        Assert.Equal("Handbuch", result[0].Category);
        Assert.Equal(1024, result[0].Size);
        Assert.Equal("Max", result[0].UploadedBy);
    }

    [Fact]
    public void ParseDocuments_emptyArray_returnsEmpty()
    {
        Assert.Empty(Documents.ParseDocuments("""{"documents":[]}"""));
    }

    [Fact]
    public void ParseDocuments_invalidJson_returnsEmpty()
    {
        Assert.Empty(Documents.ParseDocuments("broken"));
    }

    [Fact]
    public void FormatSize_bytes_returnsB()
    {
        Assert.Equal("500 B", Documents.FormatSize(500));
    }

    [Fact]
    public void FormatSize_kilobytes_returnsKB()
    {
        var result = Documents.FormatSize(1536);
        Assert.Contains("KB", result);
    }

    [Fact]
    public void FormatSize_megabytes_returnsMB()
    {
        var result = Documents.FormatSize(2621440);
        Assert.Contains("MB", result);
    }

    [Fact]
    public void GetFileIcon_pdf_returnsPdfIcon()
    {
        var icon = Documents.GetFileIcon("application/pdf");
        Assert.NotNull(icon);
        Assert.NotEmpty(icon);
    }

    [Fact]
    public void GetFileIcon_null_returnsDefaultIcon()
    {
        var icon = Documents.GetFileIcon(null);
        Assert.NotNull(icon);
    }
}
