from PIL import Image
from pathlib import Path
p=Path('ArtSource/CH_ReferenceSurvivor/textures');rough=Image.open(p/'Survivor_Roughness.png').convert('L');rgba=Image.new('RGBA',rough.size,(0,0,0,0));rgba.putalpha(rough.point(lambda x:255-x));rgba.save(p/'Survivor_MetallicSmoothness.png')
