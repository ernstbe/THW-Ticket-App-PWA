using THWTicketApp.Shared.Helpers;

namespace THWTicketApp.Tests.Helpers;

public class JsonHelperTests
{
    private record SimpleItem(string Name, int Value);

    // --- v2 format: { data: [...] } ---

    [Fact]
    public void DeserializeWrappedArray_V2_DataIsArray_ReturnsItems()
    {
        var json = """{"success":true,"data":[{"name":"A","value":1},{"name":"B","value":2}]}""";
        var result = JsonHelper.DeserializeWrappedArray<SimpleItem>(json, "items");
        Assert.Equal(2, result.Length);
        Assert.Equal("A", result[0].Name);
        Assert.Equal("B", result[1].Name);
    }

    // --- v2 format: { data: { propertyName: [...] } } ---

    [Fact]
    public void DeserializeWrappedArray_V2_DataContainsNamedProperty_ReturnsItems()
    {
        var json = """{"success":true,"data":{"tickets":[{"name":"T1","value":10}]}}""";
        var result = JsonHelper.DeserializeWrappedArray<SimpleItem>(json, "tickets");
        Assert.Single(result);
        Assert.Equal("T1", result[0].Name);
        Assert.Equal(10, result[0].Value);
    }

    // --- v2 format: { data: { single object } } ---

    [Fact]
    public void DeserializeWrappedArray_V2_DataIsSingleObject_WrapsInArray()
    {
        var json = """{"success":true,"data":{"name":"Single","value":99}}""";
        var result = JsonHelper.DeserializeWrappedArray<SimpleItem>(json, "items");
        Assert.Single(result);
        Assert.Equal("Single", result[0].Name);
        Assert.Equal(99, result[0].Value);
    }

    // --- v1 format: { propertyName: [...] } ---

    [Fact]
    public void DeserializeWrappedArray_V1_NamedProperty_ReturnsItems()
    {
        var json = """{"tickets":[{"name":"V1","value":5},{"name":"V1b","value":6}]}""";
        var result = JsonHelper.DeserializeWrappedArray<SimpleItem>(json, "tickets");
        Assert.Equal(2, result.Length);
        Assert.Equal("V1", result[0].Name);
    }

    // --- v1 new format: { success: true, tickets: [...], count: N } ---

    [Fact]
    public void DeserializeWrappedArray_V1_WithSuccessAndCount_ReturnsItems()
    {
        var json = """{"success":true,"tickets":[{"name":"X","value":1}],"count":1}""";
        var result = JsonHelper.DeserializeWrappedArray<SimpleItem>(json, "tickets");
        Assert.Single(result);
        Assert.Equal("X", result[0].Name);
    }

    // --- Fallback: root array ---

    [Fact]
    public void DeserializeWrappedArray_RootArray_ReturnsItems()
    {
        var json = """[{"name":"R1","value":1},{"name":"R2","value":2}]""";
        var result = JsonHelper.DeserializeWrappedArray<SimpleItem>(json, "whatever");
        Assert.Equal(2, result.Length);
    }

    // --- Empty responses ---

    [Fact]
    public void DeserializeWrappedArray_EmptyArray_ReturnsEmpty()
    {
        var json = """{"data":[]}""";
        var result = JsonHelper.DeserializeWrappedArray<SimpleItem>(json, "items");
        Assert.Empty(result);
    }

    [Fact]
    public void DeserializeWrappedArray_NoMatchingProperty_ReturnsEmpty()
    {
        var json = """{"other":"value"}""";
        var result = JsonHelper.DeserializeWrappedArray<SimpleItem>(json, "tickets");
        Assert.Empty(result);
    }

    [Fact]
    public void DeserializeWrappedArray_EmptyNamedArray_ReturnsEmpty()
    {
        var json = """{"tickets":[]}""";
        var result = JsonHelper.DeserializeWrappedArray<SimpleItem>(json, "tickets");
        Assert.Empty(result);
    }

    // --- Case insensitivity ---

    [Fact]
    public void DeserializeWrappedArray_CaseInsensitiveDeserialization()
    {
        var json = """{"data":[{"Name":"Upper","Value":42}]}""";
        var result = JsonHelper.DeserializeWrappedArray<SimpleItem>(json, "items");
        Assert.Single(result);
        Assert.Equal("Upper", result[0].Name);
        Assert.Equal(42, result[0].Value);
    }

    // --- Status response (different property name) ---

    [Fact]
    public void DeserializeWrappedArray_StatusProperty_ReturnsItems()
    {
        var json = """{"status":[{"name":"Open","value":1},{"name":"Closed","value":2}]}""";
        var result = JsonHelper.DeserializeWrappedArray<SimpleItem>(json, "status");
        Assert.Equal(2, result.Length);
        Assert.Equal("Open", result[0].Name);
    }
}
