#!/usr/bin/env bash
# Builds and runs the AIL end-to-end checks (checks/e2e, an xUnit project), as ail.config.json's `tests` says.
#   scripts/ail/check.sh build           builds them
#   scripts/ail/check.sh <Node> <report> runs the scenarios of <Node> (its class <Node>Checks) against the running app,
#                                        writing a JUnit report; each scenario saves its screenshot into $AIL_SHOTS_DIR
set -euo pipefail
cd "$(dirname "$0")/../.."
export PATH="$HOME/.dotnet:$PATH" DOTNET_NOLOGO=1 DOTNET_CLI_TELEMETRY_OPTOUT=1
out="${AIL_CHECKS_OUT:-checks/e2e/bin/Release/net11.0}"
if [[ "$1" == build ]]; then
  exec dotnet build checks/e2e -c Release -v quiet -nologo
fi
exec dotnet "$out/Shapeshifter.Checks.dll" -class "*.$1Checks" -result-junit "$2" -noLogo -noColor -reporter verbose
