#!/bin/bash
# usage: ev.sh N file.cs
cd "$(dirname "$0")"
python3 -c "
import json,sys;code=open(sys.argv[2]).read()
json.dump({'argv':['script','eval',code]},open('pipeline_queue/cmd_%s.json'%sys.argv[1],'w'))" $1 $2
for i in $(seq 1 60); do [ -e pipeline_queue/done_$1.json ] && break; sleep 0.5; done
python3 -c "
import json,sys
r=json.load(open('pipeline_queue/result_%s.json'%sys.argv[1])); r.pop('parameters',None); print(json.dumps(r)[:1500])" $1
