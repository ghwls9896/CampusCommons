"""Generate the original mono placeholder sounds used by the school crow."""
import math, random, struct, wave
from pathlib import Path

out = Path(__file__).resolve().parents[1] / 'Assets' / 'LibraryCharacter' / 'Resources'
out.mkdir(parents=True, exist_ok=True)
rate = 22050

def write(name, values):
    path = out / name
    with wave.open(str(path), 'wb') as f:
        f.setnchannels(1)
        f.setsampwidth(2)
        f.setframerate(rate)
        f.writeframes(b''.join(struct.pack('<h', max(-32767, min(32767, int(v * 32767)))) for v in values))

rng = random.Random(73)
# A short, low, textured footfall, designed to pan in 3D.
foot = []
for i in range(int(.20 * rate)):
    t = i / rate
    envelope = math.exp(-24 * t) * min(1, t * 180)
    impact = math.sin(2 * math.pi * (115 * t - 120 * t * t))
    texture = rng.uniform(-1, 1) * math.exp(-45 * t)
    foot.append(.56 * impact * envelope + .20 * texture)
write('CrowFootstep.wav', foot)

# A deliberately synthetic, raspy two-part warning call; replaceable in the inspector.
alert = []
phase = 0
filtered = 0
for i in range(int(.75 * rate)):
    t = i / rate
    local = t if t < .31 else t - .36
    if local < 0 or local > .31:
        alert.append(0)
        continue
    env = min(1, local / .025) * math.exp(-5.0 * local)
    frequency = 510 - 560 * local + 27 * math.sin(2 * math.pi * 19 * local)
    phase += 2 * math.pi * frequency / rate
    filtered = .72 * filtered + .28 * rng.uniform(-1, 1)
    harsh = math.sin(phase) + .40 * math.sin(2 * phase) + .16 * math.sin(3 * phase)
    alert.append(.25 * env * (harsh + .55 * filtered))
write('CrowAlert.wav', alert)

# Brief noisy impact for the capture transition (a replaceable placeholder).
jump = []
phase = 0
for i in range(int(.60 * rate)):
    t = i / rate
    env = min(1, t * 120) * math.exp(-5.5 * t)
    frequency = 460 - 300 * min(t / .4, 1) + 35 * math.sin(2 * math.pi * 24 * t)
    phase += 2 * math.pi * frequency / rate
    rasp = math.sin(phase) + .35 * math.sin(phase * 2.03)
    impact = math.sin(2 * math.pi * (90 * t - 85 * t * t)) * math.exp(-16 * t)
    jump.append(.35 * env * rasp + .32 * env * rng.uniform(-1, 1) + .30 * impact)
write('CrowJumpscare.wav', jump)
