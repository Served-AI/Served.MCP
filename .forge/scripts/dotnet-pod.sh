#!/bin/sh
# dotnet-pod.sh <name> <script> [--feed-credential]
#
# Runs <script> (a file in the build context) in a pod of the official .NET SDK image on this
# runner's node and streams its output into the step log; exits with the pod's result.
# The Forge runner image has no .NET.
#
#   CTX_NODE  build context on the node (the runner sees it under /cache), mounted at /work
#   VERSION   passed to the script
#   --feed-credential  mounts the regcred secret (the Forge registry credential Kaniko pushes
#                      with) at /feed/config.json for the NuGet push
#
# The pod runs as the runner's user (100:101) so the runner can delete what it writes.
set -eu
NAME="$1"
SCRIPT="$2"
CRED="${3:-}"
: "${CTX_NODE:?CTX_NODE not set}" "${VERSION:?VERSION not set}"

NODE="${NODE_NAME:-${FORGE_NODE_NAME:-}}"
if [ -z "$NODE" ]; then
  echo "✗ NODE_NAME is unknown — the pod must run on the node that holds the build context"
  exit 1
fi
NS=served
JOB="mcp-$NAME-${FORGE_RUN_ID:-$(date +%s)}"
IMAGE="${DOTNET_IMAGE:-mcr.microsoft.com/dotnet/sdk:10.0}"
NUGET_NODE="$(dirname "$CTX_NODE")/.nuget"

CRED_MOUNT=""
CRED_VOLUME=""
if [ "$CRED" = "--feed-credential" ]; then
  CRED_MOUNT="
            - name: feed
              mountPath: /feed
              readOnly: true"
  CRED_VOLUME="
        - name: feed
          secret:
            secretName: regcred
            items:
              - key: .dockerconfigjson
                path: config.json"
fi

cleanup() { kubectl delete job "$JOB" -n "$NS" --ignore-not-found --wait=false >/dev/null 2>&1 || true; }
trap cleanup EXIT
kubectl delete job "$JOB" -n "$NS" --ignore-not-found >/dev/null 2>&1 || true

cat <<EOF | kubectl apply -f - >/dev/null
apiVersion: batch/v1
kind: Job
metadata:
  name: $JOB
  namespace: $NS
  labels:
    app: mcp-publish
    forge-run: "${FORGE_RUN_ID:-0}"
spec:
  backoffLimit: 0
  activeDeadlineSeconds: 1500
  ttlSecondsAfterFinished: 600
  template:
    metadata:
      labels:
        app: mcp-publish
    spec:
      nodeSelector:
        kubernetes.io/hostname: $NODE
      restartPolicy: Never
      securityContext:
        runAsUser: 100
        runAsGroup: 101
      containers:
        - name: dotnet
          image: $IMAGE
          imagePullPolicy: IfNotPresent
          command: ["bash", "/work/$SCRIPT"]
          workingDir: /work
          env:
            - { name: VERSION, value: "$VERSION" }
            - { name: HOME, value: /tmp }
            - { name: NUGET_PACKAGES, value: /nuget }
            - { name: DOTNET_CLI_TELEMETRY_OPTOUT, value: "1" }
            - { name: DOTNET_NOLOGO, value: "1" }
            - { name: DOTNET_SKIP_FIRST_TIME_EXPERIENCE, value: "1" }
            - { name: MSBUILDDISABLENODEREUSE, value: "1" }
            - { name: FEED_URL, value: "http://10.10.10.20:30695/nuget/served" }
          resources:
            requests: { cpu: "1", memory: 1Gi }
            limits: { memory: 4Gi }
          volumeMounts:
            - name: work
              mountPath: /work
            - name: nuget
              mountPath: /nuget$CRED_MOUNT
      volumes:
        - name: work
          hostPath:
            path: $CTX_NODE
            type: Directory
        - name: nuget
          hostPath:
            path: $NUGET_NODE
            type: Directory$CRED_VOLUME
EOF
echo "Pod job $JOB on $NODE ($IMAGE)"

# Wait for the container to start: an image pull can take minutes the first time on a node.
DEADLINE=$(( $(date +%s) + 600 ))
LAST=""
while :; do
  PHASE=$(kubectl get pods -n "$NS" -l job-name="$JOB" -o jsonpath='{.items[0].status.phase}' 2>/dev/null || true)
  case "$PHASE" in Running|Succeeded|Failed) break ;; esac
  WAITING=$(kubectl get pods -n "$NS" -l job-name="$JOB" -o jsonpath='{.items[0].status.containerStatuses[0].state.waiting.reason}' 2>/dev/null || true)
  STATE="${PHASE:-no pod yet}${WAITING:+ ($WAITING)}"
  [ "$STATE" != "$LAST" ] && echo "  … $STATE" && LAST="$STATE"
  if [ "$(date +%s)" -ge "$DEADLINE" ]; then
    echo "✗ The pod did not start within 600 s ($STATE)"
    kubectl describe pods -n "$NS" -l job-name="$JOB" 2>/dev/null | tail -15 || true
    exit 1
  fi
  sleep 5
done

kubectl logs -f -n "$NS" "job/$JOB" || true

# The log stream ends with the container; the Job's verdict follows within seconds.
DEADLINE=$(( $(date +%s) + 120 ))
while :; do
  OK=$(kubectl get job "$JOB" -n "$NS" -o jsonpath='{.status.succeeded}' 2>/dev/null || true)
  KO=$(kubectl get job "$JOB" -n "$NS" -o jsonpath='{.status.failed}' 2>/dev/null || true)
  if [ "$OK" = "1" ]; then exit 0; fi
  if [ -n "$KO" ] && [ "$KO" != "0" ]; then
    REASON=$(kubectl get pods -n "$NS" -l job-name="$JOB" -o jsonpath='{.items[0].status.containerStatuses[0].state.terminated.reason} exit {.items[0].status.containerStatuses[0].state.terminated.exitCode}' 2>/dev/null || true)
    echo "✗ $NAME failed ($REASON)"
    exit 1
  fi
  if [ "$(date +%s)" -ge "$DEADLINE" ]; then
    echo "✗ No verdict from job $JOB 120 s after its log ended"
    exit 1
  fi
  sleep 2
done
