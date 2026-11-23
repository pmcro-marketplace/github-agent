using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

const string repo = "pmcro-marketplace/fixture-agent";
const string sha = "0123456789abcdef0123456789abcdef01234567";
var evidence = new List<object>();
void Require(bool ok, string reason) { if (!ok) throw new Exception(reason); }
async Task Denied(string name, Func<Task> action) {
    try { await action(); } catch (Exception) { evidence.Add(new { name, status = "PASS" }); return; }
    throw new Exception("Expected failure: " + name);
}
var creation = new Fixture([
    ("GET", "/repos/"+repo, 404, "{}"),
    ("POST", "/orgs/pmcro-marketplace/repos", 201, """{"id":123,"full_name":"pmcro-marketplace/fixture-agent","visibility":"private"}"""),
    ("GET", "/repos/"+repo, 200, """{"id":123,"full_name":"pmcro-marketplace/fixture-agent","visibility":"private"}""")
]);
using var createClient = new HttpClient(creation);
var create = new GithubWrite(createClient, repo, GithubWrite.Create);
create.Parse(JsonSerializer.SerializeToElement(new { visibility = "private" }));
await create.Preflight(default); var created = await create.Make(default); var verified = await create.Verify(default);
Require(creation.Calls == 3 && create.Mutation == "VERIFIED" && verified.GetProperty("repositoryId").GetInt64() == 123, "creation verification");
Require(creation.PostBody is JsonElement createdPayload && createdPayload.GetProperty("visibility").GetString() == "private" && createdPayload.GetProperty("name").GetString() == "fixture-agent", "create payload");
evidence.Add(new { name = "create", status = "PASS", calls = creation.Calls, created, verified });
var dispatch = new Fixture([
    ("GET", "/repos/"+repo, 200, """{"id":123,"full_name":"pmcro-marketplace/fixture-agent"}"""),
    ("GET", "/repos/"+repo+"/actions/workflows/22", 200, """{"id":22,"state":"active"}"""),
    ("GET", "/repos/"+repo+"/git/ref/heads/main", 200, JsonSerializer.Serialize(new { @object = new { sha } })),
    ("POST", "/repos/"+repo+"/actions/workflows/22/dispatches", 200, """{"workflow_run_id":456}"""),
    ("GET", "/repos/"+repo, 200, """{"id":123,"full_name":"pmcro-marketplace/fixture-agent"}"""),
    ("GET", "/repos/"+repo+"/actions/runs/456", 200, JsonSerializer.Serialize(new { id=456, workflow_id=22, @event="workflow_dispatch", head_branch="main", head_sha=sha, status="queued", conclusion=(string?)null }))
]);
using var dispatchClient = new HttpClient(dispatch);
var run = new GithubWrite(dispatchClient,repo,GithubWrite.Dispatch);
run.Parse(JsonSerializer.SerializeToElement(new { workflowId=22, @ref="main" }));
await run.Preflight(default); var dispatched = await run.Make(default); var checkedRun = await run.Verify(default);
Require(run.Mutation=="VERIFIED" && dispatch.Calls==6 && checkedRun.GetProperty("runId").GetInt64()==456, "dispatch verification");
Require(dispatch.PostBody is JsonElement dispatchedPayload && dispatchedPayload.GetProperty("ref").GetString()=="main", "dispatch target");
evidence.Add(new { name="dispatch", status="PASS", calls=dispatch.Calls, dispatched, checkedRun });
var existing = new Fixture([("GET","/repos/"+repo,200,"{}")]);
using var existingClient = new HttpClient(existing);
var duplicate = new GithubWrite(existingClient,repo,GithubWrite.Create);
duplicate.Parse(JsonSerializer.SerializeToElement(new { visibility="public" }));
await Denied("existing repository rejected before POST",()=>duplicate.Preflight(default));
Require(existing.Calls==1 && duplicate.Mutation=="NOT_INVOKED","duplicate write");
var forbidden = new Fixture([("GET","/repos/"+repo,404,"{}"),("POST","/orgs/pmcro-marketplace/repos",403,"{}")]);
using var forbiddenClient = new HttpClient(forbidden);
var denied = new GithubWrite(forbiddenClient,repo,GithubWrite.Create);
denied.Parse(JsonSerializer.SerializeToElement(new { visibility="private" }));
await denied.Preflight(default);
await Denied("provider permission denial",()=>denied.Make(default));
Require(denied.Mutation=="REJECTED_BY_PROVIDER" && denied.HttpStatus==403 && forbidden.Calls==2,"403 receipt");
string approvals = Directory.CreateTempSubdirectory("pmcro-approval-test-").FullName;
var id=Guid.NewGuid(); string raw=JsonSerializer.Serialize(new { approvalId=id.ToString("D"), repository=repo });
string approval=Path.Combine(approvals,id.ToString("D")+".json");
try {
    File.WriteAllText(approval,JsonSerializer.Serialize(new { approved=true, requestSha256=GithubAgent.Hash(raw), expiresUtc=DateTimeOffset.UtcNow.AddMinutes(1) }));
    await Denied("missing credential",()=>{GithubWrite.Claim(raw,approvals,false);return Task.CompletedTask;});
    await Denied("tampered request",()=>{GithubWrite.Claim(raw+" ",approvals,true);return Task.CompletedTask;});
    GithubWrite.Claim(raw,approvals,true);
    await Denied("replayed approval",()=>{GithubWrite.Claim(raw,approvals,true);return Task.CompletedTask;});
} finally { File.Delete(approval); File.Delete(approval+".claimed"); Directory.Delete(approvals); }
Console.WriteLine(JsonSerializer.Serialize(new { status="PASS", provider="HTTP_FIXTURE_NOT_GITHUB", remoteMutation="NOT_INVOKED", evidence },new JsonSerializerOptions { WriteIndented=true }));

internal sealed class Fixture((string method,string path,int status,string body)[] steps):HttpMessageHandler {
    internal int Calls { get; private set; }
    internal JsonElement? PostBody { get; private set; }
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct) {
        var step=steps[Calls++];
        if(request.RequestUri!.Host!="api.github.com" || request.Method.Method!=step.method || request.RequestUri.AbsolutePath!=step.path)
            throw new Exception("Unexpected provider call");
        if(request.Content!=null) PostBody=JsonDocument.Parse(await request.Content.ReadAsStringAsync(ct)).RootElement.Clone();
        return new HttpResponseMessage((HttpStatusCode)step.status){Content=new StringContent(step.body,Encoding.UTF8,"application/json")};
    }
}
internal static class GithubAgent {
    internal static string Hash(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
}
