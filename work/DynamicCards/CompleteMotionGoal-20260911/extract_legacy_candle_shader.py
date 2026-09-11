from pathlib import Path
W=Path(__file__).resolve().parent
code=(W/'extract_postfx_shaders.py').read_text(encoding='utf-8-sig')
code=code.replace("files=[source/'resources.assets',source/'globalgamemanagers.assets',*source.glob('sharedassets*.assets')]","files=[source/'StreamingAssets/AssetBundles/cardassets/prefabs',source/'StreamingAssets/AssetBundles/cardassets/scenes']")
code=code.replace("['pollen','glitch','bloom','candle']","['candle']")
code=code.replace("'postfx-original-shader-inventory.json'","'legacy-candle-shader-inventory.json'")
exec(compile(code,__file__,'exec'),{'__file__':__file__})
