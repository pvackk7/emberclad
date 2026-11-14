#!/bin/bash
# usage: q.sh N 'json'  [wait]
cd "$(dirname "$0")"
echo "$2" > pipeline_queue/cmd_$1.json
for i in $(seq 1 ${3:-40}); do [ -e pipeline_queue/result_$1.json ] && [ -e pipeline_queue/done_$1.json ] && break; sleep 0.5; done
head -c ${4:-700} pipeline_queue/result_$1.json; echo
