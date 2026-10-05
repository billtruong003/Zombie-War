#!/usr/bin/env bash
# Samples the HordeCall dev build on a USB-connected Android phone every few seconds:
# CPU %, memory (PSS), battery temperature, thermal status. Writes CSV to the given file.
#   bash Tools/device_sampler.sh Review/QA/device_1005/samples.csv [seconds] [package]
OUT=${1:-device_samples.csv}
EVERY=${2:-5}
PKG=${3:-com.billthedev.hordecall}
ADB=${ADB:-"/c/Program Files/Unity/Hub/Editor/6000.3.10f1/Editor/Data/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb.exe"}

echo "time,pid,cpu_pct,pss_mb,battery_c,thermal_status" > "$OUT"
while "$ADB" get-state >/dev/null 2>&1; do
    pid=$("$ADB" shell pidof "$PKG" | tr -d '\r')
    cpu=""; pss=""
    if [ -n "$pid" ]; then
        cpu=$("$ADB" shell top -b -n 1 -p "$pid" | tr -d '\r' | awk -v p="$pid" '$1==p {print $9}')
        pss=$("$ADB" shell dumpsys meminfo "$PKG" | tr -d '\r' | awk '/TOTAL PSS:/ {printf "%.0f", $3/1024; exit} /^ *TOTAL / {printf "%.0f", $2/1024; exit}')
    fi
    temp=$("$ADB" shell dumpsys battery | tr -d '\r' | awk '/temperature:/ {printf "%.1f", $2/10}')
    therm=$("$ADB" shell dumpsys thermalservice | tr -d '\r' | awk -F': ' '/Thermal Status:/ {print $2; exit}')
    echo "$(date +%H:%M:%S),$pid,$cpu,$pss,$temp,$therm" >> "$OUT"
    sleep "$EVERY"
done
