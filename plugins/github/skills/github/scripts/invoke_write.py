#!/usr/bin/env python3
"""Explicit operator approval and invocation of a single native GitHub write."""
import argparse
import datetime as dt
import hashlib
import json
import os
from pathlib import Path
import uuid
from urllib.request import Request, urlopen

parser=argparse.ArgumentParser()
sub=parser.add_subparsers(dest="action",required=True)
approve=sub.add_parser("approve")
approve.add_argument("request",type=Path)
approve.add_argument("--approval-dir",type=Path,required=True)
approve.add_argument("--approve",action="store_true",required=True)
invoke=sub.add_parser("invoke")
invoke.add_argument("request",type=Path)
invoke.add_argument("--port",type=int,required=True)
args=parser.parse_args()
raw=args.request.read_bytes()
req=json.loads(raw)
assert req["capability"] in ("/github:change-management:create-repository","/github:actions:dispatch")
aid=str(uuid.UUID(req["approvalId"]))
assert aid==req["approvalId"] and req["repository"].startswith("pmcro-marketplace/")
if args.action=="approve":
    folder=args.approval_dir
    assert folder.is_dir() and not folder.is_symlink()
    with (folder/(aid+".json")).open("x") as fp:
        json.dump({"approved": True,"requestSha256":hashlib.sha256(raw).hexdigest(),
                   "expiresUtc":(dt.datetime.now(dt.timezone.utc)+dt.timedelta(minutes=10)).isoformat()},fp)
    print(json.dumps({"status":"APPROVED","approvalId":aid,"requestSha256":hashlib.sha256(raw).hexdigest()}))
else:
    token=os.environ["PMCRO_MAF_TOKEN"]
    rpc={"jsonrpc":"2.0","id":str(uuid.uuid4()),"method":"SendStreamingMessage",
         "params":{"message":{"messageId":str(uuid.uuid4()),"role":"ROLE_USER","parts":[{"text":raw.decode()}]}}}
    headers={"Authorization":"Bearer "+token,"Content-Type":"application/json","Accept":"text/event-stream","A2A-Version":"1.0"}
    with urlopen(Request(f"http://127.0.0.1:{args.port}/a2a/github",json.dumps(rpc).encode(),headers),timeout=240) as response:
        events=[json.loads(line[6:]) for line in response if line.startswith(b"data: ")]
    result=json.loads("".join(p["text"] for e in events for p in e["result"]["message"]["parts"]))["result"]
    assert result["executorCompleted"]==["orchestrate","plan","make","check","reflect"]
    print(json.dumps(result,indent=2))
    if result["status"]!="PASS" or result["remoteMutation"]!="VERIFIED":
        raise SystemExit(1)
