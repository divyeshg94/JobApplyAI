using System.Text.Json;

namespace JobApplyAi.Infrastructure.JobSources;

public static class JsonDefaults
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
}
