#!/usr/bin/env python3
"""Live official A2A/AG-UI hosts and actual GitHub read receipts."""
import hashlib
import json
import os
from pathlib import Path
import secrets
import socket
import subprocess
import sys
import time
from urllib.request import Request, urlopen
from urllib.error import HTTPError
import uuid

dotnet, dll, root, output = sys.argv[1:]
root, output = Path(root).resolve(), Path(output).resolve()
output.mkdir(parents=True)
with socket.socket() as sock:
    sock.bind(("127.0.0.1", 0)); port=sock.getsockname()[1]
base=f"http://127.0.0.1:{port}"
token=secrets.token_hex(32)
env={**os.environ,"PMCRO_MAF_TOKEN":token}
env.pop("PMCRO_GITHUB_TOKEN",None) # explicit public-read proof, never use unknown credential
def save(path,value):
    with (output/path).open("x") as fp: json.dump(value,fp,indent=2)
def get(url):
    with urlopen(url,timeout=5) as response: return json.load(response)
def stream(path, data, auth=True):
    headers={"Content-Type":"application/json","Accept":"text/event-stream","A2A-Version":"1.0"}
    if auth: headers["Authorization"]="Bearer "+token
    with urlopen(Request(base+path,json.dumps(data).encode(),headers),timeout=120) as response:
        assert response.headers.get_content_type()=="text/event-stream"
        return [json.loads(line[6:]) for line in response if line.startswith(b"data: ")]
def audit(result, accepted):
    expected=["orchestrate","plan","make","check","reflect"]; previous="START"
    assert result["executorCompleted"]==expected and result["status"]==("PASS" if accepted else "CORRECTIVE_SEED")
    for frame,phase in zip(result["frames"],expected,strict=True):
        body=json.loads(frame["body"])
        assert hashlib.sha256(frame["body"].encode()).hexdigest()==frame["frameId"] and body["phase"]==phase and body["previousFrameId"]==previous and body["agentId"]=="github-agent"
        previous=frame["frameId"]
    return json.loads(result["frames"][2]["body"])["output"]
def a2a(name,data,accepted):
    rpc={"jsonrpc":"2.0","id":str(uuid.uuid4()),"method":"SendStreamingMessage","params":{"message":{"messageId":str(uuid.uuid4()),"role":"ROLE_USER","parts":[{"text":json.dumps(data)}]}}}
    events=stream("/a2a/github",rpc)
    assert events and all(e["id"]==rpc["id"] for e in events)
    envelope=json.loads("".join(p["text"] for e in events for p in e["result"]["message"]["parts"]))
    result=envelope["result"]
    assert envelope["phaseEvents"]==result["frames"]
    returned=audit(result,accepted)
    save(name+".json",{"request":rpc,"events":events,"returned":result})
    return returned
with (output/"host.log").open("w") as log:
    host=subprocess.Popen([dotnet,dll,str(root),str(output),str(port)],env=env,stdout=log,stderr=subprocess.STDOUT)
    try:
        for _ in range(100):
            if host.poll() is not None: raise RuntimeError("Native host exited")
            try: card=get(base+"/.well-known/agent-card.json"); break
            except OSError: time.sleep(.1)
        else: raise RuntimeError("Startup timed out")
        assert card["name"]=="github-agent" and card["capabilities"]["streaming"]
        with urlopen(base+"/studio") as page:
            assert page.status==200 and b"GitHub Agent Studio" in page.read()
        save("agent-card.json",card)
        repo="pmcro-marketplace/github-agent"
        data={"capability":"/github:repository-discovery:get-repository","repository":repo}
        returned=a2a("repository",data,True)
        assert returned["returned"]["id"]==1462049834 and returned["returned"]["fullName"]==repo
        actions=a2a("actions",{"capability":"/github:actions:inspect","repository":repo},True)
        runs=actions["returned"]["runs"]
        if runs:
            a2a("verify-run",{"capability":"/github:actions:verify-run","repository":repo,"runId":runs[0]["id"]},True)
        a2a("rejected-mutation",{"capability":"/github:change-management:create-repository","repository":"pmcro-marketplace/not-created"},False)
        inp={"threadId":str(uuid.uuid4()),"runId":str(uuid.uuid4()),"state":{},"messages":[{"id":str(uuid.uuid4()),"role":"user","content":json.dumps(data)}],"tools":[],"context":[],"forwardedProps":{}}
        try: stream("/agui",inp,False); raise AssertionError("Bearer missing but request succeeded")
        except HTTPError as exc: assert exc.code==401
        ev=stream("/agui",inp)
        deltas=[e["delta"] for e in ev if e["type"]=="TEXT_MESSAGE_CONTENT"]
        result=json.loads("".join(deltas))["result"]
        audit(result,True)
        assert len(deltas)>=7 and any(e["type"]=="RUN_FINISHED" for e in ev)
        save("agui.json",{"request":inp,"events":ev})
        save("summary.json",{"status":"PASS","repository":repo,"repositoryId":1462049834,"actualProvider":"GITHUB_REST_PUBLIC_READ","actionsRuns":actions["returned"]["totalCount"],"returnedRunCount":len(runs),"verifiedRun":runs[0]["id"] if runs else None,"phaseFrames":25 if runs else 20,"aguiDeltas":len(deltas),"mutation":"NOT_INVOKED","authenticationBoundary":401})
    finally: host.terminate(); host.wait(timeout=15)
print("GITHUB_NATIVE_PROOF_PASS")
