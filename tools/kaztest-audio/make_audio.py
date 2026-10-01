# ҚАЗТЕСТ listening audio from the recording scripts of db/seed/kaztest.json, with Piper TTS and the
# kk_KZ-issai-high voice (ISSAI KazakhTTS/KazakhTTS2, Nazarbayev University, CC BY 4.0). See README.md.
#
#   venv/bin/python make_audio.py --model voices/kk_KZ-issai-high.onnx [--only 1-2] [--length 1.1]
#
# Writes kaztest-<variant>-<recording>.m4a (AAC 64 kbps mono, via macOS afconvert) to
# src/KieliWeb/wwwroot/kieli/audio/kaztest/ and prints a report. A script line is "Name: words".
import argparse, json, os, re, subprocess, sys, tempfile, wave
import numpy as np
from piper import PiperVoice, SynthesisConfig

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SEED = os.path.join(ROOT, "db", "seed", "kaztest.json")
OUT = os.path.join(ROOT, "src", "KieliWeb", "wwwroot", "kieli", "audio", "kaztest")

# kk_KZ-issai-high speakers: 0 KazakhTTS2_M2, 1 KazakhTTS_M1_Iseke (male); 2 KazakhTTS2_F3, 3 KazakhTTS_F1_Raya,
# 4 KazakhTTS2_F1, 5 KazakhTTS2_F2 (female). Each dialogue has one male and one female voice.
ROLES = {"Ерлан": 0, "Сәуле": 4, "Мақсат": 1, "Гүлназ": 5, "Мұғалім": 1, "Асқар": 0, "Айжан": 3, "Арман": 1, "Мадина": 2, "Әйгерім": 3,
         "Нұрбек": 0, "Жанар": 5, "Тимур": 1, "Дина": 2, "Гүлмира": 4}
SPEAKERS = {0: "KazakhTTS2 M2", 1: "KazakhTTS M1 Iseke", 2: "KazakhTTS2 F3", 3: "KazakhTTS F1 Raya", 4: "KazakhTTS2 F1", 5: "KazakhTTS2 F2"}

# what the voice says where the written script differs: numbers in digits, ellipses, quotes
SAY = [(r"\b2014 жылы\b", "екі мың он төртінші жылы"), (r"\.\.\.", ","), (r"[«»]", "")]


def spoken(text):
    for pattern, repl in SAY:
        text = re.sub(pattern, repl, text)
    if re.search(r"\d", text):
        sys.exit("Write this number in words for the voice (see SAY): " + text)
    return text


def turns(script):
    who, out = "", []
    for line in script.replace("\r\n", "\n").split("\n"):
        line = line.strip()
        if not line:
            continue
        m = re.match(r"^([^:]{1,40}):\s+(.+)$", line)
        if m:
            who, line = m.group(1).strip(), m.group(2).strip()
        out.append((who, line))
    return out


def speaker(who, voices):
    if who in ROLES:
        return ROLES[who]
    # a new role: the gender written in «Дауыстар» ("Name — әйел адам, …")
    m = re.search(re.escape(who) + r"\s*—\s*(\S+)", voices or "")
    return 3 if m and m.group(1).startswith("әйел") else 1


def load(model):
    # espeak-ng keeps its data path in a short buffer: give it a short relative path
    import piper
    os.chdir(os.path.dirname(piper.__file__))
    return PiperVoice.load(os.path.abspath(model), espeak_data_dir="espeak-ng-data")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--model", required=True)
    ap.add_argument("--only", help="recordings to make: 1-2, 3 (a whole variant) or 3-1,3-2")
    ap.add_argument("--length", type=float, default=1.1, help="> 1 is slower")
    ap.add_argument("--out", default=OUT)
    a = ap.parse_args()
    a.model, a.out = os.path.abspath(a.model), os.path.abspath(a.out)
    variants = json.load(open(SEED, encoding="utf-8"))["variants"]
    os.makedirs(a.out, exist_ok=True)
    voice = load(a.model)
    rate = voice.config.sample_rate
    for vi, v in enumerate(variants):
        for ri, rec in enumerate(v.get("recordings", [])):
            key = f"{vi + 1}-{ri + 1}"
            if a.only and not any(o == key or o == str(vi + 1) for o in a.only.split(",")):
                continue
            lines = turns(rec["script"])
            dialogue = len({w for w, _ in lines}) > 1
            parts, pace = [np.zeros(int(rate * 1.0), np.float32)], []
            for li, (who, text) in enumerate(lines):
                cfg = SynthesisConfig(speaker_id=speaker(who, rec.get("voices")), length_scale=a.length, noise_scale=0.6, noise_w_scale=0.7)
                chunks = [c.audio_float_array.astype(np.float32) for c in voice.synthesize(spoken(text), cfg)]
                for ci, c in enumerate(chunks):
                    parts.append(c)
                    if ci < len(chunks) - 1:
                        parts.append(np.zeros(int(rate * 0.25), np.float32))      # between sentences
                if li < len(lines) - 1:
                    parts.append(np.zeros(int(rate * (0.7 if dialogue else 1.0)), np.float32))   # between turns / paragraphs
                pace.append((sum(len(c) for c in chunks) / rate / max(len(re.findall(r"\w", text)), 1), li + 1, who, text[:50]))
            parts.append(np.zeros(int(rate * 1.0), np.float32))
            audio = np.concatenate(parts)
            audio = audio / (float(np.max(np.abs(audio))) or 1.0) * 0.89            # peak about -1 dBFS
            pcm = (np.clip(audio, -1, 1) * 32767).astype("<i2")
            m4a = os.path.join(a.out, f"kaztest-{key}.m4a")
            with tempfile.TemporaryDirectory() as tmp:
                wav = os.path.join(tmp, "a.wav")
                with wave.open(wav, "wb") as w:
                    w.setnchannels(1); w.setsampwidth(2); w.setframerate(rate); w.writeframes(pcm.tobytes())
                subprocess.run(["afconvert", "-f", "m4af", "-d", "aac", "-b", "64000", "-q", "127", wav, m4a], check=True)
            med = sorted(p[0] for p in pace)[len(pace) // 2]
            print(json.dumps({
                "file": os.path.basename(m4a), "seconds": round(len(pcm) / rate, 1), "kb": os.path.getsize(m4a) // 1024,
                "voices": {w: SPEAKERS[speaker(w, rec.get("voices"))] for w in dict.fromkeys(w for w, _ in lines)},
                # a line read much faster or slower than the others was probably misread: listen to it
                "check": [f"line {n} ({w}) x{p / med:.2f}: {t}" for p, n, w, t in pace if not 0.7 <= p / med <= 1.4],
            }, ensure_ascii=False))


if __name__ == "__main__":
    main()
