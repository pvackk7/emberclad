#!/bin/bash
cd "$(dirname "$0")"
DESC="Library/Pipeline/.unity-pipeline-port"
echo "Pipeline watcher started. Watching pipeline_queue/ for cmd_*.json ..."
while true; do
  for f in pipeline_queue/cmd_*.json; do
    [ -e "$f" ] || continue
    n=$(basename "$f" .json | sed 's/cmd_//')
    PORT=$(python3 -c "import json;print(json.load(open('$DESC'))['port'])" 2>/dev/null)
    TOKEN=$(python3 -c "import json;print(json.load(open('$DESC'))['evalToken'])" 2>/dev/null)
    if [ -z "$PORT" ]; then
      echo '{"success":false,"error":"no descriptor / editor not running"}' > "pipeline_queue/result_$n.json"
    else
      curl -s --max-time 300 -X POST "http://127.0.0.1:$PORT/api/exec" \
        -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
        --data-binary @"$f" > "pipeline_queue/result_$n.json"
    fi
    mv "$f" "pipeline_queue/done_$n.json"
    echo "processed cmd_$n"
  done
  sleep 1
done
