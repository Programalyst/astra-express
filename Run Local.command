#!/bin/bash
set -euo pipefail
cd "$(dirname "$0")"
if [ ! -f Builds/Web/index.html ]; then
  echo 'No Web build found. Open this project in Unity 6000.3.24f1 and choose Astra Express > Build Web.'
  exit 1
fi
echo 'Astra Express: http://127.0.0.1:8090/'
echo 'Press Control-C in this window to stop the local server.'
exec python3 -u server/coach_server.py --port 8090
