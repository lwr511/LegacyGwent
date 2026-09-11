from pathlib import Path
W=Path(__file__).resolve().parent
code=(W/'extract_postfx_shaders.py').read_text(encoding='utf-8-sig').replace("['pollen','glitch','bloom','candle']","['lightning']").replace("'postfx-original-shader-inventory.json'","'lightning-original-shader-inventory.json'").replace("'native-postfx-original-shader-inventory.json'","'native-lightning-original-shader-inventory.json'")
exec(compile(code,__file__,'exec'),{'__file__':__file__})
