"""Loopback-only browser harness for the same room API used by Unity."""
import argparse
import os
from pathlib import Path
import secrets

from fastapi import FastAPI
from fastapi.middleware.trustedhost import TrustedHostMiddleware
from fastapi.responses import FileResponse, JSONResponse
from fastapi.staticfiles import StaticFiles

from mafia import CHARACTERS, mafia_router, MafiaRooms
from mafia_dialogue import DEMO_MODEL, OllamaDialogue, PROMPT_VERSION


WEB = Path(__file__).resolve().parents[1] / "Tools" / "mafia-web"


def create_demo(dialogue):
    api = FastAPI(docs_url=None, redoc_url=None, openapi_url=None)
    api.add_middleware(TrustedHostMiddleware, allowed_hosts=["127.0.0.1", "localhost"])
    token = secrets.token_urlsafe(24)
    rooms = MafiaRooms(dialogue, dialogue_source="ollama", demo_professor="D")
    api.include_router(mafia_router(token, rooms))
    api.mount("/assets", StaticFiles(directory=WEB), name="assets")

    @api.get("/")
    async def home():
        return FileResponse(WEB / "index.html", headers={"Cache-Control": "no-store"})

    @api.get("/demo/config")
    async def config():
        return JSONResponse({
            "token": token, "model": dialogue.model, "prompt_version": PROMPT_VERSION,
            "ready": await dialogue.ready(),
            "characters": [
                {"id": key, "name": char.name, "trait": char.trait}
                for key, char in CHARACTERS.items()
            ],
        }, headers={"Cache-Control": "no-store"})

    return api


def main():
    import uvicorn

    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--port", type=int, default=8874)
    parser.add_argument("--model", default=os.getenv("MAFIA_MODEL", DEMO_MODEL))
    parser.add_argument("--ollama-url", default=os.getenv("MAFIA_OLLAMA_URL", "http://127.0.0.1:11434"))
    args = parser.parse_args()
    model = OllamaDialogue(args.model, args.ollama_url)
    uvicorn.run(create_demo(model), host="127.0.0.1", port=args.port, access_log=False)


if __name__ == "__main__":
    main()
