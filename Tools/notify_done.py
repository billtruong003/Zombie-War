#!/usr/bin/env python3

import argparse
import base64
from pathlib import Path
import subprocess
import sys
import tempfile
import time


DEFAULT_MESSAGE = "Claude has finished the task."
MELODY = ((784, 180), (988, 180), (1175, 260))
PROJECT_ROOT = Path(__file__).resolve().parent.parent
ROXY_RUNTIME = PROJECT_ROOT / "Library" / "ClaudeVoice"
ROXY_PYTHON = ROXY_RUNTIME / "venv" / "Scripts" / "python.exe"
ROXY_MODEL = ROXY_RUNTIME / "models" / "Roxy" / "roxy_e660_s4620.pth"
ROXY_INDEX = (
    ROXY_RUNTIME
    / "models"
    / "Roxy"
    / "added_IVF346_Flat_nprobe_1_roxy_v2.index"
)
PIPER_MODEL = ROXY_RUNTIME / "models" / "Piper" / "en_US-lessac-medium.onnx"
PIPER_CONFIG = Path(f"{PIPER_MODEL}.json")


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Play an audible completion alert and show a Windows notification."
    )
    parser.add_argument(
        "message",
        nargs="?",
        default=DEFAULT_MESSAGE,
        help="message to display and read aloud",
    )
    parser.add_argument(
        "--repeat",
        type=int,
        choices=range(1, 11),
        default=3,
        metavar="1-10",
        help="number of times to play the alert melody (default: 3)",
    )
    parser.add_argument(
        "--no-speech",
        action="store_true",
        help="play the melody without reading the message aloud",
    )
    parser.add_argument(
        "--voice",
        choices=("roxy", "windows"),
        default="roxy",
        help="voice used to read the message (default: roxy)",
    )
    parser.add_argument(
        "--dry-run",
        action="store_true",
        help="validate arguments without showing or playing anything",
    )
    return parser.parse_args()


def run_powershell(script: str, timeout: int = 30) -> bool:
    encoded_script = base64.b64encode(script.encode("utf-16-le")).decode("ascii")
    creation_flags = getattr(subprocess, "CREATE_NO_WINDOW", 0)
    result = subprocess.run(
        [
            "powershell.exe",
            "-NoProfile",
            "-NonInteractive",
            "-ExecutionPolicy",
            "Bypass",
            "-EncodedCommand",
            encoded_script,
        ],
        capture_output=True,
        creationflags=creation_flags,
        timeout=timeout,
        check=False,
    )
    return result.returncode == 0


def launch_powershell(script: str) -> bool:
    encoded_script = base64.b64encode(script.encode("utf-16-le")).decode("ascii")
    creation_flags = getattr(subprocess, "CREATE_NO_WINDOW", 0)
    creation_flags |= getattr(subprocess, "CREATE_NEW_PROCESS_GROUP", 0)
    try:
        subprocess.Popen(
            [
                "powershell.exe",
                "-NoProfile",
                "-NonInteractive",
                "-ExecutionPolicy",
                "Bypass",
                "-EncodedCommand",
                encoded_script,
            ],
            stdin=subprocess.DEVNULL,
            stdout=subprocess.DEVNULL,
            stderr=subprocess.DEVNULL,
            creationflags=creation_flags,
        )
    except OSError:
        return False
    return True


def show_windows_notification(message: str) -> bool:
    encoded_message = base64.b64encode(message.encode("utf-8")).decode("ascii")
    script = f"""
$message = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('{encoded_message}'))
[Windows.UI.Notifications.ToastNotificationManager, Windows.UI.Notifications, ContentType = WindowsRuntime] > $null
$template = [Windows.UI.Notifications.ToastTemplateType]::ToastText02
$xml = [Windows.UI.Notifications.ToastNotificationManager]::GetTemplateContent($template)
$text = $xml.GetElementsByTagName('text')
$text.Item(0).AppendChild($xml.CreateTextNode('Claude')) > $null
$text.Item(1).AppendChild($xml.CreateTextNode($message)) > $null
$toast = [Windows.UI.Notifications.ToastNotification]::new($xml)
[Windows.UI.Notifications.ToastNotificationManager]::CreateToastNotifier('Claude').Show($toast)
"""
    if run_powershell(script):
        return True

    popup_script = f"""
$message = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('{encoded_message}'))
$shell = New-Object -ComObject WScript.Shell
[void]$shell.Popup($message, 20, 'Claude', 64)
"""
    return launch_powershell(popup_script)


def play_windows_melody(repetitions: int) -> None:
    import winsound

    for repetition in range(repetitions):
        for frequency, duration_ms in MELODY:
            winsound.Beep(frequency, duration_ms)
        if repetition < repetitions - 1:
            time.sleep(0.25)


def speak_windows_message(message: str) -> bool:
    encoded_message = base64.b64encode(message.encode("utf-8")).decode("ascii")
    script = f"""
Add-Type -AssemblyName System.Speech
$message = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('{encoded_message}'))
$speaker = New-Object System.Speech.Synthesis.SpeechSynthesizer
$speaker.Volume = 100
$speaker.Rate = 0
$speaker.Speak($message)
$speaker.Dispose()
"""
    return run_powershell(script)


def create_source_speech(message: str, output_path: Path) -> bool:
    creation_flags = getattr(subprocess, "CREATE_NO_WINDOW", 0)
    result = subprocess.run(
        [
            str(ROXY_PYTHON),
            "-m",
            "piper",
            "-m",
            str(PIPER_MODEL),
            "-f",
            str(output_path),
        ],
        cwd=PROJECT_ROOT,
        input=message,
        capture_output=True,
        text=True,
        encoding="utf-8",
        errors="replace",
        creationflags=creation_flags,
        timeout=120,
        check=False,
    )
    if result.returncode == 0 and output_path.is_file() and output_path.stat().st_size > 44:
        return True

    error = (result.stderr or result.stdout).strip().splitlines()
    if error:
        print(f"Piper error: {error[-1]}", file=sys.stderr)
    return False


def speak_roxy_message(message: str) -> bool:
    required_files = (
        ROXY_PYTHON,
        ROXY_MODEL,
        ROXY_INDEX,
        PIPER_MODEL,
        PIPER_CONFIG,
    )
    if not all(path.is_file() for path in required_files):
        print(
            "Warning: Roxy runtime is incomplete; using Windows voice.",
            file=sys.stderr,
        )
        return False

    creation_flags = getattr(subprocess, "CREATE_NO_WINDOW", 0)
    with tempfile.TemporaryDirectory(prefix="notify-", dir=ROXY_RUNTIME) as temp_dir:
        source_path = Path(temp_dir) / "source.wav"
        output_path = Path(temp_dir) / "roxy.wav"

        if not create_source_speech(message, source_path):
            return False

        print("Generating Roxy voice...")
        result = subprocess.run(
            [
                str(ROXY_PYTHON),
                "-m",
                "rvc_python",
                "cli",
                "-i",
                str(source_path),
                "-o",
                str(output_path),
                "-mp",
                str(ROXY_MODEL),
                "-ip",
                str(ROXY_INDEX),
                "-de",
                "cuda:0",
                "-me",
                "rmvpe",
                "-v",
                "v2",
                "-ir",
                "0.6",
                "-rmr",
                "0.25",
                "-pr",
                "0.5",
                "-pi",
                "0",
            ],
            cwd=PROJECT_ROOT,
            capture_output=True,
            text=True,
            encoding="utf-8",
            errors="replace",
            creationflags=creation_flags,
            timeout=300,
            check=False,
        )
        if result.returncode != 0 or not output_path.is_file():
            error = (result.stderr or result.stdout).strip().splitlines()
            if error:
                print(f"Roxy error: {error[-1]}", file=sys.stderr)
            return False

        import winsound

        winsound.PlaySound(str(output_path), winsound.SND_FILENAME)
        return True


def notify(message: str, repetitions: int, use_speech: bool, voice: str) -> None:
    print(f"Task complete: {message}")

    if sys.platform != "win32":
        for _ in range(repetitions):
            print("\a", end="", flush=True)
            time.sleep(0.5)
        print("\nWindows desktop notification and speech are unavailable on this OS.")
        return

    if not show_windows_notification(message):
        print("Warning: Windows notification could not be displayed.", file=sys.stderr)

    play_windows_melody(repetitions)

    if not use_speech:
        return

    speech_played = speak_roxy_message(message) if voice == "roxy" else False
    if not speech_played and not speak_windows_message(message):
        print("Warning: speech could not be played.", file=sys.stderr)


def main() -> int:
    args = parse_args()
    if args.dry_run:
        print(
            f"Dry run: message={args.message!r}, repeat={args.repeat}, "
            f"speech={not args.no_speech}, voice={args.voice}"
        )
        return 0

    notify(args.message, args.repeat, not args.no_speech, args.voice)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
