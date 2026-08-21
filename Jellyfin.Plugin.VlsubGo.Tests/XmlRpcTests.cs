using Xunit;

namespace Jellyfin.Plugin.VlsubGo.Tests;

public class XmlRpcTests
{
    [Fact]
    public void BuildsALoginRequest()
    {
        var xml = XmlRpc.BuildRequest("LogIn", string.Empty, string.Empty, "en", "VLSub 0.10.2");

        Assert.Contains("<methodName>LogIn</methodName>", xml, StringComparison.Ordinal);
        Assert.Contains("<value><string>en</string></value>", xml, StringComparison.Ordinal);
        Assert.Contains("<value><string>VLSub 0.10.2</string></value>", xml, StringComparison.Ordinal);
    }

    [Fact]
    public void EncodesStructMembersInSortedOrderAndEscapes()
    {
        var xml = XmlRpc.BuildRequest("SearchSubtitles", "tok", new List<object?>
        {
            new Dictionary<string, object?> { ["sublanguageid"] = "eng", ["query"] = "a & b" },
        });

        Assert.Contains(
            "<member><name>query</name><value><string>a &amp; b</string></value></member>" +
            "<member><name>sublanguageid</name><value><string>eng</string></value></member>",
            xml,
            StringComparison.Ordinal);
        Assert.Contains("<array><data>", xml, StringComparison.Ordinal);
    }

    [Fact]
    public void ParsesARealLoginResponse()
    {
        // Captured verbatim from the live api.opensubtitles.org endpoint.
        const string xml =
            "<?xml version=\"1.0\" encoding=\"utf-8\"?><methodResponse><params><param><value><struct>" +
            "<member><name>token</name><value><string>3v3h7xjoGMutP44eLY3wW52DfUe</string></value></member>" +
            "<member><name>status</name><value><string>200 OK</string></value></member>" +
            "<member><name>seconds</name><value><double>0.002000</double></value></member>" +
            "</struct></value></param></params></methodResponse>";

        var map = Assert.IsType<Dictionary<string, object?>>(XmlRpc.ParseResponse(xml));

        Assert.Equal("3v3h7xjoGMutP44eLY3wW52DfUe", XmlRpc.GetString(map, "token"));
        Assert.Equal("200 OK", XmlRpc.GetString(map, "status"));
        Assert.Equal(0.002f, XmlRpc.GetFloat(map, "seconds"), 5);
    }

    [Fact]
    public void ParsesAnArrayOfStructsAndCoercesNumbers()
    {
        const string xml =
            "<methodResponse><params><param><value><struct>" +
            "<member><name>data</name><value><array><data>" +
            "<value><struct>" +
            "<member><name>SubFileName</name><value><string>one.srt</string></value></member>" +
            "<member><name>SubDownloadsCnt</name><value><string>183922</string></value></member>" +
            "<member><name>MatchedBy</name><value><string>moviehash</string></value></member>" +
            "</struct></value>" +
            "<value><struct>" +
            "<member><name>SubFileName</name><value><string>two.srt</string></value></member>" +
            "<member><name>SubDownloadsCnt</name><value><int>7</int></value></member>" +
            "</struct></value>" +
            "</data></array></value></member>" +
            "</struct></value></param></params></methodResponse>";

        var map = Assert.IsType<Dictionary<string, object?>>(XmlRpc.ParseResponse(xml));
        var rows = Assert.IsType<List<object?>>(map["data"]);
        Assert.Equal(2, rows.Count);

        var first = Assert.IsType<Dictionary<string, object?>>(rows[0]);
        Assert.Equal("one.srt", XmlRpc.GetString(first, "SubFileName"));
        // This API returns most numbers as strings; the accessor must cope.
        Assert.Equal(183922, XmlRpc.GetInt(first, "SubDownloadsCnt"));

        var second = Assert.IsType<Dictionary<string, object?>>(rows[1]);
        Assert.Equal(7, XmlRpc.GetInt(second, "SubDownloadsCnt"));
    }

    [Fact]
    public void ThrowsOnAFaultResponse()
    {
        const string xml =
            "<methodResponse><fault><value><struct>" +
            "<member><name>faultString</name><value><string>bad token</string></value></member>" +
            "<member><name>faultCode</name><value><int>401</int></value></member>" +
            "</struct></value></fault></methodResponse>";

        var ex = Assert.Throws<InvalidOperationException>(() => XmlRpc.ParseResponse(xml));
        Assert.Contains("bad token", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ParsesTheNoResultsCaseWhereDataIsBooleanFalse()
    {
        // A search with no hits returns data as a boolean, not an empty array.
        const string xml =
            "<methodResponse><params><param><value><struct>" +
            "<member><name>status</name><value><string>200 OK</string></value></member>" +
            "<member><name>data</name><value><boolean>0</boolean></value></member>" +
            "</struct></value></param></params></methodResponse>";

        var map = Assert.IsType<Dictionary<string, object?>>(XmlRpc.ParseResponse(xml));

        Assert.IsNotType<List<object?>>(map["data"]);
        Assert.False(Assert.IsType<bool>(map["data"]));
    }
}
