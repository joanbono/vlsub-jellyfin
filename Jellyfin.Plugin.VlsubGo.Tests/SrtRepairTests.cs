using Xunit;

namespace Jellyfin.Plugin.VlsubGo.Tests;

public class SrtRepairTests
{
    [Fact]
    public void MergesConsecutiveCuesSharingATiming()
    {
        const string input =
            "1\n00:00:10,710 --> 00:00:14,540\nAll right.\n\n" +
            "2\n00:00:14,710 --> 00:00:17,940\nHere we are, gentlemen,\n\n" +
            "3\n00:00:14,710 --> 00:00:17,940\nthe Gates of Elzebub.\n\n";

        const string expected =
            "1\n00:00:10,710 --> 00:00:14,540\nAll right.\n\n" +
            "2\n00:00:14,710 --> 00:00:17,940\nHere we are, gentlemen,\nthe Gates of Elzebub.\n\n";

        var actual = SrtRepair.Apply(input, out var merged);

        Assert.Equal(1, merged);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void RenumbersAfterMerging()
    {
        const string input =
            "1\n00:00:01,000 --> 00:00:02,000\nA\n\n" +
            "2\n00:00:01,000 --> 00:00:02,000\nB\n\n" +
            "3\n00:00:03,000 --> 00:00:04,000\nC\n\n";

        const string expected =
            "1\n00:00:01,000 --> 00:00:02,000\nA\nB\n\n" +
            "2\n00:00:03,000 --> 00:00:04,000\nC\n\n";

        var actual = SrtRepair.Apply(input, out var merged);

        Assert.Equal(1, merged);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void LeavesHealthyInputByteIdentical()
    {
        const string input =
            "1\n00:00:01,000 --> 00:00:02,000\nTwo real\nlines here\n\n" +
            "2\n00:00:03,000 --> 00:00:04,000\nAnother\n\n";

        var actual = SrtRepair.Apply(input, out var merged);

        Assert.Equal(0, merged);
        Assert.Same(input, actual);
    }

    [Fact]
    public void HandlesCrlfAndByteOrderMark()
    {
        var input = "﻿1\r\n00:00:01,000 --> 00:00:02,000\r\nA\r\n\r\n" +
                    "2\r\n00:00:01,000 --> 00:00:02,000\r\nB\r\n";

        var actual = SrtRepair.Apply(input, out var merged);

        Assert.Equal(1, merged);
        Assert.Equal("1\n00:00:01,000 --> 00:00:02,000\nA\nB\n\n", actual);
    }

    [Fact]
    public void DoesNotMergeIdenticalTimingsThatAreNotAdjacent()
    {
        const string input =
            "1\n00:00:01,000 --> 00:00:02,000\nA\n\n" +
            "2\n00:00:09,000 --> 00:00:10,000\nB\n\n" +
            "3\n00:00:01,000 --> 00:00:02,000\nC\n\n";

        SrtRepair.Apply(input, out var merged);

        Assert.Equal(0, merged);
    }

    [Fact]
    public void MergesThreeWayDialogueInOrder()
    {
        // Dialogue cues are where a reversed render misattributes speakers.
        const string input =
            "1\n00:00:21,620 --> 00:00:23,950\n- No.\n\n" +
            "2\n00:00:21,620 --> 00:00:23,950\n- Here, let me try.\n\n";

        var actual = SrtRepair.Apply(input, out var merged);

        Assert.Equal(1, merged);
        Assert.Equal("1\n00:00:21,620 --> 00:00:23,950\n- No.\n- Here, let me try.\n\n", actual);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not a subtitle file at all")]
    public void ToleratesJunkInput(string input)
    {
        var actual = SrtRepair.Apply(input, out var merged);

        Assert.Equal(0, merged);
        Assert.Equal(input, actual);
    }
}
