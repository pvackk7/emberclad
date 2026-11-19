#!/bin/bash
cd "$(dirname "$0")"
N=$1; echo "{\"argv\":[\"console\",\"get\",\"--tail\",\"${2:-60}\",\"--level\",\"${3:-warn}\"]}" > pipeline_queue/cmd_$N.json
for i in $(seq 1 40); do [ -e pipeline_queue/done_$N.json ] && break; sleep 0.5; done
python3 - $N << 'PY'
import json,sys
r=json.load(open('pipeline_queue/result_%s.json'%sys.argv[1]))['result']
print('counts',r.get('counts'),'ground',r.get('groundTruth'))
seen={}
for e in r['entries']:
    m=(e.get('message') or str(e))[:500]
    seen[m]=seen.get(m,0)+1
for m,c in list(seen.items())[-12:]: print(c,'x',m)
PY
