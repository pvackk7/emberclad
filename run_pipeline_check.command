#!/bin/bash
cd "$(dirname "$0")"
DESC="Library/Pipeline/.unity-pipeline-port"
PORT=$(python3 -c "import json;print(json.load(open('$DESC'))['port'])")
TOKEN=$(python3 -c "import json;print(json.load(open('$DESC'))['evalToken'])")
{
  echo "=== editor status ==="
  curl -s -X POST "http://127.0.0.1:$PORT/api/exec" \
    -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
    -d '{"argv":["editor","status"]}'
  echo
  echo "=== command groups ==="
  curl -s -X POST "http://127.0.0.1:$PORT/api/exec" \
    -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
    -d '{"argv":[]}'
  echo
} > pipeline_check_output.json 2>&1
echo DONE >> pipeline_check_output.json
