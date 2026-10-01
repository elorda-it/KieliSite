# ҚАЗТЕСТ 听力音频生成工具

用开源的 Piper TTS 和 ISSAI 的哈萨克语声音，把 `db/seed/kaztest.json` 里每段录音的台词合成为音频，输出到 `src/KieliWeb/wwwroot/kieli/audio/kaztest/`。网站上现在的音频就是用它生成的（2026-10-01）。

声音模型 `kk_KZ-issai-high` 的训练数据是 ISSAI（纳扎尔巴耶夫大学）的 KazakhTTS/KazakhTTS2，许可是 CC BY 4.0：可以商用，但必须署名。后台每段录音的「Аудионың авторы」已经写好署名，网站上显示在播放器下面。

## 用法（macOS）

```bash
cd tools/kaztest-audio
python3 -m venv venv
venv/bin/pip install piper-tts
mkdir -p voices
curl -L -o voices/kk_KZ-issai-high.onnx https://huggingface.co/rhasspy/piper-voices/resolve/v1.0.0/kk/kk_KZ/issai/high/kk_KZ-issai-high.onnx
curl -L -o voices/kk_KZ-issai-high.onnx.json https://huggingface.co/rhasspy/piper-voices/resolve/v1.0.0/kk/kk_KZ/issai/high/kk_KZ-issai-high.onnx.json
venv/bin/python make_audio.py --model voices/kk_KZ-issai-high.onnx
```

- `--only 1-2`：只重做第 1 套的第 2 段；`--only 3`：只做第 3 套的全部录音。
- `--length 1.2`：语速更慢（默认 1.1）。
- `venv/`、`voices/` 不要提交（约 380 MB）。
- 台词里的数字要写成文字，否则程序会停下来提示；常见的写法在 `make_audio.py` 的 `SAY` 里。
- 每段录音由谁来读写在 `ROLES` 里（0、1 是男声；2–5 是女声）。
- 重新生成后，把 `db/seed/kaztest.json` 里音频链接的 `?v=` 加 1，否则浏览器可能继续播放缓存里的旧文件（静态文件缓存一年）。已经运行的网站要在后台「Тыңдалым жазбалары」里同样改一下链接。

程序会打印每段的时长和大小。`check` 里列出的句子比其他句子读得明显快或慢，可能读错了，建议听一下。
