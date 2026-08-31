using JobApplyAi.Infrastructure.JobSources;

namespace JobApplyAi.Infrastructure.Tests.JobSources;

public class HtmlTextTests
{
    [Theory]
    [InlineData("&lt;p&gt;Hello &amp;amp; welcome&lt;/p&gt;", "Hello & welcome")]
    [InlineData("<p>Plain <strong>html</strong></p>", "Plain html")]
    [InlineData("no markup", "no markup")]
    [InlineData("  <div>  spaced   out  </div>  ", "spaced out")]
    public void ToPlainText_strips_tags_and_decodes_entities(string input, string expected)
        => Assert.Equal(expected, HtmlText.ToPlainText(input));
}
