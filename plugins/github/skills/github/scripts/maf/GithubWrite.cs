using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;

// Production callers use the fixed GitHub origin and host-owned credentials.
internal sealed class GithubWrite(HttpClient client, string repository, string capability)
{
    private const string Base = "https://api.github.com";
    internal const string Create = "/github:change-management:create-repository";
    internal const string Dispatch = "/github:actions:dispatch";
    internal static bool IsWrite(string capability) => capability is Create or Dispatch;
    internal string Visibility { get; private set; } = "";
    internal long WorkflowId { get; private set; }
    internal string Ref { get; private set; } = "";
    internal string ExpectedSha { get; private set; } = "";
    internal long RepositoryId { get; private set; }
    internal long RunId { get; private set; }
    internal string Mutation { get; private set; } = "NOT_INVOKED";
    internal int? HttpStatus { get; private set; }
    internal string Endpoint => capability == Create ? Base + "/orgs/pmcro-marketplace/repos" :
        Base + "/repos/" + repository + "/actions/workflows/" + WorkflowId + "/dispatches";
    internal void Parse(JsonElement request)
    {
        if (!Regex.IsMatch(repository, "^pmcro-marketplace/[a-zA-Z0-9][a-zA-Z0-9_.-]{0,99}$"))
            throw new InvalidOperationException("Invalid write target");
        if (capability == Create) {
            Visibility = request.GetProperty("visibility").GetString() ?? "";
            if (Visibility is not ("public" or "private")) throw new InvalidOperationException("Explicit public or private visibility required");
        } else {
            WorkflowId = request.GetProperty("workflowId").GetInt64();
            Ref = request.GetProperty("ref").GetString() ?? "";
            if (WorkflowId <= 0 || !Regex.IsMatch(Ref, "^[a-zA-Z0-9][a-zA-Z0-9_./-]{0,199}$") || Ref.Contains(".."))
                throw new InvalidOperationException("Explicit workflow ID and branch ref required");
            if (request.TryGetProperty("inputs", out _)) throw new InvalidOperationException("Workflow inputs are not supported by this bounded binding");
        }
    }
    internal static void Claim(string raw, string? folder, bool authenticated)
    {
        if (!authenticated) throw new InvalidOperationException("Host GitHub credential required");
        if (string.IsNullOrWhiteSpace(folder)) throw new InvalidOperationException("Host approval directory required");
        using var req = JsonDocument.Parse(raw);
        if (!req.RootElement.TryGetProperty("approvalId", out var id) || !Guid.TryParseExact(id.GetString(), "D", out var approval))
            throw new InvalidOperationException("Exact host approval ID required");
        string file = Path.Combine(Path.GetFullPath(folder), approval.ToString("D") + ".json");
        if (!File.Exists(file) || File.GetAttributes(file).HasFlag(FileAttributes.ReparsePoint))
            throw new InvalidOperationException("Host approval not found");
        using var doc = JsonDocument.Parse(File.ReadAllText(file));
        var a = doc.RootElement;
        if (!a.GetProperty("approved").GetBoolean() || a.GetProperty("requestSha256").GetString() != GithubAgent.Hash(raw)
            || a.GetProperty("expiresUtc").GetDateTimeOffset() <= DateTimeOffset.UtcNow)
            throw new InvalidOperationException("Approval mismatch or expired");
        // Claim before any write; never auto-retry after timeout or uncertain response.
        using var claim = new FileStream(file + ".claimed", FileMode.CreateNew, FileAccess.Write, FileShare.None);
        using var writer = new StreamWriter(claim);
        writer.Write(GithubAgent.Hash(raw));
    }
    private async Task<JsonElement> Get(string path, CancellationToken ct)
    {
        using var response = await client.GetAsync(Base + "/repos/" + repository + path, ct);
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException("GitHub GET HTTP " + (int)response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        return doc.RootElement.Clone();
    }
    internal async Task Preflight(CancellationToken ct)
    {
        if (capability == Create) {
            using var response = await client.GetAsync(Base + "/repos/" + repository, ct);
            if (response.StatusCode != HttpStatusCode.NotFound)
                throw new InvalidOperationException("Repository absence not verified: HTTP " + (int)response.StatusCode);
        } else {
            var repo = await Get("", ct);
            RepositoryId = repo.GetProperty("id").GetInt64();
            if (repo.GetProperty("full_name").GetString() != repository) throw new InvalidOperationException("Repository identity mismatch");
            var workflow = await Get("/actions/workflows/" + WorkflowId, ct);
            if (workflow.GetProperty("id").GetInt64() != WorkflowId || workflow.GetProperty("state").GetString() != "active")
                throw new InvalidOperationException("Workflow not active or identity mismatch");
            var gitRef = await Get("/git/ref/heads/" + Uri.EscapeDataString(Ref), ct);
            ExpectedSha = gitRef.GetProperty("object").GetProperty("sha").GetString() ?? "";
            if (!Regex.IsMatch(ExpectedSha, "^[0-9a-f]{40}$")) throw new InvalidOperationException("Ref SHA missing");
        }
    }
    internal async Task<JsonElement> Make(CancellationToken ct)
    {
        object payload = capability == Create
            ? new { name = repository.Split('/')[1], visibility = Visibility, auto_init = true }
            : new { @ref = Ref };
        Mutation = "ATTEMPTED_OUTCOME_UNKNOWN";
        using var response = await client.PostAsJsonAsync(Endpoint, payload, ct);
        HttpStatus = (int)response.StatusCode;
        if (HttpStatus != (capability == Create ? 201 : 200)) {
            if (HttpStatus is >= 400 and < 500) Mutation = "REJECTED_BY_PROVIDER";
            throw new InvalidOperationException("GitHub POST HTTP " + HttpStatus);
        }
        Mutation = "RETURNED_SUCCESS_UNVERIFIED";
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var raw = doc.RootElement;
        if (capability == Create) {
            RepositoryId = raw.GetProperty("id").GetInt64();
            return JsonSerializer.SerializeToElement(new { id = RepositoryId, fullName = raw.GetProperty("full_name").GetString(), visibility = raw.GetProperty("visibility").GetString() });
        }
        RunId = raw.GetProperty("workflow_run_id").GetInt64();
        return JsonSerializer.SerializeToElement(new { repositoryId = RepositoryId, runId = RunId, workflowId = WorkflowId, reference = Ref, expectedSha = ExpectedSha });
    }
    internal async Task<JsonElement> Verify(CancellationToken ct)
    {
        var repo = await Get("", ct);
        if (RepositoryId <= 0 || repo.GetProperty("id").GetInt64() != RepositoryId || repo.GetProperty("full_name").GetString() != repository)
            throw new InvalidOperationException("Independent repository identity mismatch");
        if (capability == Create) {
            if (repo.GetProperty("visibility").GetString() != Visibility) throw new InvalidOperationException("Visibility mismatch");
            Mutation = "VERIFIED";
            return JsonSerializer.SerializeToElement(new { repositoryId = RepositoryId, repository, visibility = Visibility, verified = true });
        }
        JsonElement run = default;
        for (int i = 0; i < 6; i++) {
            try { run = await Get("/actions/runs/" + RunId, ct); break; }
            catch (InvalidOperationException e) when (e.Message.EndsWith("HTTP 404") && i < 5) { await Task.Delay(1000, ct); }
        }
        if (RunId <= 0 || run.GetProperty("id").GetInt64() != RunId
            || run.GetProperty("workflow_id").GetInt64() != WorkflowId
            || run.GetProperty("event").GetString() != "workflow_dispatch"
            || run.GetProperty("head_branch").GetString() != Ref
            || run.GetProperty("head_sha").GetString() != ExpectedSha)
            throw new InvalidOperationException("Independent dispatch identity mismatch");
        Mutation = "VERIFIED";
        return JsonSerializer.SerializeToElement(new { repositoryId = RepositoryId, runId = RunId, workflowId = WorkflowId,
            headSha = ExpectedSha, status = run.GetProperty("status").GetString(), conclusion = run.GetProperty("conclusion").GetString(), verified = true });
    }
}
