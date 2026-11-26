"""Run two signed-in Unity game windows on a private loopback server."""
import argparse
import json
import os
from pathlib import Path
import plistlib
import select
import signal
import socket
import subprocess
import sys
import threading
import time

HERE = Path(__file__).resolve().parent
BUNDLE = HERE.parent
PORT = 18648
sys.path[:0] = [str(HERE / "vendor"), str(HERE / "backend")]


def shutdown_signals(flag):
    def stop(signum, frame):
        flag.set()
    for signum in (signal.SIGTERM, signal.SIGHUP, signal.SIGINT):
        signal.signal(signum, stop)


def stop_process(process):
    if process.poll() is None:
        process.terminate()
        try:
            process.wait(timeout=6)
        except subprocess.TimeoutExpired:
            process.kill()
            process.wait()


def watch_player(log_dir, number, args):
    # EOF also arrives when a terminal or its launcher is killed unexpectedly.
    finished = threading.Event()
    shutdown_signals(finished)
    with open(log_dir / f"process-{number}.log", "ab", buffering=0) as output:
        child = subprocess.Popen(args, stdin=subprocess.DEVNULL, stdout=output, stderr=output,
                                 pass_fds=(int(os.environ["CC_LOBBY_LISTENER_FD"]),))
        try:
            (log_dir / f"player-{number}.pid").write_text(str(child.pid))
            while child.poll() is None and not finished.is_set():
                ready, _, _ = select.select([sys.stdin.fileno()], [], [], .25)
                if ready and not os.read(sys.stdin.fileno(), 1):
                    break
        finally:
            stop_process(child)
    return child.returncode


def executable():
    app = HERE / "CampusCommonsQuizPair.app" / "Contents"
    with (app / "Info.plist").open("rb") as info:
        name = plistlib.load(info)["CFBundleExecutable"]
    binary = app / "MacOS" / name
    if not binary.is_file():
        raise FileNotFoundError(binary)
    return str(binary)


def game_environment(listener_fd):
    # The game needs the listener guard and ordinary macOS paths/locale only.
    keys = ("PATH", "HOME", "USER", "TMPDIR", "LANG", "LOGNAME", "__CF_USER_TEXT_ENCODING")
    env = {key: os.environ[key] for key in keys if key in os.environ}
    env.update({key: value for key, value in os.environ.items() if key.startswith("LC_")})
    env["CC_LOBBY_LISTENER_FD"] = str(listener_fd)
    return env


def account_app():
    from app import Settings, create_app
    from hosted_quiz import HostedQuiz

    account = json.loads((HERE / "AccountConnection.json").read_text())
    os.environ["SUPABASE_URL"] = account["url"]
    os.environ["SUPABASE_PUBLISHABLE_KEY"] = account["publishableKey"]
    try:
        service = json.loads((HERE / "QuizConnection.json").read_text())
    except (OSError, ValueError) as error:
        raise RuntimeError("배포본의 퀴즈 서버 연결 설정을 읽지 못했습니다.") from error
    if not isinstance(service, dict) or service.get("server_url") not in (HostedQuiz.ORIGIN, HostedQuiz.ORIGIN + "/"):
        raise RuntimeError("배포본의 퀴즈 서버 주소를 확인해 주세요.")
    try:
        provider = HostedQuiz(service.get("access_token"))
    except ValueError as error:
        raise RuntimeError("배포본의 퀴즈 서버 연결 설정을 확인해 주세요.") from error
    return create_app(Settings(), hosted_quiz=provider)


def launch(options):
    import uvicorn

    os.umask(0o077)
    binary = executable()
    stop = threading.Event()
    shutdown_signals(stop)
    with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as listener:
        try:
            listener.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
            listener.bind(("127.0.0.1", PORT))
            listener.listen()
        except OSError as error:
            print(f"퀴즈 포트 {PORT}가 사용 중입니다. 2인 퀴즈 실행본이 이미 열려 있는지 확인해 주세요. ({error})", flush=True)
            return 1
        stamp = time.strftime("%Y%m%d-%H%M%S") + f"-{os.getpid()}"
        log_dir = BUNDLE / "logs" / stamp
        log_dir.mkdir(parents=True)
        server = uvicorn.Server(uvicorn.Config(account_app(), ws_max_size=1024, log_level="warning", access_log=False))
        thread = threading.Thread(target=server.run, kwargs={"sockets":[listener.dup()]}, daemon=True)
        thread.start()
        deadline = time.monotonic() + 10
        while not server.started and not stop.is_set():
            if not thread.is_alive() or time.monotonic() >= deadline:
                raise RuntimeError("퀴즈 서버를 열 수 없습니다.")
            time.sleep(.05)
        guards = []
        try:
            if stop.is_set():
                return 0
            for number in (1, 2):
                arguments = [binary, "-lobby-window", str(number),
                             "-screen-fullscreen", "0", "-screen-width", str(options.width), "-screen-height", str(options.height),
                             "-logFile", str(log_dir / f"player-{number}.log")]
                guards.append(subprocess.Popen(
                    [sys.executable, "-I", "-S", "-B", str(Path(__file__).resolve()), "watch-player", str(log_dir), str(number), *arguments],
                    stdin=subprocess.PIPE, start_new_session=True, pass_fds=(listener.fileno(),),
                    env=game_environment(listener.fileno())))
            print("퀴즈 출제와 채점은 기존 Render 서버에서 처리합니다. 두 창에서 서로 다른 계정으로 로그인한 뒤 퀴즈룸의 다른 자리에 앉으세요.", flush=True)
            print("게임 창 두 개를 닫거나 이 터미널에서 Control-C를 누르면 종료합니다.", flush=True)
            print(f"로그 폴더  {log_dir}", flush=True)
            while not stop.wait(.4) and any(guard.poll() is None for guard in guards):
                if not thread.is_alive():
                    raise RuntimeError("퀴즈 서버가 종료되었습니다.")
            if not stop.is_set() and any(guard.returncode not in (None, 0) for guard in guards):
                print(f"게임 창을 여는 중 오류가 있었습니다. {log_dir}의 로그를 확인해 주세요.", flush=True)
                return 1
        finally:
            for guard in guards:
                guard.stdin.close()
            for guard in guards:
                try:
                    guard.wait(timeout=9)
                except subprocess.TimeoutExpired:
                    stop_process(guard)
            server.should_exit = True
            thread.join(timeout=5)
    return 0


if __name__ == "__main__":
    if len(sys.argv) > 1 and sys.argv[1] == "watch-player":
        sys.exit(watch_player(Path(sys.argv[2]), int(sys.argv[3]), sys.argv[4:]))
    parser = argparse.ArgumentParser(description="CampusCommons 실제 계정 도서관 퀴즈")
    parser.add_argument("--width", type=int, default=800)
    parser.add_argument("--height", type=int, default=550)
    try:
        sys.exit(launch(parser.parse_args()))
    except (FileNotFoundError, RuntimeError) as error:
        print(f"시연을 시작하지 못했습니다. {error}", file=sys.stderr)
        sys.exit(1)
