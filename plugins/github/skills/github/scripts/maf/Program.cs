using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using A2A;
using A2A.AspNetCore;
using AgentGovernance;
using AgentGovernance.Extensions.Microsoft.Agents;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting.AGUI.AspNetCore;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

if (args.Length != 3) throw new ArgumentException("Usage: marketplace-root proof-directory port");
string root = Path.GetFullPath(args[0]), proof = Path.GetFullPath(args[1]);
int port = int.Parse(args[2]);
string token = Environment.GetEnvironmentVariable("PMCRO_MAF_TOKEN") ?? "";
if (token.Length < 32) throw new InvalidOperationException("Local bearer required");
string skill = Path.Combine(root, "plugins/github/skills/github");
string definition = Path.Combine(root, "plugins/github/agents/github-agent.agent.md");
if (!File.Exists(Path.Combine(root, ".agents/plugins/marketplace.json")) || !File.ReadAllText(definition).Contains("name: github-agent\n")) throw new InvalidOperationException("Marketplace definition missing");
string def = GithubAgent.Hash(File.ReadAllText(definition));
var registry = JsonDocument.Parse(File.ReadAllText(Path.Combine(skill, "assets/native-capabilities.json"))).RootElement.GetProperty("capabilities");
Directory.CreateDirectory(proof);
var inner = new GithubAgent(def, registry, proof);
var kernel = new GovernanceKernel(new GovernanceOptions { PolicyPaths = new() { Path.Combine(skill, "assets/maf-policy.yaml") } });
var events = new ConcurrentQueue<object>();
kernel.AuditEmitter.OnAll(e => events.Enqueue(new { type = e.Type.ToString(), e.AgentId, e.EventId, e.PolicyName, e.Timestamp }));
AIAgent governed = inner.WithGovernance(kernel, new AgentFrameworkGovernanceOptions {DefaultAgentId = "github-agent", EnableFunctionMiddleware = false});
var builder = WebApplication.CreateBuilder();
string url = $"http://127.0.0.1:{port}";
builder.WebHost.UseUrls(url);
builder.Services.AddKeyedSingleton<AIAgent>("github-agent", governed);
builder.AddA2AServer("github-agent", o => o.ServerOptions = new A2AServerOptions {SupportsStreaming = true});
builder.Services.AddAGUIServer();
var app = builder.Build();
app.Use(async (ctx,next) => {
    if ((ctx.Request.Path.StartsWithSegments("/a2a") || ctx.Request.Path.StartsWithSegments("/agui")) && !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(ctx.Request.Headers.Authorization.ToString()), Encoding.UTF8.GetBytes("Bearer " + token))) {ctx.Response.StatusCode = 401; return;}
    await next();
});
app.MapA2AJsonRpc("github-agent", "/a2a/github");
app.MapAGUIServer("/agui", governed);
app.MapGet("/studio", () => Results.Content(File.ReadAllText(Path.Combine(skill,"assets/studio.html")), "text/html; charset=utf-8"));
app.MapWellKnownAgentCard(new AgentCard {
    Name = "github-agent", Description = inner.Description, Version = "0.4.0",
    DefaultInputModes = ["text/plain"], DefaultOutputModes = ["text/plain"], Capabilities = new AgentCapabilities {Streaming = true},
    Skills = registry.EnumerateObject().Where(p => p.Value.GetProperty("binding").GetString() == "GITHUB_REST").Select(p => new A2A.AgentSkill {Id = p.Name, Name = p.Name, Description = "Scoped read-only GitHub operation", Tags = ["github","pmcro"]}).ToList(),
    SupportedInterfaces = [new AgentInterface {Url = url + "/a2a/github", ProtocolBinding = ProtocolBindingNames.JsonRpc, ProtocolVersion = "1.0"}]
});
try {await app.RunAsync();}
finally {File.WriteAllText(Path.Combine(proof,"agt-audit.json"), JsonSerializer.Serialize(events.ToArray()));}

internal sealed class GithubAgent(string definitionHash, JsonElement registry, string proof) : AIAgent
{
    protected override string IdCore => "github-agent";
    public override string Name => "github-agent";
    public override string Description => "Governed five-phase native GitHub repository and Actions inspection.";
    internal static string Hash(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    private sealed class Session : AgentSession {}
    protected override ValueTask<AgentSession> CreateSessionCoreAsync(CancellationToken ct = default) => new(new Session());
    protected override ValueTask<AgentSession> DeserializeSessionCoreAsync(JsonElement state, JsonSerializerOptions? options = null, CancellationToken ct = default) => new(new Session());
    protected override ValueTask<JsonElement> SerializeSessionCoreAsync(AgentSession session, JsonSerializerOptions? options = null, CancellationToken ct = default) => new(JsonSerializer.SerializeToElement(new {}));
    private sealed class Step(string id, Func<string,CancellationToken,Task<string>> run) : Executor<string,string>(id) {
        public override async ValueTask<string> HandleAsync(string value, IWorkflowContext context, CancellationToken ct = default) => await run(value, ct);
    }
    private async Task<string> Execute(string text, CancellationToken ct, Action<JsonElement>? emit = null)
    {
        string runId = Guid.NewGuid().ToString(), folder = Path.Combine(proof, runId);
        Directory.CreateDirectory(folder);
        string capability = "", repository = "", suffix = "", error = "";
        long run = 0, repoId = 0;
        bool planned = false, verified = false;
        JsonElement observed = default;
        var frames = new List<JsonElement>();
        using var client = new HttpClient(new HttpClientHandler {AllowAutoRedirect = false}) {Timeout=TimeSpan.FromSeconds(30)};
        client.DefaultRequestHeaders.UserAgent.ParseAdd("PMCR-O-GitHub-Agent/0.4");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        client.DefaultRequestHeaders.Add("X-GitHub-Api-Version","2026-03-10");
        string? githubToken = Environment.GetEnvironmentVariable("PMCRO_GITHUB_TOKEN");
        if (!string.IsNullOrEmpty(githubToken)) client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", githubToken);
        async Task<JsonElement> Get(string path, CancellationToken cancel) {
            using var res = await client.GetAsync("https://api.github.com/repos/" + repository + path, cancel);
            if (!res.IsSuccessStatusCode) throw new InvalidOperationException("GitHub HTTP " + (int)res.StatusCode);
            using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(cancel));
            return doc.RootElement.Clone();
        }
        string[] phases = ["orchestrate","plan","make","check","reflect"];
        async Task<string> Phase(string phase,string prior,CancellationToken cancel) {
            string status = "PASS"; object output;
            try {
                switch (phase) {
                    case "orchestrate":
                        using (var doc = JsonDocument.Parse(text)) {
                            capability = doc.RootElement.GetProperty("capability").GetString() ?? "";
                            repository = doc.RootElement.GetProperty("repository").GetString() ?? "";
                            if (doc.RootElement.TryGetProperty("runId", out var r)) run = r.GetInt64();
                        }
                        output = new {capability, repository, requestSha256=Hash(text)}; break;
                    case "plan":
                        planned = Regex.IsMatch(repository, "^pmcro-marketplace/[a-zA-Z0-9_.-]{1,100}$") && registry.TryGetProperty(capability, out var binding) && binding.GetProperty("binding").GetString()=="GITHUB_REST";
                        if (!planned) throw new InvalidOperationException("Unbound capability or unauthorized repository scope");
                        suffix = capability switch {"/github:repository-discovery:get-repository" => "", "/github:actions:inspect" => "/actions/runs?per_page=20", "/github:actions:verify-run" when run > 0 => "/actions/runs/"+run, _ => throw new InvalidOperationException("Run ID required")};
                        output = new {method="GET", endpoint="https://api.github.com/repos/"+repository+suffix, authenticated=!string.IsNullOrEmpty(githubToken)}; break;
                    case "make":
                        if (!planned || error.Length>0) throw new InvalidOperationException("Plan blocked");
                        var repo = await Get("",cancel); repoId=repo.GetProperty("id").GetInt64();
                        var raw = suffix=="" ? repo : await Get(suffix,cancel);
                        observed = suffix=="" ? JsonSerializer.SerializeToElement(new {id=repoId,fullName=raw.GetProperty("full_name").GetString(),defaultBranch=raw.GetProperty("default_branch").GetString()}) :
                            capability.EndsWith(":inspect") ? JsonSerializer.SerializeToElement(new {repositoryId=repoId,totalCount=raw.GetProperty("total_count").GetInt64(),runs=raw.GetProperty("workflow_runs").EnumerateArray().Select(r=>new {id=r.GetProperty("id").GetInt64(),status=r.GetProperty("status").GetString(),conclusion=r.GetProperty("conclusion").GetString(),headSha=r.GetProperty("head_sha").GetString()}).ToArray()}) :
                            JsonSerializer.SerializeToElement(new {repositoryId=repoId,id=raw.GetProperty("id").GetInt64(),status=raw.GetProperty("status").GetString(),conclusion=raw.GetProperty("conclusion").GetString(),headSha=raw.GetProperty("head_sha").GetString()});
                        output = new {provider="GITHUB_REST",method="GET",httpStatus=200,returned=observed}; break;
                    case "check":
                        if (observed.ValueKind==JsonValueKind.Undefined) throw new InvalidOperationException("No Make evidence");
                        var independentlyRead=await Get("",cancel);
                        verified=independentlyRead.GetProperty("id").GetInt64()==repoId && independentlyRead.GetProperty("full_name").GetString()==repository;
                        if (capability.EndsWith("verify-run")) verified=verified && (await Get(suffix,cancel)).GetProperty("id").GetInt64()==run;
                        if (!verified) throw new InvalidOperationException("Provider identity mismatch");
                        output = new {independentRepositoryId=repoId,verified,actualReceipt=Hash(observed.GetRawText())}; break;
                    default:
                        status=verified?"PASS":"CORRECTIVE_SEED"; output=new {accepted=verified,continuationAuthorized=false,correctiveSeed=verified?null:"Resolve the provider binding, scope or GitHub error and restart at Orchestrate.",error}; break;
                }
            } catch (Exception e) when (e is not OperationCanceledException) {
                error = e is InvalidOperationException ? e.Message : e.GetType().Name;
                status=phase=="check"?"REJECT":"BLOCKED"; output=new {error};
            }
            string body=JsonSerializer.Serialize(new {agentId=Id,definitionSha256=definitionHash,runId,phase,invocation="@github-agent /pmcro:"+phase,capability,repository,previousFrameId=prior,status,output});
            var frame=JsonSerializer.SerializeToElement(new {frameId=Hash(body),body});
            frames.Add(frame); File.WriteAllText(Path.Combine(folder,phase+".json"),frame.GetRawText()); emit?.Invoke(frame); return Hash(body);
        }
        var steps=phases.Select(p=>new Step(p,(prior,cancel)=>Phase(p,prior,cancel))).ToArray();
        var builder=new WorkflowBuilder(steps[0]); for(int i=1;i<steps.Length;i++)builder.AddEdge(steps[i-1],steps[i]);
        await using var workflow=await InProcessExecution.RunAsync(builder.WithOutputFrom(steps[^1]).Build(),"START",cancellationToken:ct);
        var completed=workflow.NewEvents.OfType<ExecutorCompletedEvent>().Select(e=>e.ExecutorId).ToArray();
        if (!completed.SequenceEqual(phases)) throw new InvalidOperationException("Incomplete MAF workflow");
        string result=JsonSerializer.Serialize(new {agentId=Id,definitionSha256=definitionHash,runId,status=verified?"PASS":"CORRECTIVE_SEED",capability,repository,executorCompleted=completed,frames,model="NOT_INVOKED",remoteMutation="NOT_INVOKED"});
        File.WriteAllText(Path.Combine(folder,"summary.json"),result); return result;
    }
    protected override async Task<AgentResponse> RunCoreAsync(IEnumerable<ChatMessage> messages,AgentSession? session=null,AgentRunOptions? options=null,CancellationToken ct=default) => new(){AgentId=Id,ResponseId=Guid.NewGuid().ToString(),Messages=[new ChatMessage(ChatRole.Assistant,await Execute(messages.Last(m=>m.Role==ChatRole.User).Text,ct))]};
    protected override async IAsyncEnumerable<AgentResponseUpdate> RunCoreStreamingAsync(IEnumerable<ChatMessage> messages,AgentSession? session=null,AgentRunOptions? options=null,[EnumeratorCancellation] CancellationToken ct=default) {
        var channel=Channel.CreateUnbounded<JsonElement>(); string response=Guid.NewGuid().ToString(),message=Guid.NewGuid().ToString();
        AgentResponseUpdate U(string s)=>new(){AgentId=Id,AuthorName=Name,Role=ChatRole.Assistant,ResponseId=response,MessageId=message,Contents=[new TextContent(s)]};
        async Task<string> Run(){try{return await Execute(messages.Last(m=>m.Role==ChatRole.User).Text,ct,f=>channel.Writer.TryWrite(f));}finally{channel.Writer.TryComplete();}}
        var work=Run();yield return U("{\"phaseEvents\":[");bool first=true;
        await foreach(var f in channel.Reader.ReadAllAsync(ct)){yield return U((first?"":",")+f.GetRawText());first=false;}
        yield return U("],\"result\":"+await work+"}");
    }
}
